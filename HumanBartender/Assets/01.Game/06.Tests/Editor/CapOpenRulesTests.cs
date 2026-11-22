using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>병따기 판정이 웹 본편(gimmick-mechanics.js OPEN)과 같은 경계·순서로 움직이는지 검사한다.</summary>
public sealed class CapOpenRulesTests
{
    const float Step = .01f;

    static void Advance(CapOpenRules rules, float seconds)
    {
        // 웹처럼 한 번에 0.2초까지만 흐르므로 잘게 나눠 흘린다.
        for (float t = 0f; t < seconds - 1e-6f; t += Step) rules.Tick(Mathf.Min(Step, seconds - t));
    }

    /// <summary>게이지를 원하는 위치까지 올린다(시작 직후 0에서 위로 간다).</summary>
    static void RaisePowerTo(CapOpenRules rules, float position)
    {
        Advance(rules, position / rules.Tuning.powerSpeed);
    }

    static CapOpenRules StartedRules()
    {
        var rules = new CapOpenRules();
        Assert.That(rules.Press(), Is.True);
        return rules;
    }

    static CapOpenRules InRhythm(bool perfectPower)
    {
        var rules = StartedRules();
        // 0.70은 perfect 경계(±0.08)에 걸리므로, perfect가 아닌 쪽은 경계에서 확실히 떨어진 0.66을 쓴다.
        RaisePowerTo(rules, perfectPower ? .78f : .66f);
        Assert.That(rules.Press(), Is.True);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.PowerDone));
        Advance(rules, .46f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Rhythm));
        return rules;
    }

    [Test]
    public void FirstPressStartsThePowerGauge()
    {
        var rules = new CapOpenRules();
        rules.Tick(1f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Ready), "시작 전에는 시간이 흘러도 그대로다.");
        Assert.That(rules.Press(), Is.True);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Power));
        Assert.That(rules.Started, Is.True);
        Assert.That(rules.PowerPos, Is.Zero);
    }

    [Test]
    public void PowerGaugeBouncesBetweenZeroAndOne()
    {
        var rules = StartedRules();
        Advance(rules, 1f / 1.1f + .05f);
        Assert.That(rules.PowerDirection, Is.EqualTo(-1));
        Assert.That(rules.PowerPos, Is.LessThan(1f));
        Advance(rules, 1f / 1.1f);
        Assert.That(rules.PowerDirection, Is.EqualTo(1));
    }

    [TestCase(.63f, true, false)]
    [TestCase(.71f, true, true)]
    [TestCase(.78f, true, true)]
    [TestCase(.85f, true, true)]
    [TestCase(.92f, true, false)]
    [TestCase(.60f, false, false)]
    [TestCase(.95f, false, false)]
    public void PowerJudgementUsesTheWebGreenAndPerfectZones(float position, bool success, bool perfect)
    {
        var rules = StartedRules();
        RaisePowerTo(rules, position);
        rules.Press();
        Assert.That(rules.Phase == ECapOpenPhase.PowerDone, Is.EqualTo(success));
        if (success) Assert.That(rules.PowerPerfect, Is.EqualTo(perfect));
        else
        {
            Assert.That(rules.Failures, Is.EqualTo(1));
            Assert.That(rules.PowerPos, Is.Zero, "힘 조절 실패는 게이지를 처음으로 돌린다.");
        }
    }

    [Test]
    public void PowerDoneIgnoresPressesAndWaitsBeforeTheRing()
    {
        var rules = StartedRules();
        RaisePowerTo(rules, .78f);
        rules.Press();
        Assert.That(rules.Press(), Is.False);
        Advance(rules, .44f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.PowerDone));
        Advance(rules, .02f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Rhythm));
        Assert.That(rules.BeatTime, Is.LessThan(.02f));
    }

    [TestCase(.97f, true)]
    [TestCase(1.1f, true)]
    [TestCase(1.22f, true)]
    [TestCase(.9f, false)]
    public void RingJudgementUsesThePlusMinusWindow(float pressAt, bool opened)
    {
        var rules = InRhythm(false);
        Advance(rules, pressAt);
        rules.Press();
        Assert.That(rules.Completed, Is.EqualTo(opened));
        Assert.That(rules.Phase, Is.EqualTo(opened ? ECapOpenPhase.Pop : ECapOpenPhase.Rhythm));
        if (!opened) Assert.That(rules.BeatTime, Is.Zero, "타이밍 실패는 링만 처음부터 다시 조인다.");
    }

    [Test]
    public void RingFailsByItselfAfterTheTimeout()
    {
        var rules = InRhythm(false);
        Advance(rules, 1.2f);
        Assert.That(rules.Failures, Is.Zero);
        Advance(rules, .06f);
        Assert.That(rules.Failures, Is.EqualTo(1));
        Assert.That(rules.Fx.HasValue && !rules.Fx.Value.Success, Is.True);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Rhythm));
    }

    [Test]
    public void MissesAcrossPowerAndRingAddUpToABrokenBottle()
    {
        var rules = StartedRules();
        RaisePowerTo(rules, .4f);
        rules.Press();
        RaisePowerTo(rules, .4f);
        rules.Press();
        Assert.That(rules.RoundMisses, Is.EqualTo(2));
        RaisePowerTo(rules, .78f);
        rules.Press();
        Assert.That(rules.RoundMisses, Is.EqualTo(2), "힘 조절 성공은 실패 수를 지우지 않는다.");
        Advance(rules, .46f);
        rules.Press();
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Broken));
        Assert.That(rules.Failures, Is.EqualTo(3));
        Assert.That(rules.Press(), Is.False, "깨진 동안은 입력을 받지 않는다.");

        Advance(rules, 1.19f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Broken));
        Advance(rules, .02f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Power));
        Assert.That(rules.RoundMisses, Is.Zero);
        Assert.That(rules.PowerPos, Is.Zero);
        Assert.That(rules.Failures, Is.EqualTo(3), "전체 실패 수는 그대로 남아 판정에 넘어간다.");
    }

    [Test]
    public void GaugeDirectionCarriesOverABrokenBottle()
    {
        // 끝까지 올라갔다가 내려오는 중(0.8)에 힘 조절에 성공하면 방향이 아래인 채로 링 단계에 들어간다.
        var rules = StartedRules();
        Advance(rules, 1.2f / 1.1f);
        Assert.That(rules.PowerDirection, Is.EqualTo(-1));
        Assert.That(rules.Press(), Is.True);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.PowerDone));
        Advance(rules, .46f);
        for (int i = 0; i < 3; i++) Advance(rules, 1.3f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Broken), "링 타이밍을 세 번 놓치면 깨진다.");
        // 새 병이 준비되는 그 순간까지만 흘린다. 다음 틱부터는 0에서 위로 튕겨 방향이 바뀐다.
        Advance(rules, rules.Delay + .001f);
        Assert.That(rules.Phase, Is.EqualTo(ECapOpenPhase.Power));
        Assert.That(rules.PowerDirection, Is.EqualTo(-1), "웹은 깨진 뒤에도 게이지 방향을 되돌리지 않는다.");
    }

    [TestCase(true, 1.1f, true)]
    [TestCase(true, 1.15f, true)]
    [TestCase(true, 1.18f, false)]
    [TestCase(false, 1.1f, false)]
    public void PerfectNeedsPerfectPowerAndATightRing(bool perfectPower, float pressAt, bool perfect)
    {
        var rules = InRhythm(perfectPower);
        Advance(rules, pressAt);
        rules.Press();
        Assert.That(rules.Completed, Is.True);
        Assert.That(rules.Fx.Value.Perfect, Is.EqualTo(perfect));
    }

    [Test]
    public void NextUnlocksOnlyAfterTheCarbonationRises()
    {
        var rules = InRhythm(false);
        Advance(rules, 1.1f);
        rules.Press();
        Assert.That(rules.CanFinish, Is.False);
        for (float t = 0f; t < 2.04f; t += Step) rules.TickFx(Step);
        Assert.That(rules.CanFinish, Is.False);
        for (float t = 0f; t < .02f; t += Step) rules.TickFx(Step);
        Assert.That(rules.CanFinish, Is.True);
        Assert.That(rules.Press(), Is.False, "열린 뒤에는 누름을 받지 않는다.");
    }

    [Test]
    public void FxSerialRisesWithEveryJudgement()
    {
        var rules = StartedRules();
        RaisePowerTo(rules, .3f);
        rules.Press();
        int first = rules.Fx.Value.Serial;
        RaisePowerTo(rules, .3f);
        rules.Press();
        Assert.That(rules.Fx.Value.Serial, Is.EqualTo(first + 1));
    }

    [Test]
    public void CarbonationIsDeterministicAndStaysInsideTheBottle()
    {
        var first = new List<CapCarbonation.Particle>();
        var second = new List<CapCarbonation.Particle>();
        CapCarbonation.Sample(.8f, first);
        CapCarbonation.Sample(.8f, second);
        Assert.That(first, Is.Not.Empty);
        Assert.That(second.Count, Is.EqualTo(first.Count));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.That(second[i].X, Is.EqualTo(first[i].X));
            Assert.That(second[i].Y, Is.EqualTo(first[i].Y));
            Assert.That(first[i].Y, Is.InRange(33f, 109f));
            Assert.That(first[i].Alpha, Is.InRange(0f, .46f));
        }

        CapCarbonation.Sample(CapCarbonation.Duration, first);
        Assert.That(first, Is.Empty, "탄산은 한 번만 올라오고 끝난다.");
    }

    [Test]
    public void CarbonationHoldsEachFrameForAFifteenthOfASecond()
    {
        var a = new List<CapCarbonation.Particle>();
        var b = new List<CapCarbonation.Particle>();
        CapCarbonation.Sample(.70f, a);
        CapCarbonation.Sample(.72f, b);
        Assert.That(b.Count, Is.EqualTo(a.Count));
        for (int i = 0; i < a.Count; i++) Assert.That(b[i].Y, Is.EqualTo(a[i].Y));
    }

    [Test]
    public void CarbonationClipExcludesTheLabel()
    {
        Assert.That(CapCarbonation.InsideClip(0f, 60f), Is.True, "병목 안쪽");
        Assert.That(CapCarbonation.InsideClip(0f, 120f), Is.False, "라벨 위");
        Assert.That(CapCarbonation.InsideClip(15f, 30f), Is.False, "병목 바깥");
    }

    [Test]
    public void FailSoundIsSynthesizedWithTheWebLength()
    {
        var clip = CapOpenAudio.CreateFailClip(48000);
        try
        {
            Assert.That(clip.length, Is.EqualTo(.23f).Within(.001f));
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
            Assert.That(peak, Is.GreaterThan(0f));
        }
        finally { Object.DestroyImmediate(clip); }
    }
}
