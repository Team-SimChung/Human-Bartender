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

    PourerBreakaway PreparePourerEmission()
    {
        var effect = manager.gameObject.AddComponent<PourerBreakaway>();
        var attached = new GameObject("AttachedPourer", typeof(SpriteRenderer));
        attached.transform.SetParent(manager.transform, false);
        var detached = new GameObject("DetachedPourer", typeof(SpriteRenderer));
        detached.transform.SetParent(manager.transform, false);
        var texture = new Texture2D(1, 1);
        owned.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
        owned.Add(sprite);
        attached.GetComponent<SpriteRenderer>().sprite = sprite;
        typeof(PourerBreakaway).GetField("bottle", Private).SetValue(effect, bottle);
        typeof(PourerBreakaway).GetField("attachedPourer", Private).SetValue(effect, attached.GetComponent<SpriteRenderer>());
        typeof(PourerBreakaway).GetField("detachedPourer", Private).SetValue(effect, detached.GetComponent<SpriteRenderer>());
        Set("pourer", effect);
        effect.BeginPour("gin", Vector3.up * Get<Vector2>("bottleInteriorHalfExtents").y);
        // 맥주(배율 7)나 분리된 푸어러가 1초 동안 내보내는 양까지 풀이 모자라지 않게 둔다.
        for (int i = 0; i < 768; i++)
        {
            var go = new GameObject("PooledParticle");
            owned.Add(go);
            var particle = go.AddComponent<SphParticle>();
            particle.Init(profile.physics);
            Get<Stack<SphParticle>>("pool").Push(particle);
        }
        typeof(BottleTiltController).GetField("currentAngle", Private).SetValue(bottle, 160f);
        return effect;
    }

    [Test]
    public void ParticlesUseTheAdjustedPourerTipThenTheUnadjustedBottleMouth()
    {
        var effect = PreparePourerEmission();
        var attached = typeof(PourerBreakaway).GetField("attachedPourer", Private).GetValue(effect) as SpriteRenderer;
        var body = new GameObject("BottleBody", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        body.transform.SetParent(manager.transform, false);
        body.sprite = attached.sprite;
        Bounds bounds = body.sprite.bounds;
        body.transform.localPosition = Vector3.up - new Vector3(bounds.center.x, bounds.max.y);
        typeof(PourerBreakaway).GetField("ingredientBottle", Private).SetValue(effect, body);
        const float insetX = .002f;
        const float offsetY = -.003f;
        typeof(PourerBreakaway).GetField("outletInsetX", Private).SetValue(effect, insetX);
        typeof(PourerBreakaway).GetField("higtY", Private).SetValue(effect, offsetY);
        effect.BeginPour("gin", Vector3.up);
        Vector3 bodyPosition = body.transform.position;
        Vector3 bottleMouth = body.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y));
        Bounds tip = attached.sprite.bounds;
        Vector3 attachedMouth = attached.transform.TransformPoint(new Vector3(tip.center.x - insetX, tip.max.y + offsetY));
        var emitted = Get<List<SphParticle>>("live");
        Call("EmitRow", Vector2.right, 0f, 1);
        Call("EmitRow", Vector2.down, 0f, 1);
        Assert.That(emitted.Count, Is.EqualTo(2));
        Assert.That(emitted[0].pos.x, Is.EqualTo(attachedMouth.x).Within(.0001f));
        Assert.That(emitted[1].pos.y, Is.EqualTo(attachedMouth.y).Within(.0001f));
        typeof(BottleTiltController).GetField("dragging", Private).SetValue(bottle, true);
        Assert.That(effect.TryDetach(), Is.True);
        Assert.That(body.transform.position, Is.EqualTo(bodyPosition));
        Assert.That(Vector3.Distance(manager.EmissionMouthWorldPosition, bottleMouth), Is.LessThan(.0001f));
        Assert.That(Vector3.Distance(attachedMouth, bottleMouth), Is.GreaterThan(.001f));
        Call("EmitRow", Vector2.down, 0f, 1);
        Assert.That(emitted.Count, Is.EqualTo(3));
        Assert.That(emitted[2].pos.y, Is.EqualTo(bottleMouth.y).Within(.0001f),
            "실제 입자는 예전 푸어러 끝이 아닌 병 입구에서 출발해야 한다.");
    }

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

    [TestCase(false, ENewUnit.Ml)]
    [TestCase(true, ENewUnit.Ml)]
    [TestCase(false, ENewUnit.Oz)]
    [TestCase(true, ENewUnit.Oz)]
    public void GlassReadoutStopsAtItsLimitWhileActualQuantityAndTargetJudgementKeepRealVolume(bool mug, ENewUnit unit)
    {
        float limit = mug ? 420f : 280f;
        float mlPerUnit = unit == ENewUnit.Oz ? 30f : 1f;
        var session = new CraftSession("readout_limit");
        session.Actual.SetGlass(mug ? "weizen" : "long_drink");
        Set("craftContext", CraftContext.From(session));
        var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
        owned.Add(balance);
        balance.balanceData = new NewBalanceDataBase
        {
            Config = new NewBalanceConfig { UnitOzToMl = 30f }
        };
        Set("balanceData", balance);
        Set("pourStep", new GimmickStep(ECraftGimmick.Pour, "gin", (limit + 1f) / mlPerUnit, unit, true));
        int belowCount = Mathf.CeilToInt(limit / 2f) - 1;
        for (int i = 0; i < belowCount; i++) Particle(new Vector2((i % 16 - 8) * .1f, -.95f));
        Call("MeasureRetainedVolume");
        string suffix = unit == ENewUnit.Oz ? " oz" : " ml";
        Assert.That(manager.CurrentValueText, Is.EqualTo((belowCount * 2f / mlPerUnit)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + suffix));
        Assert.That(manager.ShowOkMark, Is.False);
        Particle(new Vector2(0f, -.95f));
        Particle(new Vector2(.1f, -.95f));
        Call("MeasureRetainedVolume");
        Assert.That(manager.CurrentValueText, Is.EqualTo((limit / mlPerUnit)
            .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + suffix));
        Assert.That(manager.ProgressText, Does.StartWith(limit.ToString("0.00") + " /"));
        Assert.That(manager.PouredInTargetUnit, Is.EqualTo((belowCount + 2) * 2f / mlPerUnit).Within(.0001f),
            "최종 결과에 쓰는 실제 수량은 표시 상한으로 자르지 않는다.");
        Assert.That(manager.ShowOkMark, Is.True, "표시 상한 위에 있는 목표도 실제량으로 판정한다.");
    }

    [Test]
    public void DetachedPourerIncreasesActualEmissionWithoutMultiplyingReceivedParticleVolume()
    {
        var effect = PreparePourerEmission();
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int normalCount = simulation.ActiveParticles.Count;
        typeof(BottleTiltController).GetField("dragging", Private).SetValue(bottle, true);
        Assert.That(effect.TryDetach(), Is.True);
        Set("emitAccumulator", 0f);
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int detachedCount = simulation.ActiveParticles.Count - normalCount;
        Assert.That(normalCount, Is.EqualTo(50).Within(2));
        Assert.That(detachedCount, Is.EqualTo(normalCount * effect.FlowMultiplier).Within(Mathf.Max(4, Lanes(effect.FlowMultiplier))));
        Assert.That(manager.PouredInTargetUnit, Is.Zero, "방출된 공중의 액체는 투입량에 포함하지 않는다.");
        Particle(new Vector2(0f, -.95f));
        Call("MeasureRetainedVolume");
        Assert.That(manager.PouredInTargetUnit, Is.EqualTo(2f), "분리 배율을 입자당 용량에 다시 곱하지 않는다.");
        manager.FinishPour();
        int finishedCount = simulation.ActiveParticles.Count;
        Call("EmitFromNeck", 1f);
        Assert.That(simulation.ActiveParticles.Count, Is.EqualTo(finishedCount));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void IncreasedFlowKeepsStreamParticleDensityInsteadOfCompressingRows(bool fromRunner)
    {
        Set("drivenByRunner", fromRunner);
        var effect = PreparePourerEmission();
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int beforeCount = simulation.ActiveParticles.Count;
        float beforeSpeed = MeanEmissionSpeed(0, beforeCount);
        typeof(BottleTiltController).GetField("dragging", Private).SetValue(bottle, true);
        effect.TryDetach();
        Set("emitAccumulator", 0f);
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int afterCount = simulation.ActiveParticles.Count - beforeCount;
        float afterSpeed = MeanEmissionSpeed(beforeCount, afterCount);
        Assert.That(afterCount, Is.GreaterThan(beforeCount * 1.9f), "실제 유량 증가는 유지한다.");
        Assert.That(afterSpeed, Is.LessThanOrEqualTo(Get<float>("maxExitSpeed") + .05f),
            "분리 후 유량이 커도 분사 속도는 상한을 넘지 않고 줄기를 굵게 해서 낸다.");
        float beforeDensity = beforeCount / beforeSpeed / Lanes(1f);
        float afterDensity = afterCount / afterSpeed / Lanes(effect.FlowMultiplier);
        Assert.That(afterDensity / beforeDensity, Is.EqualTo(1f).Within(.1f),
            "유량만 증가하면 같은 길이에 입자가 압축돼 물줄기가 갈라진다. 분리 전후의 줄 하나당 입자 밀도를 유지해야 한다.");
    }

    int Lanes(float flowMultiplier) =>
        (int)typeof(PourManager).GetMethod("EmissionLanes", Private).Invoke(manager, new object[] { flowMultiplier, 1f });

    float MeanEmissionSpeed(int start, int count)
    {
        float sum = 0f;
        // 큰 유량은 병목 방향 대신 아래쪽으로 더 빠르게 나가므로, 줄 간격을 정하는 진행 방향 속력으로 잰다.
        for (int i = start; i < start + count; i++)
            sum += simulation.ActiveParticles[i].velocity.magnitude;
        return sum / count;
    }

    [TestCase(true, "beer", "gin")]
    [TestCase(false, "beer", "gin")]
    [TestCase(true, "soda_water", "beer")]
    [TestCase(true, "gin", "beer")]
    public void FlowOverrideChangesActualEmissionOnlyForTheActualIngredient(bool fromRunner, string ingredientId, string visualId)
    {
        Set("drivenByRunner", fromRunner);
        PreparePourerEmission();
        var (baseMultiplier, baseLanes) = Flow("gin");
        var (multiplier, lanes) = Flow(ingredientId);
        Assert.That(baseMultiplier, Is.EqualTo(1f), "표에 없는 재료는 기존 출수량을 쓴다.");
        if (ingredientId != "gin")
            Assert.That(multiplier, Is.GreaterThan(1f), "푸어러 없는 병 재료는 표의 배율로 빠르게 나온다.");
        Set("pourIngredientId", "gin");
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int baselineCount = simulation.ActiveParticles.Count;
        float baselineSpeed = MeanEmissionSpeed(0, baselineCount);
        Set("pourIngredientId", ingredientId);
        Set("useTestBottleId", true);
        Set("testBottleId", visualId);
        Set("emitAccumulator", 0f);
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int changedCount = simulation.ActiveParticles.Count - baselineCount;
        float changedSpeed = MeanEmissionSpeed(baselineCount, changedCount);
        // 입자는 한 줄(lanes개)씩 나가므로 1초 동안의 개수는 줄 하나 단위로 끊긴다.
        Assert.That(changedCount, Is.EqualTo(baselineCount * multiplier).Within(Mathf.Max(4, lanes)),
            "실제 재료에 맞춰 출수량을 조절하고 병 그림 테스트는 판정에 영향을 주지 않는다.");
        Assert.That(changedCount / changedSpeed / lanes / (baselineCount / baselineSpeed / baseLanes), Is.EqualTo(1f).Within(.1f),
            "출수량을 높여도 줄 하나의 입자 간격을 유지해 물줄기를 과밀하게 만들지 않는다.");
        Assert.That(changedSpeed, Is.LessThanOrEqualTo(Get<float>("maxExitSpeed") + .05f),
            "큰 유량은 분사 속도 대신 줄기 굵기로 낸다.");
        Assert.That(Get<float>("particleVolumeMl"), Is.EqualTo(2f));
    }

    [Test]
    public void LargerGlassSlowsExitToKeepTheSameRowSpacing()
    {
        PreparePourerEmission();
        Set("pourIngredientId", "gin");
        Set("glassCapacityMl", 300f);
        Set("visualCapacityMl", 300f);
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int baseCount = simulation.ActiveParticles.Count;
        float baseSpeed = MeanEmissionSpeed(0, baseCount);
        // 바이젠잔처럼 용량이 1.5배면 입자당 ml도 1.5배다. 같은 ml/s에서 입자는 덜 나오므로 속도도 그만큼 낮춘다.
        Set("visualCapacityMl", 450f);
        Set("particleVolumeMl", 3f);
        Set("emitAccumulator", 0f);
        for (int i = 0; i < 120; i++) Call("EmitFromNeck", 1f / 120f);
        int mugCount = simulation.ActiveParticles.Count - baseCount;
        float mugSpeed = MeanEmissionSpeed(baseCount, mugCount);
        Assert.That(mugCount, Is.EqualTo(baseCount / 1.5f).Within(3));
        Assert.That(mugCount / mugSpeed / (baseCount / baseSpeed), Is.EqualTo(1f).Within(.1f),
            "큰 잔에서도 물줄기의 줄 간격이 기본 잔과 같아야 한다.");
    }

    (float multiplier, int lanes) Flow(string ingredientId)
    {
        Set("pourIngredientId", ingredientId);
        float multiplier = (float)typeof(PourManager).GetMethod("IngredientFlowMultiplier", Private).Invoke(manager, null);
        return (multiplier, Lanes(multiplier));
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
