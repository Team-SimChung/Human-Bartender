using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed class PourTargetHeightTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Vector2 HalfExtents = new Vector2(.65f, 1.27f);
    readonly List<Object> owned = new();
    PourManager manager;
    Transform glass;
    LiquidProfile profile;
    SphSimulation simulation;
    float capacityMl;
    UnityEngine.Random.State randomState;
    int pourTicks;

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(90210);
        var root = new GameObject("Inactive Pour Height Test");
        owned.Add(root);
        root.SetActive(false);
        manager = root.AddComponent<PourManager>();
        var glassObject = new GameObject("Glass");
        glassObject.transform.SetParent(root.transform, false);
        glass = glassObject.transform;
        var guide = new GameObject("Authored Target Height");
        guide.transform.SetParent(glass, false);
        Set("targetHeightMarker", guide.transform);
        var light = AssetDatabase.LoadAssetAtPath<LiquidProfile>("Assets/01.Game/04.Data/01.Definitions/Minigames/Pour/Liquid_Light.asset");
        profile = light != null ? Object.Instantiate(light) : ScriptableObject.CreateInstance<LiquidProfile>();
        owned.Add(profile);
        if (light == null)
        {
            profile.physics.spacing = .065f;
            profile.physics.viscosity = .12f;
            profile.physics.velocitySmoothing = .2f;
            profile.stretchPerSpeed = .55f;
            profile.maxStretch = 4.5f;
            string[] args = Environment.GetCommandLineArgs();
            int json = Array.IndexOf(args, "-pourHeightProfile");
            if (json >= 0) EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(args[json + 1]), profile);
        }
        profile.overrideColor = true;
        var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
        owned.Add(balance);
        balance.balanceData = new NewBalanceDataBase
        {
            Config = new NewBalanceConfig { UnitOzToMl = 30f, UnitTspToMl = 5f }
        };
        Set("glassCenter", glass);
        Set("glassInteriorHalfExtents", HalfExtents);
        Set("liquidProfile", profile);
        Set("balanceData", balance);
        Set("drivenByRunner", true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in owned) Object.DestroyImmediate(item);
        owned.Clear();
        UnityEngine.Random.state = randomState;
    }

    void Set(string name, object value) => typeof(PourManager).GetField(name, Private).SetValue(manager, value);
    T Get<T>(string name) => (T)typeof(PourManager).GetField(name, Private).GetValue(manager);
    void Call(string name, params object[] args) => typeof(PourManager).GetMethod(name, Private).Invoke(manager, args);

    void Configure(float target, ENewUnit unit, bool beerGlass, float scaleX = 1f, float scaleY = 1f, bool visible = true)
    {
        glass.localScale = new Vector3(scaleX, scaleY, 1f);
        var session = new CraftSession("height_test");
        session.Actual.SetGlass(beerGlass ? "weizen" : "long_drink");
        Set("craftContext", CraftContext.From(session));
        Set("pourStep", new GimmickStep(ECraftGimmick.Pour, "gin", target, unit, visible));
        Set("volumeTracker", new SphVolumeTracker(2000));
        Call("ConfigureVolume");
        capacityMl = Get<float>("visualCapacityMl");
        var boundary = new SphContainerBoundary(glass, HalfExtents, profile.physics.spacing * .5f);
        Set("glassBoundary", boundary);
        simulation = new SphSimulation(profile.physics, new Vector2(-4f, -4f), new Vector2(5f, 5f), boundary.Constrain);
        simulation.SynchronizeTransforms = false;
        Set("simulation", simulation);
    }

    [TestCase(90f, ENewUnit.Ml, false, 1f, 1f)]
    [TestCase(3f, ENewUnit.Oz, false, 1f, 1f)]
    [TestCase(12f, ENewUnit.Oz, true, 1f, 1f)]
    [TestCase(90f, ENewUnit.Ml, false, 1.6f, .85f)]
    public void InitialGuideUsesRecipeUnitsAndSameCapacityAsCountedLiquid(float target, ENewUnit unit, bool beerGlass, float scaleX, float scaleY)
    {
        Configure(target, unit, beerGlass, scaleX, scaleY);
        Call("CreateTargetHeightMarker");
        float targetMl = unit == ENewUnit.Oz ? target * 30f : target;
        float expected = -HalfExtents.y + HalfExtents.y * 2f * profile.TargetHeightRatio(targetMl / capacityMl);
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(expected).Within(.0001f));
        Assert.That(capacityMl, Is.EqualTo(beerGlass ? 450f : 300f));
    }

    [Test]
    public void HiddenRecipeTargetHidesAuthoredHeightGuide()
    {
        Configure(90f, ENewUnit.Ml, false, visible: false);
        Call("CreateTargetHeightMarker");
        Assert.That(Get<Transform>("targetHeightMarker").gameObject.activeSelf, Is.False);
    }

    [TestCase(30f, ENewUnit.Ml, false, 1f, 1f)]
    [TestCase(90f, ENewUnit.Ml, false, 1f, 1f)]
    [TestCase(12f, ENewUnit.Oz, true, 1f, 1f)]
    [TestCase(90f, ENewUnit.Ml, false, 1.6f, .85f)]
    public void GuideRemainsFixedWhileLiquidArrives(float target, ENewUnit unit, bool beerGlass, float scaleX, float scaleY)
    {
        Configure(target, unit, beerGlass, scaleX, scaleY);
        Call("CreateTargetHeightMarker");
        float initialY = Get<Transform>("targetHeightMarker").localPosition.y;
        float targetMl = unit == ENewUnit.Oz ? target * 30f : target;
        int count = Mathf.CeilToInt(targetMl / Get<float>("particleVolumeMl"));
        SeedAndSettle(count);
        Assert.That(Get<SphVolumeTracker>("volumeTracker").ReceivedCount, Is.EqualTo(count));
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
        Assert.That(manager.ShowOkMark, Is.False, "완료 전에는 결과 표시를 띄우지 않는다.");
    }

    [Test]
    public void OverfillingAndEmptyingTheGlassKeepTheInitialGuideHeight()
    {
        Configure(60f, ENewUnit.Ml, false);
        Call("CreateTargetHeightMarker");
        float initialY = Get<Transform>("targetHeightMarker").localPosition.y;
        SeedAndSettle(Mathf.CeilToInt(90f / Get<float>("particleVolumeMl")));
        Assert.That(manager.PouredInTargetUnit, Is.GreaterThan(60f));
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
        var remove = new List<SphParticle>(simulation.ActiveParticles);
        foreach (var p in remove) simulation.Unregister(p);
        Call("MeasureRetainedVolume");
        Assert.That(manager.PouredInTargetUnit, Is.Zero);
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
    }

    [Test]
    public void StreamWidthControlNarrowsAirborneParticlesWithoutChangingLengthOrContainedLiquid()
    {
        Configure(90f, ENewUnit.Ml, false);
        var renderRoot = new GameObject("Stream Width Render");
        owned.Add(renderRoot);
        var camera = renderRoot.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var output = new RenderTexture(1024, 1024, 0);
        owned.Add(output);
        output.Create();
        camera.targetTexture = output;
        var renderer = renderRoot.AddComponent<SphLiquidRenderer>();
        var settings = new SerializedObject(renderer);
        settings.FindProperty("fieldResolutionScale").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetContainer(glass, HalfExtents);
        renderer.SetTargetCamera(camera);
        renderer.Init(profile, Color.yellow);
        var particle = Particle(Vector2.zero);
        particle.velocity = Vector2.down * 1.6f;
        Vector2Int ReadShape(float scale, bool inside, bool received = false)
        {
            typeof(SphParticle).GetField("insideContainer", Private).SetValue(particle, inside);
            typeof(SphParticle).GetField("volumeReceived", Private).SetValue(particle, received);
            renderer.SetAirborneWidthScale(scale);
            renderer.UpdateBlobs(new[] { particle });
            var build = typeof(SphLiquidRenderer).GetMethod("BuildCommands", Private);
            Graphics.ExecuteCommandBuffer((CommandBuffer)build.Invoke(renderer, new object[] { camera, 1f / 60f }));
            var field = (RenderTexture)typeof(SphLiquidRenderer).GetField("fieldTexture", Private).GetValue(renderer);
            return new Vector2Int(ReadSpan(field, true), ReadSpan(field, false));
        }
        Vector2Int normal = ReadShape(1f, false);
        Vector2Int narrow = ReadShape(.7f, false);
        Assert.That(normal.x, Is.GreaterThan(0));
        Assert.That(narrow.x, Is.GreaterThan(0).And.LessThan(normal.x * .85f));
        Assert.That(narrow.y, Is.EqualTo(normal.y).Within(2), "물줄기의 축 방향 연결 길이를 유지한다.");
        Assert.That(ReadShape(.7f, true), Is.EqualTo(narrow),
            "잔 안으로 들어왔어도 아직 담기지 않은 유입 줄기는 가는 두께를 유지한다.");
        Vector2Int contained = ReadShape(1f, true, true);
        Assert.That(ReadShape(.7f, true, true), Is.EqualTo(contained), "실제로 담긴 액체의 크기는 유지한다.");
    }

    int ReadSpan(RenderTexture field, bool horizontal)
    {
        var pixels = new Texture2D(horizontal ? field.width : 1, horizontal ? 1 : field.height,
            TextureFormat.RGBAFloat, false, true);
        owned.Add(pixels);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = field;
            pixels.ReadPixels(horizontal ? new Rect(0f, field.height / 2, field.width, 1f)
                : new Rect(field.width / 2, 0f, 1f, field.height), 0, 0);
            pixels.Apply();
        }
        finally { RenderTexture.active = previous; }
        int count = 0;
        foreach (Color pixel in pixels.GetPixels()) if (pixel.r >= profile.threshold) count++;
        return count;
    }

    [TestCase(30f, ENewUnit.Ml, false, 1f, 1f)]
    [TestCase(90f, ENewUnit.Ml, false, 1f, 1f)]
    [TestCase(12f, ENewUnit.Oz, true, 1f, 1f)]
    [TestCase(90f, ENewUnit.Ml, false, 1.6f, .85f)]
    public void FixedGuideMatchesSurfaceAfterActualBottleEmission(float target, ENewUnit unit, bool beerGlass, float scaleX, float scaleY)
    {
        Configure(target, unit, beerGlass, scaleX, scaleY);
        Call("CreateTargetHeightMarker");
        float initialY = Get<Transform>("targetHeightMarker").localPosition.y;
        float targetMl = unit == ENewUnit.Oz ? target * 30f : target;
        PourAndSettle(Mathf.CeilToInt(targetMl / Get<float>("particleVolumeMl")));
        float surfaceY = RenderedSurfaceHeight();
        float error = Mathf.Abs(initialY - surfaceY);
        Debug.Log($"[Fixed Pour Height] target={targetMl:F2}ml, received={manager.PouredInTargetUnit:F4}{unit}, scale=({scaleX},{scaleY}), guide={initialY:F5}, body={surfaceY:F5}, error={error:F5}");
        Assert.That(error, Is.LessThan(profile.physics.spacing / scaleY));
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
        Assert.That(manager.ShowOkMark, Is.False, "완료 전에는 결과 표시를 띄우지 않는다.");
    }

    [Test]
    public void FixedBeerGuideAlsoMatchesWhileActualPouringReaches360Ml()
    {
        Configure(12f, ENewUnit.Oz, true);
        Call("CreateTargetHeightMarker");
        float initialY = Get<Transform>("targetHeightMarker").localPosition.y;
        PrepareBottleEmission();
        int target = Mathf.CeilToInt(360f / Get<float>("particleVolumeMl"));
        int steps = 0;
        while (Get<SphVolumeTracker>("volumeTracker").ReceivedCount < target && steps++ < 6000) TickPour(true);
        Assert.That(manager.ShowOkMark, Is.False, "완료 전에는 결과 표시를 띄우지 않는다.");
        float surface = RenderedSurfaceHeight();
        Debug.Log($"[Fixed Pour Inflow] received={manager.PouredInTargetUnit:F4}oz, guide={initialY:F5}, body={surface:F5}, error={Mathf.Abs(initialY-surface):F5}");
        Assert.That(Mathf.Abs(initialY - surface), Is.LessThan(profile.physics.spacing * 1.5f));
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
    }

    void PrepareBottleEmission(float angle = 140f)
    {
        var go = new GameObject("Actual Bottle");
        go.transform.SetParent(manager.transform, false);
        go.transform.localPosition = new Vector3(-2.03f, .13f, 0f);
        go.transform.localScale = new Vector3(1.4f, 3.6f, 1f);
        var bottle = go.AddComponent<BottleTiltController>();
        foreach (var pair in new Dictionary<string, float> { { "maxTiltAngle", Mathf.Max(140f, angle) }, { "liftHeight", 2.6f }, { "pourPivotHeight", .05f } })
            typeof(BottleTiltController).GetField(pair.Key, Private).SetValue(bottle, pair.Value);
        bottle.ConfigurePourGeometry(new Vector3(0f, .4f, 0f), glass.TransformPoint(new Vector3(-HalfExtents.x * .8f, HalfExtents.y, 0f)));
        typeof(BottleTiltController).GetField("currentAngle", Private).SetValue(bottle, angle);
        typeof(BottleTiltController).GetField("pickupProgress", Private).SetValue(bottle, 1f);
        typeof(BottleTiltController).GetMethod("ApplyPose", Private).Invoke(bottle, null);
        Set("bottle", bottle);
        Set("pourStartAngle", 95f);
        Set("pourFullOpenAngle", 110f);
        Set("neckHalfWidth", profile.physics.spacing * 2.5f * .5f / 1.4f);
        Set("unlimitedLiquid", true);
        Set("remainingInBottle", 400);
        Set("mlPerSecondAtFullFlow", 70f);
        Set("quantityAccumulateMultiplier", .25f);
        Set("isPlay", true);
        pourTicks = 0;
        Call("CreatePool");
        Call("CalibrateRestDensity");
    }

    void TickPour(bool emit)
    {
        if (emit) Call("EmitFromNeck", 1f / 120f);
        simulation.Tick(1f / 120f);
        Call("RecycleSpilledParticles");
        if (++pourTicks % 2 == 0) Call("MeasureRetainedVolume");
    }

    void PourAndSettle(int targetParticles)
    {
        PrepareBottleEmission();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int needed = targetParticles - Get<SphVolumeTracker>("volumeTracker").ReceivedCount;
            if (needed <= 0) break;
            int start = Get<int>("remainingInBottle");
            int steps = 0;
            while (start - Get<int>("remainingInBottle") < needed && steps++ < 6000) TickPour(true);
            Assert.That(steps, Is.LessThan(6000));
            for (int i = 0; i < 600; i++) TickPour(false);
        }
        Assert.That(Get<SphVolumeTracker>("volumeTracker").ReceivedCount, Is.GreaterThanOrEqualTo(targetParticles));
    }

    [Test]
    public void FullFlowBeerStreamDoesNotDigADeepCraterIntoPooledLiquid()
    {
        Configure(12f, ENewUnit.Oz, true);
        PrepareBottleEmission();
        Set("pourIngredientId", "beer");
        Call("ConfigureNeck");
        for (int i = 0; i < 540; i++) TickPour(true); // 최대 유량으로 4.5초
        var heights = new List<float>();
        var pixels = RenderSurfacePixels();
        for (int x = pixels.Length / 10; x < pixels.Length * 9 / 10; x++)
            heights.Add(pixels[x].a > .5f ? pixels[x].r : 0f);
        heights.Sort();
        float crater = heights[heights.Count * 3 / 4] - heights[0];
        Debug.Log($"[Pour Crater] beer full flow 4.5s: received={manager.PouredInTargetUnit:F2}oz, crater={crater:F3} of glass height");
        Assert.That(crater, Is.LessThan(.25f),
            "굵은 맥주 줄기가 고인 액체를 밀어내 V자 구덩이를 파면 안 된다(속도 제한 전 잔 높이의 약 46%).");
    }

    [Test]
    public void SteepBeerPourAlongTheWallStaysOneStreamWithoutSprayingBackUp()
    {
        Configure(12f, ENewUnit.Oz, true);
        PrepareBottleEmission(170f);
        Set("pourIngredientId", "beer");
        Call("ConfigureNeck");
        var inside = typeof(SphParticle).GetField("insideContainer", Private);
        var received = typeof(SphParticle).GetField("volumeReceived", Private);
        var heights = new List<float>();
        float hangingSum = 0f;
        int samples = 0;
        for (int i = 1; i <= 360; i++) // 최대 유량으로 3초. 입구가 잔 왼쪽 벽 바로 위에 온다.
        {
            TickPour(true);
            if (i <= 120 || i % 2 != 0) continue;
            heights.Clear();
            foreach (var p in simulation.ActiveParticles)
                if ((bool)received.GetValue(p)) heights.Add(GlassHeight(p.pos));
            heights.Sort();
            float surface = heights.Count > 0 ? heights[heights.Count * 9 / 10] : 0f;
            // 수면보다 한참 위에서 떨어지지도 않고 떠 있는 잔 안 입자 = 튀어 오른 물보라나 막혀서 멈춘 줄기.
            foreach (var p in simulation.ActiveParticles)
                if ((bool)inside.GetValue(p) && !(bool)received.GetValue(p) &&
                    GlassHeight(p.pos) > surface + .12f && p.velocity.y > -1.5f) hangingSum++;
            samples++;
        }
        float hanging = hangingSum / samples;
        Debug.Log($"[Pour Spray] beer 170deg full flow: hanging particles mean={hanging:F1}, received={manager.PouredInTargetUnit:F2}oz");
        Assert.That(hanging, Is.LessThan(15f),
            "벽에 붙어 떨어지는 줄기가 입구 아래에서 막히거나, 고인 액체에 부딪혀 입구까지 튀어 오르면 안 된다(수정 전 약 27).");
    }

    float GlassHeight(Vector2 position) => (glass.InverseTransformPoint(position).y + HalfExtents.y) / (2f * HalfExtents.y);

    float RenderedSurfaceHeight()
    {
        var heights = new List<float>();
        var pixels = RenderSurfacePixels();
        for (int x = pixels.Length / 8; x < pixels.Length * 7 / 8; x++)
            if (pixels[x].a > .5f) heights.Add(-HalfExtents.y + pixels[x].r * HalfExtents.y * 2f);
        Assert.That(heights.Count, Is.GreaterThan(0));
        heights.Sort();
        return heights[heights.Count / 2];
    }

    /// <summary>수면 추출 패스의 열별 결과. r=잔 높이 비율, a=바닥에서 이어진 액체가 있는지.</summary>
    Color[] RenderSurfacePixels()
    {
        var root = new GameObject("Actual Liquid Render");
        owned.Add(root);
        var camera = root.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.5f * Mathf.Max(glass.lossyScale.x, glass.lossyScale.y);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var output = new RenderTexture(1024, 1024, 0);
        owned.Add(output);
        output.Create();
        camera.targetTexture = output;
        var renderer = root.AddComponent<SphLiquidRenderer>();
        renderer.SetContainer(glass, HalfExtents);
        renderer.SetTargetCamera(camera);
        renderer.Init(profile, Color.yellow, profile.ResolveAppearance("beer"));
        renderer.UpdateBlobs(simulation.ActiveParticles);
        var build = typeof(SphLiquidRenderer).GetMethod("BuildCommands", Private);
        Graphics.ExecuteCommandBuffer((CommandBuffer)build.Invoke(renderer, new object[] { camera, 1f / 60f }));
        var surface = (RenderTexture)typeof(SphLiquidRenderer).GetField("surfaceRaw", Private).GetValue(renderer);
        var texture = new Texture2D(surface.width, 1, TextureFormat.RGBAFloat, false, true);
        owned.Add(texture);
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = surface;
            texture.ReadPixels(new Rect(0f, 0f, surface.width, 1f), 0, 0);
            texture.Apply();
        }
        finally { RenderTexture.active = previous; }
        return texture.GetPixels();
    }

    SphParticle Particle(Vector2 position)
    {
        var go = new GameObject("Particle");
        go.transform.SetParent(manager.transform, false);
        var p = go.AddComponent<SphParticle>();
        p.Init(profile.physics);
        p.Activate(position);
        simulation.Register(p);
        return p;
    }

    void SeedAndSettle(int count)
    {
        float initialGuideY = Get<Transform>("targetHeightMarker").localPosition.y;
        float spacing = profile.physics.spacing;
        var calibration = new List<SphParticle>();
        for (int i = 0; i < 25; i++) calibration.Add(Particle(new Vector2((i % 5 - 2) * spacing, 3f + (i / 5 - 2) * spacing)));
        simulation.CalibrateRestDensity();
        foreach (var p in calibration) { simulation.Unregister(p); Object.DestroyImmediate(p.gameObject); }
        Vector3 scale = glass.lossyScale;
        int columns = Mathf.FloorToInt(HalfExtents.x * 2f * scale.x / spacing);
        for (int i = 0; i < count; i++)
        {
            Vector3 local = new Vector3(-HalfExtents.x + (i % columns + .5f) * spacing / scale.x,
                -HalfExtents.y + (i / columns + .5f) * spacing / scale.y, 0f);
            Particle(glass.TransformPoint(local));
        }
        for (int i = 0; i < 600; i++)
        {
            simulation.Tick(1f / 120f);
            if (i % 2 == 0)
            {
                Call("MeasureRetainedVolume");
                Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialGuideY));
            }
        }
        Call("MeasureRetainedVolume");
    }
}
