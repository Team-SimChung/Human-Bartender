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
        Assert.That(manager.ShowOkMark, Is.True);
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
        Assert.That(manager.ShowOkMark, Is.True);
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
        Assert.That(manager.ShowOkMark, Is.True);
        float surface = RenderedSurfaceHeight();
        Debug.Log($"[Fixed Pour Inflow] received={manager.PouredInTargetUnit:F4}oz, guide={initialY:F5}, body={surface:F5}, error={Mathf.Abs(initialY-surface):F5}");
        Assert.That(Mathf.Abs(initialY - surface), Is.LessThan(profile.physics.spacing * 1.5f));
        Assert.That(Get<Transform>("targetHeightMarker").localPosition.y, Is.EqualTo(initialY));
    }

    void PrepareBottleEmission()
    {
        var go = new GameObject("Actual Bottle");
        go.transform.SetParent(manager.transform, false);
        go.transform.localPosition = new Vector3(-2.03f, .13f, 0f);
        go.transform.localScale = new Vector3(1.4f, 3.6f, 1f);
        var bottle = go.AddComponent<BottleTiltController>();
        foreach (var pair in new Dictionary<string, float> { { "maxTiltAngle", 140f }, { "liftHeight", 2.6f }, { "pourPivotHeight", .05f } })
            typeof(BottleTiltController).GetField(pair.Key, Private).SetValue(bottle, pair.Value);
        bottle.ConfigurePourGeometry(new Vector3(0f, .4f, 0f), glass.TransformPoint(new Vector3(-HalfExtents.x * .8f, HalfExtents.y, 0f)));
        typeof(BottleTiltController).GetField("currentAngle", Private).SetValue(bottle, 140f);
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

    float RenderedSurfaceHeight()
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
        var heights = new List<float>();
        var pixels = texture.GetPixels();
        for (int x = pixels.Length / 8; x < pixels.Length * 7 / 8; x++)
            if (pixels[x].a > .5f) heights.Add(-HalfExtents.y + pixels[x].r * HalfExtents.y * 2f);
        Assert.That(heights.Count, Is.GreaterThan(0));
        heights.Sort();
        return heights[heights.Count / 2];
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
