using System;
using System.Collections.Generic;

public interface IOrderPaymentService
{
    int Pay(IEnumerable<OrderSettlement> settlements);
}

/// <summary>장부에 확정된 주문을 소지금에 한 번만 반영한다. 지급 시점은 주문 담당이 결정한다.</summary>
public sealed class OrderPaymentService : IOrderPaymentService
{
    readonly DailySales sales;
    readonly IPlayerDataWriter player;
    readonly HashSet<string> paidIds = new(StringComparer.Ordinal);

    public OrderPaymentService(DailySales sales, IPlayerDataWriter player)
    {
        this.sales = sales ?? throw new ArgumentNullException(nameof(sales));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public int Pay(IEnumerable<OrderSettlement> settlements)
    {
        if (settlements == null) throw new ArgumentNullException(nameof(settlements));
        var pending = new HashSet<string>(StringComparer.Ordinal);
        int amount = 0;
        foreach (var item in settlements)
        {
            if (!sales.Contains(item)) throw new InvalidOperationException("장부에 없는 주문은 지급할 수 없습니다.");
            if (paidIds.Contains(item.SettlementId) || !pending.Add(item.SettlementId)) continue;
            amount = checked(amount + item.SettlementAmount);
        }

        // 재화 변경 알림에서 재진입해도 같은 주문을 다시 지급하지 않는다.
        paidIds.UnionWith(pending);
        if (amount != 0) player.AddMoney(amount);
        return amount;
    }
}
