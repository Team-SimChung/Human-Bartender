using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// CPU에서는 위치/속도/충격만 전달한다. GPU가 입자 사각형, 액체 필드, 기포의 이동과
/// 수면 거품을 만든다. 판정은 이 텍스처들을 읽지 않는다.
/// </summary>
public class SphLiquidRenderer : MonoBehaviour
{
    [SerializeField] int sortingOrder = 25;
    [Range(0.25f, 1f)] [SerializeField] float fieldResolutionScale = 0.5f;
    [SerializeField] Camera targetCamera;
    [Range(64, 256)] [SerializeField] int effectResolution = 128;
    [Tooltip("2D Camera Sorting Layer Texture에 액체 뒤 배경만 담긴 경우 사용.")]
    [Range(0f, 0.02f)] [SerializeField] float refractionStrength;

    [StructLayout(LayoutKind.Sequential)]
    struct GpuParticle
    {
        public Vector4 positionVelocity;
        public Vector4 impact;
    }

    Material splatMaterial, compositeMaterial;
    MeshRenderer compositeRenderer;
    Mesh compositeMesh;
    GraphicsBuffer particleBuffer;
    GpuParticle[] upload;
    int capacity, particleCount;
    RenderTexture fieldTexture, effectRead, effectWrite, surfaceRaw, surfaceRead, surfaceWrite;
    CommandBuffer commands;
    Transform container;
    Vector2 halfExtents;
    LiquidProfile profile;
    LiquidAppearance appearance;
    Color liquidColor;
    bool initialized, clearHistory = true;
    int renderedFrame = -1;
    Vector3 previousContainerPosition;
    float effectTime;

    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int FieldId = Shader.PropertyToID("_FieldTex");
    static readonly int EffectsId = Shader.PropertyToID("_EffectsTex");
    static readonly int SurfaceId = Shader.PropertyToID("_SurfaceTex");

