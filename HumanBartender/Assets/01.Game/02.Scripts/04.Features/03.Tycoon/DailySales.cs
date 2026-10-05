using System.Collections.Generic;

/// <summary>
/// 당일 매출 누계.
///
/// 같은 정산을 두 번 더하지 않는 것이 이 클래스가 있는 이유다. 반응 연출과 퇴장이 겹치거나 같은
/// 결과가 다시 흘러들어와도 settlement_id로 한 번만 받는다(칵테일 제공·정산·다회 주문 §6.3.4).
///
/// 1부에서는 잔별 정산 팝업을 띄우지 않는다. 여기 쌓인 값은 2부가 끝난 뒤 일일 매출 정산 화면이
/// 한 번에 보여줄 몫이라, 지금은 화면 없이 값만 들고 있다.
/// </summary>
public class DailySales
{
    readonly HashSet<string> appliedSettlementIds = new();
    readonly List<OrderSettlement> items = new();

    /// <summary>부호를 포함한 당일 매출 누계. 배상이 많으면 음수가 될 수 있다.</summary>
    public int Total { get; private set; }

    /// <summary>반영된 회차별 정산 기록. 일일 매출 정산 화면이 읽을 몫이다.</summary>
    public IReadOnlyList<OrderSettlement> Items => items;

    /// <summary>정산 하나를 누계에 더한다. 이미 반영된 ID거나 정산이 없으면 아무것도 하지 않는다.</summary>
    public bool Apply(OrderSettlement settlement)
    {
        if (settlement == null) return false;
        if (!appliedSettlementIds.Add(settlement.SettlementId)) return false;

        settlement.IsApplied = true;
        items.Add(settlement);
        Total += settlement.SettlementAmount;
        return true;
    }

    /// <summary>하루가 시작될 때 누계를 비운다.</summary>
    public void Reset()
    {
        appliedSettlementIds.Clear();
        items.Clear();
        Total = 0;
    }
}
