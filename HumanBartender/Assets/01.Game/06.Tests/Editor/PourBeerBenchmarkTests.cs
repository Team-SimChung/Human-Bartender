using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// 실제 PourManager 방출 경로로 목표량까지 따르며 CPU 물리 비용과 GPU 패스 비용을 잰다.
/// 결과는 Console의 [Pour Benchmark] 줄로 남긴다. 판정용 테스트가 아니라 수정 전후 비교용 측정이다.
/// </summary>
public sealed class PourBeerBenchmarkTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float Step = 1f / 120f;
    static readonly Vector2 HalfExtents = new Vector2(.65f, 1.27f);
    readonly List<Object> owned = new();
    PourManager manager;
    Transform glass;
    LiquidProfile profile;
    SphSimulation simulation;
    SphVolumeTracker tracker;
    UnityEngine.Random.State randomState;
    int ticks;

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(90210);
        var root = new GameObject("Inactive Pour Benchmark");
        owned.Add(root);
        root.SetActive(false);
        manager = root.AddComponent<PourManager>();
        var glassObject = new GameObject("Glass");
        glassObject.transform.SetParent(root.transform, false);
        glass = glassObject.transform;
        var light = AssetDatabase.LoadAssetAtPath<LiquidProfile>("Assets/01.Game/04.Data/01.Definitions/Minigames/Pour/Liquid_Light.asset");
        Assert.That(light, Is.Not.Null, "프리팹이 쓰는 Liquid_Light 프로파일이 필요하다.");
        profile = Object.Instantiate(light);
        owned.Add(profile);
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

    // detachedMultiplier > 0이면 푸어러가 분리된 상태(병째 쏟기)로 따른다. 2.26은 이전 값, 4.52는 현재 프리팹 값이다.
    [TestCase("beer", "weizen", 360f, true, 0f)]
    [TestCase("red_wine", "long_drink", 240f, false, 0f)]
    [TestCase("orange_juice", "long_drink", 180f, false, 0f)]
    [TestCase("soda_water", "long_drink", 120f, false, 0f)]
    [TestCase("gin", "long_drink", 60f, false, 0f)]
    [TestCase("whiskey", "long_drink", 120f, false, 95f / 42f)]
    [TestCase("whiskey", "long_drink", 120f, false, 2f * 95f / 42f)]
    public void MeasureActualPourCost(string ingredient, string glassId, float targetMl, bool measureGpu, float detachedMultiplier)
    {
        var report = new StringBuilder();
        var session = new CraftSession("pour_benchmark");
        session.Actual.SetGlass(glassId);
        Set("craftContext", CraftContext.From(session));
        Set("pourStep", new GimmickStep(ECraftGimmick.Pour, ingredient, targetMl, ENewUnit.Ml, true));
        Set("pourIngredientId", ingredient);
        tracker = new SphVolumeTracker(2000);
        Set("volumeTracker", tracker);
        Call("ConfigureVolume");
        var bottle = PrepareBottle();
        if (detachedMultiplier > 0f) DetachPourer(detachedMultiplier);
        var boundary = new SphContainerBoundary(glass, HalfExtents, profile.physics.spacing * .5f);
        Set("glassBoundary", boundary);
        Vector2 bottlePos = bottle.BottleVisual.position;
        Vector2 glassPos = glass.position;
        simulation = new SphSimulation(profile.physics, Vector2.Min(bottlePos, glassPos) - Vector2.one * 3f,
            Vector2.Max(bottlePos, glassPos) + Vector2.one * 3f, boundary.Constrain);
        simulation.SynchronizeTransforms = false;
        Set("simulation", simulation);

        var poolWatch = Stopwatch.StartNew();
        Call("CreatePool");
        poolWatch.Stop();
        Call("CalibrateRestDensity");

        float particleMl = Get<float>("particleVolumeMl");
        int target = Mathf.CeilToInt(targetMl / particleMl);
        string pourer = detachedMultiplier > 0f ? $" (pourer detached x{detachedMultiplier:F2})" : "";
        report.AppendLine($"[Pour Benchmark] {ingredient}{pourer} {targetMl}ml in {glassId}: {particleMl:F3} ml/particle, target {target} particles, " +
                          $"pool of {Get<int>("maxLiveParticles")} GameObjects created in {poolWatch.Elapsed.TotalMilliseconds:F1} ms");

        // 따르는 동안: 매 물리 스텝(120Hz) 비용을 살아 있는 입자 수 구간별로 모은다.
        var samples = new List<(int count, double ms)>(6000);
        float[] milestones = { .25f, .5f, .75f, 1f };
        var reachedAt = new float[milestones.Length];
        int next = 0, steps = 0, fullIterations = 0;
        while (tracker.ReceivedCount < target && steps < 6000)
        {
            long start = Stopwatch.GetTimestamp();
            TickPour(true);
            samples.Add((simulation.ActiveParticles.Count, Ms(start)));
            if (simulation.LastDensityIterations >= profile.physics.densityIterations) fullIterations++;
            steps++;
            while (next < milestones.Length && tracker.ReceivedCount >= target * milestones[next])
                reachedAt[next++] = steps * Step;
        }
        Assert.That(tracker.ReceivedCount, Is.GreaterThanOrEqualTo(target), "목표량까지 따르지 못했다.");
        report.AppendLine($"[Pour Benchmark] fill time at full tilt (simulated s): 25%={reachedAt[0]:F2} 50%={reachedAt[1]:F2} " +
                          $"75%={reachedAt[2]:F2} 100%={reachedAt[3]:F2}, steps={steps}, " +
                          $"steps using all {profile.physics.densityIterations} density iterations={100f * fullIterations / Mathf.Max(1, steps):F0}%");
        for (int bucket = 0; bucket < 10; bucket++)
        {
            var bucketMs = new List<double>();
            foreach (var sample in samples)
                if (Mathf.Min(sample.count / 100, 9) == bucket) bucketMs.Add(sample.ms);
            if (bucketMs.Count == 0) continue;
            bucketMs.Sort();
            double mean = 0;
            foreach (double ms in bucketMs) mean += ms;
            mean /= bucketMs.Count;
            double p95 = bucketMs[Mathf.Min(bucketMs.Count - 1, (int)(bucketMs.Count * .95f))];
            report.AppendLine($"[Pour Benchmark]   live {bucket * 100,4}-{bucket * 100 + 99,4}: step mean={mean:F3} ms p95={p95:F3} ms " +
                              $"-> per 60fps frame (2 steps)={mean * 2:F2} ms, CPU share of real time={mean * 120 / 10:F1}% ({bucketMs.Count} steps)");
        }

        // 다 따른 뒤 잔에 고인 상태의 비용과 그 안의 비중.
        for (int i = 0; i < 120; i++) TickPour(false);
        double settled = Average(240, () => TickPour(false));
        var assign = (Action)Delegate.CreateDelegate(typeof(Action), simulation, typeof(SphSimulation).GetMethod("AssignToGrid", Private));
        var density = (Action)Delegate.CreateDelegate(typeof(Action), simulation, typeof(SphSimulation).GetMethod("CalculateDensity", Private));
        double search = Average(240, () => { assign(); density(); });
        double measure = Average(240, () => tracker.Measure(simulation.ActiveParticles, boundary, profile.physics.spacing));
        int iterations = profile.physics.densityIterations;
        profile.physics.densityIterations = 1;
        double oneIteration = Average(240, () => simulation.Tick(Step));
        profile.physics.densityIterations = iterations;
        double allIterations = Average(240, () => simulation.Tick(Step));
        report.AppendLine($"[Pour Benchmark] settled with {simulation.ActiveParticles.Count} particles: full step={settled:F3} ms, " +
                          $"SPH tick ({iterations} iterations)={allIterations:F3} ms, SPH tick (1 iteration)={oneIteration:F3} ms, " +
                          $"one grid+neighbour search={search:F3} ms (x{iterations} per tick), volume measure={measure:F3} ms (every 2nd step)");

        if (measureGpu)
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) })
                MeasureGpu(ingredient, size, report);
        Debug.Log(report.ToString());
    }

    BottleTiltController PrepareBottle()
    {
        var go = new GameObject("Benchmark Bottle");
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
        // 프리팹(NewPourGame)과 같은 배출 설정.
        Set("bottle", bottle);
        Set("pourStartAngle", 95f);
        Set("pourFullOpenAngle", 110f);
        Call("ConfigureNeck"); // 재료별 물줄기 굵기(유량 표)에 맞춘 병목 폭
        Set("unlimitedLiquid", true);
        Set("remainingInBottle", 400);
        Set("mlPerSecondAtFullFlow", 70f);
        Set("quantityAccumulateMultiplier", .25f);
        Set("isPlay", true);
        ticks = 0;
        return bottle;
    }

    // 푸어러가 이미 분리된 상태. 분리 각도까지 병을 돌리지 않고 분리 결과(유량 배율, 굵은 줄기)만 재현한다.
    void DetachPourer(float multiplier)
    {
        var effect = manager.gameObject.AddComponent<PourerBreakaway>();
        typeof(PourerBreakaway).GetField("detachedFlowMultiplier", Private).SetValue(effect, multiplier);
        typeof(PourerBreakaway).GetField("<IsDetached>k__BackingField", Private).SetValue(effect, true);
        Set("pourer", effect);
    }

    // PourManager.StepSimulation의 한 스텝과 같은 순서.
    void TickPour(bool emit)
    {
        if (emit) Call("EmitFromNeck", Step);
        simulation.Tick(Step);
        Call("RecycleSpilledParticles");
        if (++ticks % 2 == 0) Call("MeasureRetainedVolume");
    }

    void MeasureGpu(string ingredient, Vector2Int size, StringBuilder report)
    {
        var root = new GameObject("Benchmark Render");
        owned.Add(root);
        var camera = root.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 3.6f; // Play 씬 CM 카메라와 같은 화각
        camera.transform.position = new Vector3(-.8f, .6f, -10f);
        var output = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32);
        owned.Add(output);
        output.Create();
        camera.targetTexture = output;
        var renderer = root.AddComponent<SphLiquidRenderer>();
        renderer.SetContainer(glass, HalfExtents);
        renderer.SetTargetCamera(camera);
        renderer.Init(profile, new Color(.9f, .6f, .15f, .75f), profile.ResolveAppearance(ingredient));
        renderer.ReserveParticles(2000);
        renderer.UpdateBlobs(simulation.ActiveParticles);
        var build = typeof(SphLiquidRenderer).GetMethod("BuildCommands", Private);
        var composite = (Material)typeof(SphLiquidRenderer).GetField("compositeMaterial", Private).GetValue(renderer);
        var probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        owned.Add(probe);
        void Sync()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = output;
            probe.ReadPixels(new Rect(0f, 0f, 1f, 1f), 0, 0);
            RenderTexture.active = previous;
        }
        void Simulate() => Graphics.ExecuteCommandBuffer((CommandBuffer)build.Invoke(renderer, new object[] { camera, 1f / 60f }));
        var draw = new CommandBuffer { name = "Pour Composite Benchmark" };
        draw.SetRenderTarget(output);
        draw.ClearRenderTarget(false, true, Color.clear);
        draw.Blit(Texture2D.blackTexture, output, composite, 0);

        for (int i = 0; i < 10; i++) { Simulate(); Graphics.ExecuteCommandBuffer(draw); }
        Sync();
        double syncOnly = Average(60, Sync);
        // 몰아서 제출한 뒤 한 번만 기다린다. GPU가 병목이면 한 번당 GPU 시간에 가깝다.
        double simulatePasses = Batched(200, Simulate, Sync);
        double compositePass = Batched(200, () => Graphics.ExecuteCommandBuffer(draw), Sync);
        double simulateCpu = Average(200, Simulate);
        Sync();

        var pixels = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
        owned.Add(pixels);
        Graphics.ExecuteCommandBuffer(draw);
        var previousActive = RenderTexture.active;
        RenderTexture.active = output;
        pixels.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
        RenderTexture.active = previousActive;
        int covered = 0;
        foreach (Color32 pixel in pixels.GetPixels32()) if (pixel.a > 0) covered++;
        draw.Release();
        // 에디트 모드에서는 OnDestroy가 자동으로 불리지 않는다. GPU 버퍼와 렌더 타깃을 직접 정리한다.
        typeof(SphLiquidRenderer).GetMethod("OnDestroy", Private).Invoke(renderer, null);
        report.AppendLine($"[Pour Benchmark] GPU {SystemInfo.graphicsDeviceName} {size.x}x{size.y}: " +
                          $"field+cloud+surface+foam passes={simulatePasses:F3} ms (CPU submit {simulateCpu:F3} ms), " +
                          $"composite={compositePass:F3} ms, liquid covers {100.0 * covered / (size.x * size.y):F1}% of pixels, sync overhead={syncOnly:F3} ms");
    }

    static double Ms(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    static double Average(int samples, Action action)
    {
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < samples; i++) action();
        return Ms(start) / samples;
    }

    static double Batched(int samples, Action action, Action sync)
    {
        sync();
        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < samples; i++) action();
        sync();
        return Ms(start) / samples;
    }
}
