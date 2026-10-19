using UnityEngine;

/// <summary>
/// 플레이어의 상태에 맞춰 이동·애니메이션·상호작용 감지를 호출함.
/// 기존 추적 이벤트를 유지하기 위해 InteractorEvent에서 자신을 전달함.
/// </summary>
public class Player : MonoBehaviour, IInteractor
{
    [Header("Composition")]
    [SerializeField] private PlayerMovement2D movement2D;
    [SerializeField] private PlayerAnimator2D animator2D;

    [SerializeField] private InteractionDetector detector;
    [SerializeField] private EInteractorState state = EInteractorState.None;

    [Header("Stupid Issue")]
    [SerializeField] protected ITrackedbleEvent OnTrackedText;
    [SerializeField] private bool isAvaliable;
    [SerializeField] protected Vector2 buttonOffset;
    [SerializeField] private Transform barspawn;
    [SerializeField] private Transform homespawn;
    private WorldInteractionSceneContext sceneEntities;

    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public EInteractorState State { get => state; set => state = value; }

    /// <summary>공통 상호작용을 시작할 때 연결된 애니메이터와 기존 감지기의 포커스를 정리함.</summary>
    public void PrepareWorldInteraction()
    {
        animator2D?.Stop();
        detector?.CurrentTarget?.OnFocusExit();
        detector?.InteractedTarget();
    }


    Vector2 ITrackedble.ButtonOffset => buttonOffset;
    Vector2 ITrackedble.TextOffset => buttonOffset;
    bool ITrackedble.IsAvaliable => isAvaliable;



    public bool TrySetPosition()
    {
        bool atHome = gameObject.scene.name == "Home";
        Transform destination = atHome || GameStateManager.Instance.GameFlow != EGameFlow.CommuteOut
            ? homespawn : barspawn;
        if (destination == null) return false;
        transform.position = destination.position;
        return true;
    }
    public void setpos()
    {
        if (!TrySetPosition()) Debug.LogError("[Player] 현재 씬의 시작 위치가 연결되지 않았습니다.", this);
    }
    /// <summary>
    /// 상태별 게이트: Lock/Interct 상태에서는 완전히 정지, ForceMove(엘리베이터 탑승 등) 상태에서는
    /// 이동/애니메이션은 계속하되 상호작용 감지는 멈춤.
    /// </summary>
    public void Update()
    {
        if (gameObject.scene.name is "Home" or "OutSide")
        {
            if (sceneEntities == null)
                foreach (var candidate in FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { sceneEntities = candidate; break; }
            if (sceneEntities == null || !sceneEntities.IsReady) return;
        }
        if (state == EInteractorState.Lock) return;
        if (state == EInteractorState.Interct) return;

        movement2D.Handle();
        animator2D.Handle(movement2D.VelocityX);

        if (state == EInteractorState.ForceMove) return;
        if (detector != null && detector.isActiveAndEnabled) detector.Handle();
    }

    /// <summary>플레이어 조작을 잠그고 애니메이션을 정지시킨다 (대사 진행 중 등).</summary>
    public void Lock()
    {
        animator2D.Handle(0);
        state = EInteractorState.Lock;
    }
    /// <summary>조작 잠금을 해제한다.</summary>
    public void UnLock() => state = EInteractorState.None;

    /// <summary>IInteractable 구현: 다른 상호작용 대상이 플레이어 자신에게 상호작용을 걸 때 텍스트 추적 이벤트를 발생시킴.</summary>
    public void Interact(IInteractor player)
    {
        OnTrackedText?.Raise(this);
    }

    /// <summary>InteractionDetector가 상호작용 입력을 처리한 뒤 호출: 플레이어 자기 자신에 대한 Interact를 트리거한다.</summary>
    public void InteractorEvent()
    {
        Interact(this);
    }
}
