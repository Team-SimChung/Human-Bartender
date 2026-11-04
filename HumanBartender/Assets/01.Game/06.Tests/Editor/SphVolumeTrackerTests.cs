using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SphVolumeTrackerTests
{
    readonly List<GameObject> owned = new();
    readonly List<SphParticle> particles = new();
    Transform glass;
    SphContainerBoundary boundary;
    SphVolumeTracker tracker;
    const float Spacing = 0.1f;
    static readonly FieldInfo Neighbours = typeof(SphParticle).GetField("neighbours", BindingFlags.Instance | BindingFlags.NonPublic);

    [SetUp]
    public void SetUp()
    {
        glass = NewObject("Glass").transform;
        boundary = new SphContainerBoundary(glass, Vector2.one, Spacing * .5f);
        tracker = new SphVolumeTracker(64);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in owned) Object.DestroyImmediate(go);
        owned.Clear();
        particles.Clear();
    }

    GameObject NewObject(string name)
    {
        var go = new GameObject(name);
        owned.Add(go);
        return go;
    }

    SphParticle Particle(Vector2 position, Vector2 velocity = default)
    {
        var p = NewObject("Particle").AddComponent<SphParticle>();
        p.Init(new SphConfig { spacing = Spacing });
        p.Activate(position);
        boundary.Constrain(p);
        p.velocity = velocity;
        particles.Add(p);
        return p;
    }

    static void Link(SphParticle a, SphParticle b)
    {
        Type type = typeof(SphParticle).Assembly.GetType("SphNeighbour");
        ((IList)Neighbours.GetValue(a)).Add(Activator.CreateInstance(type, b, .5f, Vector2.up));
        ((IList)Neighbours.GetValue(b)).Add(Activator.CreateInstance(type, a, .5f, Vector2.down));
    }

    void Measure() => tracker.Measure(particles, boundary, Spacing);

    [Test]
    public void EmptyGlassAndAirborneParticlesHaveNoVolume()
    {
        Measure();
        Assert.That(tracker.ReceivedCount, Is.Zero);
        Particle(new Vector2(0f, 1.5f));
        Particle(new Vector2(0f, .4f), Vector2.down * 2f);
        Particle(new Vector2(.8f, -.4f)); // 잔 안에 있지만 바닥/액체에 아직 닿지 않았다.
        Measure();
        Assert.That(tracker.ReceivedCount, Is.Zero);
        Assert.That(tracker.PendingCount, Is.EqualTo(3));
    }

    [Test]
    public void FloorAndSlowSupportedStackCountButFastIncomingStreamWaits()
    {
        SphParticle previous = Particle(new Vector2(0f, -.95f));
        for (int i = 1; i < 7; i++)
        {
            SphParticle p = Particle(new Vector2(0f, -.95f + i * Spacing));
            Link(previous, p);
            previous = p;
        }
        SphParticle stream = Particle(new Vector2(0f, -.25f), Vector2.down * 2f);
        Link(previous, stream);
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(7));
        Assert.That(tracker.PendingCount, Is.EqualTo(1));
        stream.velocity = Vector2.zero;
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(8));
        Assert.That(tracker.PendingCount, Is.Zero);
    }

    [Test]
    public void OverflowAndSideSpillsLeaveVolumeAndReentryDoesNotDoubleCount()
    {
        SphParticle p = Particle(new Vector2(0f, -.95f));
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(1));
        p.pos = new Vector2(0f, 1.1f);
        boundary.Constrain(p);
        Measure();
        Assert.That(tracker.ReceivedCount, Is.Zero);
        p.previousPos = p.pos;
        p.pos = new Vector2(0f, -.95f);
        boundary.Constrain(p);
        Measure();
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(1));
        p.pos = new Vector2(1.2f, 0f);
        Measure();
        Assert.That(tracker.ReceivedCount, Is.Zero);
        Assert.That(tracker.PendingCount, Is.Zero);
    }

    [Test]
    public void FloatingClusterAndDistantStaleNeighbourAreNotAccepted()
    {
        SphParticle floor = Particle(new Vector2(0f, -.95f));
        SphParticle floating = Particle(new Vector2(0f, .3f));
        SphParticle other = Particle(new Vector2(0f, .4f));
        Link(floor, floating); // SPH 보정 전의 이웃이어도 최종 거리를 다시 확인한다.
        Link(floating, other);
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(1));
    }

    [Test]
    public void PoolReuseClearsArrivalAndSmallInternalSplashesRetainVolume()
    {
        SphParticle p = Particle(new Vector2(0f, -.95f));
        Measure();
        p.pos = new Vector2(0f, -.6f);
        p.velocity = Vector2.up;
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(1));
        p.Deactivate();
        p.Activate(new Vector2(0f, .5f));
        boundary.Constrain(p);
        Measure();
        Assert.That(tracker.ReceivedCount, Is.Zero);
    }

    [Test]
    public void FloorDetectionUsesContainerTransformAndWorldParticleRadius()
    {
        glass.position = new Vector3(40f, -12f, 0f);
        glass.rotation = Quaternion.Euler(0f, 0f, 25f);
        glass.localScale = new Vector3(2f, 3f, 1f);
        boundary = new SphContainerBoundary(glass, Vector2.one, Spacing * .5f);
        Particle(glass.TransformPoint(new Vector3(0f, -.99f, 0f)));
        Particle(glass.TransformPoint(Vector3.zero));
        Measure();
        Assert.That(tracker.ReceivedCount, Is.EqualTo(1));
    }
}
