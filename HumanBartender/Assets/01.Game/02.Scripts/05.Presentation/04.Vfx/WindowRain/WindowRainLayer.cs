using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 카메라 앞 유리에 맺히고 흘러내리는 빗물. 이 컴포넌트가 붙은 카메라에만
/// <see cref="WindowRainFeature"/>가 합성 패스를 추가한다. 캡처 카메라·다른 씬·기믹 카메라는
/// 등록되지 않으므로 효과가 중복되지 않는다.
///
/// 방울 생성은 씬의 기존 비 파티클(FX_Rain)이 실제로 방출 중인지에 맞춘다. 오디오의 날짜 조건이 아니라
/// 화면에 보이는 비를 기준으로 삼아 시각 효과끼리 어긋나지 않게 한다. 비 파티클이 없는
/// 타이틀 배경은 ambientRain을 켜서 계속 방울을 생성할 수 있다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)] // Cinemachine이 카메라를 옮긴 뒤 위치를 읽는다.
[RequireComponent(typeof(Camera))]
public sealed class WindowRainLayer : MonoBehaviour
{
    [Tooltip("씬의 기존 비 효과(FX_Rain). 하위 반복 파티클이 방출 중일 때만 새 방울이 맺힌다.")]
    [SerializeField] GameObject rainSource;
    [Tooltip("비 파티클이 없는 타이틀 배경에서 계속 방울을 생성한다. rainSource가 있으면 실제 파티클 방출 상태를 따른다.")]
    [SerializeField] bool ambientRain;
    [Tooltip("캐릭터의 이동 기준 위치. 카메라가 화면 끝에서 고정돼도 이동 중에는 물방울이 계속 흐른다.")]
    [SerializeField] Transform movementSource;
    [SerializeField] WindowRainSettings settings = new WindowRainSettings();

    [Header("바람과 관성")]
    [Tooltip("카메라 이동 속도(화면 높이/초)에 대한 상대 바람. 중력 방향은 그대로 두고 옆 힘만 더한다.")]
    [SerializeField, Min(0f)] float windFromMotion = 1.1f;
    [Tooltip("카메라 가감속에 대한 관성.")]
    [SerializeField, Min(0f)] float inertia = 0.3f;
    [Tooltip("정지해 있을 때의 바람(가상 픽셀/초²).")]
    [SerializeField] Vector2 ambientWind = new Vector2(-14f, 0f);
    [SerializeField, Min(0f)] float gustStrength = 22f;

    [Header("광학 (최종 화면 색으로 근사)")]
    [Tooltip("물방울·물자국의 전체 시각 강도. 0은 숨김, 1은 최대. 비의 양과 움직임은 유지한다.")]
    [SerializeField, Range(0f, 1f)] float strength = 0.45f;
    [Tooltip("물방울 셰이딩의 픽셀 크기 배율. 1은 원래 픽셀 크기. 물리 격자에는 영향을 주지 않는다.")]
    [SerializeField, Range(1f, 3f)] float shadingPixelSize = 1f;
    [Tooltip("굴절로 방울 안에 비치는 장면의 최대 이동량(화면 높이 비율).")]
    [SerializeField, Range(0f, 0.2f)] float refraction = 0.045f;
    [Tooltip("가장자리 색 분산(가상 픽셀). 강한 굴절 부위에만 적용한다.")]
    [SerializeField, Range(0f, 2f)] float dispersion = 0.65f;
    [SerializeField, Range(0f, 1f)] float highlight = 0.45f;
    [SerializeField, Range(0f, 1f)] float rimShadow = 0.3f;
    [Tooltip("흐른 자리에 남는 얇은 물자국의 진하기.")]
    [SerializeField, Range(0f, 1f)] float trailStrength = 0.7f;
    [SerializeField, Min(0.1f)] float trailLifetime = 4.5f;

    [Header("시작")]
    [Tooltip("씬에 들어왔을 때 이미 비가 오면 유리가 젖은 상태로 시작한다.")]
    [SerializeField] bool prewarm = true;
    [SerializeField] int seed = 20261019;
    [SerializeField] Shader dropShader;
    [SerializeField] Shader compositeShader;

