using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using VContainer;

/// <summary>엘리베이터의 이동과 탑승자·카메라 복원을 담당함.</summary>
public class OutsideElevator : MonoBehaviour, IWorldInteractionAction, IWorldInteractionCancellationPolicy
{
    [SerializeField] private LogoFade logoEvent;
    [SerializeField] private float logoFadeTiming = 0.2f;
    [SerializeField] private OutsideElevatorRadio radio;
    [SerializeField] private IntEvent changeCameraModeEvent;
    [SerializeField] private float cameraReturnTiming = 0.8f;
    [SerializeField] private Transform topPoint;
    [SerializeField] private Transform bottomPoint;
    [SerializeField] private GameObject wallColider;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float delayDuration = 3f;
    [SerializeField] private Ease ease;

    [Header("빠른 이동")]
    [SerializeField, InspectorName("도착 카메라")] private OutsideCamera outsideCamera;
    [SerializeField, Min(0), InspectorName("화면 암전 시간 (초)")] private float fastFadeOutDuration = .35f;
    [SerializeField, Min(0), InspectorName("암전 후 이동 대기 (초)")] private float fastBeforeMoveDelay = .05f;
    [SerializeField, Min(0), InspectorName("이동 후 암전 유지 (초)")] private float fastAfterMoveDelay = .30f;
    [SerializeField, Min(0), InspectorName("화면 복원 시간 (초)")] private float fastFadeInDuration = .45f;

    [Inject] private IConditionUtil conditions;
    [Inject] private ISceneFadeService fades;
    private CancellationTokenSource execution;
    private CancellationTokenSource movement;
    private UniTaskCompletionSource finished;
    private Ride ride;
    private bool isTop;
    private bool fastTravelling;
    public bool CanExecute => isActiveAndEnabled && execution == null;
    public bool CanCancel => !fastTravelling;
    public bool IsMoving => movement != null;
    public bool IsTop => isTop;

    public void SetPosition(bool top)
    {
        if (execution != null) throw new InvalidOperationException("사용 중인 엘리베이터의 위치를 바꿀 수 없습니다.");
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("엘리베이터 상·하단 앵커가 없습니다.");
        isTop = top;
        transform.position = (top ? topPoint : bottomPoint).position;
    }

    // 플레이어가 위층 집 문 앞에서 시작할 때만 엘리베이터가 위층에서 기다린다. 첫 출근과 퇴근은 1층에서 시작한다.
    private void Start()
    {
        var state = GameStateManager.Instance;
        SetPosition(OutsideStorySceneRules.ResolveSpawn(gameObject.scene.name, state.CurrentDay, state.GameFlow) == EPlayerSpawn.Home);
    }
    private void OnDisable()
    {
        execution?.Cancel();
        movement?.Cancel();
        ride?.RestorePose();
    }

