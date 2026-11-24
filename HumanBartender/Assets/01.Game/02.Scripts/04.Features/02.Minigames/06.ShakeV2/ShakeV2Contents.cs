using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>단면 내용물(물·얼음·관성·성에) 조절 값. 셰이커(04.Gameplay/Shaker)의 Inspector에서 바꾼다.</summary>
[Serializable]
public sealed class ShakeV2ContentsTuning
{
    [Header("관성·중력")]
    [Tooltip("셰이커 속도 변화를 통 안 물·얼음에 전하는 배율. 1 = 실제 그대로(통 기준 관성), 0 = 내용물이 셰이커를 따라 붙어 다님. " +
             "올리면 더 빨리·세게 치는 쪽 벽으로 몰린다.")]
    [Range(0f, 3f)] public float inertia = 1f;
    [Tooltip("통 안 중력(셰이커 단위/초²). 셰이커 기울기만큼 돌려서 준다. 웹 B.G.")]
    [Min(0f)] public float gravity = 900f;
    [Tooltip("게임 좌표 / 셰이커 단위. 셰이커 속도(게임 좌표)를 통 기준(셰이커 단위)으로 바꿀 때 나눈다. 웹 B.K.")]
    [Min(.01f)] public float gameUnitsPerShakerUnit = 1.3f;

    [Header("물(수위 방식)")]
    public ShakeV2WaterTuning water = new ShakeV2WaterTuning();

    [Header("얼음(2D 물리)")]
    public ShakeV2IceTuning ice = new ShakeV2IceTuning();

    [Header("성에")]
    [Tooltip("냉각도가 이 값을 넘으면 단면 위아래 벽에 성에 점이 맺힌다. 웹 10.")]
    [Range(0f, 100f)] public float frostFrom = 10f;
    [Tooltip("냉각도 1당 성에 점 수. 웹 1.2.")]
    [Min(0f)] public float frostPerChill = 1.2f;
    [Tooltip("성에 색. 불투명도는 냉각도 100일 때의 값이다. 웹 rgba(235,250,255,.5).")]
    public Color frostColor = new Color(235f / 255f, 250f / 255f, 1f, .5f);
}

/// <summary>
/// 셰이커 단면의 내용물. 셰이커 화면(ShakeV2ShakerView)이 소유하고 게임 시간으로 진행한다.
///
/// - 물: IShakeV2Liquid(기본 ShakeV2ColumnWater). 코드로 칠한 작은 텍스처 하나를 실행 중 스프라이트로 만들어 Liquid에 붙인다.
/// - 얼음: ShakeV2IceSimulation(Unity 2D 물리). IcePhysics 묶음을 처음 쉐이킹 때 따로 만든 물리 씬으로 옮겨 다른 콜라이더와
///   엮이지 않게 하고, 프로젝트 물리(FixedUpdate)가 아니라 여기서 직접 한 스텝씩 진행한다. 화면의 얼음 그림은 몸체의 위치·각도를
///   읽어 포즈 시트 칸을 고른다(그림은 돌리지 않는다).
/// - 관성: 매 프레임 셰이커 속도 변화를 기울기만큼 돌려 통 기준으로 바꾼 뒤, 부분 스텝마다 나눠 물과 얼음 속도에서 뺀다.
/// </summary>
public sealed class ShakeV2Contents : IDisposable
{
    /// <summary>프리팹에 미리 둔 부품.</summary>
    public struct Parts
    {
        public Transform ClosedBody;
        public SpriteRenderer Liquid, Frost;
        public SpriteRenderer[] Ice;
        public ShakeV2IcePoseSheet Poses;
        public Transform IcePhysics;
        public EdgeCollider2D Chamber;
        public Rigidbody2D[] Bodies;
        /// <summary>원본 1px의 월드 크기(Scale 1). 물 텍스처의 PPU를 여기서 정한다.</summary>
        public float WorldPerArtPixel;
    }

    /// <summary>밖으로 나온 얼음 하나의 그릴 값.</summary>
    public struct Spill
    {
        public bool Visible;
        /// <summary>단면 좌표(셰이커 단위, 파손 순간의 셰이커 자세 기준).</summary>
        public Vector2 Position;
        /// <summary>단면 기준 웹 각도(rad).</summary>
        public float Angle, Alpha;
        public int Diameter;
    }

    static int sceneCounter;
    static bool quitting;

    // 도메인 리로드를 끈 에디터에서도 플레이마다 처음 값으로. 종료(플레이 모드 끝) 중에는 씬을 따로 내리지 않는다(Unity가 모두 정리한다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sceneCounter = 0;
        quitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    static void OnQuitting() => quitting = true;