    [StructLayout(LayoutKind.Sequential)]
    struct GpuDrop
    {
        public Vector4 shape;   // 중심(가상 픽셀), 반지름, 꼬리 늘어남
        public Vector4 motion;  // 진행 방향, 이전 위치(가상 픽셀)
        public Vector4 merge;   // 흡수 중인 방울의 상대 위치, 반지름, 연결부 폭
        public Vector4 surface; // 방울 고유 위상, 변형 세기, 예비
    }

    static readonly Dictionary<Camera, WindowRainLayer> Layers = new Dictionary<Camera, WindowRainLayer>();

    Camera targetCamera;
    WindowRainSimulation simulation;
    readonly WindowRainMotion motion = new WindowRainMotion();
    readonly WindowRainMotion subjectMotion = new WindowRainMotion();
    readonly List<ParticleSystem> rainSystems = new List<ParticleSystem>();
    RenderTexture water, trail;
    GraphicsBuffer dropBuffer;
    GpuDrop[] upload;
    Material dropMaterial, compositeMaterial;
    CommandBuffer commands;
    float referenceHeight, trailEnergy, windTime;
    bool prewarmed;

    public static bool TryGet(Camera camera, out WindowRainLayer layer)
    {
        layer = null;
        return camera != null && Layers.TryGetValue(camera, out layer) && layer != null && layer.isActiveAndEnabled;
    }

    public Material CompositeMaterial => compositeMaterial;
    public float Strength { get => strength; set => strength = Mathf.Clamp01(value); }
    /// <summary>방울이나 마르지 않은 물자국이 있어 합성할 것이 있는지.</summary>
    public bool HasVisibleWater => strength > 0f && water != null && compositeMaterial != null &&
        (simulation != null && !simulation.IsDry || trailEnergy > 0.02f);
    public bool IsRaining { get; private set; }
    public WindowRainSimulation Simulation => simulation;
    public WindowRainMotion Motion => motion;
    public bool IsMoving => motion.IsMoving || movementSource != null && subjectMotion.IsMoving;
    public RenderTexture WaterTexture => water;
    public RenderTexture TrailTexture => trail;

