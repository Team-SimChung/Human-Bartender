using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>원본 애니메이션의 알파를 발 앵커 기준으로 바닥에 투영한다. 캐릭터 머티리얼/프로퍼티 블록은 건드리지 않는다.
/// 접지 실루엣은 광원과 무관하게 항상 그려져 걷기/대기 프레임의 다리 모양이 그대로 바닥에 따라간다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer)), DefaultExecutionOrder(11000)]
public sealed class OutsideCharacterShadow : MonoBehaviour
{
    [SerializeField] Material shadowMaterial;
    [SerializeField] Vector2 footOffset = new Vector2(0, -0.3f);
    [Header("광원 투영 그림자")]
    [SerializeField, Range(0, 1)] float opacity = 0.65f;
    [SerializeField, Range(0.02f, 0.5f)] float flatten = 0.28f;
    [SerializeField, Min(0.05f)] float maxLength = 0.85f;
    [SerializeField, Min(0.01f)] float transitionSeconds = 0.25f;
    [Header("접지 그림자 (애니메이션 실루엣)")]
    [SerializeField, Range(0, 1)] float contactOpacity = 0.55f;
    [Tooltip("캐릭터 키 대비 바닥에 눕는 길이. 작을수록 납작하다.")]
    [SerializeField, Range(0.02f, 0.5f)] float contactFlatten = 0.12f;
    [Tooltip("실루엣을 옆으로 기울이는 정도. 0이면 발 바로 아래로 뒤집힌다.")]
    [SerializeField, Range(-1, 1)] float contactLean = 0.15f;
    [Tooltip("그림자 가장자리 번짐(원본 텍셀 단위).")]
    [SerializeField, Range(0, 3)] float softness = 1;
    [SerializeField] string sortingLayer = "GroundShadow";

    sealed class Shape
    {
        public Vector2[] vertices, uv;
        public int[] triangles;
    }
    sealed class Draw
    {
        public GameObject gameObject;
        public Mesh mesh;
        public MeshRenderer renderer;
        public OutsideShadowLight light;
        public float weight, strength;
        public Vector3 lastLightPosition;
        public Vector3[] positions;
        public Sprite sprite;
    }
    readonly Dictionary<Sprite, Shape> shapes = new Dictionary<Sprite, Shape>();
    readonly Draw[] projections = new Draw[2];
    Draw silhouette;
    SpriteRenderer source;
    MaterialPropertyBlock block;
    OutsideShadowLight selected;
    public Vector3 FootPosition
    {
        get
        {
            Vector2 local = footOffset;
            if (source != null)
            {
                if (source.flipX) local.x = -local.x;
                if (source.flipY) local.y = -local.y;
            }
            return transform.TransformPoint(local);
        }
    }
    public OutsideGroundReceiver CurrentReceiver { get; private set; }

