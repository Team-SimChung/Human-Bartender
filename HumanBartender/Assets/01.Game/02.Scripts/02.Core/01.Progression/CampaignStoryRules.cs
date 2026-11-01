/// <summary>캠페인 진행 요구 조건. 저장 값은 기존 플레이어 플래그에만 존재한다.</summary>
public static class CampaignStoryRules
{
    public const string WildDogAvailable = "(flag.gang_wild_dog_received || flag.shiba_bought_wild_dog)";
    public static bool HasWildDog(IConditionUtil conditions) => conditions.CheckRequired(WildDogAvailable);
    public static bool CanPrepareJohnnyDrink(IConditionUtil conditions) => conditions.CheckRequired(
        WildDogAvailable + " && flag.shiba_bought_bitters && flag.campaign_observation");
}
