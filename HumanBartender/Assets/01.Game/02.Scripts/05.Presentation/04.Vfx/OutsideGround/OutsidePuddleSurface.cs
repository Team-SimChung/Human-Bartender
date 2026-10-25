using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>배경 캡처는 전용 2D Renderer가 담당하고, 이 컴포넌트는 수면별 반사 기준과 파문 수명을 관리한다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
public sealed class OutsidePuddleSurface : MonoBehaviour
{
    public enum Outline { BrokenOval, JoinedPools, Crescent, Runoff }
    [SerializeField] Camera reflectionCamera;
    [SerializeField] GameObject rainSource;
    [Tooltip("반사 기준선. 비워두면 물웅덩이 위쪽 가장자리를 사용한다.")]
    [SerializeField] Transform reflectionLine;
    [SerializeField, Range(0.04f, 1f)] float reflectionCompression = 0.18f;
    [SerializeField, Range(0f, 1f)] float opacity = 0.6f;
    [SerializeField, Min(1)] float pixelsPerUnit = 100;
    [SerializeField, Min(0)] float dropsPerSecond = 10;
    [SerializeField, Range(0f, 3f)] float distortionPixels = 1.25f;
    [SerializeField] Outline outline;
    [SerializeField, Range(0f, 1f)] float edgeBreakup = 0.45f;
    [SerializeField, Range(0f, 2f)] float rippleBrightness = 0.7f;
    [Tooltip("캐릭터보다 앞에 그려지는 전봇대 등. 원래 정렬 순서를 유지하면서 반사에만 추가한다. 최대 4개.")]
    [SerializeField] SpriteRenderer[] foregroundReflectors = new SpriteRenderer[0];
    [SerializeField] int seed = 19;

    readonly List<ParticleSystem> systems = new List<ParticleSystem>();
    readonly Vector4[] ripples = new Vector4[RippleCapacity];
    MaterialPropertyBlock properties;
    MeshRenderer surface;
    System.Random random;
    float emissionRemainder;
    int nextRipple;
    const int RippleCapacity = 16;
    readonly Dictionary<Sprite, Vector3[]> foregroundUV = new Dictionary<Sprite, Vector3[]>();
    static readonly int[] ForegroundTexture = PropertyIDs("_ForegroundTexture");
    static readonly int[] ForegroundMatrix = PropertyIDs("_ForegroundMatrix");
    static readonly int[] ForegroundU = PropertyIDs("_ForegroundU");
    static readonly int[] ForegroundV = PropertyIDs("_ForegroundV");
    static readonly int[] ForegroundBounds = PropertyIDs("_ForegroundBounds");
    static readonly int[] ForegroundColor = PropertyIDs("_ForegroundColor");
    public const float RippleLifetime = 1.25f;
    public int LivingRipples { get; private set; }
    public bool IsRaining => ParticleRainSource.IsEmitting(rainSource, systems);
    public Outline Shape => outline;
    public void SetForegroundReflectors(SpriteRenderer[] renderers) => foregroundReflectors = renderers ?? new SpriteRenderer[0];

