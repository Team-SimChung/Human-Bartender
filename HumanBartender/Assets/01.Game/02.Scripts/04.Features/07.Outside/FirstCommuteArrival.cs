using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 0일차 첫 출근의 거리 도착 연출. 바 문 왼쪽에서 바 앞까지 걸어 들어오고, 바에 들어가기 전까지
/// 바 입구 주변으로 이동 범위를 막는다. 시작 자리는 OutsideStorySceneRules가 고르고 Player가 옮기며,
/// 이 컴포넌트는 도착한 뒤의 걷기와 경계만 소유한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FirstCommuteArrival : MonoBehaviour
{
    [Tooltip("자동으로 걸어갈 도착 지점. 바 문 바로 왼쪽에 둔다. x 좌표만 사용한다.")]
    [SerializeField] private Transform walkTarget;

    [Tooltip("첫 출근 동안만 켜는 투명 벽 묶음. 자식 BoxCollider2D가 플레이어 이동 충돌 레이어(Ground)에 있어야 막힌다.")]
    [SerializeField] private GameObject bounds;

    private void Awake()
    {
        // 씬에 켜진 채 저장되어도 첫 출근이 아닌 날에는 길을 막지 않는다.
        if (bounds != null) bounds.SetActive(false);
    }

    /// <summary>플레이어를 바 앞까지 걷게 한 뒤 첫 출근 경계를 켠다.</summary>
    public async UniTask PlayAsync(Player player, CancellationToken token)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (walkTarget == null || bounds == null)
            throw new InvalidOperationException("첫 출근 도착 지점과 경계를 연결하세요.");
        await player.WalkToXAsync(walkTarget.position.x, token);
        token.ThrowIfCancellationRequested();
        bounds.SetActive(true);
    }

    private void OnValidate()
    {
        if (walkTarget == null) Debug.LogWarning("[FirstCommuteArrival] Walk Target이 비어 있습니다.", this);
        if (bounds == null) Debug.LogWarning("[FirstCommuteArrival] Bounds가 비어 있습니다.", this);
    }

    private void OnDrawGizmos()
    {
        if (walkTarget == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(walkTarget.position + Vector3.down * .5f, walkTarget.position + Vector3.up * 1.5f);
    }
}
