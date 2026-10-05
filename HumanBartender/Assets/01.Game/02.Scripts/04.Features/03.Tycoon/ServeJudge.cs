/// <summary>
/// 완성한 잔을 손님 앞에 놓는 순간의 판정.
///
/// 제조 점수는 얼마나 정확하게 만들었는지만 본다. 그 잔이 이 손님에게 맞는 잔인지는 낼 때가 되어야
/// 알 수 있어서, 최종 등급은 제조가 아니라 여기서 정해진다.
/// </summary>
public static class ServeJudge
{
    /// <summary>이 잔이 손님이 주문한 칵테일인지.</summary>
    public static bool IsOrderMatch(CraftedDrink drink, Guest guest)
    {
        return guest != null && IsOrderMatch(drink, guest.targetCocktailId);
    }

    /// <summary>
    /// 이 잔이 주문한 칵테일인지. 주문한 쪽을 id로만 받는다.
    ///
    /// 2부의 주문자는 Guest가 아니라 대본이 앉힌 단골이라 손님 객체가 없다. 판정 규칙은 같아야 하므로
    /// 규칙을 옮겨 적지 않고, 손님을 받는 쪽이 이 함수로 넘어온다.
    /// </summary>
    public static bool IsOrderMatch(CraftedDrink drink, string orderedCocktailId)
    {
        return drink != null && !string.IsNullOrEmpty(orderedCocktailId) &&
               drink.CocktailId == orderedCocktailId;
    }

    /// <summary>
    /// 손님이 받아 든 잔의 최종 등급.
    ///
    /// 주문과 다른 잔과 핵심 재료를 빠뜨린 잔은 아무리 정확하게 만들었어도 뒤집힌다
    /// (balance.json의 force_sewage_on_order_mismatch / force_sewage_on_missing_core).
    /// 채점하지 못한 잔은 null 그대로 둔다 — 등급을 임의로 만들어 붙이지 않는다.
    /// </summary>
    public static ENewGrade? ResolveFinalGrade(CraftedDrink drink, Guest guest, NewBalanceConfig config)
    {
        return ResolveFinalGrade(drink, guest?.targetCocktailId, config);
    }

    /// <summary>주문한 쪽을 id로만 받는 최종 등급. 2부가 쓴다.</summary>
    public static ENewGrade? ResolveFinalGrade(CraftedDrink drink, string orderedCocktailId,
                                               NewBalanceConfig config)
    {
        if (drink == null) return null;

        if (config.ForceSewageOnOrderMismatch && !IsOrderMatch(drink, orderedCocktailId)) return ENewGrade.Sewage;
        if (config.ForceSewageOnMissingCore && drink.HasMissingCore) return ENewGrade.Sewage;

        return drink.CraftGrade;
    }

    /// <summary>등급에 대응하는 barks.json의 반응 situation.</summary>
    public static string ReactSituation(ENewGrade grade) => grade switch
    {
        ENewGrade.Excellent => "react_excellent",
        ENewGrade.Good => "react_good",
        ENewGrade.Decent => "react_decent",
        ENewGrade.Poor => "react_poor",
        _ => "react_sewage",
    };

    /// <summary>이 등급의 잔을 받은 손님이 웃으며 나가는지. 채점하지 못한 잔은 웃으며 보낸다.</summary>
    public static bool IsSatisfied(ENewGrade? grade)
    {
        return grade == null || grade <= ENewGrade.Decent;
    }
}
