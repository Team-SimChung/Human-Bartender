using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Freeform Light2D의 원뿔 모양을 그대로 따라 그리는 가산 빛줄기.
/// 가로등이 바닥 스프라이트와 합쳐져 있어도 라이트만으로 배치하며, 라이트 모양을 고치면 빛줄기도 따라간다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class LightShaft : MonoBehaviour
{
    [SerializeField] Light2D source;
    [SerializeField] Material material;
    [Tooltip("ForeLightground에 두면 물웅덩이 반사에도 함께 비친다.")]
    [SerializeField] string sortingLayer = "ForeLightground";
    [SerializeField] int sortingOrder = 50;
    [Header("모양")]
    [Tooltip("라이트 모양 대비 빛줄기 길이.")]
    [SerializeField, Range(0.3f, 2.5f)] float length = 1.15f;
    [Tooltip("라이트 모양 대비 빛줄기 폭.")]
    [SerializeField, Range(0.3f, 2f)] float width = 0.9f;
    [Header("빛")]
    [SerializeField, Range(0, 2)] float intensity = 0.3f;
    [Tooltip("0이면 tint, 1이면 라이트 색을 그대로 쓴다.")]
    [SerializeField, Range(0, 1)] float useLightColor = 0.85f;
    [SerializeField] Color tint = new Color(1, 0.9f, 0.75f, 1);
    [Tooltip("클수록 램프 가까이에서만 밝고 빨리 사라진다.")]
    [SerializeField, Range(0.3f, 4)] float falloff = 1.6f;
    [SerializeField, Range(0.05f, 1)] float edgeSoftness = 0.6f;
    [Tooltip("램프 바로 아래에서 빛줄기가 생겨나는 길이(비율).")]
    [SerializeField, Range(0, 0.5f)] float startFade = 0.08f;
    [Tooltip("0이면 부드러운 그라데이션, 값이 있으면 그 단계 수로 끊어 픽셀아트 느낌을 낸다.")]
    [SerializeField, Range(0, 8)] int brightnessSteps;
    [Header("움직임")]
    [SerializeField, Range(0, 1)] float rayStrength = 0.35f;
    [SerializeField, Range(1, 12)] float rayCount = 5;
    [SerializeField, Range(0, 2)] float raySpeed = 0.4f;
    [Tooltip("빛 속을 떠다니는 먼지/빗방울 양.")]
    [SerializeField, Range(0, 1)] float dust = 0.35f;
    [SerializeField, Range(0, 3)] float dustSpeed = 0.6f;
    [SerializeField, Range(0, 0.3f)] float flicker = 0.04f;
    [SerializeField, Min(1)] float pixelsPerUnit = 100;

    const int Rows = 16;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int ShapeId = Shader.PropertyToID("_Shape");
    static readonly int MotionId = Shader.PropertyToID("_Motion");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    readonly Vector3[] vertices = new Vector3[(Rows + 1) * 2];
    GameObject child;
    Mesh mesh;
    MeshRenderer shaftRenderer;
    MaterialPropertyBlock block;
    Vector3[] builtPath;
    Vector2 builtScale;
    public bool IsVisible => shaftRenderer != null && shaftRenderer.enabled;

    public void Configure(Light2D light, Material shaftMaterial)
    {
        source = light; material = shaftMaterial;
        if (isActiveAndEnabled) Refresh();
    }

    void OnEnable()
    {
        if (source == null) source = GetComponent<Light2D>();
        block ??= new MaterialPropertyBlock();
        child = new GameObject("Light shaft") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        child.transform.SetParent(transform, false);
        mesh = new Mesh { name = "Light shaft", hideFlags = HideFlags.HideAndDontSave };
        builtPath = null;
        mesh.MarkDynamic();
        var uv = new Vector2[vertices.Length];
        var triangles = new int[Rows * 6];
        for (int i = 0; i <= Rows; i++)
        {
            uv[i * 2] = new Vector2(0, i / (float)Rows);
            uv[i * 2 + 1] = new Vector2(1, i / (float)Rows);
            if (i == Rows) continue;
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        shaftRenderer = child.AddComponent<MeshRenderer>();
        shaftRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shaftRenderer.receiveShadows = false;
        shaftRenderer.lightProbeUsage = LightProbeUsage.Off;
        shaftRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        GraphicsEffectOptions.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        GraphicsEffectOptions.Changed -= Refresh;
        if (Application.isPlaying) { Destroy(child); Destroy(mesh); }
        else { DestroyImmediate(child); DestroyImmediate(mesh); }
        child = null; mesh = null; shaftRenderer = null;
    }

    void OnValidate() { if (isActiveAndEnabled && mesh != null) Refresh(); }
    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (shaftRenderer == null) return;
        bool lit = source != null && source.isActiveAndEnabled && source.intensity > 0 && material != null;
        shaftRenderer.enabled = lit && GraphicsEffectOptions.LightShafts && BuildShape();
        if (!shaftRenderer.enabled) return;
        shaftRenderer.sharedMaterial = material;
        shaftRenderer.sortingLayerName = sortingLayer;
        shaftRenderer.sortingOrder = sortingOrder;
        Color color = Color.Lerp(tint, source.color, useLightColor) * intensity;
        block.Clear();
        block.SetColor(ColorId, new Color(color.r, color.g, color.b, 1));
        block.SetVector(ShapeId, new Vector4(falloff, edgeSoftness, rayStrength, rayCount));
        block.SetVector(MotionId, new Vector4(raySpeed, dust, dustSpeed, pixelsPerUnit));
        block.SetVector(FadeId, new Vector4(startFade, flicker, brightnessSteps, 0));
        shaftRenderer.SetPropertyBlock(block);
    }

    /// <summary>라이트 경로에서 가장 높은 두 점을 램프 쪽 변, 가장 낮은 두 점을 바닥 쪽 변으로 보고 사다리꼴을 만든다.</summary>
    bool BuildShape()
    {
        var path = source.shapePath;
        if (path == null || path.Length < 3) return false;
        // 라이트 모양과 길이/폭이 그대로면 매 프레임 다시 만들지 않는다.
        if (builtPath != null && builtScale == new Vector2(length, width) && System.Linq.Enumerable.SequenceEqual(builtPath, path)) return true;
        builtPath = (Vector3[])path.Clone();
        builtScale = new Vector2(length, width);
        var sorted = (Vector3[])path.Clone();
        System.Array.Sort(sorted, (a, b) => b.y.CompareTo(a.y));
        Vector3 topA = sorted[0], topB = sorted[1], bottomA = sorted[sorted.Length - 2], bottomB = sorted[sorted.Length - 1];
        // 꼭짓점 하나짜리 원뿔이면 램프 쪽을 한 점으로 모은다.
        if (path.Length == 3) { topB = topA; bottomA = sorted[1]; }
        if (topA.x > topB.x) (topA, topB) = (topB, topA);
        if (bottomA.x > bottomB.x) (bottomA, bottomB) = (bottomB, bottomA);
        Vector3 apex = (topA + topB) * .5f, baseCenter = (bottomA + bottomB) * .5f;
        Vector3 farCenter = apex + (baseCenter - apex) * length;
        // 원뿔이 벌어지는 비율을 유지한 채 길이만큼 연장한다.
        Vector3 topHalf = (topB - topA) * .5f * width, baseHalf = (bottomB - bottomA) * .5f * width;
        Vector3 farHalf = topHalf + (baseHalf - topHalf) * length;
        for (int i = 0; i <= Rows; i++)
        {
            float t = i / (float)Rows;
            Vector3 center = Vector3.Lerp(apex, farCenter, t), half = Vector3.Lerp(topHalf, farHalf, t);
            vertices[i * 2] = center - half;
            vertices[i * 2 + 1] = center + half;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        return true;
    }
}