    public async UniTask StopAsync()
    {
        if (execution == null) return;
        var completion = finished;
        execution.Cancel();
        await completion.Task;
    }

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute || context.Player?.Transform == null)
            throw new InvalidOperationException("현재 엘리베이터에 탑승할 수 없습니다.");
        if (context.ChooseMenu == null || conditions == null)
            throw new InvalidOperationException("엘리베이터의 선택창과 진행 조건이 연결되지 않았습니다.");
        token.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        execution = source;
        var completion = finished = new UniTaskCompletionSource();
        try
        {
            var selected = await context.ChooseMenu(CreateOptions(), null, source.Token);
            source.Token.ThrowIfCancellationRequested();
            switch (selected.Id)
            {
                case null:
                case "cancel":
                    return;
                case "ride":
                    await RideAsync(context, source.Token);
                    break;
                case "fast":
                    // 선택창을 연 뒤 상태가 바뀌어도 수리 전 빠른 이동을 허용하지 않는다.
                    if (!IsRepaired()) throw new InvalidOperationException("엘리베이터 수리 후 빠른 이동을 사용할 수 있습니다.");
                    await FastTravelAsync(context.Player, source.Token);
                    break;
                default:
                    throw new InvalidOperationException("알 수 없는 엘리베이터 메뉴: " + selected.Id);
            }
        }
        finally
        {
            if (ReferenceEquals(execution, source)) execution = null;
            completion.TrySetResult();
        }
    }

    private bool IsRepaired() => conditions.CheckRequired("flag.elevator_repaired");

    private NewStreetOptionData[] CreateOptions()
    {
        var normal = new NewStreetOptionData
        {
            Id = "ride", Text = new Texts { Ko = isTop ? "내려간다" : "올라간다", En = isTop ? "Go down" : "Go up" }
        };
        var cancel = new NewStreetOptionData { Id = "cancel", Text = new Texts { Ko = "취소", En = "Cancel" } };
        return IsRepaired()
            ? new[] { new NewStreetOptionData { Id = "fast", Text = new Texts { Ko = "빠른 이동", En = "Fast travel" } }, normal, cancel }
            : new[] { normal, cancel };
    }

    private async UniTask RideAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        using var broadcast = CancellationTokenSource.CreateLinkedTokenSource(token);
        UniTask<StoryExecutionResult>? dialogue = null;
        var script = radio != null && radio.isActiveAndEnabled && radio.IsAvailable ? radio.Steps : null;
        var radioSceneId = radio != null ? radio.DialogueSceneId : null;
        try
        {
            if (script != null && script.Length > 0 && context.PlayAutomaticDialogue != null)
                dialogue = context.PlayAutomaticDialogue(script, radio.transform, broadcast.Token).Preserve();
            await MoveAsync(context.Player, token, broadcast.Cancel);
        }
        finally
        {
            broadcast.Cancel();
            if (dialogue.HasValue)
            {
                var result = await dialogue.Value;
                // 이동 복원이 끝난 뒤 완료 이력과 다음 방송을 갱신함.
                if (radio != null) radio.CompletePlayback(radioSceneId, result);
                if (result.Status == StoryExecutionStatus.Failed)
                    throw new InvalidOperationException("엘리베이터 방송 재생 실패: " + result.Error);
            }
        }
    }

    private async UniTask FastTravelAsync(IInteractor player, CancellationToken token)
    {
        if (topPoint == null || bottomPoint == null || outsideCamera == null || fades == null)
            throw new InvalidOperationException("빠른 이동의 상·하단 지점, 도착 카메라와 화면 페이드를 연결하세요.");
        ValidateFastDuration(fastFadeOutDuration);
        ValidateFastDuration(fastBeforeMoveDelay);
        ValidateFastDuration(fastAfterMoveDelay);
        ValidateFastDuration(fastFadeInDuration);

        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        // 일반 탑승과 달리 암전 중에는 이동 입력과 방향 전환까지 잠근다.
        using var lease = InteractionStateLease.Acquire(player);
        movement = source;
        fastTravelling = true;
        var actor = player.Transform;
        var actorStart = actor.position;
        var start = transform.position;
        bool startedAtTop = isTop;
        bool oldWall = wallColider != null && wallColider.activeSelf;
        var destination = (isTop ? bottomPoint : topPoint).position;
        bool blackOwned = false;
        bool moved = false;
        bool completed = false;
        try
        {
            await fades.FadeOutAsync(fastFadeOutDuration, source.Token);
            blackOwned = true;
            await WaitFastTravelAsync(fastBeforeMoveDelay, source.Token);
            source.Token.ThrowIfCancellationRequested();
            if (wallColider != null) wallColider.SetActive(true);
            // 일반 탑승과 같은 발 위치를 유지하며 승강기와 탑승자를 함께 옮긴다.
            transform.position = destination;
            actor.position = new Vector3(destination.x, actorStart.y + destination.y - start.y, destination.z);
            isTop = !startedAtTop;
            moved = true;
            outsideCamera.SnapToFollow();
            float movedAt = Time.unscaledTime;
            // 대기 시간을 0으로 설정해도 카메라의 최종 위치가 암전 중 갱신되게 한다.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, source.Token);
            await WaitFastTravelAsync(Mathf.Max(0, fastAfterMoveDelay - (Time.unscaledTime - movedAt)), source.Token);
            await fades.FadeInAsync(fastFadeInDuration, source.Token);
            source.Token.ThrowIfCancellationRequested();
            completed = true;
        }
        finally
        {
            try
            {
                if (!completed && moved)
                {
                    if (actor != null) actor.position = actorStart;
                    if (this != null) { transform.position = start; isTop = startedAtTop; }
                    if (outsideCamera != null)
                    {
                        outsideCamera.SnapToFollow();
                        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                    }
                }
            }
            finally
            {
                try
                {
                    // 암전을 얻기 전 실패했다면 다른 전환의 화면을 건드리지 않는다.
                    if (blackOwned && !completed) await fades.FadeInAsync(0, CancellationToken.None);
                }
                finally
                {
                    if (wallColider != null) wallColider.SetActive(oldWall);
                    fastTravelling = false;
                    if (ReferenceEquals(movement, source)) movement = null;
                }
            }
        }
    }

    private static UniTask WaitFastTravelAsync(float seconds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return seconds > 0 ? UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: token) : UniTask.CompletedTask;
    }

    private static void ValidateFastDuration(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0)
            throw new InvalidOperationException("빠른 이동 시간은 유한한 0 이상의 값이어야 합니다.");
    }

    private async UniTask MoveAsync(IInteractor player, CancellationToken token, Action onArrived)
    {
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("엘리베이터 상·하단 앵커가 없습니다.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        movement = source;
        using var lease = InteractionStateLease.Acquire(player, EInteractorState.ForceMove);
        var current = new Ride(this, player.Transform, source.Token);
        ride = current;
        Sequence sequence = null;
        bool tweenAwaitStarted = false;
        try
        {
            current.Begin();
            sequence = DOTween.Sequence();
            sequence.Append(transform.DOMove(current.Destination, Mathf.Max(0, duration)).SetEase(ease).OnUpdate(current.MovePassenger));
            if (!GameStateManager.Instance.IsOutsideLogo && logoEvent != null)
                sequence.InsertCallback(Mathf.Max(0, duration) * Mathf.Clamp01(logoFadeTiming), current.StartLogo);
            sequence.InsertCallback(Mathf.Max(0, duration) * Mathf.Clamp01(cameraReturnTiming), current.FollowCamera);
            tweenAwaitStarted = true;
            await sequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, source.Token);
            source.Token.ThrowIfCancellationRequested();
            current.Arrive();
            onArrived?.Invoke();
            await current.Logo;
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0, delayDuration)), cancellationToken: source.Token);
        }
        finally
        {
            source.Cancel();
            if (!tweenAwaitStarted) sequence?.Kill();
            current.RestorePose();
            try { await current.Logo; }
            catch (OperationCanceledException) { }
            finally
            {
                ride = null;
                if (ReferenceEquals(movement, source)) movement = null;
                current.FollowCamera();
            }
        }
    }

    private async UniTask ShowLogoAsync(CancellationToken token)
    {
        await logoEvent.ShowLogoAsync(token);
        token.ThrowIfCancellationRequested();
        GameStateManager.Instance.IsOutsideLogo = true;
    }

    /// <summary>한 번의 탑승에서만 필요한 위치·벽·로고 상태를 보관함.</summary>
    private sealed class Ride
    {
        private readonly OutsideElevator owner;
        private readonly Transform actor;
        private readonly CancellationToken token;
        private readonly Vector3 actorStart;
        private readonly Vector3 start;
        private readonly bool oldWall;
        private Vector3 previousPosition;
        private bool arrived;
        private bool restored;
        public Vector3 Destination { get; }
        public UniTask Logo { get; private set; } = UniTask.CompletedTask;

        public Ride(OutsideElevator owner, Transform actor, CancellationToken token)
        {
            this.owner = owner;
            this.actor = actor;
            this.token = token;
            actorStart = actor.position;
            start = previousPosition = owner.transform.position;
            Destination = (owner.isTop ? owner.bottomPoint : owner.topPoint).position;
            oldWall = owner.wallColider != null && owner.wallColider.activeSelf;
        }

        public void Begin()
        {
            actor.position = new Vector3(start.x, actor.position.y, start.z);
            if (owner.wallColider != null) owner.wallColider.SetActive(true);
            owner.changeCameraModeEvent?.Raise((int)EOutsideCameraMode.Elevator);
        }

        public void MovePassenger()
        {
            // 부모를 바꾸지 않아 승강기 비활성화가 플레이어에게 전파되지 않게 함.
            if (actor != null) actor.position += owner.transform.position - previousPosition;
            previousPosition = owner.transform.position;
        }

        public void StartLogo() => Logo = owner.ShowLogoAsync(token).Preserve();
        public void FollowCamera()
        {
            if (owner != null && owner.changeCameraModeEvent != null)
                owner.changeCameraModeEvent.Raise((int)EOutsideCameraMode.Follow);
        }

        public void Arrive()
        {
            owner.transform.position = Destination;
            arrived = true;
            owner.isTop = !owner.isTop;
            RestorePose();
        }

        public void RestorePose()
        {
            if (restored) return;
            restored = true;
            if (!arrived && actor != null) actor.position = actorStart;
            if (owner == null) return;
            if (!arrived) owner.transform.position = start;
            if (owner.wallColider != null) owner.wallColider.SetActive(oldWall);
        }
    }
}
