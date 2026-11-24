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
    [SerializeField] private IntEvent changeCameraModeEvent;
    [SerializeField] private float cameraReturnTiming = 0.8f;
    [SerializeField] private Transform topPoint;
    [SerializeField] private Transform bottomPoint;
    [SerializeField, Tooltip("칸 왼쪽을 막는 벽(Floor 레이어 콜라이더, 칸 아래 LeftWall). " +
        "타고 오르내리는 동안에만 켜고, 어느 층이든 서 있을 때는 꺼서 칸 왼쪽으로 드나들 수 있게 합니다. 비워 두면 켜고 끄지 않습니다.")]
    private GameObject leftWall;
    [SerializeField, Tooltip("칸 오른쪽을 막는 벽(Floor 레이어 콜라이더, 칸 아래 RightWall). " +
        "타고 오르내리는 동안과 위층에 서 있을 때 켭니다(위층 칸 오른쪽은 바닥이 없음). 아래층에 서 있을 때는 끕니다. 비워 두면 켜고 끄지 않습니다.")]
    private GameObject rightWall;
    [SerializeField] private float duration = 1f;
    [SerializeField] private float delayDuration = 3f;
    [SerializeField] private Ease ease;

    [Header("빠른 이동")]
    [SerializeField, InspectorName("도착 카메라")] private OutsideCamera outsideCamera;
    [SerializeField, Min(0), InspectorName("화면 암전 시간 (초)")] private float fastFadeOutDuration = .35f;
    [SerializeField, Min(0), InspectorName("암전 후 이동 대기 (초)")] private float fastBeforeMoveDelay = .05f;
    [SerializeField, Min(0), InspectorName("이동 후 암전 유지 (초)")] private float fastAfterMoveDelay = .30f;
    [SerializeField, Min(0), InspectorName("화면 복원 시간 (초)")] private float fastFadeInDuration = .45f;

    [Header("CSV 메뉴")]
    [SerializeField, InspectorName("아래층 메뉴 씬 ID"), Tooltip("수리 전 아래층: 올라간다·취소")]
    private string bottomMenuSceneId = "outside_elevator_menu_bottom";
    [SerializeField, InspectorName("위층 메뉴 씬 ID"), Tooltip("수리 전 위층: 내려간다·취소")]
    private string topMenuSceneId = "outside_elevator_menu_top";
    [SerializeField, InspectorName("아래층 빠른 이동 메뉴 씬 ID"), Tooltip("flag.elevator_repaired 후 아래층: 빠른 이동·올라간다·취소")]
    private string bottomFastMenuSceneId = "outside_elevator_menu_bottom_fast";
    [SerializeField, InspectorName("위층 빠른 이동 메뉴 씬 ID"), Tooltip("flag.elevator_repaired 후 위층: 빠른 이동·내려간다·취소")]
    private string topFastMenuSceneId = "outside_elevator_menu_top_fast";

    [Header("하차 높이")]
    [SerializeField, InspectorName("아래층 하차 기준"), Tooltip("아래층에 도착하면 루나의 Y를 이 Transform의 Y에 맞춥니다(웹과 같음). " +
        "바 앞 시작 지점 BarSpawn을 연결하세요. 비우면 엘리베이터 이동량만큼만 옮겨, 상·하단 지점 간격과 층 높이 차이만큼 어긋납니다.")]
    private Transform bottomArrival;
    [SerializeField, InspectorName("위층 하차 기준"), Tooltip("위층에 도착하면 루나의 Y를 이 Transform의 Y에 맞춥니다(웹과 같음). " +
        "집 앞 시작 지점 HomeSpawn을 연결하세요. 비우면 엘리베이터 이동량만큼만 옮깁니다.")]
    private Transform topArrival;

    [Inject] private IConditionUtil conditions;
    [Inject] private ISceneFadeService fades;
    private CancellationTokenSource execution;
    private CancellationTokenSource movement;
    private UniTaskCompletionSource finished;
    private Ride ride;
    private bool isTop;
    private bool fastTravelling;
    public bool CanExecute => isActiveAndEnabled && execution == null;
    public bool CanCancel => !IsMoving;
    // 일반 탑승은 이동 입력을 잠가도 ESC로 설정창을 열 수 있다. 빠른 이동의 암전 구간만 ESC를 막는다.
    public bool BlocksEscape => fastTravelling;
    public bool IsMoving => movement != null;
    public bool IsTop => isTop;

    public void SetPosition(bool top)
    {
        if (execution != null) throw new InvalidOperationException("사용 중인 엘리베이터의 위치를 바꿀 수 없습니다.");
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("엘리베이터 상·하단 앵커가 없습니다.");
        isTop = top;
        transform.position = (top ? topPoint : bottomPoint).position;
        ApplyWalls();
    }

    // 칸 벽은 탑승 전 상태를 기억했다 되돌리지 않고, 지금 상태에서 매번 다시 정한다(QA-61).
    // 타고 오르내리는 동안(IsMoving, 이동 입력이 잠긴 구간)은 양쪽 모두, 서 있을 때는 위층에서만 오른쪽을 막는다.
    // 위층 칸 오른쪽은 바닥이 없고 왼쪽은 집 앞 복도로 이어진다. 아래층은 양쪽 모두 길이다.
    private void ApplyWalls()
    {
        bool moving = IsMoving;
        SetWall(leftWall, moving);
        SetWall(rightWall, moving || isTop);
    }

    private static void SetWall(GameObject wall, bool active)
    {
        if (wall != null && wall.activeSelf != active) wall.SetActive(active);
    }

    // 플레이어가 위층 집 문 앞에서 시작하면 엘리베이터도 위층에서 기다린다.
    private void Start()
    {
        var state = GameStateManager.Instance;
        SetPosition(OutsideStorySceneRules.ResolveSpawn(gameObject.scene.name, state) == EPlayerSpawn.Home);
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
        if (context.ChooseOutsideChoices == null || conditions == null)
            throw new InvalidOperationException("엘리베이터의 선택창과 진행 조건이 연결되지 않았습니다.");
        token.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        execution = source;
        var completion = finished = new UniTaskCompletionSource();
        try
        {
            var selected = await context.ChooseOutsideChoices(MenuSceneId, null, null, source.Token);
            source.Token.ThrowIfCancellationRequested();
            switch (selected.Id)
            {
                case null:
                case "cancel":
                    return;
                case "ride":
                    // 웹과 같이 일반 탑승은 이동·로고·카메라만 진행하고 방송 대사는 재생하지 않는다.
                    await MoveAsync(context.Player, source.Token);
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

    /// <summary>
    /// 도착 층에서 루나가 설 Y. 웹(outside.js)처럼 층마다 정해진 높이에 맞춘다.
    /// 엘리베이터 상·하단 지점 간격과 두 층의 루나 높이 차이가 달라서, 이동량만 더하면 도착 높이가 어긋난다.
    /// </summary>
    internal float ArrivalY(bool toTop, float actorStartY, float cabTravel)
    {
        var reference = toTop ? topArrival : bottomArrival;
        if (reference != null) return reference.position.y;
        Debug.LogWarning("[OutsideElevator] " + (toTop ? "위층" : "아래층") +
            " 하차 기준이 비어 있어 엘리베이터 이동량만큼만 옮깁니다. Inspector의 하차 높이에 HomeSpawn·BarSpawn을 연결하세요.", this);
        return actorStartY + cabTravel;
    }

    // 층과 수리 여부마다 CSV 메뉴를 따로 둔다. CSV 조건(when)은 선택지를 숨기지 않고 잠근 채 보여 주기 때문이다(웹도 수리 전엔 빠른 이동을 숨김).
    internal string MenuSceneId => IsRepaired()
        ? isTop ? topFastMenuSceneId : bottomFastMenuSceneId
        : isTop ? topMenuSceneId : bottomMenuSceneId;

    private async UniTask FastTravelAsync(IInteractor player, CancellationToken token)
    {
        if (topPoint == null || bottomPoint == null || outsideCamera == null || fades == null)
            throw new InvalidOperationException("빠른 이동의 상·하단 지점, 도착 카메라와 화면 페이드를 연결하세요.");
        ValidateFastDuration(fastFadeOutDuration);
        ValidateFastDuration(fastBeforeMoveDelay);
        ValidateFastDuration(fastAfterMoveDelay);
        ValidateFastDuration(fastFadeInDuration);

        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        // 일반 탑승과 같이 이동 입력과 방향 전환을 잠근다.
        using var lease = InteractionStateLease.Acquire(player);
        movement = source;
        fastTravelling = true;
        ApplyWalls();
        var actor = player.Transform;
        var actorStart = actor.position;
        var start = transform.position;
        bool startedAtTop = isTop;
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
            // 일반 탑승과 같이 승강기와 탑승자를 함께 옮기고, 루나는 도착 층의 하차 높이에 세운다.
            transform.position = destination;
            actor.position = new Vector3(destination.x, ArrivalY(!startedAtTop, actorStart.y, destination.y - start.y), destination.z);
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
                    fastTravelling = false;
                    if (ReferenceEquals(movement, source)) movement = null;
                    ApplyWalls();
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

    private async UniTask MoveAsync(IInteractor player, CancellationToken token)
    {
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("엘리베이터 상·하단 앵커가 없습니다.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        movement = source;
        ApplyWalls();
        // 웹(outside.js tickLift)처럼 탑승 중에는 이동 입력과 방향 전환을 받지 않는다(빠른 이동과 같은 잠금).
        // 탑승 중 걸을 수 있으면 승강기 통로와 겹친 2층 바닥에 밀려 칸 벽을 넘는다(QA-28).
        // 서 있는 모습은 상호작용 시작 때 Player.PrepareWorldInteraction이 맞추고, 잠금 중에는 애니메이션을 갱신하지 않는다.
        using var lease = InteractionStateLease.Acquire(player);
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
                ApplyWalls();
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

    /// <summary>한 번의 탑승에서만 필요한 위치·로고 상태를 보관함. 칸 벽은 OutsideElevator.ApplyWalls가 정한다.</summary>
    private sealed class Ride
    {
        private readonly OutsideElevator owner;
        private readonly Transform actor;
        private readonly CancellationToken token;
        private readonly Vector3 actorStart;
        private readonly Vector3 start;
        private readonly float arrivalY;
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
            start = owner.transform.position;
            Destination = (owner.isTop ? owner.bottomPoint : owner.topPoint).position;
            arrivalY = owner.ArrivalY(!owner.isTop, actorStart.y, Destination.y - start.y);
        }

        public void Begin()
        {
            actor.position = new Vector3(start.x, actor.position.y, start.z);
            owner.changeCameraModeEvent?.Raise((int)EOutsideCameraMode.Elevator);
        }

        public void MovePassenger()
        {
            // 부모를 바꾸지 않아 승강기 비활성화가 플레이어에게 전파되지 않게 함.
            if (actor == null) return;
            var cab = owner.transform.position;
            // 웹(outside.js tickLift의 x = EX)처럼 x·z는 승강기에 고정하고,
            // y는 출발 높이에서 도착 층 하차 높이까지 승강기 진행도에 맞춰 옮긴다.
            float travel = Destination.y - start.y;
            float y = Mathf.Abs(travel) > .0001f
                ? Mathf.LerpUnclamped(actorStart.y, arrivalY, (cab.y - start.y) / travel)
                : actor.position.y;
            actor.position = new Vector3(cab.x, y, cab.z);
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
            // 빠른 이동과 같이 도착 층에서도 승강기 x에 세운다.
            if (actor != null) actor.position = new Vector3(Destination.x, arrivalY, Destination.z);
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
        }
    }
}
