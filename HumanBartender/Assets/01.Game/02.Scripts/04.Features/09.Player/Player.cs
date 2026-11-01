using System.Threading;
using Cysharp.Threading.Tasks;
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
    [Header("Spawn")]
    [Tooltip("퇴근할 때 바 문 앞에서 시작하는 위치.")]
    [SerializeField] private Transform barspawn;
    [Tooltip("집 안, 또는 거리 위층 집 문 앞에서 시작하는 위치.")]
    [SerializeField] private Transform homespawn;
    [Tooltip("0일차 첫 출근 때 거리 1층 바 문 왼쪽에서 시작하는 위치. OutSide 씬에서만 연결한다.")]
    [SerializeField] private Transform firstCommuteSpawn;
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



    /// <summary>현재 씬·일차·출퇴근 흐름에 맞는 시작 자리로 옮긴다. 자리 선택 규칙은 OutsideStorySceneRules가 소유한다.</summary>
    public bool TrySetPosition()
    {
        var state = GameStateManager.Instance;
        return TrySetPosition(OutsideStorySceneRules.ResolveSpawn(gameObject.scene.name, state.CurrentDay, state.GameFlow));
    }

    /// <summary>정해진 시작 자리의 씬 Transform으로 옮기고 그 자리의 기본 방향을 본다.</summary>
    public bool TrySetPosition(EPlayerSpawn spawn)
    {
        Transform destination = spawn switch
        {
            EPlayerSpawn.Bar => barspawn,
            EPlayerSpawn.FirstCommute => firstCommuteSpawn,
            _ => homespawn,
        };
        if (destination == null) return false;
        transform.position = destination.position;
        // 첫 출근은 바 쪽(오른쪽)으로 걸어 들어오고, 퇴근은 집 쪽(왼쪽)을 보고 나온다.
        if (spawn != EPlayerSpawn.Home) movement2D.Face(spawn == EPlayerSpawn.FirstCommute);
        return true;
    }

    /// <summary>
    /// 입력을 잠근 채 평소 이동·애니메이션 경로로 targetX까지 걷는다. 옵션 창이 열려 있으면 기다리고,
    /// 벽에 막혀 더 나아가지 못하면 그 자리에서 끝낸다. 취소되어도 입력과 상태는 원래대로 돌린다.
    /// </summary>
    public async UniTask WalkToXAsync(float targetX, CancellationToken token)
    {
        using var lease = InteractionStateLease.Acquire(this, EInteractorState.ForceMove);
        float direction = Mathf.Sign(targetX - transform.position.x);
        movement2D.SetScriptedInput(direction);
        try
        {
            float lastX = transform.position.x;
            int stalledFrames = 0;
            while ((targetX - transform.position.x) * direction > 0f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (UIDisplayOptions.IsOptionOpen) continue;
                stalledFrames = Mathf.Approximately(lastX, transform.position.x) ? stalledFrames + 1 : 0;
                if (stalledFrames > StalledFrameLimit) break;
                lastX = transform.position.x;
            }
        }
        finally
        {
            // 씬이 바뀌어 취소된 경우에는 플레이어가 이미 파괴되었으므로 되돌릴 것이 없다.
            if (this != null && movement2D != null) movement2D.SetScriptedInput(null);
            if (this != null && animator2D != null) animator2D.Handle(0);
        }
    }

    // 장면 준비 대기나 벽 때문에 이만큼 연속으로 제자리면 자동 걷기를 끝낸다.
    const int StalledFrameLimit = 30;
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
        if (UIDisplayOptions.IsOptionOpen) { animator2D?.Stop(); return; }
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
