using System.Collections.Generic;

/// <summary>캠페인 진행 요구 조건. 저장 값은 기존 플레이어 플래그에만 존재한다.</summary>
public static class CampaignStoryRules
{
    /// <summary>3일차 히든 레시피. cocktails.csv의 id.</summary>
    public const string JohnnyCocktailId = "johnny_old_fashioned";
    /// <summary>갱 두목 거래·시바견 상점에서 받는 재료 ID(shelf_items.csv). 획득 알림에 쓴다.</summary>
    public const string WildDogIngredientId = "wild_dog";
    public const string BittersIngredientId = "bitters";
    public const string WildDogAvailable = "(flag.gang_wild_dog_received || flag.shiba_bought_wild_dog)";
    public static bool HasWildDog(IConditionUtil conditions) => conditions.CheckRequired(WildDogAvailable);
    public static bool CanPrepareJohnnyDrink(IConditionUtil conditions) => conditions.CheckRequired(
        WildDogAvailable + " && flag.shiba_bought_bitters && flag.campaign_observation");

    /// <summary>
    /// 3일차 바에 들어오기 전에 정상 진행에서 채워지는 기록(와일드 독·비터스·조니 기억).
    /// 에디터에서 일차를 건너뛰는 테스트 진입만 이 목록으로 대신 채운다(웹 seedJohnnyPrerequisites).
    /// </summary>
    public static readonly IReadOnlyList<string> JohnnyPrerequisiteFlags =
        new[] { "gang_wild_dog_received", "shiba_bought_bitters", "campaign_observation" };

    /// <summary>올드 패션드 해금에 아직 없는 전날 조건. 웹 johnnyMissing과 같은 이름을 쓴다.</summary>
    public static List<string> MissingJohnnyPrerequisites(IConditionUtil conditions)
    {
        var missing = new List<string>();
        if (!HasWildDog(conditions)) missing.Add("wild_dog");
        if (!conditions.CheckRequired("flag.shiba_bought_bitters")) missing.Add("bitters");
        if (!conditions.CheckRequired("flag.campaign_observation")) missing.Add("johnny_memory");
        return missing;
    }

    /// <summary>
    /// 빠진 전날 조건만 채우고 채운 플래그를 돌려준다. 이미 있는 기록(시바에게 산 와일드 독 등)은 건드리지 않는다.
    /// 정상 진행에서는 부르지 않는다.
    /// </summary>
    public static List<string> SeedJohnnyPrerequisites(IConditionUtil conditions)
    {
        var seeded = new List<string>();
        foreach (string missing in MissingJohnnyPrerequisites(conditions))
        {
            string flag = missing switch
            {
                "wild_dog" => JohnnyPrerequisiteFlags[0],
                "bitters" => JohnnyPrerequisiteFlags[1],
                _ => JohnnyPrerequisiteFlags[2]
            };
            conditions.ApplyRequired($"flag.{flag} = true");
            seeded.Add(flag);
        }
        return seeded;
    }

    /// <summary>
    /// 이름을 밝히는 대사에서 '새로운 인물' 알림을 띄우는 인물(웹 story-notices.js introductions).
    /// 입장 때는 {id}_met만 조용히 켜고(인물 정보 해금은 그대로), 알림은 이 대사가 시작될 때 낸다.
    /// </summary>
    static readonly Dictionary<string, string> IntroductionLines = new(System.StringComparer.Ordinal)
    {
        { "tom", "campaign_d1_bar_77" }
    };

    /// <summary>알림을 입장이 아니라 이름 대사로 미루는 인물인지.</summary>
    public static bool HasDelayedIntroduction(string characterId) =>
        characterId != null && IntroductionLines.ContainsKey(characterId);

    /// <summary>알림이 끝났음을 남기는 플래그(웹 {id}_introduction_notified).</summary>
    public static string IntroductionNotifiedFlag(string characterId) => characterId + "_introduction_notified";

    /// <summary>
    /// 지금 시작하는 대사가 그 인물의 이름 대사이면 적용할 기록을 돌려준다.
    /// 웹과 같이 알림 완료·만남·이름 앎을 함께 켠다. 알림은 SceneAudioController가 _introduction_notified 변화로 띄운다.
    /// </summary>
    public static bool TryGetIntroduction(string actor, string dialogueId, out string effects)
    {
        effects = null;
        if (actor == null || dialogueId == null || !IntroductionLines.TryGetValue(actor, out string line) || line != dialogueId)
            return false;
        effects = $"flag.{IntroductionNotifiedFlag(actor)} = true; flag.{actor}_met = true; flag.{actor}_name_known = true";
        return true;
    }

    /// <summary>웹과 같이 3일차 각설탕 수령 대사를 확정한 뒤, 획득·기억 조건을 모두 충족하면 한 번 해금한다.</summary>
    public static bool TryUnlockJohnnyRecipe(int day, IConditionUtil conditions)
    {
        if (day != 3 || conditions == null || conditions.CheckRequired("flag.johnny_recipe_unlocked") ||
            !CanPrepareJohnnyDrink(conditions) || !conditions.CheckRequired("flag.day3_sugar_received")) return false;
        conditions.ApplyRequired("flag.johnny_recipe_unlocked = true");
        return true;
    }
}
