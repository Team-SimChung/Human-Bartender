public enum EBarSeatSignal { Empty, Occupied, Arrival, Waiting, LeavingSoon, Departing }

/// <summary>
/// 좌석 표시가 보여 줄 손님 상태를 정한다. 화면과 씬을 모르는 순수 규칙이라 테스트로 값을 고정한다.
/// 대기 시간 기준이 바뀌면 이 파일만 고친다.
/// </summary>
public static class BarSeatSignalRules
{
    /// <summary>남은 대기 시간이 이 값(초) 이하면 곧 떠남(빨강).</summary>
    public const float LeavingSoonSeconds = 10f;
    /// <summary>남은 대기 시간이 전체의 이 비율 이하면 기다리는 중(주황).</summary>
    public const float WaitingRatio = .5f;
    /// <summary>손님이 바뀐 뒤 이 시간(초) 동안은 도착(깜빡임)으로 본다.</summary>
    public const float ArrivalSeconds = 2.4f;

    /// <param name="occupied">좌석에 손님(또는 대화 캐릭터)이 있는지</param>
    /// <param name="leaving">영업 중 손님이 퇴장 연출 중인지</param>
    /// <param name="waitKnown">GuestManager가 남은 대기 시간을 알려 줬는지(영업 중 주문 대기)</param>
    /// <param name="remaining">남은 대기 시간(초)</param>
    /// <param name="total">전체 대기 시간(초)</param>
    /// <param name="secondsSinceArrival">좌석의 손님이 바뀐 뒤 지난 시간(초)</param>
    public static EBarSeatSignal Evaluate(bool occupied, bool leaving, bool waitKnown, float remaining, float total,
        float secondsSinceArrival)
    {
        if (!occupied) return EBarSeatSignal.Empty;
        if (leaving) return EBarSeatSignal.Departing;
        if (waitKnown)
        {
            if (remaining <= LeavingSoonSeconds) return EBarSeatSignal.LeavingSoon;
            if (remaining <= total * WaitingRatio) return EBarSeatSignal.Waiting;
        }
        return secondsSinceArrival < ArrivalSeconds ? EBarSeatSignal.Arrival : EBarSeatSignal.Occupied;
    }
}