    static int[] PropertyIDs(string prefix)
    {
        var ids = new int[4];
        for (int i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID(prefix + i);
        return ids;
    }

    void ApplyForeground()
    {
        for (int i = 0; i < 4; i++)
        {
            var renderer = i < foregroundReflectors.Length ? foregroundReflectors[i] : null;
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy || renderer.sprite == null)
            {
                properties.SetColor(ForegroundColor[i], Color.clear);
                continue;
            }
            var sprite = renderer.sprite;
            if (!foregroundUV.TryGetValue(sprite, out var rows))
            {
                // 위치→UV 아핀 변환을 삼각형에서 구해 아틀라스 배치/회전에도 대응한다.
                var vertices = sprite.vertices; var uv = sprite.uv; var indices = sprite.triangles;
                rows = null;
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                    Vector2 ab = vertices[b] - vertices[a], ac = vertices[c] - vertices[a];
                    float determinant = ab.x * ac.y - ac.x * ab.y;
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    Vector2 ub = uv[b] - uv[a], uc = uv[c] - uv[a];
                    Vector2 x = (ub * ac.y - uc * ab.y) / determinant;
                    Vector2 y = (uc * ab.x - ub * ac.x) / determinant;
                    Vector2 origin = uv[a] - x * vertices[a].x - y * vertices[a].y;
                    rows = new[] { new Vector3(x.x, y.x, origin.x), new Vector3(x.y, y.y, origin.y) };
                    break;
                }
                rows ??= new[] { Vector3.zero, Vector3.zero };
                foregroundUV.Add(sprite, rows);
            }
            properties.SetTexture(ForegroundTexture[i], sprite.texture);
            properties.SetMatrix(ForegroundMatrix[i], Matrix4x4.Scale(new Vector3(renderer.flipX ? -1 : 1, renderer.flipY ? -1 : 1, 1)) * renderer.transform.worldToLocalMatrix);
            properties.SetVector(ForegroundU[i], rows[0]);
            properties.SetVector(ForegroundV[i], rows[1]);
            var bounds = sprite.bounds;
            properties.SetVector(ForegroundBounds[i], new Vector4(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y));
            properties.SetColor(ForegroundColor[i], renderer.color);
        }
    }

    public void SetAppearance(Outline shape, Transform horizon, float compression = 0.12f)
    {
        outline = shape;
        reflectionLine = horizon;
        reflectionCompression = compression;
    }

    public void Configure(Camera camera, GameObject rain, int randomSeed)
    {
        reflectionCamera = camera;
        rainSource = rain;
        seed = randomSeed;
        ResetRipples();
        ParticleRainSource.Collect(rainSource, systems);
    }

    void OnEnable()
    {
        surface = GetComponent<MeshRenderer>();
        properties ??= new MaterialPropertyBlock();
        ResetRipples();
        ParticleRainSource.Collect(rainSource, systems);
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        foregroundUV.Clear();
        if (surface == null) return;
        surface.GetPropertyBlock(properties);
        properties.SetFloat("_SurfaceEnabled", 0);
        surface.SetPropertyBlock(properties);
    }

    void ResetRipples()
    {
        random = new System.Random(seed);
        emissionRemainder = 0;
        nextRipple = LivingRipples = 0;
        for (int i = 0; i < ripples.Length; i++) ripples[i] = new Vector4(0, 0, -1, 0);
    }

    void Update()
    {
        if (Application.isPlaying) Advance(Time.deltaTime, IsRaining);
    }

    /// <summary>비가 멎어도 기존 파문은 끝까지 감쇠한다. 카메라 이동과 무관한 로컬 UV로 저장한다.</summary>
    public void Advance(float dt, bool raining)
    {
        dt = Mathf.Clamp(dt, 0, 0.1f);
        random ??= new System.Random(seed);
        for (int i = 0; i < ripples.Length; i++)
            if (ripples[i].z >= 0)
            {
                ripples[i].z += dt;
                if (ripples[i].z >= RippleLifetime) ripples[i].z = -1;
            }
        if (raining)
        {
            emissionRemainder += dt * dropsPerSecond;
            while (emissionRemainder >= 1)
            {
                emissionRemainder--;
                // 타원 안을 균등 샘플링. 가장자리에는 여유를 둬 새 파문이 잘리지 않게 한다.
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * 0.78f;
                ripples[nextRipple] = new Vector4(0.5f + Mathf.Cos(angle) * radius * 0.5f,
                    0.5f + Mathf.Sin(angle) * radius * 0.5f, 0, 0.8f + (float)random.NextDouble() * 0.4f);
                nextRipple = (nextRipple + 1) % RippleCapacity;
            }
        }
        else emissionRemainder = 0;
        LivingRipples = 0;
        foreach (var ripple in ripples) if (ripple.z >= 0) LivingRipples++;
    }

    void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        if (surface == null) return;
        surface.GetPropertyBlock(properties);
        properties.SetFloat("_SurfaceEnabled", 1);
        // 설정에서 반사를 끄면 파문과 수면색만 남긴다.
        properties.SetFloat("_ReflectionEnabled", camera == reflectionCamera && GraphicsEffectOptions.PuddleReflections ? 1 : 0);
        float line = reflectionLine != null ? reflectionLine.position.y : transform.TransformPoint(new Vector3(0, 0.5f, 0)).y;
        properties.SetVector("_Surface", new Vector4(line, reflectionCompression, pixelsPerUnit, opacity));
        Vector3 scale = transform.lossyScale;
        properties.SetVector("_SurfaceSize", new Vector4(Mathf.Abs(scale.x), Mathf.Abs(scale.y), distortionPixels, RippleLifetime));
        properties.SetVector("_Shape", new Vector4((int)outline, seed % 997, edgeBreakup, rippleBrightness));
        properties.SetVectorArray("_Ripples", ripples);
        ApplyForeground();
        surface.SetPropertyBlock(properties);
    }
}
