using NUnit.Framework;

/// <summary>좌석 표시 색을 정하는 손님 상태 규칙. 기준 값이 바뀌면 이 테스트도 함께 바꾼다.</summary>
public sealed class BarSeatSignalRulesTests
{
    const float Long = 10f; // 도착 표시 시간(2.4초)이 지난 뒤

    [Test]
    public void EmptySeatIsEmptyEvenWhileLeavingOrWaiting()
    {
        Assert.That(BarSeatSignalRules.Evaluate(false, true, true, 1, 30, Long), Is.EqualTo(EBarSeatSignal.Empty));
    }

    [Test]
    public void LeavingGuestIsDeparting()
    {
        Assert.That(BarSeatSignalRules.Evaluate(true, true, true, 1, 30, Long), Is.EqualTo(EBarSeatSignal.Departing));
        Assert.That(BarSeatSignalRules.Evaluate(true, true, false, 0, 0, 0), Is.EqualTo(EBarSeatSignal.Departing), "막 앉았어도 퇴장이 우선");
    }

    [TestCase(10f, EBarSeatSignal.LeavingSoon)]
    [TestCase(10.01f, EBarSeatSignal.Waiting)]
    [TestCase(15f, EBarSeatSignal.Waiting)]
    [TestCase(15.01f, EBarSeatSignal.Occupied)]
    public void WaitTimeThresholds(float remaining, EBarSeatSignal expected)
    {
        Assert.That(BarSeatSignalRules.Evaluate(true, false, true, remaining, 30, Long), Is.EqualTo(expected));
    }

    [Test]
    public void NewGuestBlinksAsArrivalOnlyUntilWaitingStarts()
    {
        Assert.That(BarSeatSignalRules.Evaluate(true, false, false, 0, 0, 0), Is.EqualTo(EBarSeatSignal.Arrival));
        Assert.That(BarSeatSignalRules.Evaluate(true, false, false, 0, 0, 2.39f), Is.EqualTo(EBarSeatSignal.Arrival));
        Assert.That(BarSeatSignalRules.Evaluate(true, false, false, 0, 0, 2.4f), Is.EqualTo(EBarSeatSignal.Occupied));
        Assert.That(BarSeatSignalRules.Evaluate(true, false, true, 5, 30, 0), Is.EqualTo(EBarSeatSignal.LeavingSoon), "대기 경고가 도착보다 우선");
    }

    [Test]
    public void DialogueCharactersHaveNoWaitTime()
    {
        // 대화(2부)에서는 대기 시간이 없어 도착·자리 있음만 나온다.
        Assert.That(BarSeatSignalRules.Evaluate(true, false, false, 0, 0, Long), Is.EqualTo(EBarSeatSignal.Occupied));
    }
}
