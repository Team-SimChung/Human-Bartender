using UnityEngine;

/// <summary>
/// 병의 외곽선을 메시로 그린다. PourManager가 액체를 가두는 내부 프로파일(몸통 반너비 → 어깨 → 병목)을
/// 그대로 넘겨받아 거기에 유리 두께만 더해 그리므로, "보이는 병 모양"과 "액체가 갇히는 형태"가
/// 어긋날 수 없다. 스프라이트를 따로 두면 병 모양 값을 만질 때마다 둘이 어긋나 액체가 병 밖으로
/// 삐져나와 보였다.
/// </summary>
public class BottleSilhouette : MonoBehaviour
{
    [SerializeField] Color glassColor = new Color(0.75f, 0.8f, 0.85f, 0.4f);
    [Tooltip("내부 프로파일 바깥으로 그릴 유리벽 두께(병 로컬 좌표).")]
    [SerializeField] float wallThickness = 0.08f;
    [SerializeField] int sortingOrder = 10;

    MeshRenderer meshRenderer;
    Mesh mesh;
    Material material;

    /// <summary>
    /// 내부 프로파일을 받아 실루엣을 다시 만든다. 아래에서부터 몸통(사각형) → 어깨(사다리꼴) →
    /// 병목 통로(사각형)로, 세 조각을 위아래로 맞붙여 그린다.
    ///
    /// 삼각형 부채꼴로 한 번에 분할하지 않는 이유는 이 8각형이 오목하기 때문이다 — 어깨의 사선이
    /// 통로 벽을 만나는 두 모서리가 안쪽으로 꺾여 있어서, 한 점에서 뻗는 부채꼴로는 그 바깥의
    /// 빈 공간까지 칠해버린다.
    /// </summary>
    public void Build(Vector2 interiorHalfExtents, float neckHalfWidth, float shoulderY, float channelY)
    {
        EnsureCreated();

        float m = wallThickness;
        float bodyHalfWidth = interiorHalfExtents.x + m;
        float mouthHalfWidth = neckHalfWidth + m;
        float bottomY = -interiorHalfExtents.y - m;
        float topY = interiorHalfExtents.y + m;

        var vertices = new Vector3[]
        {
            new Vector3(-bodyHalfWidth, bottomY, 0f),   // 0
            new Vector3(bodyHalfWidth, bottomY, 0f),    // 1
            new Vector3(bodyHalfWidth, shoulderY, 0f),  // 2  어깨 시작
            new Vector3(mouthHalfWidth, channelY, 0f),  // 3  어깨 끝 = 통로 시작
            new Vector3(mouthHalfWidth, topY, 0f),      // 4  입구
            new Vector3(-mouthHalfWidth, topY, 0f),     // 5
            new Vector3(-mouthHalfWidth, channelY, 0f), // 6
            new Vector3(-bodyHalfWidth, shoulderY, 0f), // 7
        };

        var colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = glassColor;

        var triangles = new int[]
        {
            0, 1, 2,  0, 2, 7,  // 몸통
            7, 2, 3,  7, 3, 6,  // 어깨
            6, 3, 4,  6, 4, 5,  // 통로
        };

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void EnsureCreated()
    {
        if (mesh != null) return;

        var go = new GameObject("Silhouette");
        go.transform.SetParent(transform, false);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sortingOrder = sortingOrder;

        material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.material = material;

        mesh = new Mesh { name = "BottleSilhouetteMesh" };
        meshFilter.mesh = mesh;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }
}
