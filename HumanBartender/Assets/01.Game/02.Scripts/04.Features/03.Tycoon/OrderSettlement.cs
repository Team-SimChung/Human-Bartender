using UnityEngine;

/// <summary>
/// 주문 한 회차의 정산 결과.
///
/// 잔 하나가 낸 돈은 서빙 반응이 끝난 시점에 딱 한 번 매출에 들어간다. 한 손님이 여러 잔을 시키면
/// 회차마다 이 기록이 하나씩 쌓이고, 마지막에 합친 값은 확인·로그용이라 매출에 다시 더하지 않는다
/// (칵테일 제공·정산·다회 주문 §6.4.7).
///
/// 기준 등급은 제조 등급이 아니라 서빙에서 확정한 최종 등급이다. 주문과 다른 잔이나 핵심 재료를
/// 빠뜨린 잔은 아무리 정확하게 만들었어도 Sewage로 뒤집히고, 그러면 판매금액 0에 잔값 전액 배상이 된다.
/// </summary>
public class OrderSettlement
{
    /// <summary>매출 반영을 식별하는 ID. 같은 ID는 두 번 반영하지 않는다.</summary>
    public string SettlementId { get; }

    /// <summary>이 정산을 만든 서빙 결과의 ID.</summary>
    public string ServeResultId { get; }

    /// <summary>정산 기준이 된 최종 등급.</summary>
    public ENewGrade FinalGrade { get; }

    public int SaleAmount { get; }
    public int TipAmount { get; }
    public int RefundAmount { get; }

    /// <summary>이 회차가 매출에 더할 값. 배상이 판매금액보다 크면 음수가 된다.</summary>
    public int SettlementAmount => SaleAmount + TipAmount - RefundAmount;

    /// <summary>당일 매출 누계에 이미 들어갔는지. DailySales가 반영할 때 표시한다.</summary>
    public bool IsApplied { get; internal set; }

    OrderSettlement(string settlementId, string serveResultId, ENewGrade finalGrade,
                    int saleAmount, int tipAmount, int refundAmount)
    {
        SettlementId = settlementId;
        ServeResultId = serveResultId;
        FinalGrade = finalGrade;
        SaleAmount = saleAmount;
        TipAmount = tipAmount;
        RefundAmount = refundAmount;
    }

    /// <summary>
    /// 판매금액·팁·배상액을 계산한다(§6.3.2·6.3.3).
    ///
    ///   sale   = price × sale_rate
    ///   tip    = price × tip_rate × personality.tip_mult
    ///   refund = price × refund_rate
    ///
    /// 등급을 내지 못한 잔(채점 불가)은 정산하지 않고 null을 돌려준다 — 어느 규칙을 쓸지 고를 근거가
    /// 없는데 임의로 등급을 정해 돈을 움직이면 그 오차가 그대로 매출에 남는다.
    ///
    /// 소수점은 반올림해 정수 금액으로 만든다. 명세가 자리수를 정하지 않아, 소지금(gold)이 정수인 것을 따랐다.
    /// </summary>
    public static OrderSettlement Calculate(string settlementId, string serveResultId,
                                            ENewGrade? finalGrade, int price, float tipMult,
                                            NewBalanceDataBase balance)
    {
        if (finalGrade == null) return null;

        if (balance == null || balance.SettlementRules == null)
        {
            Debug.LogError("[Settle] balance.json의 settlement_rules를 읽지 못했습니다. " +
                           "데이터 로딩이 끝나기 전의 balance 객체를 들고 있는지 확인하세요 — " +
                           "settlement_rules는 Dictionary라 로딩 전 객체에서는 통째로 비어 있습니다.");
            return null;
        }

        if (!balance.TryGetSettlementRule(finalGrade.Value, out NewSettlementRule rule))
        {
            Debug.LogError($"[Settle] settlement_rules에 '{finalGrade.Value.ToString().ToLowerInvariant()}' 항목이 " +
                           "없어 정산하지 않았습니다. balance.json을 확인하세요.");
            return null;
        }

        return new OrderSettlement(settlementId, serveResultId, finalGrade.Value,
                                   Mathf.RoundToInt(price * rule.SaleRate),
                                   Mathf.RoundToInt(price * rule.TipRate * tipMult),
                                   Mathf.RoundToInt(price * rule.RefundRate));
    }

    public override string ToString()
    {
        return $"판매 {SaleAmount} + 팁 {TipAmount} - 배상 {RefundAmount} = {SettlementAmount}";
    }
}