    public void Configure(Material material, Vector2 anchor)
    {
        shadowMaterial = material; footOffset = anchor;
        if (isActiveAndEnabled) Rebuild();
    }
    void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        block ??= new MaterialPropertyBlock();
        Rebuild();
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
    }
    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Release();
        CurrentReceiver = null;
        selected = null;
    }

    void Rebuild()
    {
        Release();
        if (shadowMaterial == null) return;
        silhouette = CreateDraw("Silhouette shadow");
        for (int i = 0; i < projections.Length; i++) projections[i] = CreateDraw("Projected shadow " + i);
    }
    Draw CreateDraw(string label)
    {
        var go = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label, hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = shadowMaterial;
        renderer.sortingLayerName = sortingLayer;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;
        return new Draw { gameObject = go, mesh = mesh, renderer = renderer };
    }

    void LateUpdate()
    {
        if (silhouette != null && silhouette.renderer.sharedMaterial != shadowMaterial || silhouette == null && shadowMaterial != null) Rebuild();
        if (source == null || silhouette == null) return;
        Vector3 foot = FootPosition;
        selected = OutsideShadowLight.Select(foot, source, selected);
        bool assigned = selected == null;
        foreach (var draw in projections) if (draw.light == selected) assigned = true;
        if (!assigned)
        {
            var replace = projections[0].weight <= projections[1].weight ? projections[0] : projections[1];
            replace.light = selected;
            replace.weight = 0;
        }
        float step = Application.isPlaying ? Time.deltaTime / Mathf.Max(0.01f, transitionSeconds) : 1;
        foreach (var draw in projections)
        {
            bool valid = draw.light != null && draw.light.Influence(foot, source) > 0;
            draw.weight = Mathf.MoveTowards(draw.weight, valid && draw.light == selected ? 1 : 0, step);
            if (valid)
            {
                draw.lastLightPosition = draw.light.Position;
                draw.strength = Mathf.Sqrt(Mathf.Clamp01(draw.light.Influence(foot, source)));
            }
        }
        Synchronize();
    }

    // Timeline/Animator와 이동 플랫폼이 갱신된 최종 좌표를 렌더 직전에 읽는다.
    void BeforeCamera(ScriptableRenderContext context, Camera camera) => Synchronize();

    public static Vector3 ProjectVertex(Vector3 vertex, Vector3 foot, Vector3 light, float compression, float lengthLimit, float height)
    {
        float slope = Mathf.Clamp((foot.x - light.x) / Mathf.Max(0.25f, light.y - foot.y), -3, 3);
        Vector2 direction = new Vector2(slope, -compression);
        float length = direction.magnitude * Mathf.Max(0.01f, height);
        if (length > lengthLimit) direction *= lengthLimit / length;
        return Shear(vertex, foot, direction);
    }

    /// <summary>발 높이보다 위의 정점을 direction 방향으로 눕힌다. 발은 고정된다.</summary>
    public static Vector3 Shear(Vector3 vertex, Vector3 foot, Vector2 direction)
    {
        float aboveFoot = Mathf.Max(0, vertex.y - foot.y);
        return new Vector3(vertex.x + direction.x * aboveFoot, foot.y + direction.y * aboveFoot, foot.z);
    }

    void Synchronize()
    {
        if (source == null || silhouette == null) return;
        Vector3 foot = FootPosition;
        CurrentReceiver = OutsideGroundReceiver.Find(foot, gameObject.scene);
        bool visible = source.enabled && !source.forceRenderingOff && source.sprite != null && source.color.a > 0.001f && CurrentReceiver != null;
        silhouette.renderer.enabled = visible && contactOpacity > 0;
        if (!visible)
        {
            foreach (var draw in projections) draw.renderer.enabled = false;
            return;
        }
        Shape shape = GetShape(source.sprite);
        float height = Mathf.Max(0.05f, source.bounds.max.y - foot.y);
        if (silhouette.renderer.enabled)
        {
            // 바라보는 방향 기준으로 기울여 좌우 반전 시에도 같은 쪽 뒤로 눕는다.
            float lean = source.flipX ? -contactLean : contactLean;
            Vector2 direction = new Vector2(lean * contactFlatten, -contactFlatten);
            UpdateSpriteMesh(silhouette, shape, world => Shear(world, foot, direction));
            // 광원 그림자가 진할 때는 겹쳐서 발밑만 과하게 어두워지지 않도록 절반까지 양보한다.
            float lit = 0;
            foreach (var draw in projections) lit = Mathf.Max(lit, draw.weight * draw.strength);
            Apply(silhouette, contactOpacity * (1 - 0.5f * lit), Mathf.Max(0.05f, height * contactFlatten * 1.5f));
        }
        foreach (var draw in projections)
        {
            draw.renderer.enabled = draw.weight > 0.001f;
            if (!draw.renderer.enabled) continue;
            Vector3 light = draw.lastLightPosition;
            UpdateSpriteMesh(draw, shape, world => ProjectVertex(world, foot, light, flatten, maxLength, height));
            Apply(draw, opacity * draw.weight * draw.strength, maxLength);
        }
    }

    // 스프라이트가 바뀐 프레임에만 삼각형을 교체하고, 정점은 매 렌더 직전 현재 프레임 기준으로 다시 투영한다.
    void UpdateSpriteMesh(Draw draw, Shape shape, System.Func<Vector3, Vector3> project)
    {
        if (draw.sprite != source.sprite)
        {
            draw.sprite = source.sprite;
            draw.mesh.Clear();
            if (draw.positions == null || draw.positions.Length != shape.vertices.Length)
                draw.positions = new Vector3[shape.vertices.Length];
            draw.mesh.vertices = draw.positions;
            draw.mesh.uv = shape.uv;
            draw.mesh.triangles = shape.triangles;
        }
        for (int i = 0; i < shape.vertices.Length; i++)
        {
            Vector2 local = shape.vertices[i];
            if (source.flipX) local.x = -local.x;
            if (source.flipY) local.y = -local.y;
            draw.positions[i] = transform.InverseTransformPoint(project(transform.TransformPoint(local)));
        }
        draw.mesh.vertices = draw.positions;
        draw.mesh.RecalculateBounds();
    }

    Shape GetShape(Sprite sprite)
    {
        if (shapes.TryGetValue(sprite, out var shape)) return shape;
        ushort[] indices = sprite.triangles;
        shape = new Shape { vertices = sprite.vertices, uv = sprite.uv, triangles = new int[indices.Length] };
        for (int i = 0; i < indices.Length; i++) shape.triangles[i] = indices[i];
        shapes.Add(sprite, shape);
        return shape;
    }
    void Apply(Draw draw, float alpha, float fadeLength)
    {
        block.Clear();
        CurrentReceiver.Apply(block);
        var texture = source.sprite.texture;
        block.SetTexture("_MainTex", texture);
        block.SetVector("_ShadowTexel", new Vector4(1f / texture.width, 1f / texture.height, softness, 0));
        block.SetFloat("_Opacity", alpha * source.color.a);
        block.SetVector("_Foot", new Vector4(FootPosition.x, FootPosition.y, fadeLength, 0));
        draw.renderer.sortingLayerName = CurrentReceiver.ShadowSortingLayer;
        draw.renderer.sortingOrder = CurrentReceiver.ShadowSortingOrder;
        draw.renderer.SetPropertyBlock(block);
    }
    void Release()
    {
        DestroyDraw(silhouette); silhouette = null;
        for (int i = 0; i < projections.Length; i++) { DestroyDraw(projections[i]); projections[i] = null; }
        shapes.Clear();
    }
    static void DestroyDraw(Draw draw)
    {
        if (draw == null) return;
        if (draw.renderer != null) draw.renderer.enabled = false;
        if (Application.isPlaying) { Destroy(draw.gameObject); Destroy(draw.mesh); }
        else { DestroyImmediate(draw.gameObject); DestroyImmediate(draw.mesh); }
    }
}
