using UnityEngine;

/// <summary>
/// 상호작용 가능한 엔티티의 공통 베이스. IInteractable/ITrackedble 구현(우선순위, 라벨, 오프셋)과
/// 포커스 진입/이탈 시 아웃라인 하이라이트 처리를 담당하며, 실제 상호작용 동작은 하위 클래스가 구현한다.
/// </summary>
public abstract class InteractiveEntity : OutsideEntity, IInteractable
{
    public string souceid;
    [SerializeField] private bool isAvaliable;
    [System.NonSerialized] protected bool isInteracting;
    [SerializeField] private string label;
    [System.NonSerialized] private bool isInter;
    [SerializeField] protected Vector2 buttonOffset;
    [SerializeField] protected Vector2 textOffset;
    [SerializeField] protected OutlineHighlight outlineHighlight;
    [SerializeField] protected InteractableEvent OnInteracted;
    [SerializeField] protected VoidEvent OnRefreshCondition;

    public bool IsInteracting { get => isInteracting; set => isInteracting = value; }
    public bool IsAvaliable { get => isAvaliable; set => isAvaliable = value; }
    public string EntityLabel { get => label; set => label = value; }
    public bool isInteract { get => isInter; set => isInter = value; }

    public NewInteractPointData? Definition { get; private set; }
    public string DialogueSceneId { get; private set; }
    public ENewInteractKind kind => Definition?.Kind ?? default;
    public EActivationMode ActivationMode => Definition?.ActivationMode ?? EActivationMode.None;
    public EActionType ActionType => Definition?.ActionType ?? EActionType.None;
    public string ActionRef => Definition?.ActionRef;
    public Step[] steps { get; private set; }
    int IInteractable.Priority => Definition?.Priority ?? 0;
    string IInteractable.Label => label;
    Vector2 ITrackedble.ButtonOffset => buttonOffset;
    Vector2 ITrackedble.TextOffset => textOffset;
    bool ITrackedble.IsAvaliable => isAvaliable;

    // 실행 설정은 CSV에서 선택한 행 한 곳에서 읽는다.
    public void BindContent(NewInteractPointData? definition, NewSceneData? dialogue, bool available)
    {
        Definition = definition;
        DialogueSceneId = dialogue?.Id;
        steps = dialogue?.Steps;
        isInteract = available;
    }

    public virtual bool SupportsAction(NewInteractPointData definition) => definition.ActionType == EActionType.Dialogue;

    public virtual void ApplySpot(NewSpotData spot)
    {
        if (!IsInteracting) transform.position = spot.Position;
    }
    public virtual void Init(InteractableEvent onInteracted, VoidEvent onRefresh)
    {
        OnInteracted = onInteracted;
        OnRefreshCondition = onRefresh;
    }
    /// <summary>실제 상호작용 동작. 대사 시작, 오브젝트 사용 등 하위 클래스에서 구현.</summary>
    public abstract void Interact(IInteractor player);

    /// <summary>상호작용 중이 아닐 때, 포커스를 받으면 아웃라인을 켠다.</summary>
    public virtual void OnFocusEnter()
    {
        if(!isInteracting && outlineHighlight != null)
            outlineHighlight.SetHighlight(true);
    }
    /// <summary>포커스를 잃으면 아웃라인을 끈다.</summary>
    public virtual void OnFocusExit()
    {
        if(outlineHighlight != null)
            outlineHighlight.SetHighlight(false);
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 offset = (Vector3)(buttonOffset * 0.0025f);

        Color previous = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position + offset+new Vector3(0,-0.15f,0),
            new Vector3(0.2f, 0.1f, 0f));
        Gizmos.color = previous;
    }
}
