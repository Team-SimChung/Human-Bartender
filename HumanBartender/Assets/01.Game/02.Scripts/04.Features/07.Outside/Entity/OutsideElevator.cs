using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>엘리베이터의 이동과 탑승자·카메라 복원을 담당함.</summary>
public class OutsideElevator : MonoBehaviour, IWorldInteractionAction
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

    private CancellationTokenSource movement;
    private UniTaskCompletionSource finished;
    private Ride ride;
    private bool isTop;
    public bool CanExecute => isActiveAndEnabled && movement == null;
    public bool IsMoving => movement != null;
    public bool IsTop => isTop;

    public void SetPosition(bool top)
    {
        if (movement != null) throw new InvalidOperationException("이동 중인 엘리베이터의 위치를 바꿀 수 없습니다.");
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("엘리베이터 상·하단 앵커가 없습니다.");
        isTop = top;
        transform.position = (top ? topPoint : bottomPoint).position;
    }

    private void Start() => SetPosition(GameStateManager.Instance.GameFlow == EGameFlow.CommuteIn);
    private void OnDisable()
    {
        movement?.Cancel();
        ride?.RestorePose();
    }

    public async UniTask StopAsync()
    {
        if (movement == null) return;
        var completion = finished;
        movement.Cancel();
        await completion.Task;
    }

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute || context.Player?.Transform == null)
            throw new InvalidOperationException("현재 엘리베이터에 탑승할 수 없습니다.");
        token.ThrowIfCancellationRequested();
        finished = new UniTaskCompletionSource();
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
            try
            {
                if (dialogue.HasValue)
                {
                    var result = await dialogue.Value;
                    // 이동 복원이 끝난 뒤 완료 이력과 다음 방송을 갱신함.
                    if (radio != null) radio.CompletePlayback(radioSceneId, result);
                    if (result.Status == StoryExecutionStatus.Failed)
                        throw new InvalidOperationException("엘리베이터 방송 재생 실패: " + result.Error);
                }
            }
            finally { finished.TrySetResult(); }
        }
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
