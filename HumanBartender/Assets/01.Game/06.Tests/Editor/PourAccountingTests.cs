using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PourAccountingTests
{
    readonly List<Object> owned = new();
    PourManager manager;
    BottleTiltController bottle;
    SphSimulation simulation;
    LiquidProfile profile;
    SphContainerBoundary boundary;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        var root = new GameObject("Inactive Pour Test");
        owned.Add(root);
        root.SetActive(false); // 실제 화면/사운드/Start를 실행하지 않고 물리와 수량 경로만 검사한다.
        manager = root.AddComponent<PourManager>();
        bottle = root.AddComponent<BottleTiltController>();
        bottle.ConfigurePourGeometry(Vector3.up, Vector3.up);
        var glass = new GameObject("Glass");
        owned.Add(glass);
        profile = ScriptableObject.CreateInstance<LiquidProfile>();
        owned.Add(profile);
        profile.physics.spacing = .1f;
        boundary = new SphContainerBoundary(glass.transform, Vector2.one, .05f);
        simulation = new SphSimulation(profile.physics, new Vector2(-3f, -3f), new Vector2(3f, 3f), boundary.Constrain);
        simulation.SynchronizeTransforms = false;
        Set("bottle", bottle);
        Set("liquidProfile", profile);
        Set("glassCenter", glass.transform);
        Set("glassInteriorHalfExtents", Vector2.one);
        Set("glassBoundary", boundary);
        Set("simulation", simulation);
        Set("volumeTracker", new SphVolumeTracker(32));
        Set("particleVolumeMl", 2f);
        Set("mlPerSecondAtFullFlow", 100f);
        Set("quantityAccumulateMultiplier", 1f);
        Set("unlimitedLiquid", true);
        Set("drivenByRunner", true);
        Set("isPlay", true);
        Set("pourStep", new GimmickStep(ECraftGimmick.Pour, "gin", 20f, ENewUnit.Ml, true));
        typeof(BottleTiltController).GetField("currentAngle", Private).SetValue(bottle, 90f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in owned) Object.DestroyImmediate(item);
        owned.Clear();
    }

    void Set(string name, object value) => typeof(PourManager).GetField(name, Private).SetValue(manager, value);
    T Get<T>(string name) => (T)typeof(PourManager).GetField(name, Private).GetValue(manager);
    void Call(string name, params object[] args) => typeof(PourManager).GetMethod(name, Private).Invoke(manager, args);

    SphParticle Particle(Vector2 position)
    {
        var go = new GameObject("Particle");
        owned.Add(go);
        var p = go.AddComponent<SphParticle>();
        p.Init(profile.physics);
        p.Activate(position);
        boundary.Constrain(p);
        simulation.Register(p);
        Get<List<SphParticle>>("live").Add(p);
        return p;
    }

    [Test]
    public void EmptyPoolDoesNotIncreaseQuantityWhileBottleIsTilted()
    {
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        Assert.That(simulation.ActiveParticles.Count, Is.Zero);
        Assert.That(manager.PouredInTargetUnit, Is.Zero);
    }

    [Test]
    public void DisplayAndRecipeQuantityUseReceivedParticlesWithoutApplyingFlowMultiplierAgain()
    {
        Particle(new Vector2(0f, -.95f));
        Particle(new Vector2(.3f, -.95f));
        Particle(new Vector2(0f, .5f));
        Set("quantityAccumulateMultiplier", .25f);
        Call("MeasureRetainedVolume");
        Assert.That(manager.PouredInTargetUnit, Is.EqualTo(4f));
        Assert.That(manager.CurrentValueText, Is.EqualTo("4.00 ml"));
        Assert.That(manager.ShowOkMark, Is.False);
    }

    [Test]
    public void FallingParticleCountsOnlyAfterTheExistingSimulationBringsItToTheFloor()
    {
        SphParticle p = Particle(new Vector2(0f, .4f));
        for (int i = 0; i < 120; i++)
        {
            simulation.Tick(1f / 120f);
            if (i % 2 != 0) continue;
            Call("MeasureRetainedVolume");
            if (p.pos.y > -.93f) Assert.That(manager.PouredInTargetUnit, Is.Zero);
        }
        Call("MeasureRetainedVolume");
        Assert.That(p.pos.y, Is.EqualTo(-.95f).Within(.001f));
        Assert.That(manager.PouredInTargetUnit, Is.EqualTo(2f));
    }

    [Test]
    public void FinishingStopsEmissionAndWaitsForAirborneLiquidBeforeCompletion()
    {
        SphParticle p = Particle(new Vector2(0f, .4f));
        manager.FinishPour();
        Assert.That(manager.CanEndNow, Is.False);
        Assert.That(manager.IsManualInputActive, Is.False, "정리 시간은 제조 타이머에서 제외되어야 한다.");
        Call("EmitFromNeck", 1f);
        Assert.That(simulation.ActiveParticles.Count, Is.EqualTo(1));
        Call("MeasureRetainedVolume");
        Set("finishSettleElapsed", .3f);
        Call("TryCompletePour");
        Assert.That(Get<bool>("isFinished"), Is.False);
        p.pos = new Vector2(0f, -.95f);
        boundary.Constrain(p);
        Call("MeasureRetainedVolume");
        Call("TryCompletePour");
        Assert.That(Get<bool>("isFinished"), Is.True);
        Assert.That(manager.PouredInTargetUnit, Is.EqualTo(2f));
    }

    [Test]
    public void UnreachableLiquidCannotBlockCompletionPastTheSettleTimeout()
    {
        Particle(new Vector2(0f, 2f));
        manager.FinishPour();
        Call("MeasureRetainedVolume");
        Set("finishSettleElapsed", 1.5f);
        Call("TryCompletePour");
        Assert.That(Get<bool>("isFinished"), Is.True);
        Assert.That(manager.PouredInTargetUnit, Is.Zero);
    }
}
