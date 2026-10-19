using System;

/// <summary>요청 컨트롤러가 사용하는 판정/장부 경계.</summary>
public interface IServeProcessor
{
    ServeResult Evaluate(OrderDetails order, CraftedDrink drink);
    void Apply(ServeResult result);
}

/// <summary>화면과 무관하게 기존 판정·정산 규칙을 사용한다. 장부는 기존 DailySales를 공유한다.</summary>
public sealed class ServeProcessor : IServeProcessor
{
    readonly NewCocktailDataSO cocktails;
    readonly NewBalanceDataSO balance;
    readonly DailySales sales;

    public ServeProcessor(NewCocktailDataSO cocktails, NewBalanceDataSO balance, DailySales sales)
    {
        this.cocktails = cocktails;
        this.balance = balance;
        this.sales = sales ?? throw new ArgumentNullException(nameof(sales));
    }

    public ServeResult Evaluate(OrderDetails order, CraftedDrink drink)
    {
        if (drink?.CraftGrade == null) return null;
        var data = balance != null ? balance.balanceData : null;
        if (data == null || cocktails == null || !cocktails.TryGet(order.CocktailId, out var cocktail))
            throw new InvalidOperationException("주문 판정에 필요한 데이터가 없습니다.");
        bool matches = ServeJudge.IsOrderMatch(drink, order.CocktailId);
        var grade = ServeJudge.ResolveFinalGrade(drink, order.CocktailId, data.Config);
        var settlement = OrderSettlement.Calculate(order.Id + "_settle", order.Id + "_serve",
            grade, cocktail.Price, order.TipMultiplier, data);
        if (settlement == null) throw new InvalidOperationException("정산 계산에 실패했습니다.");
        return new ServeResult(order, drink, grade.Value, matches, settlement);
    }

    public void Apply(ServeResult result)
    {
        if (!sales.Apply(result.Settlement))
            throw new InvalidOperationException("이미 반영된 주문 정산입니다: " + result.OrderId);
    }
}
