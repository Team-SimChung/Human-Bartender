using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 대사를 가진 NPC 엔티티의 베이스. 상호작용 오브젝트도 이 클래스를 쓴다 —
/// 따로 있던 InteractiveObjectEntity는 구형 대사(flows) 전용이라 걷어냈다.
///
/// 대사는 InteractiveEntityManager가 street 데이터에서 찾아 steps에 꽂아 준다.
/// </summary>
public class InteractiveNPCEntity : InteractiveEntity
{
    [SerializeField] protected ITrackedbleEvent OnTrackedText;
    [SerializeField] protected DialogueRunner runner;
    [SerializeField] protected OutsideDialoguePresenter presenter;
    protected InteractiveEntityManager entityManager;
    CancellationTokenSource interaction;

    protected virtual void OnDisable() => interaction?.Cancel();

    protected bool isTalking { get => isInteracting; set => isInteracting = value; }

    public void Init(
        InteractableEvent onInteracted,
        VoidEvent onRefresh,
        ITrackedbleEvent onTrackedText,
        DialogueRunner runner,
        OutsideDialoguePresenter presenter,
        InteractiveEntityManager manager)
    {
        // 부모 필드 초기화
        base.Init(onInteracted, onRefresh);
        entityManager = manager;
        // NPC 전용 필드 초기화
        OnTrackedText = onTrackedText;
        this.runner = runner;
        this.presenter = presenter;
    }
    /// <summary>
    /// 대화 종료·취소 후 입력을 복원하고 CSV 조건을 다시 확인한다.
    /// </summary>
    public override async void Interact(IInteractor player)
    {
        if (!isActiveAndEnabled || !isInteract || isTalking || entityManager == null || !entityManager.IsReady || runner == null || presenter == null) return;
        if (runner.IsRunning && presenter.playMode != EActivationMode.Proximity) return;
        isTalking = true;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        interaction = source;
        var selectedSteps = steps;
        string sceneId = DialogueSceneId;
        IDisposable lease = null;
        try
        {
            await runner.StopAsync();
            var token = source.Token;
            token.ThrowIfCancellationRequested();
            if (ActivationMode == EActivationMode.Interact) lease = InteractionStateLease.Acquire(player);
            OnInteracted?.Raise(this);
            OnTrackedText?.Raise(this);
            player?.InteractorEvent();
            presenter.playMode = ActivationMode;
            runner.Bind(presenter);
            var result = await runner.PlayOutsideAsync(selectedSteps, token);
            if (entityManager != null) entityManager.CompleteDialogue(sceneId, result);
            if (result.Status == StoryExecutionStatus.Failed) Debug.LogError($"[Interact] {sceneId}: {result.Error}");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"[Interact] {DialogueSceneId}: {e}"); }
        finally
        {
            lease?.Dispose();
            if (ReferenceEquals(interaction, source)) interaction = null;
            isTalking = false;
            if (this != null && OnRefreshCondition != null) OnRefreshCondition.Raise(new Void());
        }
    }
}