    void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        Layers[targetCamera] = this;
        CollectRainSystems();
        motion.Reset();
        subjectMotion.Reset();
        referenceHeight = 0f;
        simulation ??= new WindowRainSimulation(settings, seed);
        simulation.Resize(targetCamera.aspect);
        if (prewarm && !prewarmed && RainActive())
        {
            simulation.Prewarm(8f);
            prewarmed = true;
        }
    }

    void OnDisable()
    {
        if (targetCamera != null && Layers.TryGetValue(targetCamera, out var layer) && layer == this)
            Layers.Remove(targetCamera);
        ReleaseGpu();
    }

    void OnDestroy()
    {
        ReleaseGpu();
        DestroyResource(ref dropMaterial);
        DestroyResource(ref compositeMaterial);
        commands?.Release();
        commands = null;
    }

    /// <summary>기존 비 프리팹의 반복 파티클만 본다. 한 번만 터지는 루트 시스템은 비의 지속 여부가 아니다.</summary>
    void CollectRainSystems()
    {
        rainSystems.Clear();
        if (rainSource == null) return;
        foreach (var system in rainSource.GetComponentsInChildren<ParticleSystem>(true))
            if (system.main.loop) rainSystems.Add(system);
    }

    bool RainActive()
    {
        if (rainSource == null) return ambientRain;
        if (!rainSource.activeInHierarchy) return false;
        foreach (var system in rainSystems)
            if (system != null && system.gameObject.activeInHierarchy && system.emission.enabled && system.isEmitting)
                return true;
        return false;
    }

    /// <summary>테스트와 연출용. 비 파티클 없이 방울을 확인할 때 쓴다.</summary>
    public void SetRainSource(GameObject source)
    {
        rainSource = source;
        CollectRainSystems();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;
        float dt = Time.deltaTime;
        IsRaining = RainActive();
        // 카메라 OnEnable보다 비 파티클의 Play/Start가 늦을 수 있다.
        // 첫 LateUpdate까지 한 번만 기다리고, 이후 비가 시작되면 정상 생성 속도로 젖는다.
        if (!prewarmed)
        {
            if (prewarm && IsRaining) simulation.Prewarm(8f);
            prewarmed = true;
        }
        SampleMotion(dt);
        simulation.Resize(targetCamera.aspect);
        simulation.Step(dt, IsRaining ? 1f : 0f, ExternalAcceleration(dt), IsMoving);
        Render(dt);
    }

    void SampleMotion(float dt)
    {
        if (referenceHeight <= 0f)
            referenceHeight = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 1f;
        motion.Sample(targetCamera.transform.position, referenceHeight, dt);
        if (movementSource != null) subjectMotion.Sample(movementSource.position, referenceHeight, dt);
    }

    /// <summary>중력은 시뮬레이션이 더한다. 여기서는 이동 바람·관성·주변 바람만 만든다.</summary>
    Vector2 ExternalAcceleration(float dt)
    {
        windTime += dt;
        float gust = (Mathf.PerlinNoise(windTime * 0.35f, 3.1f) - 0.45f) * 2f * gustStrength;
        Vector2 relative = -(motion.Velocity * windFromMotion + motion.Acceleration * inertia) * settings.virtualRows;
        return ambientWind + new Vector2(gust, 0f) + relative;
    }

    void Render(float dt)
    {
        if (!EnsureResources()) return;
        var drops = simulation.Drops;
        int count = drops.Count;
        bool anySliding = false;
        if (count > 0)
        {
            if (dropBuffer == null || dropBuffer.count < count)
            {
                dropBuffer?.Dispose();
                int capacity = Mathf.Max(128, Mathf.NextPowerOfTwo(count));
                dropBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, Marshal.SizeOf<GpuDrop>());
                upload = new GpuDrop[capacity];
                dropMaterial.SetBuffer("_Drops", dropBuffer);
            }
            for (int i = 0; i < count; i++)
            {
                var d = drops[i];
                float speed = d.velocity.magnitude;
                bool moving = speed > 0.1f;
                float stretch = moving ? Mathf.Clamp(1f + speed / 35f, 1f, 2.6f) : 1f;
                // heading은 가속도가 향하는 방향이며 실제 속도와 다를 수 있다.
                // 실루엣은 실제 이동 속도를 따라야 옆으로 미끄러지는 것처럼 보이지 않는다.
                Vector2 visualDirection = moving ? d.velocity / speed : Vector2.down;
                float remaining = Mathf.SmoothStep(0f, 1f, d.mergeRemaining);
                float fraction = d.mergeFraction * remaining;
                float growth = simulation.FormationScale(d);
                Vector2 offset = d.mergeOffset * remaining;
                Vector2 center = d.position - offset * (d.mergeFraction * 0.5f);
                float radius = d.radius * Mathf.Sqrt(1f - fraction) * growth;
                float absorbedRadius = d.radius * Mathf.Sqrt(fraction) * growth;
                upload[i].shape = new Vector4(center.x, center.y, radius, stretch);
                upload[i].merge = new Vector4(offset.x, offset.y, absorbedRadius, Mathf.Min(radius, absorbedRadius) * 0.65f);
                upload[i].surface = new Vector4(d.phase, Mathf.Lerp(0.035f, 0.2f, Mathf.Clamp01(speed / 20f)), 0f, 0f);
                // 멈춘 방울은 이전 위치와 같아 물자국 선분이 생기지 않는다.
                // 합체의 시각 중심 보정이 물자국을 옆으로 긋지 않도록 같은 보정을 적용한다.
                Vector2 previous = moving ? d.previous + center - d.position : center;
                upload[i].motion = new Vector4(visualDirection.x, visualDirection.y, previous.x, previous.y);
                anySliding |= moving;
            }
            dropBuffer.SetData(upload, 0, 0, count);
        }

        float fade = dt > 0f ? Mathf.Exp(-dt / trailLifetime) : 1f;
        trailEnergy = Mathf.Max(trailEnergy * fade, anySliding ? 1f : 0f);
        var size = new Vector4(water.width, water.height, 1f / water.width, 1f / water.height);
        dropMaterial.SetVector("_RainGrid", size);
        dropMaterial.SetFloat("_RainTime", simulation.ElapsedTime);
        dropMaterial.SetFloat("_Fade", fade);
        commands.Clear();
        commands.SetRenderTarget(water);
        commands.ClearRenderTarget(false, true, Color.clear);
        if (count > 0) commands.DrawProcedural(Matrix4x4.identity, dropMaterial, 0, MeshTopology.Triangles, 6, count);
        commands.SetRenderTarget(trail);
        commands.DrawProcedural(Matrix4x4.identity, dropMaterial, 1, MeshTopology.Triangles, 3, 1);
        if (count > 0) commands.DrawProcedural(Matrix4x4.identity, dropMaterial, 2, MeshTopology.Triangles, 6, count);
        Graphics.ExecuteCommandBuffer(commands);

        compositeMaterial.SetTexture("_RainWater", water);
        compositeMaterial.SetTexture("_RainTrail", trail);
        compositeMaterial.SetVector("_RainGrid", size);
        compositeMaterial.SetVector("_RainStyle", new Vector4(Mathf.Clamp01(strength), Mathf.Clamp(shadingPixelSize, 1f, 3f), 0f, 0f));
        compositeMaterial.SetVector("_RainOptics", new Vector4(refraction, dispersion, highlight, rimShadow));
        compositeMaterial.SetVector("_RainTrailParams", new Vector4(trailStrength, 0f, 0f, 0f));
    }

    bool EnsureResources()
    {
        if (dropMaterial == null)
        {
            var drops = dropShader != null ? dropShader : Shader.Find("Hidden/WindowRain/Drops");
            var composite = compositeShader != null ? compositeShader : Shader.Find("Hidden/WindowRain/Composite");
            if (drops == null || composite == null || !drops.isSupported || !composite.isSupported)
            {
                Debug.LogError("[WindowRain] 빗물 셰이더를 사용할 수 없습니다. Shader Model 4.5 지원을 확인하세요.", this);
                enabled = false;
                return false;
            }
            dropMaterial = new Material(drops) { hideFlags = HideFlags.DontSave };
            compositeMaterial = new Material(composite) { hideFlags = HideFlags.DontSave };
            commands = new CommandBuffer { name = "Window Rain Drops" };
        }
        Vector2 grid = simulation.Size;
        int width = Mathf.Max(16, Mathf.RoundToInt(grid.x)), height = Mathf.Max(16, Mathf.RoundToInt(grid.y));
        if (water != null && water.width == width && water.height == height) return true;
        ReleaseTargets();
        water = Target("Window Rain Water", width, height, RenderTextureFormat.RGHalf);
        trail = Target("Window Rain Trail", width, height, RenderTextureFormat.RHalf);
        var previous = RenderTexture.active;
        RenderTexture.active = trail;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
        if (dropBuffer != null) dropMaterial.SetBuffer("_Drops", dropBuffer);
        return true;
    }

    static RenderTexture Target(string name, int width, int height, RenderTextureFormat format)
    {
        var rt = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
        {
            name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            useMipMap = false, autoGenerateMips = false, hideFlags = HideFlags.DontSave
        };
        rt.Create();
        return rt;
    }

    void ReleaseGpu()
    {
        ReleaseTargets();
        dropBuffer?.Dispose();
        dropBuffer = null;
        upload = null;
        trailEnergy = 0f;
    }

    void ReleaseTargets()
    {
        Release(ref water);
        Release(ref trail);
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        if (RenderTexture.active == rt) RenderTexture.active = null;
        rt.Release();
        if (Application.isPlaying) Object.Destroy(rt); else Object.DestroyImmediate(rt);
        rt = null;
    }

    static void DestroyResource<T>(ref T resource) where T : Object
    {
        if (resource == null) return;
        if (Application.isPlaying) Object.Destroy(resource); else Object.DestroyImmediate(resource);
        resource = null;
    }
}
