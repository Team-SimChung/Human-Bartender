using System;
using System.Linq;

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
    internal static EPlayerSpawn ResolveSpawn(string sceneName, GameStateManager state) =>
        sceneName == "OutSide" && state.ArrivingAtOutsideHomeDoor
            ? EPlayerSpawn.Home
            : ResolveSpawn(sceneName, state.CurrentDay, state.GameFlow);

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

    // 웹에서 JS 모듈이 등장·대본을 정하던 선택 NPC(outside-newspaper.js, outside-beggar.js, outside-believer.js).
    public const string NewspaperEntry = "shared-newspaper-intro";
    public const string BeggarEntry = "shared-beggar-intro";
    public const string BeggarWitnessEntry = "shared-beggar-warning-unmet";
    public const string BelieverEntry = "shared-believer-intro";

    static string RuntimeScene(string id) => id switch
    {
        "neighborhood-reveal" or "neighborhood-repairGreeting" or "neighborhood-guardBlocked" or "neighborhood-bossIntro" or
            NewspaperEntry or BeggarEntry or BeggarWitnessEntry or BelieverEntry => "OutSide",
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

    /// <summary>
    /// 갱단 경비가 아래층 거리 왼쪽(갱단 구역)을 막고 있는지(웹 outside-neighborhood.js clampBoundary·guardBlocked).
    /// 0일차 퇴근길부터 막고, 경비에게 "술을 찾고 있어요."로 통행 허가를 받으면 연다. 경비 대화와 이동 벽이 같은 조건을 쓴다.
    /// </summary>
    public static bool IsGangPassBlocked(int day, EGameFlow flow, IConditionUtil conditions) =>
        (day > 0 || flow == EGameFlow.CommuteOut) && !conditions.CheckRequired("flag.gang_pass_granted");

    public static bool IsAvailable(string id, int day, EGameFlow flow, IConditionUtil conditions)
    {
        bool Flag(string key) => conditions.CheckRequired("flag." + key);
        bool After(int value) => day >= value;
        switch (id)
        {
            case "neighborhood-reveal": return After(2) && !Flag("boobi_adopted");
            case "neighborhood-repairGreeting": return IsRepairmanPresent(day, conditions);
            case "neighborhood-guardBlocked": return IsGangPassBlocked(day, flow, conditions);
            case "neighborhood-bossIntro": return After(2) && Flag("gang_pass_granted");
            case "neighborhood-homeCat": return Flag("boobi_adopted");
            // 1일차 출퇴근길. 대화를 끝낸 뒤에는 끝낸 그 길에서만 남아 있다(웹 newspaper-progress.js available).
            case NewspaperEntry: return day == 1 && (!Flag("newspaper_conversation_done") || Flag("newspaper_talked_" + Route(flow)));
            // 0일차 퇴근길. 사기꾼은 200원을 받으면 떠난다.
            case BeggarEntry: return day == 0 && flow == EGameFlow.CommuteOut && !Flag("street_beggar_paid");
            case BeggarWitnessEntry: return day == 0 && flow == EGameFlow.CommuteOut;
            // 0일차 퇴근길과 1일차 출퇴근길, 대화를 끝낼 때까지.
            case BelieverEntry: return (day == 0 && flow == EGameFlow.CommuteOut || day == 1) && !Flag("believer_conversation_done");
            default: return true;
        }
    }

    static string Route(EGameFlow flow) => flow == EGameFlow.CommuteIn ? "in" : "out";

    public static NewSceneData ResolveEntry(NewSceneData bound, int day, IConditionUtil conditions, IStoryContentSource content,
        EGameFlow flow = EGameFlow.CommuteIn)
    {
        bool Flag(string key) => conditions.CheckRequired("flag." + key);
        string id = bound.Id;
        if (id == NewspaperEntry)
            id = Flag("newspaper_conversation_done") ? "shared-newspaper-repeat" : Flag("newspaper_gear_received") ? "shared-newspaper-finish" : id;
        if (id == BeggarEntry && Flag("street_beggar_met")) id = "shared-beggar-repeat";
        // 웹 outside-beggar.js warningKey: 돈을 줬는지, 사기꾼을 만났는지, 경고를 이미 들었는지로 고른다.
        if (id == BeggarWitnessEntry)
            id = Flag("street_beggar_paid")
                ? Flag("street_beggar_witness_paid_seen") ? "shared-beggar-warning-paid-repeat" : "shared-beggar-warning-paid"
                : Flag("street_beggar_witness_met") ? "shared-beggar-warning-unpaid-repeat"
                : Flag("street_beggar_met") ? "shared-beggar-warning-met" : id;
        if (id == BelieverEntry && Flag("believer_emblem_received")) id = "shared-believer-finish";
        string entryId = id;
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
        // 대화를 시작한 길을 기록해, 끝낸 뒤에도 그 길에서는 신문 읽는 남자가 남게 한다.
        if (bound.Id == NewspaperEntry)
            bound.Steps = new[] { OutsideStorySteps.Effect("flag.newspaper_talked_" + Route(flow) + " = true") }.Concat(bound.Steps ?? Array.Empty<Step>()).ToArray();
        // 경고를 끝까지 들으면 사기꾼에게 "혹시 사기꾼인가요?"를 물을 수 있다.
        if (bound.Id == BeggarWitnessEntry)
            bound.Steps = (bound.Steps ?? Array.Empty<Step>()).Concat(new[] { OutsideStorySteps.Effect("flag.street_beggar_witness_met = true; flag.street_beggar_warned = true" +
                (entryId == "shared-beggar-warning-paid" ? "; flag.street_beggar_witness_paid_seen = true" : "")) }).ToArray();
        // 웹 outside-shop.js entryRows: 팔 물건이 없으면 첫 만남·재방문 인사 대신 퀴즈 또는 판매 종료 한 줄.
        if ((bound.Id is "shiba-shop" or "shiba-shop-revisit") && !ShopStoryEvents.HasStock(day, conditions))
            bound.Steps = new[] { OutsideStorySteps.Command(ShopStoryEvents.NeedsQuiz(conditions)
                ? OutsideStoryCommand.ShopOpen : OutsideStoryCommand.ShopSoldOut) };
        return bound;
    }
}
