using UnityEngine;
using UnityEngine.Rendering;

/// <summary>배경 빌딩 층 사이에 끼우는 안개 한 겹. 기준 스프라이트 영역에 맞춰 그려지며
/// 패럴랙스 그룹의 자식으로 두면 그 층과 함께 움직인다. 앞쪽 층일수록 옅게 두면 거리감이 생긴다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class BackgroundFog : MonoBehaviour
{
    [SerializeField] Material material;
    [Tooltip("이 스프라이트의 가로 폭에 맞춰 안개를 깐다(세로 범위는 groundY~groundY+height). 비우면 size.x를 쓴다.")]
    [SerializeField] SpriteRenderer fitTo;
    [SerializeField] Vector2 size = new Vector2(28.8f, 8);
    [SerializeField] string sortingLayer = "BackgroundFar";
    [Tooltip("같은 레이어의 빌딩보다 먼저 그려지도록 음수로 둔다.")]
    [SerializeField] int sortingOrder = -10;
    [Header("안개")]
    [SerializeField] Color color = new Color(0.5f, 0.66f, 0.88f, 1);
    [SerializeField, Range(0, 1)] float density = 0.6f;
    [Tooltip("안개가 가장 짙은 높이(월드 y). 벽 밑선 근처에 둔다.")]
    [SerializeField] float groundY = -0.9f;
    [Tooltip("이 높이만큼 올라가면 안개가 사라진다. 안개가 덮는 세로 범위이기도 하다.")]
    [SerializeField, Min(0.1f)] float height = 6;
    [Tooltip("1보다 작으면 위쪽까지 진하게 유지되고, 크면 바닥에만 낮게 깔린다.")]
    [SerializeField, Range(0.2f, 4)] float heightFalloff = 0.8f;
    [Header("흐름")]
    [SerializeField, Min(0.1f)] float noiseScale = 2.5f;
    [SerializeField, Range(0, 1)] float noiseStrength = 0.45f;
    [SerializeField, Range(-1, 1)] float driftSpeed = 0.12f;
    [Header("표현")]
    [Tooltip("같은 레이어를 비추는 2D 라이트 색으로 안개가 물드는 정도.")]
    [SerializeField, Range(0, 1)] float lightInfluence = 0.2f;
    [Tooltip("0이면 부드러운 그라데이션, 값이 있으면 그 단계 수로 끊어 도트 느낌을 낸다.")]
    [SerializeField, Range(0, 8)] int alphaSteps;
    [SerializeField, Range(0.001f, 0.3f)] float edgeFade = 0.04f;
    [SerializeField, Min(1)] float pixelsPerUnit = 100;

    static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int NoiseId = Shader.PropertyToID("_Noise");
    static readonly int StyleId = Shader.PropertyToID("_Style");
    GameObject child;
    Mesh quad;
    MeshRenderer fogRenderer;
    MaterialPropertyBlock block;
    public bool IsVisible => fogRenderer != null && fogRenderer.enabled;
    public Bounds Area => fogRenderer != null ? fogRenderer.bounds : default;

    public void Configure(Material fogMaterial, SpriteRenderer target, string layer, int order, float fogDensity, Color fogColor)
    {
        material = fogMaterial; fitTo = target; sortingLayer = layer; sortingOrder = order; density = fogDensity; color = fogColor;
        if (isActiveAndEnabled) Refresh();
    }

    void OnEnable()
    {
        block ??= new MaterialPropertyBlock();
        child = new GameObject("Background fog") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        child.transform.SetParent(transform, false);
        quad = new Mesh { name = "Background fog", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        child.AddComponent<MeshFilter>().sharedMesh = quad;
        fogRenderer = child.AddComponent<MeshRenderer>();
        fogRenderer.shadowCastingMode = ShadowCastingMode.Off;
        fogRenderer.receiveShadows = false;
        fogRenderer.lightProbeUsage = LightProbeUsage.Off;
        fogRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Refresh();
    }

    void OnDisable()
    {
        if (Application.isPlaying) { Destroy(child); Destroy(quad); }
        else { DestroyImmediate(child); DestroyImmediate(quad); }
        child = null; quad = null; fogRenderer = null;
    }

    void OnValidate() { if (isActiveAndEnabled && fogRenderer != null) Refresh(); }
    // 패럴랙스가 기준 스프라이트를 옮긴 뒤 영역을 다시 맞춘다.
    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (fogRenderer == null) return;
        fogRenderer.enabled = material != null && density > 0;
        if (!fogRenderer.enabled) return;
        // 가로는 기준 빌딩 폭을 따르고, 세로는 빌딩 높이와 상관없이 안개 높이만큼 덮는다.
        Bounds width = fitTo != null ? fitTo.bounds : new Bounds(transform.position, size);
        float bottom = groundY - 1, top = groundY + height;
        var area = new Bounds(new Vector3(width.center.x, (bottom + top) * .5f), new Vector3(width.size.x, top - bottom));
        child.transform.position = new Vector3(area.center.x, area.center.y, transform.position.z);
        child.transform.rotation = Quaternion.identity;
        Vector3 parentScale = transform.lossyScale;
        child.transform.localScale = new Vector3(area.size.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), area.size.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1);
        fogRenderer.sharedMaterial = material;
        fogRenderer.sortingLayerName = sortingLayer;
        fogRenderer.sortingOrder = sortingOrder;
        block.Clear();
        block.SetColor(FogColorId, color);
        block.SetVector(DensityId, new Vector4(density, groundY, height, heightFalloff));
        block.SetVector(NoiseId, new Vector4(noiseScale, noiseStrength, driftSpeed, pixelsPerUnit));
        block.SetVector(StyleId, new Vector4(lightInfluence, alphaSteps, edgeFade, 0));
        fogRenderer.SetPropertyBlock(block);
    }
}
