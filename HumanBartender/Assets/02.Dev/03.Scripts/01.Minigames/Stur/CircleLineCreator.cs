using UnityEngine;

/// <summary>
/// 스터링 미니게임에서 타원형 경로를 LineRenderer로 생성하는 컴포넌트.
/// BuildCircle()을 호출하면 지정한 반지름/중심 위치로 원형 라인을 구성한다.
/// </summary>
public class CircleLineCreator : MonoBehaviour
{
    [Header("Path Line")]
    [SerializeField] private Color lineColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    [SerializeField] float radiusX = 2f;
    [SerializeField] float radiusY = 2f;
    [SerializeField] int segments = 64;
    [SerializeField] float width = 0.05f;
    [SerializeField] Vector3 centerPos;
    [SerializeField] private int sortingOrder = 20;

    [SerializeField] LineRenderer lr;

    public void BuildCircle(float radX, float radY, Vector3 center)
    {
        centerPos = center;
        radiusX = radX;
        radiusY = radY;

        lr.transform.position = centerPos;
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sortingOrder = sortingOrder - 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.startWidth = lr.endWidth = width;
        lr.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radiusX,
                Mathf.Sin(angle) * radiusY,
                0f
            ));
        }
    }
}