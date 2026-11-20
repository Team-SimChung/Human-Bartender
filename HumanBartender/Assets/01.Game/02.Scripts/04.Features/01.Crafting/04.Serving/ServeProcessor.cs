using System;
using System.Linq;

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
    readonly IPlayerDataWriter player;
    readonly IConditionUtil conditions;

    public ServeProcessor(NewCocktailDataSO cocktails, NewBalanceDataSO balance, DailySales sales,
        IPlayerDataWriter player, IConditionUtil conditions)
    {
        this.cocktails = cocktails;
        this.balance = balance;
        this.sales = sales ?? throw new ArgumentNullException(nameof(sales));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    public ServeResult Evaluate(OrderDetails order, CraftedDrink drink)
    {
        if (drink?.CraftGrade == null) return null;
        var data = balance != null ? balance.balanceData : null;
        if (data == null || cocktails == null || !cocktails.TryGet(order.CocktailId, out var cocktail))
            throw new InvalidOperationException("[Serve] 주문 판정에 필요한 데이터가 없습니다.");
        bool matches = ServeJudge.IsOrderMatch(drink, order.CocktailId);
        var grade = ServeJudge.ResolveFinalGrade(drink, order.CocktailId, data.Config);
        var settlement = OrderSettlement.Calculate(order.Id + "_settle", order.Id + "_serve",
            grade, order.Paid ? cocktail.Price : 0, order.Paid ? order.TipMultiplier : 0, data);
        if (settlement == null) throw new InvalidOperationException("[Serve] 정산 계산에 실패했습니다.");
        if (!cocktails.TryGet(drink.CocktailId, out var servedCocktail))
            throw new InvalidOperationException("[Serve] 제공한 칵테일 데이터가 없습니다: " + drink.CocktailId);
        int affinityDelta = CalculateAffinity(order, drink, grade.Value, matches, servedCocktail, data);
        return new ServeResult(order, drink, grade.Value, matches, settlement, servedCocktail, affinityDelta);
    }

    public void Apply(ServeResult result)
    {
        if (!sales.Apply(result.Settlement))
            throw new InvalidOperationException("[Serve] 이미 반영된 주문 정산입니다: " + result.OrderId);
        // 장부가 먼저 ID를 예약하므로 이벤트에서 재진입해도 호감도와 팝업은 한 번만 반영된다.
        if (result.AffinityDelta != 0)
            player.AddCharacterAffinityAmount(result.ReceiverId, result.AffinityDelta);
    }

    int CalculateAffinity(OrderDetails order, CraftedDrink drink, ENewGrade grade, bool matches,
        NewCocktailData servedCocktail, NewBalanceDataBase data)
    {
        var characters = NewDataLoadManager.StoryCharacters ??
            throw new InvalidOperationException("[Serve] 캐릭터 데이터가 로드되지 않았습니다.");
        if (!characters.Any(c => c.Id == order.ReceiverId && c.Affinity)) return 0;

        ENewTasteTier tier = matches ? ENewTasteTier.Ok : ENewTasteTier.Miss;
        if (matches)
        {
            var tastes = NewDataLoadManager.StoryTastes ??
                throw new InvalidOperationException("[Serve] 취향 데이터가 로드되지 않았습니다.");
            var scope = conditions.CreateExecutionScope();
            scope.Result = new StoryResultContext(drink.CraftGrade.Value, grade, matches,
                order.CocktailId, drink.CocktailId, servedCocktail);
            foreach (var taste in tastes.Where(t => t.CharacterId == order.ReceiverId).OrderBy(t => t.Seq))
            {
                if (!scope.CheckRequired(taste.When)) continue;
                tier = taste.Tier;
                break;
            }
        }

        foreach (var row in data.AffinityMatrix ?? Array.Empty<NewAffinityMatrixRow>())
        {
            if (row.TasteTier != tier) continue;
            return grade switch
            {
                ENewGrade.Excellent => row.Excellent,
                ENewGrade.Good => row.Good,
                ENewGrade.Decent => row.Decent,
                ENewGrade.Poor => row.Poor,
                ENewGrade.Sewage => row.Sewage,
                _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "[Serve] 알 수 없는 등급입니다.")
            };
        }
        throw new InvalidOperationException("[Serve] 호감도 계산표에 취향이 없습니다: " + tier);
    }
}
