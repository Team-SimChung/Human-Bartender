using UnityEngine;

/// <summary>
/// LineRenderer로 원 하나를 그린다. 병따기의 '조여드는 고리'와 '목표 고리' 양쪽에 쓴다.
///
/// 스프라이트가 아니라 선으로 그리는 이유는 반지름이 매 프레임 변하기 때문이다. 스프라이트를
/// 스케일로 늘리면 선 두께까지 같이 늘어나 조여들수록 굵어져 보이는데, 그러면 판정 순간
/// 두 고리의 두께가 달라 어디서 겹친 건지 눈으로 읽기 어렵다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class CapRing : MonoBehaviour
{
    [SerializeField] int segments = 64;
    [SerializeField] float lineWidth = 0.04f;
    [SerializeField] int sortingOrder = 20;

    LineRenderer line;
    float radius = 1f;

    void Awake()
    {
        line = GetComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 4;
        line.sortingOrder = sortingOrder;

        // Sprites/Default는 정점 색을 그대로 받으므로 머티리얼을 따로 만들지 않아도 색이 먹는다.
        line.material = new Material(Shader.Find("Sprites/Default"));

        Rebuild();
    }

    /// <summary>반지름(월드 단위). 0 이하면 그리지 않는다.</summary>
    public void SetRadius(float value)
    {
        radius = Mathf.Max(0f, value);
        line.enabled = radius > 0.001f;

        if (line.enabled) Rebuild();
    }

    public void SetColor(Color color)
    {
        line.startColor = color;
        line.endColor = color;
    }

    public void SetVisible(bool visible) => line.enabled = visible && radius > 0.001f;

    void Rebuild()
    {
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
        }
    }

    void OnDestroy()
    {
        if (line != null && line.material != null) Destroy(line.material);
    }
}
