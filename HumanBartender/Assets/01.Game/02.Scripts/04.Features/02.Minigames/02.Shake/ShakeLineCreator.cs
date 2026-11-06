using UnityEngine;


/// <summary>
/// 쉐이킹 미니게임에서 점들을 연결하는 LineRenderer 경로를 생성하는 컴포넌트.
/// CreateLine()으로 초기화하고 SetLinePosition()으로 각 점의 위치를 설정한다.
/// </summary>
public class ShakeLineCreator : MonoBehaviour
{
    [Header("Path Line")]
    [HideInInspector, SerializeField] private Color startColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    [HideInInspector, SerializeField] private Color endColor = new Color(1f, 0.6f, 0.2f, 1f);
    [HideInInspector, SerializeField] private float pathWidth = 0.06f;

    [Tooltip("선 구간당 중간 포인트 수 (부드러움)")]
    [SerializeField] private int pointsPerSegment = 8;

    [Header("Sorting")]
    [HideInInspector, SerializeField] private int sortingOrder = 20;

    [SerializeField] private LineRenderer dotLine;


    public void SetLinePosition(int i, Vector3 pos)
    {
        if (dotLine != null) dotLine.SetPosition(i, dotLine.useWorldSpace ? pos : dotLine.transform.InverseTransformPoint(pos));
    }

    public void CreateLine()
    {
        if (dotLine == null)
        {
            Debug.LogError("[ShakeLine] 프리팹에 배치한 경로 LineRenderer를 연결해 주세요.", this);
            return;
        }
        dotLine.positionCount = pointsPerSegment;
    }
}

