using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class WindowRainMorphologyTests
{
    static WindowRainSimulation StillSimulation() => new WindowRainSimulation(new WindowRainSettings
    {
        spawnPerSecond = 0, gravity = 0, slideRadius = 100, evaporation = 0,
        formationTime = 0.4f, mergeTime = 0.38f
    });

    static List<WindowDrop> Drops(WindowRainSimulation sim) =>
        (List<WindowDrop>)typeof(WindowRainSimulation).GetField("drops", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sim);

    static WindowDrop Drop(Vector2 position, float radius, float age = 1f) => new WindowDrop
    {
        position = position, previous = position, radius = radius, age = age,
        pin = 1, phase = 4.3f, heading = Vector2.down
    };

    [Test]
    public void MergeConservesAreaAndRelaxesWithoutAdvancingWhenPaused()
    {
        var sim = StillSimulation();
        Drops(sim).Add(Drop(new Vector2(100, 100), 5));
        Drops(sim).Add(Drop(new Vector2(107, 100), 4));
        sim.Step(0.01f, 0, Vector2.zero);
        Assert.That(sim.Drops.Count, Is.EqualTo(1));
        var first = sim.Drops[0];
        Assert.That(first.radius * first.radius, Is.EqualTo(41).Within(0.001));
        Assert.That(first.mergeOffset.x, Is.EqualTo(7).Within(0.001));
        Assert.That(first.mergeFraction, Is.EqualTo(16f / 41).Within(0.001));
        Assert.That(first.mergeRemaining, Is.EqualTo(1));
        sim.Step(0, 0, Vector2.zero);
        Assert.That(sim.Drops[0].mergeRemaining, Is.EqualTo(first.mergeRemaining));
        sim.Step(0.1f, 0, Vector2.zero);
        Assert.That(sim.Drops[0].mergeRemaining, Is.InRange(0.5f, 0.9f));
        for (int i = 0; i < 5; i++) sim.Step(0.1f, 0, Vector2.zero);
        Assert.That(sim.Drops[0].mergeRemaining, Is.Zero);
        Assert.That(sim.Drops[0].radius * sim.Drops[0].radius, Is.EqualTo(41).Within(0.001));
    }

    [Test]
    public void FormationGrowsGraduallyAndMergeOffsetFollowsAspect()
    {
        var sim = StillSimulation();
        var drop = Drop(new Vector2(240, 135), 3, 0);
        drop.mergeOffset = new Vector2(8, 3);
        Drops(sim).Add(drop);
        float initial = sim.FormationScale(sim.Drops[0]);
        sim.Step(0, 0, Vector2.zero);
        Assert.That(sim.FormationScale(sim.Drops[0]), Is.EqualTo(initial));
        sim.Step(0.1f, 0, Vector2.zero);
        float growing = sim.FormationScale(sim.Drops[0]);
        Assert.That(growing, Is.GreaterThan(initial).And.LessThan(1));
        for (int i = 0; i < 5; i++) sim.Step(0.1f, 0, Vector2.zero);
        Assert.That(sim.FormationScale(sim.Drops[0]), Is.EqualTo(1));
        sim.Resize(4f / 3);
        Assert.That(sim.Drops[0].mergeOffset, Is.EqualTo(new Vector2(6, 3)));
        Assert.That(sim.Drops[0].radius, Is.EqualTo(3));
    }

    [Test]
    public void MediumDropPausesAndResumesWhileLargeStreamPassesContacts()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings
        {
            spawnPerSecond = 0, pinVariation = 0, meander = 0, trailChance = 0, evaporation = 0, slideRadius = 2.6f
        });
        Drops(sim).Add(Drop(new Vector2(100, 250), 3.8f));
        Drops(sim).Add(Drop(new Vector2(300, 250), 7f));
        bool stopped = false, resumed = false;
        for (int i = 0; i < 90; i++)
        {
            sim.Step(1f / 60f, 0, Vector2.zero);
            var medium = sim.Drops.First(d => d.radius < 5);
            stopped |= medium.adhesionStops > 0 && medium.velocity == Vector2.zero;
            resumed |= stopped && medium.sliding && medium.velocity.magnitude > 5;
            Assert.That(sim.Drops.First(d => d.radius > 5).adhesionStops, Is.Zero);
        }
        Assert.That(stopped, Is.True, "A medium drop should briefly pin to the glass.");
        Assert.That(resumed, Is.True, "Pinned drops should release and accelerate again.");
        var before = sim.Drops.First(d => d.radius < 5);
        sim.Step(0, 0, Vector2.zero);
        Assert.That(sim.Drops.First(d => d.radius < 5).adhesionPause, Is.EqualTo(before.adhesionPause));
    }

    [Test]
    public void CameraMovementReleasesFlowingDropsWithoutUnpinningSmallBeads()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings
        {
            spawnPerSecond = 0, pinVariation = 0, meander = 0, trailChance = 0, evaporation = 0
        });
        var flow = Drop(new Vector2(100, 250), 4.5f);
        flow.adhesionPause = 0.3f;
        flow.adhesionTravel = 30;
        Drops(sim).Add(flow);
        Drops(sim).Add(Drop(new Vector2(300, 250), 1));
        for (int i = 0; i < 30; i++)
        {
            sim.Step(1f / 60, 0, Vector2.zero, true);
            Assert.That(sim.Drops[0].adhesionPause, Is.Zero);
            Assert.That(sim.Drops[0].sliding, Is.True);
            Assert.That(sim.Drops[0].velocity.magnitude, Is.GreaterThan(0));
            Assert.That(sim.Drops[1].position, Is.EqualTo(new Vector2(300, 250)));
        }
        bool stoppedAgain = false;
        for (int i = 0; i < 90; i++)
        {
            sim.Step(1f / 60, 0, Vector2.zero, false);
            stoppedAgain |= sim.Drops[0].adhesionPause > 0 && sim.Drops[0].velocity == Vector2.zero;
        }
        Assert.That(stoppedAgain, Is.True, "Standing still should restore intermittent flow.");
    }

    [Test]
    public void StandingStillMixesPinnedAndContinuouslyFlowingDrops()
    {
        var sim = new WindowRainSimulation(new WindowRainSettings
        {
            spawnPerSecond = 0, pinVariation = 0, meander = 0, trailChance = 0, evaporation = 0
        });
        var sticky = Drop(new Vector2(100, 250), 4.5f);
        sticky.pin = 1.1f;
        var free = Drop(new Vector2(300, 250), 4.5f);
        free.pin = 0.9f;
        Drops(sim).Add(sticky); Drops(sim).Add(free);
        bool mixed = false;
        for (int i = 0; i < 120; i++)
        {
            sim.Step(1f / 60, 0, Vector2.zero);
            mixed |= sim.Drops[0].adhesionPause > 0 && sim.Drops[0].velocity == Vector2.zero && sim.Drops[1].velocity.magnitude > 3;
            Assert.That(sim.Drops[1].adhesionStops, Is.Zero);
        }
        Assert.That(mixed, Is.True, "A sticky drop can rest while another drop keeps running on the same stationary glass.");
    }

    [Test]
    public void CharacterMovementIsDetectedEvenWithFixedCamera()
    {
        var root = new GameObject("fixed camera movement test");
        var subject = new GameObject("walking character");
        root.AddComponent<Camera>().enabled = false;
        var layer = root.AddComponent<WindowRainLayer>();
        try
        {
            // EditMode에서는 MonoBehaviour.OnEnable 실행에 의존하지 않고 샘플러의 카메라를 설정한다.
            typeof(WindowRainLayer).GetField("targetCamera", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(layer, root.GetComponent<Camera>());
            typeof(WindowRainLayer).GetField("movementSource", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(layer, subject.transform);
            var sample = typeof(WindowRainLayer).GetMethod("SampleMotion", BindingFlags.NonPublic | BindingFlags.Instance);
            sample.Invoke(layer, new object[] { 1f / 60 });
            for (int i = 0; i < 60; i++)
            {
                subject.transform.position += Vector3.right * 0.02f;
                sample.Invoke(layer, new object[] { 1f / 60 });
            }
            Assert.That(layer.Motion.IsMoving, Is.False);
            Assert.That(layer.IsMoving, Is.True);
            for (int i = 0; i < 90; i++) sample.Invoke(layer, new object[] { 1f / 60 });
            Assert.That(layer.IsMoving, Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(subject); }
    }

    [Test]
    public void MovementDetectionIgnoresJitterAndSettlesAfterWalking()
    {
        var motion = new WindowRainMotion();
        motion.Sample(Vector3.zero, 2.7f, 1f / 60);
        for (int i = 0; i < 60; i++) motion.Sample(new Vector3(i % 2 * 0.001f, 0, 0), 2.7f, 1f / 60);
        Assert.That(motion.IsMoving, Is.False);
        for (int i = 0; i < 60; i++) motion.Sample(new Vector3(i * 0.01f, 0, 0), 2.7f, 1f / 60);
        Assert.That(motion.IsMoving, Is.True);
        for (int i = 0; i < 90; i++) motion.Sample(new Vector3(0.59f, 0, 0), 2.7f, 1f / 60);
        Assert.That(motion.IsMoving, Is.False);
        motion.Sample(new Vector3(80, 0, 0), 2.7f, 1f / 60);
        Assert.That(motion.IsMoving, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReducedLargeDropRateKeepsFewerDropsFlowing(bool moving)
    {
        var old = new WindowRainSimulation(new WindowRainSettings { largeDropChance = 0.08f, slideRadius = 2.6f }, 41);
        var tuned = new WindowRainSimulation(new WindowRainSettings(), 41);
        float oldFlow = 0, tunedFlow = 0;
        for (int i = 0; i < 1200; i++)
        {
            var wind = moving ? new Vector2(-120, 0) : Vector2.zero;
            old.Step(1f / 60, 1, wind, moving);
            tuned.Step(1f / 60, 1, wind, moving);
            if (i < 600) continue;
            oldFlow += old.Drops.Count(d => d.velocity.sqrMagnitude > 1);
            tunedFlow += tuned.Drops.Count(d => d.velocity.sqrMagnitude > 1);
        }
        TestContext.WriteLine($"moving={moving}, average flowing before={oldFlow / 600:F2}, after={tunedFlow / 600:F2}");
        Assert.That(tunedFlow, Is.LessThan(oldFlow * 0.85f));
        Assert.That(tuned.Drops.Count, Is.GreaterThan(80), "Keep beads on the glass instead of simply emptying the effect.");
    }

    [Test]
    public void HeightPassRendersConnectedMergeAndContinuousFadingTrail()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required");
        var root = new GameObject("rain morphology validation");
        root.AddComponent<Camera>().enabled = false;
        var layer = root.AddComponent<WindowRainLayer>();
        var sim = StillSimulation();
        typeof(WindowRainLayer).GetField("simulation", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(layer, sim);
        var render = typeof(WindowRainLayer).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            Drops(sim).Add(Drop(new Vector2(100, 100), 3, 0));
            render.Invoke(layer, new object[] { 0f });
            var seed = Read(layer.WaterTexture);
            Save(seed, "formation-start");
            sim.Step(0.1f, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            Save(Read(layer.WaterTexture), "formation-middle");
            for (int i = 0; i < 5; i++) sim.Step(0.1f, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            var formed = Read(layer.WaterTexture);
            Save(formed, "formation-full");
            Assert.That(formed.Count(c => c.r > 0.1f), Is.GreaterThan(seed.Count(c => c.r > 0.1f) + 10));
            sim.Step(0, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            Assert.That(Read(layer.WaterTexture), Is.EqualTo(formed), "Shape animation must freeze when simulation time stops.");
            sim.Clear();
            Drops(sim).Add(Drop(new Vector2(100, 100), 5));
            Drops(sim).Add(Drop(new Vector2(107, 100), 4));
            sim.Step(0.01f, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            Assert.That(layer.WaterTexture, Is.Not.Null, "Production GPU upload and shader must initialize.");
            var joined = Read(layer.WaterTexture);
            // D3D height-map orientation may be flipped; either row must contain a continuous neck.
            bool connected = Enumerable.Range(101, 6).All(x => Sample(joined, x, 100) > 0.1f) ||
                             Enumerable.Range(101, 6).All(x => Sample(joined, x, 169) > 0.1f);
            Assert.That(connected, Is.True, "Absorbed and retained lobes must have a wet connecting neck.");
            Save(joined, "merge-start");
            sim.Step(0.1f, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            Save(Read(layer.WaterTexture), "merge-middle");
            for (int i = 0; i < 5; i++) sim.Step(0.1f, 0, Vector2.zero);
            render.Invoke(layer, new object[] { 0f });
            var settled = Read(layer.WaterTexture);
            Save(settled, "merge-settled");
            Assert.That(joined.Where((v, i) => Mathf.Abs(v.r - settled[i].r) > 0.1f).Count(), Is.GreaterThan(10));

            sim.Clear();
            var moving = Drop(new Vector2(240, 120), 5);
            moving.sliding = true;
            // 가속도 방향을 일부러 옆으로 둬도 실제 하강 속도를 따라 실루엣이 세로로 늘어나야 한다.
            moving.heading = Vector2.right;
            moving.velocity = new Vector2(0, -50);
            moving.previous = new Vector2(240, 160);
            Drops(sim).Add(moving);
            render.Invoke(layer, new object[] { 0f });
            var flow = Read(layer.WaterTexture);
            Save(flow, "flowing");
            var occupied = Enumerable.Range(0, flow.Length).Where(i => flow[i].r > 0.1f).ToArray();
            int width = occupied.Max(i => i % 480) - occupied.Min(i => i % 480);
            int height = occupied.Max(i => i / 480) - occupied.Min(i => i / 480);
            Assert.That(height, Is.GreaterThan(width * 1.3f), "The rendered tail must follow actual vertical velocity, not the sideways acceleration heading.");
            var wet = Read(layer.TrailTexture);
            Assert.That(wet.Count(c => c.r > 0.1f), Is.GreaterThan(35), "Travel segment must leave a continuous line.");
            Save(wet, "trail", 1);
            CheckComposite(layer, render);
            sim.Clear();
            render.Invoke(layer, new object[] { 4.5f });
            var drying = Read(layer.TrailTexture);
            float ratio = drying.Sum(c => c.r) / wet.Sum(c => c.r);
            Assert.That(ratio, Is.InRange(0.35f, 0.39f), "Wet line should fade gradually with the configured lifetime.");

            foreach (string name in new[] { "Hidden/WindowRain/Drops", "Hidden/WindowRain/Composite" })
                Assert.That(ShaderUtil.GetShaderMessages(Shader.Find(name)).Where(m => m.severity.ToString() == "Error"), Is.Empty);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static float Sample(Color[] data, int x, int y) => data[y * 480 + x].r;

    static void CheckComposite(WindowRainLayer layer, MethodInfo render)
    {
        var background = new Texture2D(480, 270, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
        var target = new RenderTexture(480, 270, 0, RenderTextureFormat.ARGBFloat);
        var commands = new CommandBuffer();
        float strength = layer.Strength;
        try
        {
            var pixels = new Color[480 * 270];
            for (int y = 0; y < 270; y++)
                for (int x = 0; x < 480; x++)
                    pixels[y * 480 + x] = new Color((x / 7 % 2) * 0.35f + 0.05f, (y / 11 % 2) * 0.25f + 0.05f, 0.15f, 1);
            background.SetPixels(pixels); background.Apply();
            target.Create();
            var results = new List<Color[]>();
            foreach (float amount in new[] { 0f, 0.45f, 1f })
            {
                layer.Strength = amount;
                render.Invoke(layer, new object[] { 0f });
                layer.CompositeMaterial.SetTexture("_BlitTexture", background);
                layer.CompositeMaterial.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                commands.Clear(); commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.magenta);
                commands.DrawProcedural(Matrix4x4.identity, layer.CompositeMaterial, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
                results.Add(Read(target));
            }
            float full = 0, gentle = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                // A bypass must reproduce the source (either vertical orientation is accepted).
                int flipped = (269 - i / 480) * 480 + i % 480;
                Assert.That(Mathf.Min(ColorDistance(results[0][i], pixels[i]), ColorDistance(results[0][i], pixels[flipped])), Is.LessThan(0.015f));
                gentle += ColorDistance(results[1][i], results[0][i]);
                full += ColorDistance(results[2][i], results[0][i]);
            }
            Assert.That(full, Is.GreaterThan(0.5f), "The composite must render visible optics.");
            Assert.That(gentle / full, Is.InRange(0.43f, 0.47f), "Global strength must reduce all optical contributions.");
            string directory = Path.Combine(Path.GetTempPath(), "WindowRainOrganicValidation");
            var screenshot = new Texture2D(480, 270, TextureFormat.RGB24, false, true);
            screenshot.SetPixels(results[1]); screenshot.Apply();
            File.WriteAllBytes(Path.Combine(directory, "optics.png"), screenshot.EncodeToPNG());
            Object.DestroyImmediate(screenshot);
        }
        finally
        {
            layer.Strength = strength;
            commands.Release(); target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(background);
        }
    }

    static float ColorDistance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

    static Color[] Read(RenderTexture rt)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        try
        {
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            return texture.GetPixels();
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
    }

    static void Save(Color[] pixels, string name, float scale = 8)
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowRainOrganicValidation");
        Directory.CreateDirectory(directory);
        var texture = new Texture2D(480, 270, TextureFormat.RGB24, false, true);
        try
        {
            texture.SetPixels(pixels.Select(p => new Color(p.r / scale, p.r / scale, p.r / scale, 1)).ToArray());
            texture.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
    }
}