    readonly Parts parts;
    readonly ShakeV2ContentsTuning tuning;
    readonly IShakeV2Liquid liquid;
    readonly ShakeV2DotCanvas liquidCanvas, frostCanvas;
    Texture2D liquidTexture, frostTexture;
    Sprite liquidSprite, frostSprite;
    ShakeV2IceSimulation ice;
    Scene scene;
    bool running, spilling, liquidDirty;
    Vector2 lastVelocity;
    float lastChill = -1f, breakAt, breakAngle;
    int[] spillOrder = Array.Empty<int>();
    float[] spillLife = Array.Empty<float>();
    bool[] launched = Array.Empty<bool>();
    Vector2[] breakPosition = Array.Empty<Vector2>();
    float[] breakSpin = Array.Empty<float>(), breakRotation = Array.Empty<float>();
    Spill[] spills = Array.Empty<Spill>();

    public ShakeV2Contents(Parts parts, ShakeV2ContentsTuning tuning, IShakeV2Liquid liquid = null)
    {
        this.parts = parts;
        this.tuning = tuning ?? new ShakeV2ContentsTuning();
        this.liquid = liquid ?? new ShakeV2ColumnWater(this.tuning.water, Random.Range(1, int.MaxValue));
        float dots = ShakeV2DotCanvas.DotsPerUnit;
        int width = 2 * Mathf.CeilToInt(Mathf.Max(-ShakeV2Presentation.ChamberX0, ShakeV2Presentation.ChamberX1) * dots);
        int height = 2 * Mathf.CeilToInt(ShakeV2Presentation.ChamberMiddle * dots);
        liquidCanvas = new ShakeV2DotCanvas(width, height);
        frostCanvas = new ShakeV2DotCanvas(width, height);
    }

    public IShakeV2Liquid Liquid => liquid;
    public ShakeV2IceSimulation Ice => ice;
    public ShakeV2DotCanvas LiquidCanvas => liquidCanvas;
    public ShakeV2DotCanvas FrostCanvas => frostCanvas;
    /// <summary>쉐이킹 중 내용물이 움직이고 있다.</summary>
    public bool Running => running;
    public IReadOnlyList<Spill> Spills => spills;
    /// <summary>얼음 물리 씬. 테스트·캡처가 다른 물리와 섞이지 않는지 확인할 때 읽는다.</summary>
    public Scene PhysicsScene => scene;

    public void SetLayers(IReadOnlyList<ShakeV2Layer> layers, Color? target)
    {
        liquid.SetLayers(layers, target);
        liquidDirty = true;
    }

    // ── 시작·정지 ───────────────────────────────────────────────────────

    /// <summary>쉐이킹 시작: 수평 수면, 얼음은 프리팹에 저장된 자리(왼쪽 아래).</summary>
    public void Begin(ShakeV2Rules rules)
    {
        EnsurePhysics();
        liquid.Refill();
        ice?.Reset(tuning.ice);
        lastVelocity = rules.Velocity;
        running = true;
        spilling = false;
        liquidDirty = true;
        lastChill = -1f;
        HideSpills();
    }

    /// <summary>모두 멈추고 숨긴다(새 판, 분리).</summary>
    public void Stop()
    {
        running = false;
        spilling = false;
        ice?.Hide();
        HideSpills();
    }

    void HideSpills()
    {
        for (int i = 0; i < spills.Length; i++) spills[i].Visible = false;
    }

    void EnsurePhysics()
    {
        if (ice != null || parts.IcePhysics == null) return;
        var root = parts.IcePhysics.gameObject;
        scene = CreatePhysicsScene($"ShakeV2IcePhysics_{++sceneCounter}");
        root.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        root.SetActive(true);
        var physics = scene.GetPhysicsScene2D();
        if (physics == Physics2D.defaultPhysicsScene)
            Debug.LogWarning("[ShakeV2] 얼음 물리 씬이 기본 물리 씬과 같습니다. 다른 콜라이더와 섞일 수 있습니다.");
        ice = new ShakeV2IceSimulation(parts.Bodies, parts.Chamber, physics);
        ice.Hide();

        int count = ice.Count;
        spillOrder = new int[count];
        spillLife = new float[count];
        launched = new bool[count];
        breakPosition = new Vector2[count];
        breakSpin = new float[count];
        breakRotation = new float[count];
        spills = new Spill[count];
    }

