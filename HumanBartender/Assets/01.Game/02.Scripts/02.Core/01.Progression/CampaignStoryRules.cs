using System.Collections.Generic;

/// <summary>캠페인 진행 요구 조건. 저장 값은 기존 플레이어 플래그에만 존재한다.</summary>
public static class CampaignStoryRules
{
    /// <summary>3일차 히든 레시피. cocktails.csv의 id.</summary>
    public const string JohnnyCocktailId = "johnny_old_fashioned";
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

    /// <summary>웹과 같이 3일차 각설탕 수령 대사를 확정한 뒤, 획득·기억 조건을 모두 충족하면 한 번 해금한다.</summary>
    public static bool TryUnlockJohnnyRecipe(int day, IConditionUtil conditions)
    {
        if (day != 3 || conditions == null || conditions.CheckRequired("flag.johnny_recipe_unlocked") ||
            !CanPrepareJohnnyDrink(conditions) || !conditions.CheckRequired("flag.day3_sugar_received")) return false;
        conditions.ApplyRequired("flag.johnny_recipe_unlocked = true");
        return true;
    }
}
