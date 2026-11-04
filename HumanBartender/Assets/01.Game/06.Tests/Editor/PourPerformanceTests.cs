using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>CPU 경로의 재현 가능한 비용 측정. GPU/전체 Play 씬의 FPS를 대신하지 않는다.</summary>
public sealed class PourPerformanceTests
{
    [TestCase(0)]
    [TestCase(256)]
    [TestCase(1024)]
    [TestCase(2000)]
    public void MeasureVolumeAgainstExistingSimulationAndCheckSteadyStateAllocations(int particleCount)
    {
        var root = new GameObject("Pour CPU Benchmark");
        try
        {
            root.transform.localScale = new Vector3(2f, 2f, 1f);
            var config = new SphConfig { spacing = .065f };
            var boundary = new SphContainerBoundary(root.transform, new Vector2(.65f, 1.27f), config.spacing * .5f);
            var simulation = new SphSimulation(config, new Vector2(-4f, -4f), new Vector2(4f, 5f), boundary.Constrain);
            simulation.SynchronizeTransforms = false;
            var tracker = new SphVolumeTracker(2000);
            for (int i = 0; i < particleCount; i++)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(root.transform, false);
                var p = go.AddComponent<SphParticle>();
                p.Init(config);
                p.Activate(new Vector2(-1.2675f + i % 40 * config.spacing, -2.5075f + i / 40 * config.spacing));
                boundary.Constrain(p);
                simulation.Register(p);
            }
            simulation.CalibrateRestDensity();
            for (int i = 0; i < 30; i++) simulation.Tick(1f / 120f);
            for (int i = 0; i < 10; i++) tracker.Measure(simulation.ActiveParticles, boundary, config.spacing);

            double physicsMs = Measure(60, () => simulation.Tick(1f / 120f), out _);
            double volumeMs = Measure(240, () => tracker.Measure(simulation.ActiveParticles, boundary, config.spacing), out long volumeBytes);
            Debug.Log($"[Pour CPU Benchmark] particles={particleCount}, SPH tick mean={physicsMs:F4} ms (120 Hz), volume mean={volumeMs:F4} ms (60 Hz), volume allocations={volumeBytes} bytes/240 calls, received={tracker.ReceivedCount}");
            Assert.That(tracker.ReceivedCount, particleCount == 0 ? Is.EqualTo(0) : Is.GreaterThan(0));
            Assert.That(volumeBytes, Is.Zero, "정상 수량 측정 중에는 관리 힙을 할당하지 않아야 한다.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void BottlePickupAndDragCoreHasNoRepeatedAllocations()
    {
        var root = new GameObject("Bottle CPU Benchmark");
        try
        {
            var visual = new GameObject("Bottle Visual");
            visual.transform.SetParent(root.transform, false);
            var bottle = visual.AddComponent<BottleTiltController>();
            bottle.ConfigurePourGeometry(Vector3.up, root.transform.TransformPoint(Vector3.up));
            bottle.BeginDrag(new Vector3(-1f, -1f, 0f));
            bottle.DragTo(new Vector3(-1f, 1f, 0f));
            double dragMs = Measure(20000, () => bottle.DragTo(new Vector3(-1f, 1f, 0f)), out long bytes);
            Debug.Log($"[Pour CPU Benchmark] bottle DragTo mean={dragMs:F6} ms, allocations={bytes} bytes/20000 calls");
            Assert.That(bytes, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static double Measure(int samples, Action action, out long allocatedBytes)
    {
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < samples; i++) action();
        watch.Stop();
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return watch.Elapsed.TotalMilliseconds / samples;
    }
}
