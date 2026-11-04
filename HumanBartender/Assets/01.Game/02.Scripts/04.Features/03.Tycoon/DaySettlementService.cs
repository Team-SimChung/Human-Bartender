using System;

/// <summary>장부와 실제 잔액에서 만든 읽기 전용 표시 값. 별도 매출 장부가 아니다.</summary>
public readonly struct DaySettlementSummary
{
    public int Day { get; }
    public int ServedCount { get; }
    public long Sales { get; }
    public long Tips { get; }
    public long Refunds { get; }
    public long Income => Sales + Tips - Refunds;
    public int BalanceBefore { get; }
    public int Upkeep { get; }
    public long BalanceAfter => (long)BalanceBefore - Upkeep;
    public long Shortfall => Math.Max(0, -BalanceAfter);
    public bool CanAfford => BalanceAfter >= 0;

    public DaySettlementSummary(int day, DailySales sales, int balanceBefore, int upkeep)
    {
        Day = day;
        ServedCount = sales.Items.Count;
        Sales = Tips = Refunds = 0;
        foreach (var item in sales.Items)
        {
            Sales += item.SaleAmount;
            Tips += item.TipAmount;
            Refunds += item.RefundAmount;
        }
        BalanceBefore = balanceBefore;
        Upkeep = upkeep;
    }
}

/// <summary>하루 정산의 조회와 유지비 확정만 소유한다. 매출 지급과 씬 이동은 하지 않는다.</summary>
public sealed class DaySettlementService
{
    readonly DailySales sales;
    readonly IPlayerDataReader reader;
    readonly IPlayerDataWriter writer;
    int day;
    int upkeep;
    bool started;
    bool confirming;
    DaySettlementSummary confirmed;
    public bool IsCommitted { get; private set; }

    public DaySettlementService(DailySales sales, IPlayerDataReader reader, IPlayerDataWriter writer)
    {
        this.sales = sales ?? throw new ArgumentNullException(nameof(sales));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void BeginDay(int currentDay, int upkeepGold)
    {
        if (started) throw new InvalidOperationException("이 영업의 정산은 이미 시작되었습니다.");
        if (upkeepGold < 0) throw new ArgumentOutOfRangeException(nameof(upkeepGold));
        day = currentDay;
        upkeep = upkeepGold;
        sales.Reset();
        started = true;
    }

    public DaySettlementSummary Preview()
    {
        if (!started) throw new InvalidOperationException("영업 시작 전에는 정산할 수 없습니다.");
        return IsCommitted ? confirmed : new DaySettlementSummary(day, sales, reader.HasMoney(), upkeep);
    }

    public bool TryConfirm()
    {
        if (IsCommitted) return true;
        if (confirming) return false;
        var summary = Preview();
        if (!summary.CanAfford) return false;
        confirming = true;
        try
        {
            if (!writer.TrySpend(upkeep)) return false;
            confirmed = summary;
            IsCommitted = true;
            return true;
        }
        finally { confirming = false; }
    }
}
