/// <summary>레시피 목록, 제조 진입점, 무작위 주문이 공유하는 해금 규칙.</summary>
public static class CocktailUnlockPolicy
{
    public static bool IsUnlocked(NewCocktailData cocktail, int day)
    {
        if (cocktail.Status != ENewDataStatus.Confirmed || cocktail.UnlockDay > day) return false;

        // 조건부 해금은 아직 평가하는 진행 상태가 없다. 알 수 없는 조건은 해금으로 취급하지 않는다.
        return string.IsNullOrWhiteSpace(cocktail.UnlockWhen) || cocktail.UnlockWhen == "\\N";
    }

    /// <summary>대본이 현재 주문으로 지정한 레시피는 그 주문 동안 선택할 수 있다.</summary>
    public static bool CanSelect(NewCocktailData cocktail, int day, bool hasWaitingOrder)
    {
        if (cocktail.Status != ENewDataStatus.Confirmed) return false;
        if (IsUnlocked(cocktail, day)) return true;
        return hasWaitingOrder;
    }
}