    void Awake() => CreateResources();

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        clearHistory = true;
        renderedFrame = -1;
        if (compositeRenderer != null) compositeRenderer.enabled = initialized;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (compositeRenderer != null) compositeRenderer.enabled = false;
        ReleaseTargets();
        particleBuffer?.Dispose();
        particleBuffer = null;
        capacity = particleCount = 0;
        upload = null;
        effectTime = 0f;
        clearHistory = true;
    }

    void CreateResources()
    {
        if (splatMaterial != null) return;
        Shader splat = Shader.Find("Pour/LiquidBlobSplat");
        Shader composite = Shader.Find("Pour/LiquidComposite");
        if (splat == null || composite == null || !splat.isSupported || !composite.isSupported)
        {
            Debug.LogError("[Pour] GPU 액체 셰이더를 사용할 수 없습니다. Shader Model 4.5 지원을 확인하세요.", this);
            enabled = false;
            return;
        }
        splatMaterial = new Material(splat) { hideFlags = HideFlags.DontSave };
        compositeMaterial = new Material(composite) { hideFlags = HideFlags.DontSave };
        commands = new CommandBuffer { name = "Pour GPU Liquid" };
        var go = new GameObject("Liquid Composite");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        compositeRenderer = go.AddComponent<MeshRenderer>();
        compositeRenderer.sortingOrder = sortingOrder;
        compositeRenderer.sharedMaterial = compositeMaterial;
        compositeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        compositeRenderer.receiveShadows = false;
        compositeRenderer.enabled = false;
        compositeMesh = new Mesh { name = "LiquidCompositeQuad" };
        compositeMesh.vertices = new[] {
            new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
            new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f)
        };
        compositeMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        compositeMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        compositeMesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = compositeMesh;
    }

    public void Init(LiquidProfile liquidProfile, Color color)
        => Init(liquidProfile, color, liquidProfile.ResolveAppearance(null));

    public void Init(LiquidProfile liquidProfile, Color color, LiquidAppearance materialAppearance)
    {
        CreateResources();
        if (compositeMaterial == null) return;
        profile = liquidProfile;
        appearance = materialAppearance;
        liquidColor = color;
        initialized = true;
        clearHistory = true;
        effectTime = 0f;
        renderedFrame = -1;
        compositeRenderer.enabled = isActiveAndEnabled;
    }

    public void SetContainer(Transform glass, Vector2 interiorHalfExtents)
    {
        container = glass;
        halfExtents = new Vector2(Mathf.Max(0.001f, Mathf.Abs(interiorHalfExtents.x)),
                                  Mathf.Max(0.001f, Mathf.Abs(interiorHalfExtents.y)));
        previousContainerPosition = glass != null ? glass.position : Vector3.zero;
        clearHistory = true;
    }

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        clearHistory = true;
        renderedFrame = -1;
    }

    public void UpdateBlobs(IReadOnlyList<SphParticle> particles)
    {
        particleCount = particles.Count;
        if (!initialized || !isActiveAndEnabled || splatMaterial == null || particleCount == 0) return;
        if (particleBuffer == null || particleCount > capacity)
        {
            particleBuffer?.Dispose();
            capacity = Mathf.Max(256, Mathf.NextPowerOfTwo(particleCount));
            upload = new GpuParticle[capacity];
            particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 32);
            splatMaterial.SetBuffer(ParticlesId, particleBuffer);
        }
        for (int i = 0; i < particleCount; i++)
        {
            var p = particles[i];
            upload[i].positionVelocity = new Vector4(p.pos.x, p.pos.y, p.velocity.x, p.velocity.y);
            upload[i].impact = p.renderImpact;
            upload[i].impact.w = p.insideContainer ? 1f : 0f;
            p.renderImpact = Vector4.zero;
        }
        particleBuffer.SetData(upload, 0, 0, particleCount);
    }

    Camera ResolveCamera() => targetCamera != null ? targetCamera : Camera.main;

    void LateUpdate()
    {
        var camera = ResolveCamera();
        if (!initialized || camera == null || compositeRenderer == null) return;
        // 입자 데이터도 이 프로젝트의 물리도 XY 평면에 있다.
        float distance = Vector3.Dot(Vector3.zero - camera.transform.position, camera.transform.forward);
        var quad = compositeRenderer.transform;
        quad.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        quad.rotation = camera.transform.rotation;
        float height = camera.orthographicSize * 2f;
        // 월드 크기를 부모 스케일과 독립적으로 유지한다.
        quad.localScale = Vector3.one;
        Vector3 parentScale = quad.lossyScale;
        quad.localScale = new Vector3(height * camera.aspect / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                                     height / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != ResolveCamera() || !initialized || !isActiveAndEnabled || !camera.orthographic) return;
        // 같은 카메라가 같은 프레임에 다시 렌더되어도 충격/거품을 중복 적분하지 않는다.
        if (renderedFrame == Time.frameCount) return;
        var buffer = BuildCommands(camera, Mathf.Min(Time.deltaTime, 1f / 15f));
        context.ExecuteCommandBuffer(buffer);
        renderedFrame = Time.frameCount;
    }

    CommandBuffer BuildCommands(Camera camera, float dt)
    {
        EnsureTargets(camera);
        commands.Clear();
        if (clearHistory)
        {
            foreach (var rt in new[] { effectRead, effectWrite, surfaceRead, surfaceWrite })
            {
                commands.SetRenderTarget(rt);
                commands.ClearRenderTarget(false, true, Color.clear);
            }
            clearHistory = false;
        }

        float pixel = Mathf.Max(1f, Mathf.Round(Mathf.Max(1, profile.pixelSize) * camera.pixelHeight / 1080f));
        float distance = Vector3.Dot(Vector3.zero - camera.transform.position, camera.transform.forward);
        Vector3 origin = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        Vector3 axisX = camera.ViewportToWorldPoint(new Vector3(1, 0, distance)) - origin;
        Vector3 axisY = camera.ViewportToWorldPoint(new Vector3(0, 1, distance)) - origin;
        Matrix4x4 screenToWorld = Matrix4x4.identity;
        screenToWorld.SetColumn(0, new Vector4(axisX.x, axisX.y, axisX.z, 0));
        screenToWorld.SetColumn(1, new Vector4(axisY.x, axisY.y, axisY.z, 0));
        screenToWorld.SetColumn(2, new Vector4(camera.transform.forward.x, camera.transform.forward.y, camera.transform.forward.z, 0));
        screenToWorld.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
        Matrix4x4 worldToScreen = screenToWorld.inverse;
        var glassToWorld = container != null ? container.localToWorldMatrix : Matrix4x4.identity;
        Vector3 containerVelocity = container != null && dt > 0 ? (container.position - previousContainerPosition) / dt : Vector3.zero;
        if (container != null) previousContainerPosition = container.position;
        effectTime += dt;

        compositeMaterial.SetMatrix("_ScreenToWorld", screenToWorld);
        compositeMaterial.SetMatrix("_WorldToScreen", worldToScreen);
        compositeMaterial.SetMatrix("_GlassToWorld", glassToWorld);
        compositeMaterial.SetMatrix("_WorldToGlass", glassToWorld.inverse);
        compositeMaterial.SetVector("_GlassSize", new Vector4(halfExtents.x, halfExtents.y, container != null ? 1f : 0f, 0f));
        compositeMaterial.SetVector("_ContainerVelocity", containerVelocity);
        compositeMaterial.SetVector("_PixelGrid", new Vector4(camera.pixelWidth / pixel, camera.pixelHeight / pixel, Mathf.Max(3, profile.colorSteps), profile.surfaceDepth));
        compositeMaterial.SetVector("_Optics", new Vector4(appearance.absorption, appearance.turbidity, appearance.gloss, refractionStrength));
        compositeMaterial.SetVector("_Cloud", new Vector4(appearance.impactCloud, Mathf.Max(0.05f, appearance.cloudLifetime), appearance.bubbleRise, appearance.carbonation));
        compositeMaterial.SetVector("_Foam", new Vector4(appearance.foamGain, Mathf.Max(0.05f, appearance.foamLifetime), appearance.foamHeight, appearance.pulp));
        compositeMaterial.SetVector("_Step", new Vector4(Mathf.Max(0f, dt), effectTime, profile.physics.spacing, 0f));
        compositeMaterial.SetFloat("_Threshold", profile.threshold);
        compositeMaterial.SetFloat("_BubbleSize", profile.bubbleSize);
        compositeMaterial.SetFloat("_EdgeSmoothness", Mathf.Max(0.001f, profile.edgeSmoothness));
        compositeMaterial.SetColor("_Color", liquidColor);
        compositeMaterial.SetColor("_FoamColor", appearance.foamColor);
        compositeMaterial.SetTexture(FieldId, fieldTexture);
        compositeMaterial.SetTexture("_PreviousEffects", effectRead);
        compositeMaterial.SetTexture("_PreviousSurface", surfaceRead);
        compositeMaterial.SetTexture("_RawSurface", surfaceRaw);
        compositeMaterial.SetTexture(EffectsId, effectWrite);
        compositeMaterial.SetTexture(SurfaceId, surfaceWrite);

        splatMaterial.SetMatrix("_WorldToScreen", worldToScreen);
        splatMaterial.SetVector("_BlobShape", new Vector4(profile.physics.spacing * profile.blobRadiusScale,
            profile.stretchPerSpeed, profile.maxStretch, profile.streamThinning));
        commands.SetRenderTarget(fieldTexture);
        commands.ClearRenderTarget(false, true, Color.clear);
        if (particleCount > 0 && particleBuffer != null)
            commands.DrawProcedural(Matrix4x4.identity, splatMaterial, 0, MeshTopology.Triangles, 6, particleCount);

        commands.Blit(Texture2D.blackTexture, surfaceRaw, compositeMaterial, 2);
        commands.Blit(Texture2D.blackTexture, effectWrite, compositeMaterial, 1);
        commands.Blit(Texture2D.blackTexture, surfaceWrite, compositeMaterial, 3);
        Swap(ref effectRead, ref effectWrite);
        Swap(ref surfaceRead, ref surfaceWrite);
        return commands;
    }

    void EnsureTargets(Camera camera)
    {
        int width = Mathf.Max(64, Mathf.RoundToInt(camera.pixelWidth * fieldResolutionScale));
        int height = Mathf.Max(64, Mathf.RoundToInt(camera.pixelHeight * fieldResolutionScale));
        if (fieldTexture != null && fieldTexture.width == width && fieldTexture.height == height &&
            effectRead != null && effectRead.width == Mathf.Clamp(effectResolution, 64, 256)) return;
        ReleaseTargets();
        fieldTexture = Target("Liquid Field (density, velocity, impact)", width, height, RenderTextureFormat.ARGBHalf);
        int size = Mathf.Clamp(effectResolution, 64, 256);
        effectRead = Target("Liquid Cloud A", size, size * 2, RenderTextureFormat.RHalf);
        effectWrite = Target("Liquid Cloud B", size, size * 2, RenderTextureFormat.RHalf);
        surfaceRaw = Target("Liquid Surface Raw", size, 1, RenderTextureFormat.ARGBHalf);
        surfaceRead = Target("Liquid Surface A", size, 1, RenderTextureFormat.ARGBHalf);
        surfaceWrite = Target("Liquid Surface B", size, 1, RenderTextureFormat.ARGBHalf);
        clearHistory = true;
    }

    static RenderTexture Target(string name, int width, int height, RenderTextureFormat format)
    {
        var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.DontSave
        };
        rt.Create();
        return rt;
    }

    static void Swap(ref RenderTexture a, ref RenderTexture b) { var swap = a; a = b; b = swap; }

    void ReleaseTargets()
    {
        Release(ref fieldTexture); Release(ref effectRead); Release(ref effectWrite);
        Release(ref surfaceRaw); Release(ref surfaceRead); Release(ref surfaceWrite);
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        if (RenderTexture.active == rt) RenderTexture.active = null;
        rt.Release();
        if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
        rt = null;
    }

    void OnDestroy()
    {
        ReleaseTargets();
        particleBuffer?.Dispose();
        particleBuffer = null;
        commands?.Release();
        DestroyResource(splatMaterial);
        DestroyResource(compositeMaterial);
        DestroyResource(compositeMesh);
    }

    static void DestroyResource(Object resource)
    {
        if (resource == null) return;
        if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
    }
}
