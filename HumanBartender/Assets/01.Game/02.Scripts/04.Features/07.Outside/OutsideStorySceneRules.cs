using System;

/// <summary>씬에 들어올 때 플레이어가 서는 자리. 실제 위치는 Player에 연결한 씬 Transform이 소유한다.</summary>
public enum EPlayerSpawn
{
    /// <summary>집 안, 또는 거리의 위층 집 문 앞(엘리베이터로 내려가야 함).</summary>
    Home,
    /// <summary>바에서 나온 직후의 바 문 앞.</summary>
    Bar,
    /// <summary>0일차 첫 출근. 거리 1층 바 문 왼쪽에서 걸어 들어온다.</summary>
    FirstCommute,
}

/// <summary>Unity 동네 사건의 진입 조건과 대본 선택. 위치와 대상의 수명은 씬 오브젝트가 소유한다.</summary>
public static class OutsideStorySceneRules
{
    /// <summary>
    /// 씬·일차·출퇴근 흐름으로 시작 자리를 고른다. 첫 출근은 아직 집과 바의 길을 모르는 날이라
    /// 엘리베이터를 거치지 않고 바 앞에서 시작한다. 그 밖의 출근은 집 문 앞, 퇴근은 바 문 앞이다.
    /// </summary>
    public static EPlayerSpawn ResolveSpawn(string sceneName, int day, EGameFlow flow)
    {
        if (sceneName == "Home") return EPlayerSpawn.Home;
        if (flow == EGameFlow.CommuteOut) return EPlayerSpawn.Bar;
        return day == 0 ? EPlayerSpawn.FirstCommute : EPlayerSpawn.Home;
    }

    static string RuntimeScene(string id) => id switch
    {
        "neighborhood-reveal" or "neighborhood-repairGreeting" or "neighborhood-guardBlocked" or "neighborhood-bossIntro" => "OutSide",
        "neighborhood-homeCat" => "Home",
        _ => null
    };
    public static bool IsNativeEntry(string id) => RuntimeScene(id) != null;

    public static bool IsRepairmanPresent(int day, IConditionUtil conditions) =>
        day >= 1 && !conditions.CheckRequired("flag.elevator_repaired");

    public static bool TryBind(NewSceneData source, string runtimeScene, out NewSceneData bound)
    {
        bound = source;
        string scene = RuntimeScene(source.Id);
        if (scene == null || scene != runtimeScene) return false;
        bound.RuntimeScene = scene;
        bound.Route = "both";
        bound.Day = null;
        return true;
    }

    public static bool IsAvailable(string id, int day, EGameFlow flow, IConditionUtil conditions)
    {
        bool Flag(string key) => conditions.CheckRequired("flag." + key);
        bool After(int value) => day >= value;
        switch (id)
        {
            case "neighborhood-reveal": return After(2) && !Flag("boobi_adopted");
            case "neighborhood-repairGreeting": return IsRepairmanPresent(day, conditions);
            case "neighborhood-guardBlocked": return (day > 0 || flow == EGameFlow.CommuteOut) && !Flag("gang_pass_granted");
            case "neighborhood-bossIntro": return After(2) && Flag("gang_pass_granted");
            case "neighborhood-homeCat": return Flag("boobi_adopted");
            default: return true;
        }
    }

    public static NewSceneData ResolveEntry(NewSceneData bound, int day, IConditionUtil conditions, IStoryContentSource content)
    {
        bool Flag(string key) => conditions.CheckRequired("flag." + key);
        string id = bound.Id;
        if (id == "neighborhood-reveal" && Flag("boobi_revealed")) id = Flag("boobi_feeding_pending") ? "neighborhood-feed" : "neighborhood-cat";
        if (id == "neighborhood-repairGreeting") id = Flag("elevator_repair_paid") ? "neighborhood-repairPaid" : Flag("repairman_met") ? "neighborhood-repairMenu" : id;
        if (id == "neighborhood-guardBlocked" && day >= 2) id = "neighborhood-guardMenu";
        if (id == "neighborhood-bossIntro")
        {
            bool done = CampaignStoryRules.HasWildDog(conditions);
            if (done) id = "neighborhood-bossDone";
            else if (Flag("gang_wild_dog_paid") || Flag("gang_wild_dog_repair_promised"))
                id = "neighborhood-boss" + (Flag("gang_wild_dog_paid") ? "Pay" : "Repair") + (Flag("gang_wild_dog_repeat_deal") ? "Repeat" : "First");
            else if (Flag("gang_boss_met")) id = "neighborhood-bossRepeat";
        }
        if (id != bound.Id) bound.Steps = content.GetSceneSteps(id);
        return bound;
    }
}