    // 실행 중에는 로컬 2D 물리를 가진 씬, 편집 모드(테스트·캡처)에서는 미리보기 씬을 쓴다. 둘 다 자동으로 진행되지 않는다.
    static Scene CreatePhysicsScene(string name)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
#endif
        return SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
    }

    public void Dispose()
    {
        if (parts.IcePhysics != null && scene.IsValid() && parts.IcePhysics.gameObject.scene == scene) Destroy(parts.IcePhysics.gameObject);
        if (scene.IsValid())
        {
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            else
#endif
            if (scene.isLoaded && !quitting) SceneManager.UnloadSceneAsync(scene);
        }
        scene = default;
        ice = null;
        Destroy(liquidSprite);
        Destroy(liquidTexture);
        Destroy(frostSprite);
        Destroy(frostTexture);
        liquidSprite = frostSprite = null;
        liquidTexture = frostTexture = null;
    }

    static void Destroy(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    // ── 진행 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 한 프레임. 쉐이킹 중이고 게임 시간이 흘렀을 때만 물·얼음이 움직인다(순간 정지·일시정지·다른 단계에서는 멈춤).
    /// </summary>
    public void Tick(ShakeV2Rules rules, float dt)
    {
        if (spilling)
        {
            TickSpill(rules, dt);
            return;
        }
        if (!running || dt <= 0f || rules.Phase != EShakeV2Phase.Shake) return;

        float k = tuning.gameUnitsPerShakerUnit, tilt = rules.Tilt, c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
        Vector2 change = rules.Velocity - lastVelocity;
        lastVelocity = rules.Velocity;
        // 게임 좌표(+Y 아래) → 통 기준: 기울기만큼 되돌리고 셰이커 단위로.
        Vector2 velocityChange = new Vector2(change.x * c + change.y * s, -change.x * s + change.y * c) / k * tuning.inertia;
        Vector2 gravity = new Vector2(s, c) * tuning.gravity;

        liquid.LostFraction = rules.SpilledFraction;
        int substeps = Mathf.Max(1, tuning.water.substeps);
        float frame = Mathf.Min(dt, tuning.water.maxFrameSec), h = frame / substeps;
        Vector2 share = velocityChange / substeps;
        float now = rules.GameTime;
        for (int i = 0; i < substeps; i++)
        {
            liquid.Step(h, gravity, share);
            ice?.Step(h, gravity, share, liquid, tuning.ice, now);
        }
        liquid.EndFrame(frame, gravity, velocityChange, rules.Mix / 100f);
        liquidDirty = true;
    }

    // ── 파손 ────────────────────────────────────────────────────────────

    /// <summary>파손: 단면과 안의 얼음을 바로 숨기고, 입구에 가까운 조각부터 차례로 내보낼 준비를 한다(웹 popOff).</summary>
    public void BeginBreak(ShakeV2Rules rules, float poseAngle)
    {
        running = false;
        if (ice == null) return;
        int count = ice.Count;
        float lip = tuning.ice.lipX;
        for (int i = 0; i < count; i++)
        {
            spillOrder[i] = i;
            launched[i] = false;
            spillLife[i] = 0f;
            breakPosition[i] = ice.Position(i);
            breakRotation[i] = ice.Angle(i);
            breakSpin[i] = ice.Spin(i);
        }
        Array.Sort(spillOrder, (a, b) => Mathf.Abs(breakPosition[a].x - lip).CompareTo(Mathf.Abs(breakPosition[b].x - lip)));
        ice.BeginSpill();
        HideSpills();
        breakAt = rules.GameTime;
        breakAngle = poseAngle;
        spilling = true;
    }

    void TickSpill(ShakeV2Rules rules, float dt)
    {
        if (ice == null || dt <= 0f) return;
        var t = tuning.ice;
        float k = tuning.gameUnitsPerShakerUnit, c = Mathf.Cos(breakAngle), s = Mathf.Sin(breakAngle);
        Vector2 ToChamber(Vector2 game) => new Vector2(game.x * c + game.y * s, -game.x * s + game.y * c) / k;

        float age = rules.GameTime - breakAt;
        int count = spillOrder.Length;
        for (int n = 0; n < count; n++)
        {
            int i = spillOrder[n];
            if (launched[i] || age < t.spillFirstSec + n * t.spillIntervalSec) continue;
            float progress = n / (float)count, half = ice.Half(i);
            Vector2 from = new Vector2(t.lipX + half, Mathf.Clamp(breakPosition[i].y + 20f, 0f, 48f - half));
            Vector2 velocity = new Vector2(
                t.spillSpeed.x + t.spillSpeed.y * progress + Random.Range(-t.spillSpeedJitter, t.spillSpeedJitter),
                Random.Range(t.spillLift.x, t.spillLift.y) + t.spillLift.z * progress);
            float spin = breakSpin[i] * t.spillSpin.x + Random.Range(-t.spillSpin.y, t.spillSpin.y);
            ice.Launch(i, from, ToChamber(velocity), breakRotation[i], spin);
            launched[i] = true;
            spillLife[i] = t.spillLife.x;
        }

        int substeps = Mathf.Max(1, tuning.water.substeps);
        float frame = Mathf.Min(dt, tuning.water.maxFrameSec), h = frame / substeps;
        Vector2 gravity = ToChamber(new Vector2(0f, t.spillGravity));
        for (int i = 0; i < substeps; i++) ice.StepSpill(h, gravity);

        for (int i = 0; i < count; i++)
        {
            if (!launched[i]) continue;
            if (spillLife[i] > 0f)
            {
                spillLife[i] -= dt;
                if (spillLife[i] <= 0f) ice.Retire(i);
            }
            bool visible = spillLife[i] > 0f;
            spills[i] = new Spill
            {
                Visible = visible,
                Position = ice.Position(i),
                Angle = ice.Angle(i),
                Alpha = Mathf.Clamp01(spillLife[i] / Mathf.Max(.0001f, t.spillLife.y)),
                Diameter = DiameterOf(i),
            };
        }
    }

    int DiameterOf(int i) => ice != null ? Mathf.RoundToInt(2f * ice.Half(i) * ShakeV2DotCanvas.DotsPerUnit) : 0;

    // ── 그리기 ──────────────────────────────────────────────────────────

    /// <summary>단면 그림(물·성에 텍스처, 안의 얼음). 쉐이킹 중에만 보인다(외피가 꺼지면 함께 숨는다).</summary>
    public void Draw(ShakeV2Rules rules)
    {
        if (!running) return;
        EnsureTextures();
        float now = rules.GameTime;
        if (liquidDirty)
        {
            liquid.Draw(liquidCanvas, rules.Chill, now);
            liquidTexture.SetPixels32(liquidCanvas.Pixels);
            liquidTexture.Apply(false);
            liquidDirty = false;
        }
        if (!Mathf.Approximately(lastChill, rules.Chill))
        {
            lastChill = rules.Chill;
            ShakeV2ColumnWater.DrawFrost(frostCanvas, rules.Chill, tuning.frostColor, tuning.frostFrom, tuning.frostPerChill);
            frostTexture.SetPixels32(frostCanvas.Pixels);
            frostTexture.Apply(false);
        }
        DrawIce(now);
    }

    void EnsureTextures()
    {
        float ppu = 1f / Mathf.Max(.0001f, parts.WorldPerArtPixel);
        if (liquidTexture == null) (liquidTexture, liquidSprite) = CreateCanvasSprite(liquidCanvas, "ShakeV2Liquid", ppu);
        if (frostTexture == null) (frostTexture, frostSprite) = CreateCanvasSprite(frostCanvas, "ShakeV2Frost", ppu);
        if (parts.Liquid != null && parts.Liquid.sprite != liquidSprite) parts.Liquid.sprite = liquidSprite;
        if (parts.Frost != null && parts.Frost.sprite != frostSprite) parts.Frost.sprite = frostSprite;
    }

    static (Texture2D, Sprite) CreateCanvasSprite(ShakeV2DotCanvas canvas, string name, float ppu)
    {
        var texture = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false)
        {
            name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
        };
        var pivot = new Vector2(canvas.OriginX / (float)canvas.Width, 1f - canvas.OriginY / (float)canvas.Height);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, canvas.Width, canvas.Height), pivot, ppu, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return (texture, sprite);
    }

    // 안의 얼음: 몸체 위치를 원본 도트의 정수 자리로 맞추고, 크기·각도에 맞는 포즈 칸을 회전 없이 놓는다.
    void DrawIce(float now)
    {
        var renderers = parts.Ice ?? Array.Empty<SpriteRenderer>();
        var body = parts.ClosedBody;
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null) continue;
            bool visible = ice != null && i < ice.Count && ice.Active(i);
            if (renderer.enabled != visible) renderer.enabled = visible;
            if (!visible) continue;

            Vector2 p = ice.Position(i);
            var dot = new Vector2(Mathf.Round(p.x * ShakeV2DotCanvas.DotsPerUnit), Mathf.Round(p.y * ShakeV2DotCanvas.DotsPerUnit));
            // 단면 좌표(+Y 아래)는 셰이커 피벗 좌표(+Y 위)와 같은 원점이다. 외피(ClosedBody)는 반 바퀴 돌아 있으므로 그 안 좌표로 바꾼다.
            var pivotLocal = new Vector3(dot.x, -dot.y, 0f) * parts.WorldPerArtPixel;
            renderer.transform.localPosition = body != null
                ? Quaternion.Inverse(body.localRotation) * (pivotLocal - body.localPosition)
                : pivotLocal;
            if (parts.Poses != null) renderer.sprite = parts.Poses.SpriteFor(DiameterOf(i), ice.Angle(i));
            var color = renderer.color;
            float hot = Mathf.Max(0f, 1f - (now - ice.HitAt(i)) / tuning.ice.flashSec);
            color.a = tuning.ice.alpha + (1f - tuning.ice.alpha) * hot;
            renderer.color = color;
        }
    }
}
