using UnityEngine;

/// <summary>
/// 갱단 경비가 통행을 허락하기 전까지 아래층 거리 왼쪽(갱단 구역)으로 걸어가지 못하게 막는다.
/// 웹 outside-neighborhood.js clampBoundary는 루나 x를 경비 x + 0.34 이상으로 고정한다. 엔진은 같은 자리에
/// 투명 벽(플레이어 이동 충돌 레이어)을 켜서 막는다. 조건은 경비 대화와 같은 OutsideStorySceneRules.IsGangPassBlocked다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GangPassBoundary : MonoBehaviour
{
    [Tooltip("통행 허가 전까지만 켜는 투명 벽 묶음. 평소에는 꺼 두고 이 컴포넌트가 켠다.")]
    [SerializeField] private GameObject bounds;

    [Tooltip("막는 벽. 플레이어 이동 충돌 레이어(Floor)에 있어야 막힌다. " +
             "오른쪽 면 = 경비 x + 0.34(웹 clampBoundary, 루나 발 위치) − 루나 충돌 반폭 0.16.")]
    [SerializeField] private BoxCollider2D wall;

    private IConditionUtil conditions;
    private Transform player;
    private bool warnedInside;

    /// <summary>벽 오른쪽 면의 월드 x. 이보다 왼쪽에 있는 플레이어는 이미 구역 안에 있다.</summary>
    public float WallRightFace => wall.transform.TransformPoint(wall.offset + new Vector2(wall.size.x * .5f, 0f)).x;

    private void Awake()
    {
        // 씬에 켜진 채 저장되어도 조건을 확인하기 전에는 길을 막지 않는다.
        if (bounds != null) bounds.SetActive(false);
    }

    /// <summary>씬 준비가 끝나기 전에 부른다. 이후 매 프레임 통행 조건을 확인해 벽을 켜고 끈다.</summary>
    public void Initialize(IConditionUtil conditionUtil, Transform playerTransform)
    {
        conditions = conditionUtil;
        player = playerTransform;
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (bounds == null || wall == null || conditions == null || player == null) return;
        var state = GameStateManager.Instance;
        bool blocked = OutsideStorySceneRules.IsGangPassBlocked(state.CurrentDay, state.GameFlow, conditions);
        if (blocked && !bounds.activeSelf && player.position.x < WallRightFace)
        {
            // 저장 복원·테스트 순간이동으로 이미 구역 안에 있으면 벽 안에 갇히지 않게 나올 때까지 켜지 않는다.
            if (!warnedInside) Debug.LogWarning("[GangPass] 플레이어가 통행 허가 전에 갱단 구역 안에 있어, 나올 때까지 벽을 켜지 않습니다.", this);
            warnedInside = true;
            return;
        }
        warnedInside = false;
        if (bounds.activeSelf != blocked) bounds.SetActive(blocked);
    }

    private void OnValidate()
    {
        if (bounds == null) Debug.LogWarning("[GangPass] Bounds가 비어 있습니다.", this);
        if (wall == null) Debug.LogWarning("[GangPass] Wall이 비어 있습니다.", this);
    }

    private void OnDrawGizmos()
    {
        if (wall == null) return;
        Gizmos.color = new Color(1f, .4f, .2f);
        float x = WallRightFace;
        Vector3 center = wall.transform.TransformPoint(wall.offset);
        Gizmos.DrawLine(new Vector3(x, center.y - 1f), new Vector3(x, center.y + 1f));
    }
}
