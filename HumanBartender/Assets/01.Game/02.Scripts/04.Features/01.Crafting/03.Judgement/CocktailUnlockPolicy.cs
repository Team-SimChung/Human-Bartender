/// <summary>레시피 목록, 제조 진입점, 무작위 주문이 공유하는 해금 규칙.</summary>
public static class CocktailUnlockPolicy
{
    public static bool IsUnlocked(NewCocktailData cocktail, int day, IConditionUtil conditions = null)
    {
        if (cocktail.Status != ENewDataStatus.Confirmed || cocktail.UnlockDay > day) return false;

        return IsUnconditional(cocktail.UnlockWhen) || conditions != null && conditions.CheckRequired(cocktail.UnlockWhen);
    }

    /// <summary>대본이 현재 주문으로 지정한 레시피는 그 주문 동안 선택할 수 있다.</summary>
    public static bool CanSelect(NewCocktailData cocktail, int day, bool hasWaitingOrder, IConditionUtil conditions = null)
    {
        if (cocktail.Status != ENewDataStatus.Confirmed) return false;
        if (IsUnlocked(cocktail, day, conditions)) return true;
        // 대본 주문은 일차 제한을 풀 수 있지만, 아직 획득하지 않은 조건부 레시피를 해금하지 않는다.
        return hasWaitingOrder && IsUnconditional(cocktail.UnlockWhen);
    }

    public static bool IsUnconditional(string condition) => string.IsNullOrWhiteSpace(condition) || condition == "\\N";
}
