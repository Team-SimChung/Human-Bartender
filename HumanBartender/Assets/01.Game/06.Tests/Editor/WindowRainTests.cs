using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public sealed class WindowRainTests
{
    static string Directory => Path.Combine(Path.GetTempPath(), "WindowRainValidation");

    // ── 시뮬레이션 ────────────────────────────────────────────────────────

    [Test]
    public void SmallDropsStickAndLargeDropsSlideStraightDownWithoutWind()
    {
        // 낙하 거리 회귀 검사는 미관용 기본값 튜닝과 독립된 고정 접촉 조건으로 실행한다.
        var sim = new WindowRainSimulation(new WindowRainSettings { spawnPerSecond = 0, pinVariation = 0, stickSlip = 0, slideRadius = 2.6f }, 1);
        sim.Resize(16f / 9f);
        AddDrop(sim, new Vector2(100, 200), 1f);
        AddDrop(sim, new Vector2(300, 200), 5f);
        for (int i = 0; i < 30; i++) sim.Step(1f / 60f, 0f, Vector2.zero);
        var small = sim.Drops.OrderBy(d => d.radius).First();
        var large = sim.Drops.OrderBy(d => d.radius).Last();
        Assert.That(small.position.y, Is.EqualTo(200f).Within(0.01f), "Surface tension keeps small drops in place.");
        Assert.That(large.position.y, Is.LessThan(185f), "Large drops must run down.");
        Assert.That(Mathf.Abs(large.position.x - 300f), Is.LessThan(Mathf.Abs(large.position.y - 200f) * 0.4f),
            "Without wind the flow stays mostly downward.");
    }

    [Test]
    public void WindMakesDiagonalFlowThatSettlesBackDownward()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings { spawnPerSecond = 0, pinVariation = 0, meander = 0, stickSlip = 0, slideRadius = 2.6f }, 2);
        sim.Resize(16f / 9f);
        AddDrop(sim, new Vector2(400, 260), 5f);
        Vector2 start = sim.Drops[0].position;
        for (int i = 0; i < 40; i++) sim.Step(1f / 60f, 0f, new Vector2(-250f, 0f));
        var windy = sim.Drops.OrderBy(d => d.radius).Last();
        Vector2 moved = windy.position - start;
        Assert.That(moved.x, Is.LessThan(-5f), "Relative wind must push the drop sideways.");
        Assert.That(moved.y, Is.LessThan(0f), "Gravity stays down while the wind blows.");
        Assert.That(windy.heading.x, Is.LessThan(-0.3f));
        for (int i = 0; i < 90; i++) sim.Step(1f / 60f, 0f, Vector2.zero);
        var calm = sim.Drops.OrderBy(d => d.radius).Last();
        Assert.That(calm.heading.y, Is.LessThan(-0.95f), "After the wind stops the flow settles downward.");
    }

    [Test]
    public void RainSpawnsVariedDropsThatMergeSlideAndLeaveTrails()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings(), 3);
        sim.Resize(16f / 9f);
        int maxSliding = 0;
        var radii = new List<float>();
        for (int i = 0; i < 60 * 12; i++)
        {
            sim.Step(1f / 60f, 1f, Vector2.zero);
            maxSliding = Mathf.Max(maxSliding, sim.Drops.Count(d => d.sliding));
        }
        foreach (var d in sim.Drops) radii.Add(d.radius);
        float mean = radii.Average();
        float deviation = Mathf.Sqrt(radii.Average(r => (r - mean) * (r - mean)));
        float xs = sim.Drops.Average(d => d.position.x);
        TestContext.WriteLine($"drops={sim.Drops.Count}, sliding={maxSliding}, radius={mean}±{deviation}, max={radii.Max()}, meanX={xs}");
        Assert.That(sim.Drops.Count, Is.InRange(80, 650));
        Assert.That(deviation, Is.GreaterThan(0.3f), "Drop sizes must vary.");
        Assert.That(radii.Max(), Is.GreaterThan(new WindowRainSettings().slideRadius), "Merging must grow drops past the slide size.");
        Assert.That(maxSliding, Is.GreaterThan(0), "Grown drops must start running.");
        Assert.That(xs, Is.InRange(sim.Size.x * 0.3f, sim.Size.x * 0.7f), "New drops must cover the whole glass.");
    }

    [Test]
    public void SlidingDropLeavesSmallDropsBehind()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings { spawnPerSecond = 0, pinVariation = 0, trailChance = 1f }, 4);
        sim.Resize(16f / 9f);
        AddDrop(sim, new Vector2(240, 250), 6f);
        for (int i = 0; i < 60; i++) sim.Step(1f / 60f, 0f, Vector2.zero);
        var head = sim.Drops.OrderBy(d => d.radius).Last();
        var behind = sim.Drops.Where(d => d.radius < 2.5f && d.position.y > head.position.y).ToList();
        Assert.That(behind.Count, Is.GreaterThan(2), "A running drop must leave a dotted trail.");
        Assert.That(head.radius, Is.LessThan(6f), "Trail drops take water from the running drop.");
    }

    [Test]
    public void StoppedRainStopsSpawningAndRemainingWaterDriesOrRunsOff()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings(), 5);
        sim.Resize(16f / 9f);
        sim.Prewarm(6f);
        int wet = sim.Drops.Count;
        for (int i = 0; i < 60 * 3; i++) sim.Step(1f / 60f, 0f, Vector2.zero);
        Assert.That(sim.Intensity, Is.LessThan(0.01f));
        int afterStop = sim.Drops.Count;
        for (int i = 0; i < 60 * 40; i++) sim.Step(1f / 60f, 0f, Vector2.zero);
        TestContext.WriteLine($"wet={wet}, afterStop={afterStop}, later={sim.Drops.Count}");
        Assert.That(afterStop, Is.GreaterThan(wet / 3), "Water must not vanish instantly when rain stops.");
        Assert.That(sim.Drops.Count, Is.LessThan(afterStop / 2), "Water must dry or run off over time.");
    }

    [Test]
    public void PausedStepDoesNotMoveDrops()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings(), 6);
        sim.Resize(16f / 9f);
        sim.Prewarm(3f);
        var before = sim.Drops.Select(d => d.position).ToArray();
        sim.Step(0f, 1f, new Vector2(-500, 0));
        Assert.That(sim.Drops.Select(d => d.position).ToArray(), Is.EqualTo(before));
    }

    [Test]
    public void AspectChangeKeepsDropSizeAndRelativePosition()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings { spawnPerSecond = 0 }, 7);
        sim.Resize(16f / 9f);
        AddDrop(sim, new Vector2(240, 135), 2f);
        sim.Resize(4f / 3f);
        Assert.That(sim.Size.y, Is.EqualTo(270f));
        Assert.That(sim.Drops[0].position.x / sim.Size.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(sim.Drops[0].radius, Is.EqualTo(2f));
    }

    // ── 카메라 이동 신호 ───────────────────────────────────────────────────

    [Test]
    public void CameraCutAndTeleportDoNotProduceWind()
    {
        var motion = new WindowRainMotion();
        float x = 0;
        for (int i = 0; i < 60; i++) motion.Sample(new Vector3(x += 0.03f, 0, -10), 2.7f, 1f / 60f);
        Assert.That(motion.Velocity.x, Is.GreaterThan(0.5f));
        motion.Sample(new Vector3(x + 40f, 0, -10), 2.7f, 1f / 60f);
        Assert.That(motion.Velocity, Is.EqualTo(Vector2.zero), "A cut must not be read as a huge speed.");
        Assert.That(motion.Acceleration, Is.EqualTo(Vector2.zero));
        Assert.That(motion.Cuts, Is.EqualTo(1));
        motion.Sample(new Vector3(x + 40.03f, 0, -10), 2.7f, 1f / 60f);
        Assert.That(motion.Velocity.magnitude, Is.LessThan(0.5f));
    }

    [Test]
    public void PixelSnappedMotionIsSmoothedAndStoppingSettles()
    {
        var motion = new WindowRainMotion();
        float trueX = 0, maxAcceleration = 0;
        for (int i = 0; i < 180; i++)
        {
            trueX += 0.6f / 60f;
            // 1/100 월드 격자에 스냅된 위치: 프레임마다 0 또는 2칸씩 튄다.
            float snapped = Mathf.Round(trueX * 50f) / 50f;
            motion.Sample(new Vector3(snapped, 0, -10), 2.7f, 1f / 60f);
            if (i > 60) maxAcceleration = Mathf.Max(maxAcceleration, motion.Acceleration.magnitude);
        }
        Assert.That(motion.Velocity.x, Is.EqualTo(0.6f / 2.7f).Within(0.05f));
        Assert.That(maxAcceleration, Is.LessThan(1.5f), "Snapping steps must not become inertia spikes.");
        for (int i = 0; i < 90; i++) motion.Sample(new Vector3(Mathf.Round(trueX * 50f) / 50f, 0, -10), 2.7f, 1f / 60f);
        Assert.That(motion.Velocity.magnitude, Is.LessThan(0.01f));
        var paused = motion.Velocity;
        motion.Sample(new Vector3(100, 100, -10), 2.7f, 0f);
        Assert.That(motion.Velocity, Is.EqualTo(paused), "Paused frames are ignored.");
    }

    // ── 실제 URP 2D 렌더링 ────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator SingleDropFallsDownOnScreenAcrossZoomAndAspectChanges()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return ExerciseScreenDirection();
        yield return new ExitPlayMode();
    }

    static IEnumerator ExerciseScreenDirection()
    {
        var root = new GameObject("rain direction test");
        var owned = new List<Object>();
        var main = CreateCamera(root, "wet", 1280, 720, true);
        var clean = CreateCamera(root, "dry", 1280, 720, true);
        BuildStreet(root, owned);
        var layer = main.gameObject.AddComponent<WindowRainLayer>();
        Set(layer, "ambientWind", Vector2.zero);
        Set(layer, "gustStrength", 0f);
        Set(layer, "trailStrength", 0f);
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        try
        {
            AddDrop(layer.Simulation, new Vector2(280, 210), 6f);
            yield return null; yield return null;
            var wet = Copy(main.targetTexture); var dry = Copy(clean.targetTexture);
            Vector2 start = ChangedCentre(wet, dry);
            Object.DestroyImmediate(wet); Object.DestroyImmediate(dry);
            Assert.That(start.x, Is.EqualTo(280f / 480f).Within(0.04f));
            Assert.That(start.y, Is.EqualTo(210f / 270f).Within(0.05f), "GPU height map and final camera color must use the same vertical orientation.");
            for (int i = 0; i < 30; i++) yield return null;
            wet = Copy(main.targetTexture); dry = Copy(clean.targetTexture);
            Vector2 end = ChangedCentre(wet, dry);
            Object.DestroyImmediate(wet); Object.DestroyImmediate(dry);
            Assert.That(end.y, Is.LessThan(start.y - 0.03f), "A falling drop must move down in the final rendered image.");

            // 줌과 종횡비가 바뀌어도 가상 격자와 방울 반지름은 유지한다.
            foreach (var cam in new[] { main, clean })
            {
                var old = cam.targetTexture;
                cam.targetTexture = new RenderTexture(1024, 768, 24) { filterMode = FilterMode.Point };
                old.Release(); Object.DestroyImmediate(old);
                cam.GetComponent<PixelPerfectCamera>().enabled = false;
                cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                cam.orthographicSize = 2.5f;
            }
            layer.Simulation.Clear();
            layer.Simulation.Resize(4f / 3f);
            AddDrop(layer.Simulation, new Vector2(180, 180), 2f);
            yield return null; yield return null;
            Assert.That(layer.WaterTexture.width, Is.EqualTo(360));
            Assert.That(layer.WaterTexture.height, Is.EqualTo(270));
            Assert.That(layer.Simulation.Drops[0].radius, Is.InRange(1.9f, 2f));
            Assert.That(layer.Motion.Velocity.magnitude, Is.LessThan(0.01f), "Lens-only zoom must not create wind.");
            wet = Copy(main.targetTexture); dry = Copy(clean.targetTexture);
            Vector2 resized = ChangedCentre(wet, dry);
            System.IO.Directory.CreateDirectory(Directory);
            Save(wet, "aspect-4x3-no-postprocess");
            Object.DestroyImmediate(wet); Object.DestroyImmediate(dry);
            Assert.That(resized.x, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(resized.y, Is.EqualTo(2f / 3f).Within(0.04f));
        }
        finally
        {
            Time.captureDeltaTime = previousCapture;
            DestroyCameras(root);
            Object.DestroyImmediate(root);
            foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
        }
    }

    [UnityTest]
    public IEnumerator ActualOutsideSceneHasWorkingRainAndKeepsOverlayUiSeparate()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return ExerciseOutsideScene();
        yield return new ExitPlayMode();
    }

    static IEnumerator ExerciseOutsideScene()
    {
        var loading = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/01.Game/01.Scenes/OutSide.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone) yield return null;
        yield return null;
        var layer = Object.FindFirstObjectByType<WindowRainLayer>();
        Assert.That(layer, Is.Not.Null, $"Loaded scene={SceneManager.GetActiveScene().name}, rain layers including inactive={Object.FindObjectsByType<WindowRainLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
        var cam = layer.GetComponent<Camera>();
        var previousTarget = cam.targetTexture;
        var target = new RenderTexture(1920, 1080, 24) { filterMode = FilterMode.Point };
        cam.targetTexture = target;
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        float previousScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            for (int i = 0; i < 120; i++) yield return null;
            Assert.That(layer.IsRaining, Is.True, "The saved scene must reference the existing emitting FX_Rain.");
            Assert.That(layer.HasVisibleWater, Is.True);
            Assert.That(layer.Simulation.Drops.Count(d => d.sliding), Is.GreaterThan(0));
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera != cam) Assert.That(WindowRainLayer.TryGet(camera, out _), Is.False);
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            Assert.That(overlays.Length, Is.GreaterThan(0), "Scene UI must remain in a separate overlay pass.");
            Time.timeScale = 0f;
            yield return null; yield return null;
            var wet = Copy(target);
            layer.enabled = false;
            yield return null; yield return null;
            var dry = Copy(target);
            float changed = Difference(wet, dry);
            TestContext.WriteLine($"actual outside: wet pixels={changed}, overlay canvases={overlays.Length}");
            Assert.That(changed, Is.InRange(0.005f, 0.6f));
            System.IO.Directory.CreateDirectory(Directory);
            Save(wet, "outside-wet"); Save(dry, "outside-dry");
            Object.DestroyImmediate(wet); Object.DestroyImmediate(dry);
            layer.enabled = true;
        }
        finally
        {
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCapture;
            cam.targetTexture = previousTarget;
            target.Release(); Object.DestroyImmediate(target);
        }
        // 씬 재진입 시 이전 등록과 GPU 자원이 남지 않고 새 카메라만 등록된다.
        yield return null; yield return null;
        var oldWater = layer.WaterTexture;
        Assert.That(oldWater, Is.Not.Null, "Reenabled scene camera must recreate its GPU targets before the unload check.");
        loading = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/01.Game/01.Scenes/OutSide.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        while (!loading.isDone) yield return null;
        yield return null;
        Assert.That(cam == null, Is.True);
        Assert.That(oldWater == null, Is.True);
        var reentered = Object.FindFirstObjectByType<WindowRainLayer>();
        Assert.That(reentered, Is.Not.Null);
        Assert.That(WindowRainLayer.TryGet(reentered.GetComponent<Camera>(), out var registered), Is.True);
        Assert.That(registered, Is.SameAs(reentered));
    }

    static void DestroyCameras(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>())
        {
            var target = cam.targetTexture; cam.targetTexture = null;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        }
    }

    static Vector2 ChangedCentre(Texture2D wet, Texture2D dry)
    {
        var a = wet.GetPixels32(); var b = dry.GetPixels32();
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int y = 0; y < wet.height; y++)
            for (int x = 0; x < wet.width; x++)
            {
                int i = y * wet.width + x;
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) <= 6) continue;
                sum += new Vector2((x + 0.5f) / wet.width, (y + 0.5f) / wet.height);
                count++;
            }
        Assert.That(count, Is.GreaterThan(0), "The registered camera must actually composite water.");
        return sum / count;
    }

    [UnityTest]
    public IEnumerator RendersOnlyOnTheRegisteredCameraAndReleasesResources()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return ExerciseRendering();
        yield return new ExitPlayMode();
    }

    static IEnumerator ExerciseRendering()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var root = new GameObject("window rain test");
        var shown = new List<Object>();
        var main = CreateCamera(root, "main", 1920, 1080, true);
        var capture = CreateCamera(root, "capture", 1920, 1080, true);
        BuildStreet(root, shown);
        var rain = new GameObject("FX_Rain");
        rain.transform.SetParent(root.transform, false);
        var system = rain.AddComponent<ParticleSystem>();
        // 방출 상태만 제공한다. 기본 파티클 머티리얼이 테스트 배경을 덮지 않게 한다.
        rain.GetComponent<ParticleSystemRenderer>().enabled = false;
        var module = system.main; module.loop = true;
        system.Play();
        var layer = main.gameObject.AddComponent<WindowRainLayer>();
        layer.SetRainSource(rain);
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        try
        {
            // 레이어 OnEnable 다음에 비가 연결돼도 첫 프레임에 젖은 상태로 시작한다.
            yield return null; yield return null;
            Assert.That(layer.Simulation.Drops.Count, Is.GreaterThan(200), "Startup order must not skip prewarm when rain starts before the first LateUpdate.");
            for (int i = 0; i < 60; i++) yield return null;
            Assert.That(layer.IsRaining, Is.True);
            Assert.That(layer.Simulation.Drops.Count, Is.GreaterThan(50));
            Assert.That(WindowRainLayer.TryGet(capture, out _), Is.False);
            var still = Copy(main.targetTexture);
            var clean = Copy(capture.targetTexture);
            float changed = Difference(still, clean);
            TestContext.WriteLine($"changed fraction with rain: {changed}");
            Assert.That(changed, Is.InRange(0.01f, 0.6f), "Drops must refract only part of the screen.");
            Save(still, "still"); Save(clean, "capture-camera"); SaveCrop(still, "still-crop", 560, 300, 480, 270);
            float fringe = ColorFringe(still, clean);
            TestContext.WriteLine($"color fringe fraction: {fringe}");
            Assert.That(fringe, Is.LessThan(0.03f), "Dispersion must stay at drop edges.");
            Object.DestroyImmediate(still); Object.DestroyImmediate(clean);

            // 오른쪽으로 걷다가 멈춘다.
            for (int i = 0; i < 90; i++) { main.transform.position += Vector3.right * 0.03f; capture.transform.position = main.transform.position; yield return null; }
            var moving = layer.Simulation.Drops.Where(d => d.sliding).ToList();
            float headingX = moving.Count > 0 ? moving.Average(d => d.heading.x) : 0f;
            TestContext.WriteLine($"moving: sliding={moving.Count}, headingX={headingX}, wind={layer.Motion.Velocity}");
            Assert.That(headingX, Is.LessThan(-0.15f), "Walking right through rain makes the water run diagonally back.");
            var walking = Copy(main.targetTexture); Save(walking, "walking"); SaveCrop(walking, "walking-crop", 560, 300, 480, 270); Object.DestroyImmediate(walking);
            // 카메라 컷: 멀리 순간 이동해도 바람이 튀지 않는다.
            main.transform.position += Vector3.right * 80f; capture.transform.position = main.transform.position;
            yield return null;
            Assert.That(layer.Motion.Velocity.magnitude, Is.LessThan(0.01f));
            for (int i = 0; i < 150; i++) yield return null;
            var settled = layer.Simulation.Drops.Where(d => d.sliding).ToList();
            float settledX = settled.Count > 0 ? settled.Average(d => d.heading.x) : 0f;
            TestContext.WriteLine($"settled: sliding={settled.Count}, headingX={settledX}");
            Assert.That(Mathf.Abs(settledX), Is.LessThan(0.25f), "Standing still lets the flow settle downward.");

            // 일시정지: 방울이 멈춘다.
            Time.captureDeltaTime = 0f; Time.timeScale = 0f;
            var frozen = layer.Simulation.Drops.Select(d => d.position).ToArray();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(layer.Simulation.Drops.Select(d => d.position).ToArray(), Is.EqualTo(frozen));
            Time.timeScale = 1f; Time.captureDeltaTime = 1f / 60f;

            // 성능: 시뮬레이션과 방울 그리기 CPU 시간, 렌더 타깃 메모리.
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 120; i++) layer.Simulation.Step(1f / 60f, 1f, Vector2.zero);
            double stepMs = timer.Elapsed.TotalMilliseconds / 120.0;
            var render = typeof(WindowRainLayer).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            timer.Restart();
            for (int i = 0; i < 120; i++) render.Invoke(layer, new object[] { 1f / 60f });
            double renderMs = timer.Elapsed.TotalMilliseconds / 120.0;
            long bytes = Profiler.GetRuntimeMemorySizeLong(layer.WaterTexture) + Profiler.GetRuntimeMemorySizeLong(layer.TrailTexture);
            var recorder = Recorder.Get("Window Rain");
            recorder.enabled = true;
            for (int i = 0; i < 30; i++) yield return null;
            string perf = $"drops={layer.Simulation.Drops.Count}, simulation={stepMs:0.000}ms, drop render CPU={renderMs:0.000}ms, " +
                $"rain RT={layer.WaterTexture.width}x{layer.WaterTexture.height} {bytes / 1024f:0.0}KiB, " +
                $"composite CPU={recorder.elapsedNanoseconds / 1e6:0.000}ms GPU={recorder.gpuElapsedNanoseconds / 1e6:0.000}ms (sampleCount={recorder.sampleBlockCount})";
            recorder.enabled = false;
            TestContext.WriteLine(perf);
            File.WriteAllText(Path.Combine(Directory, "performance.txt"), perf);
            Assert.That(stepMs, Is.LessThan(2.0), "The drop simulation must stay cheap on the CPU.");

            // 비활성화: GPU 자원을 놓고 합성하지 않는다.
            layer.enabled = false;
            yield return null; yield return null;
            Assert.That(layer.WaterTexture, Is.Null);
            var off = Copy(main.targetTexture); var offClean = Copy(capture.targetTexture);
            Assert.That(Difference(off, offClean), Is.LessThan(0.0005f), "Disabled layer must not draw.");
            Object.DestroyImmediate(off); Object.DestroyImmediate(offClean);
            layer.enabled = true;
            yield return null; yield return null;
            Assert.That(layer.WaterTexture, Is.Not.Null);

            // 비가 그침: 새 방울은 멈추고 남은 물은 흐르거나 마른다.
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return null;
            Assert.That(layer.IsRaining, Is.False);
            int remaining = layer.Simulation.Drops.Count;
            for (int i = 0; i < 60 * 25; i++) yield return null;
            TestContext.WriteLine($"after rain stopped: {remaining} -> {layer.Simulation.Drops.Count}");
            Assert.That(layer.Simulation.Drops.Count, Is.LessThan(remaining * 0.6f));
            var dry = Copy(main.targetTexture); Save(dry, "after-rain"); Object.DestroyImmediate(dry);
        }
        finally
        {
            Time.captureDeltaTime = previousCapture;
            Time.timeScale = 1f;
            foreach (var cam in root.GetComponentsInChildren<Camera>())
            {
                var target = cam.targetTexture; cam.targetTexture = null;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
            Object.DestroyImmediate(root);
            foreach (var o in shown) if (o != null) Object.DestroyImmediate(o);
        }
    }

    static Camera CreateCamera(GameObject root, string name, int width, int height, bool pixelPerfect)
    {
        var go = new GameObject(name, typeof(Camera));
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0, 0, -10);
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1.35f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.09f, 0.14f);
        cam.targetTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        if (pixelPerfect)
        {
            var ppc = go.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 100; ppc.refResolutionX = 480; ppc.refResolutionY = 270;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
        }
        return cam;
    }

    /// <summary>어두운 거리와 밝은 간판·창문. 굴절과 색 분산이 보이도록 대비가 큰 픽셀 아트 배경.</summary>
    static void BuildStreet(GameObject root, List<Object> owned)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        owned.Add(material);
        void Block(string name, Vector2 position, Vector2 size, Color color)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, color); texture.Apply();
            owned.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            owned.Add(sprite);
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material;
            renderer.sortingOrder = name == "window" || name == "sign" ? 1 : 0;
        }
        Block("street", new Vector2(0, -1.15f), new Vector2(200, 0.4f), new Color(0.18f, 0.18f, 0.22f));
        for (int i = -40; i < 40; i++)
        {
            float x = i * 0.9f;
            Block("building", new Vector2(x, 0.1f), new Vector2(0.8f, 2.1f), new Color(0.13f + (i & 1) * 0.03f, 0.12f, 0.19f));
            Block("window", new Vector2(x - 0.15f, 0.45f), new Vector2(0.14f, 0.2f), new Color(1f, 0.82f, 0.35f));
            Block("window", new Vector2(x + 0.18f, -0.1f), new Vector2(0.14f, 0.2f), new Color(0.95f, 0.75f, 0.3f));
            if ((i & 3) == 0) Block("sign", new Vector2(x, 0.95f), new Vector2(0.5f, 0.12f), (i & 4) == 0 ? new Color(1f, 0.15f, 0.55f) : new Color(0.2f, 0.95f, 1f));
        }
    }

    static void AddDrop(WindowRainSimulation sim, Vector2 position, float radius)
    {
        var drops = (List<WindowDrop>)typeof(WindowRainSimulation).GetField("drops", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sim);
        drops.Add(new WindowDrop { position = position, previous = position, radius = radius, age = 1f, heading = Vector2.down, pin = 1f, phase = drops.Count * 7.3f });
    }

    static float Difference(Texture2D a, Texture2D b)
    {
        var pa = a.GetPixels32(); var pb = b.GetPixels32(); int changed = 0;
        for (int i = 0; i < pa.Length; i++)
            if (Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b) > 6) changed++;
        return changed / (float)pa.Length;
    }

    /// <summary>원본에는 없던 강한 채널 분리(무지개 테두리)가 생긴 화면 비율.</summary>
    static float ColorFringe(Texture2D rain, Texture2D clean)
    {
        var pa = rain.GetPixels32(); var pb = clean.GetPixels32(); int fringe = 0;
        for (int i = 0; i < pa.Length; i++)
        {
            int spreadA = Mathf.Max(pa[i].r, Mathf.Max(pa[i].g, pa[i].b)) - Mathf.Min(pa[i].r, Mathf.Min(pa[i].g, pa[i].b));
            int spreadB = Mathf.Max(pb[i].r, Mathf.Max(pb[i].g, pb[i].b)) - Mathf.Min(pb[i].r, Mathf.Min(pb[i].g, pb[i].b));
            if (spreadA - spreadB > 90) fringe++;
        }
        return fringe / (float)pa.Length;
    }

    static Texture2D Copy(RenderTexture target)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var copy = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        return copy;
    }

    static void Save(Texture2D texture, string name) =>
        File.WriteAllBytes(Path.Combine(Directory, name + ".png"), texture.EncodeToPNG());

    static void SaveCrop(Texture2D texture, string name, int x, int y, int w, int h)
    {
        var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
        crop.SetPixels(texture.GetPixels(x, y, w, h)); crop.Apply();
        Save(crop, name);
        Object.DestroyImmediate(crop);
    }

    static void RequireGpu()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device, run without -nographics.");
    }

    static void Set(object owner, string name, object value) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(owner, value);

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
