/// <summary>캠페인 진행 요구 조건. 저장 값은 기존 플레이어 플래그에만 존재한다.</summary>
public static class CampaignStoryRules
{
    public const string WildDogAvailable = "(flag.gang_wild_dog_received || flag.shiba_bought_wild_dog)";
    public static bool HasWildDog(IConditionUtil conditions) => conditions.CheckRequired(WildDogAvailable);
    public static bool CanPrepareJohnnyDrink(IConditionUtil conditions) => conditions.CheckRequired(
        WildDogAvailable + " && flag.shiba_bought_bitters && flag.campaign_observation");

    /// <summary>웹과 같이 3일차 각설탕 수령 대사를 확정한 뒤, 획득·기억 조건을 모두 충족하면 한 번 해금한다.</summary>
    public static bool TryUnlockJohnnyRecipe(int day, IConditionUtil conditions)
    {
        if (day != 3 || conditions == null || conditions.CheckRequired("flag.johnny_recipe_unlocked") ||
            !CanPrepareJohnnyDrink(conditions) || !conditions.CheckRequired("flag.day3_sugar_received")) return false;
        conditions.ApplyRequired("flag.johnny_recipe_unlocked = true");
        return true;
    }
}
