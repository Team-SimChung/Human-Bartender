using UnityEngine;

/// <summary>
/// 플레이어 파사드. 이동/애니메이션/상호작용 감지 컴포넌트를 조합해 상태(state)에 따라 매 프레임 위임 호출한다.
/// IInteractor 구현체이면서 동시에 (다소 특이하게) 자기 자신을 상호작용 대상으로도 취급해 InteractorEvent에서
/// Interact(this)를 호출한다 — 엘리베이터 등 "상호작용 주체가 곧 트리거 대상"인 케이스를 위한 것으로 보인다.
/// </summary>
public class Player : MonoBehaviour, IInteractor
{
    [Header("Composition")]
    [SerializeField] PlayerMovement2D movement2D;
    [SerializeField] PlayerAnimator2D animator2D;

    [SerializeField] InteractionDetector detector;
    [SerializeField] EInteractorState state = EInteractorState.None;

    [Header("Stupid Issue")]
    [SerializeField] protected ITrackedbleEvent OnTrackedText;
    [SerializeField] private bool isAvaliable;
    [SerializeField] protected Vector2 buttonOffset;
    [SerializeField] Transform barspawn;
    [SerializeField] Transform homespawn;

    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
    public EInteractorState State { get => state; set => state = value; }


    Vector2 ITrackedble.ButtonOffset => buttonOffset;
    Vector2 ITrackedble.TextOffset => buttonOffset;
    bool ITrackedble.IsAvaliable => isAvaliable;



    public void setpos()
    {
        if(barspawn != null && homespawn != null)
        {
            Debug.Log($"{GameStateManager.Instance.GameFlow}");
            if(GameStateManager.Instance.GameFlow != EGameFlow.CommuteOut)
            {
                transform.position = homespawn.position;
            }
            else
            {
                transform.position = barspawn.position;
            }
        }
    }
    /// <summary>
    /// 상태별 게이트: Lock/Interct 상태에서는 완전히 정지, ForceMove(엘리베이터 탑승 등) 상태에서는
    /// 이동/애니메이션은 계속하되 상호작용 감지는 멈춘다.
    /// </summary>
    public void Update()
    {
        if (state == EInteractorState.Lock) return;
        if (state == EInteractorState.Interct) return;

        movement2D.Handle();
        animator2D.Handle(movement2D.VelocityX);

        if (state == EInteractorState.ForceMove) return;
        detector.Handle();
    }

    /// <summary>플레이어 조작을 잠그고 애니메이션을 정지시킨다 (대사 진행 중 등).</summary>
    public void Lock()
    {
        animator2D.Handle(0);
        state = EInteractorState.Lock;
    }
    /// <summary>조작 잠금을 해제한다.</summary>
    public void UnLock() => state = EInteractorState.None;

    /// <summary>IInteractable 구현: 다른 상호작용 대상이 플레이어 자신에게 상호작용을 걸 때 텍스트 추적 이벤트를 발생시킨다.</summary>
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