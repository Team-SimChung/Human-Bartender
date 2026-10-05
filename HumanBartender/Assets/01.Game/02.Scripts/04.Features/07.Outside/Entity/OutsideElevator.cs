using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>탑승 이동과 입력·라디오·카메라 복원을 함께 관리한다.</summary>
public class OutsideElevator : InteractiveEntity
{
    [SerializeField] LogoFade logoEvent;
    [SerializeField] float logoFadeTiming = 0.2f;
    [SerializeField] OutsideElevatorRadio radio;
    [SerializeField] IntEvent changeCameraModeEvent;
    [SerializeField] float cameraReturnTiming = 0.8f;
    [SerializeField] Transform topPoint;
    [SerializeField] Transform bottomPoint;
    [SerializeField] GameObject wallColider;
    [SerializeField] float duration = 1f;
    [SerializeField] float delayDuration = 3f;
    [SerializeField] Ease ease;

    CancellationTokenSource movement;
    UniTaskCompletionSource finished;
    Action restorePose;
    bool isTop;

    public override bool SupportsAction(NewInteractPointData definition) =>
        definition.ActionType == EActionType.System && definition.ActionRef == OutsideActions.ElevatorToggle;

    // 엘리베이터의 실제 위치는 씬의 상·하단 앵커가 결정한다.
    public override void ApplySpot(NewSpotData spot) { }

    public void SetPosition(bool top)
    {
        if (movement != null) throw new InvalidOperationException("Cannot reposition a moving elevator.");
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("Elevator anchors are missing.");
        isTop = top;
        transform.position = (top ? topPoint : bottomPoint).position;
    }

    void Start() => SetPosition(GameStateManager.Instance.GameFlow == EGameFlow.CommuteIn);
    void OnDisable()
    {
        movement?.Cancel();
        restorePose?.Invoke();
    }

    public async UniTask StopAsync()
    {
        if (movement == null) return;
        var completion = finished;
        movement.Cancel();
        await completion.Task;
    }

    public override void Interact(IInteractor player)
    {
        if (!isActiveAndEnabled || !isInteract || movement != null || player?.Transform == null) return;
        finished = new UniTaskCompletionSource();
        MoveAndCompleteAsync(player, finished).Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
    }

    async UniTask MoveAndCompleteAsync(IInteractor player, UniTaskCompletionSource completion)
    {
        try { await MoveAsync(player); }
        finally { completion.TrySetResult(); }
    }

    async UniTask MoveAsync(IInteractor player)
    {
        if (topPoint == null || bottomPoint == null) throw new InvalidOperationException("Elevator anchors are missing.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        movement = source;
        isInteracting = true;
        using var lease = InteractionStateLease.Acquire(player, EInteractorState.ForceMove);
        Transform actor = player.Transform;
        Vector3 actorStart = actor.position;
        Vector3 start = transform.position;
        Vector3 target = (isTop ? bottomPoint : topPoint).position;
        bool oldWall = wallColider != null && wallColider.activeSelf;
        bool arrived = false, restored = false;
        Sequence sequence = null;
        bool tweenAwaitStarted = false;
        UniTask logo = UniTask.CompletedTask;
        restorePose = () =>
        {
            if (restored) return;
            restored = true;
            if (actor != null)
            {
                if (!arrived) actor.position = actorStart;
            }
            if (!arrived && this != null) transform.position = start;
            if (wallColider != null) wallColider.SetActive(oldWall);
        };
        try
        {
            actor.position = new Vector3(start.x, actor.position.y, start.z);
            if (wallColider != null) wallColider.SetActive(true);
            changeCameraModeEvent?.Raise((int)EOutsideCameraMode.Elevator);
            OnInteracted?.Raise(this);
            if (radio != null) radio.Interact(null);

            sequence = DOTween.Sequence();
            var previousPosition = start;
            // 부모를 바꾸지 않아 승강기 비활성화/파괴가 플레이어에게 전파되지 않는다.
            sequence.Append(transform.DOMove(target, Mathf.Max(0, duration)).SetEase(ease).OnUpdate(() =>
            {
                if (actor != null) actor.position += transform.position - previousPosition;
                previousPosition = transform.position;
            }));
            if (!GameStateManager.Instance.IsOutsideLogo && logoEvent != null)
                sequence.InsertCallback(Mathf.Max(0, duration) * Mathf.Clamp01(logoFadeTiming),
                    () => logo = ShowLogoAsync(source.Token).Preserve());
            sequence.InsertCallback(Mathf.Max(0, duration) * Mathf.Clamp01(cameraReturnTiming),
                () => changeCameraModeEvent?.Raise((int)EOutsideCameraMode.Follow));
            tweenAwaitStarted = true;
            await sequence.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, source.Token);
            source.Token.ThrowIfCancellationRequested();
            transform.position = target;
            arrived = true;
            isTop = !isTop;
            restorePose();
            if (radio != null) await radio.EndInteractAsync();
            await logo;
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0, delayDuration)), cancellationToken: source.Token);
        }
        finally
        {
            source.Cancel();
            if (!tweenAwaitStarted) sequence?.Kill();
            restorePose?.Invoke();
            restorePose = null;
            try
            {
                if (radio != null) await radio.EndInteractAsync();
                await logo;
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (ReferenceEquals(movement, source)) movement = null;
                isInteracting = false;
                if (changeCameraModeEvent != null) changeCameraModeEvent.Raise((int)EOutsideCameraMode.Follow);
            }
        }
    }

    async UniTask ShowLogoAsync(CancellationToken token)
    {
        await logoEvent.ShowLogoAsync(token);
        token.ThrowIfCancellationRequested();
        GameStateManager.Instance.IsOutsideLogo = true;
    }
}
