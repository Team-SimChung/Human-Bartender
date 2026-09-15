using UnityEngine;

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

    protected bool isTalking = false;

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
    /// 플레이어 상태를 Interct로 잠그고 대사를 재생한다. 재생 완료 후 다음 flow로 인덱스를 순환시키고
    /// (Conditional이면 매번 0으로 리셋) 상태를 원복, 조건 갱신 이벤트를 발생시킨다.
    /// </summary>
    public override async void Interact(IInteractor player)
    {
        if (isTalking)
        {
            return;
        }
        if (presenter.playMode == EActivationMode.Proximity&&ActionType==EActionType.Dialogue)
        {
            runner.Stop();
        }
        isTalking = true;
        isInteracting = true;
        if(ActivationMode == EActivationMode.Interact)
        player.State = EInteractorState.Interct;
        string playingSceneId = DialogueSceneId;
        OnInteracted?.Raise(this);
        OnTrackedText?.Raise(this);
        player.InteractorEvent();
        presenter.playMode = ActivationMode;
        runner.Bind(presenter);

        // SO에서 첫 번째 Scene의 Steps 배열을 추출하여 실행
        if (steps != null)
        {
            await runner.PlayOutsideAsync(steps);
        }
        else
        {
            Debug.LogError($"[Interact] 지정된 스크립트를 찾을 수 없습니다.");
        }

        isTalking = false;
        isInteracting = false;
        player.State = EInteractorState.None;
        entityManager.CompleteDialogue(playingSceneId);
        OnRefreshCondition?.Raise(new Void());
    }
}