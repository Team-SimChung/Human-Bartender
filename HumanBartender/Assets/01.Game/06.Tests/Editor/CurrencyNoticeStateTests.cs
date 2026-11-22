using NUnit.Framework;

public sealed class CurrencyNoticeStateTests
{
    [Test]
    public void InitialBalanceHasNoNotice()
    {
        var state = new CurrencyNoticeState();

        Assert.That(state.Balance, Is.Zero);
        Assert.That(state.Displayed, Is.Zero);
        Assert.That(state.Delta, Is.Zero);
        Assert.That(state.Active, Is.False);
        Assert.That(state.Opacity, Is.Zero);
    }

    [TestCase(100, 180, 170d)]
    [TestCase(200, 120, 130d)]
    [TestCase(-20, -100, -90d)]
    [TestCase(-50, 30, 20d)]
    public void CountsIncomeAndExpensesWithTheWebEaseOut(int initial, int balance, double halfway)
    {
        var state = new CurrencyNoticeState();
        state.Reset(initial);
        state.Change(balance);

        Assert.That(state.Balance, Is.EqualTo(balance), "the target balance is committed immediately");
        Assert.That(state.Displayed, Is.EqualTo(initial));
        Assert.That(state.Delta, Is.EqualTo((long)balance - initial));
        Assert.That(state.Active, Is.True);
        Assert.That(state.Opacity, Is.EqualTo(1));

        Advance(state, 4);
        Assert.That(state.Displayed, Is.EqualTo(halfway).Within(.000001));

        Advance(state, 4);
        Assert.That(state.Displayed, Is.EqualTo(balance));
        Assert.That(state.Active, Is.True, "the notice remains after the number finishes counting");
    }

    [TestCase(100, 110, 140)]
    [TestCase(100, 90, 60)]
    public void SameDirectionChangesCombineAndContinueFromTheDisplayedFraction(int initial, int first, int second)
    {
        var state = new CurrencyNoticeState();
        state.Reset(initial);
        state.Change(first);
        state.Advance(.1f);
        double displayed = state.Displayed;

        state.Change(second);

        Assert.That(state.Displayed, Is.EqualTo(displayed));
        Assert.That(state.Delta, Is.EqualTo((long)second - initial));
        Assert.That(state.Elapsed, Is.Zero);
        Advance(state, 8);
        Assert.That(state.Displayed, Is.EqualTo(second));
    }

    [TestCase(100, 200, 180)]
    [TestCase(200, 100, 120)]
    public void DirectionReversalStartsAtThePreviousTarget(int initial, int first, int second)
    {
        var state = new CurrencyNoticeState();
        state.Reset(initial);
        state.Change(first);
        state.Advance(.1f);

        state.Change(second);

        Assert.That(state.Displayed, Is.EqualTo(first), "a reversed change must immediately count in its own direction");
        Assert.That(state.Delta, Is.EqualTo((long)second - first));
        state.Advance(.1f);
        Assert.That(state.Displayed, Is.InRange((double)System.Math.Min(first, second), System.Math.Max(first, second)));
        Assert.That(state.Displayed, Is.Not.EqualTo(first));
        Advance(state, 7);
        Assert.That(state.Displayed, Is.EqualTo(second));
    }

    [Test]
    public void UnchangedBalanceDoesNotRestartTheNotice()
    {
        var state = new CurrencyNoticeState();
        state.Reset(100);
        state.Change(180);
        Advance(state, 3);
        double elapsed = state.Elapsed;
        double displayed = state.Displayed;

        state.Change(180);

        Assert.That(state.Elapsed, Is.EqualTo(elapsed));
        Assert.That(state.Displayed, Is.EqualTo(displayed));
        Assert.That(state.Delta, Is.EqualTo(80));
    }

    [Test]
    public void NoticeHoldsThenFadesForTheLastHalfSecond()
    {
        var state = new CurrencyNoticeState();
        state.Change(80);
        Advance(state, 21);

        Assert.That(state.Opacity, Is.EqualTo(1).Within(.000001));
        state.Advance(.1f);
        Assert.That(state.Opacity, Is.EqualTo(.8f).Within(.000001));
        Assert.That(state.Active, Is.True);
        Advance(state, 4);
        Assert.That(state.Active, Is.False);
        Assert.That(state.Opacity, Is.Zero);
        Assert.That(state.Delta, Is.EqualTo(80), "expiry hides the notice without discarding its change history");

        state.Change(100);
        Assert.That(state.Delta, Is.EqualTo(20), "expired changes must not aggregate into a new notice");
        Assert.That(state.Displayed, Is.EqualTo(80));
        Assert.That(state.Opacity, Is.EqualTo(1));
    }

    [Test]
    public void AdvanceClampsEachFrameToOneTenthSecondAndRejectsNegativeTime()
    {
        var state = new CurrencyNoticeState();
        state.Change(80);
        state.Advance(10f);

        Assert.That(state.Elapsed, Is.EqualTo(.1));
        Assert.That(state.Displayed, Is.GreaterThan(0).And.LessThan(80));
        double displayed = state.Displayed;
        state.Advance(-1f);
        Assert.That(state.Elapsed, Is.EqualTo(.1));
        Assert.That(state.Displayed, Is.EqualTo(displayed));
    }

    [Test]
    public void ResetImmediatelyReplacesTheBalanceAndCancelsTheNotice()
    {
        var state = new CurrencyNoticeState();
        state.Reset(200);
        state.Change(100);
        state.Advance(.1f);

        state.Reset(-40);
        Advance(state, 10);

        Assert.That(state.Balance, Is.EqualTo(-40));
        Assert.That(state.Displayed, Is.EqualTo(-40));
        Assert.That(state.Delta, Is.Zero);
        Assert.That(state.Active, Is.False);
        Assert.That(state.Opacity, Is.Zero);
    }

    [Test]
    public void FullIntegerRangeChangesDoNotOverflowDeltaOrInterpolation()
    {
        var state = new CurrencyNoticeState();
        state.Reset(int.MinValue);
        state.Change(0);
        state.Advance(.1f);
        state.Change(int.MaxValue);

        Assert.That(state.Delta, Is.EqualTo(4294967295L));
        Advance(state, 8);
        Assert.That(state.Displayed, Is.EqualTo((double)int.MaxValue));

        state.Change(int.MinValue);
        Assert.That(state.Delta, Is.EqualTo(-4294967295L));
        Assert.That(state.Displayed, Is.EqualTo((double)int.MaxValue));
        state.Advance(.1f);
        Assert.That(state.Displayed, Is.InRange((double)int.MinValue, (double)int.MaxValue));
        Advance(state, 7);
        Assert.That(state.Displayed, Is.EqualTo((double)int.MinValue));
    }

    static void Advance(CurrencyNoticeState state, int frames)
    {
        for (int frame = 0; frame < frames; frame++) state.Advance(.1f);
    }
}
