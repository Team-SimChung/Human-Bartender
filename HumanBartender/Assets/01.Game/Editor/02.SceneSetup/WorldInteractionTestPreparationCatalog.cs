using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>실제 조건을 우회하지 않고 시작 상태를 준비한다. 아이템 소유권도 원본 진행 플래그를 쓴다.</summary>
internal static class WorldInteractionTestPreparationCatalog
{
    internal const string Prefix = "prepared-test-";

    internal sealed class Preparation
    {
        internal string Group { get; }
        internal HomeInteractionTestScenario Scenario { get; }
        internal bool TargetAvailable { get; }
        internal Preparation(string group, HomeInteractionTestScenario scenario, bool targetAvailable = true)
        { Group = group; Scenario = scenario; TargetAvailable = targetAvailable; }
    }

    // 같은 계열을 반복 적용할 때 이전 시험의 구매·완료 기록이 선택지를 가리지 않도록 명시한다.
    private static readonly Dictionary<string, string[]> ResetFlags = new()
    {
        ["부비"] = new[] { "boobi_revealed", "boobi_adopted", "boobi_feeding_pending", "boobi_rest_seen", "shiba_bought_tuna_can" },
        ["상점"] = new[] { "shiba_shop_met", "shiba_bought_bitters", "shiba_bought_tuna_can", "shiba_allowance_received", "shiba_old_fashioned_taught", "gang_wild_dog_received", "shiba_bought_wild_dog", "boobi_adopted" },
        ["수리·엘리베이터"] = new[] { "repairman_met", "has_gear_part", "elevator_repair_paid", "elevator_repaired" },
        ["경비·보스"] = new[] { "gang_pass_granted", "gang_boss_met", "gang_wild_dog_received", "shiba_bought_wild_dog", "gang_wild_dog_paid", "gang_wild_dog_repair_promised", "gang_wild_dog_repeat_deal", "elevator_repaired" },
        ["불량배·정보"] = new[] { "day1_thug_done", "day1_thug_choice", "day2_thug_done" },
        ["TV"] = new[] { "tv_news_d0_seen", "tv_news_d1_seen", "tv_news_d2_seen", "tv_news_d3_seen" },
        ["취침·엔딩"] = new[] { "home_night0_seen", "home_night1_seen", "home_night2_seen", "home_night3_seen", "workshop_seen",
            "gang_wild_dog_received", "shiba_bought_wild_dog", "shiba_bought_bitters", "campaign_observation", "samho_death_route", "boobi_adopted", "boobi_rest_seen" }
    };

    internal static IReadOnlyList<Preparation> All { get; } = Build();

    internal static Preparation Find(string id) => All.FirstOrDefault(value => value.Scenario.Id == id)
        ?? throw new KeyNotFoundException("[WorldTest] 조건 준비 상황을 찾을 수 없습니다: " + id);

    internal static IEnumerable<Preparation> ForTarget(string scene, string key) => All.Where(value =>
        value.Scenario.RuntimeScene == scene && value.Scenario.FocusId == key.Substring(key.IndexOf(':') + 1));

    internal static string ClosedReason(string scene, string key) => scene != "OutSide" ? null : key switch
    {
        "source:bubi" => "구형 부비: CSV의 day > 3 && day == 2는 동시에 충족할 수 없습니다. neighborhood-reveal을 사용하세요.",
        "source:shiba" => "구형 시바: CSV가 day > 3으로 제한합니다. shiba-shop / shiba-shop-revisit을 사용하세요.",
        "source:sp_parttime_poster" or "source:sp_experiment_poster" => "CSV가 day > 3으로 제한하므로 현재 0~3일차 캠페인에서는 등장하지 않습니다.",
        _ => null
    };

    internal static string NativeEntryRequirement(string key) => key switch
    {
        "scene:neighborhood-reveal" => "2일차 이상 · 부비 입양 전",
        "scene:neighborhood-homeCat" => "부비 입양 완료",
        "scene:neighborhood-repairGreeting" => "1일차 이상 · 엘리베이터 수리 미완료",
        "scene:neighborhood-guardBlocked" => "1일차 이상 또는 퇴근 · 통행 허가 전",
        "scene:neighborhood-bossIntro" => "2일차 이상 · 경비 통행 허가 완료",
        "scene:" + OutsideStorySceneRules.NewspaperEntry => "1일차 출퇴근 · 대화를 끝낸 뒤에는 끝낸 길에서만",
        "scene:" + OutsideStorySceneRules.BeggarEntry => "0일차 퇴근 · 200원을 주기 전",
        "scene:" + OutsideStorySceneRules.BeggarWitnessEntry => "0일차 퇴근",
        "scene:" + OutsideStorySceneRules.BelieverEntry => "0일차 퇴근·1일차 출퇴근 · 대화 완료 전",
        _ => null
    };

    private static IReadOnlyList<Preparation> Build()
    {
        var result = new List<Preparation>();
        foreach (var source in HomeInteractionTestScenarios.All.Where(value => !value.Opening).Concat(OutsideInteractionTestScenarios.All))
        {
            string group = GroupFor(source.FocusId);
            var flags = Flags(group, Array.Empty<string>());
            if (group == "취침·엔딩")
            {
                flags["workshop_seen"] = source.Day > 1;
                for (int day = 0; day <= 3; day++) flags["home_night" + day + "_seen"] = day < source.Day;
            }
            foreach (var pair in source.FlagOverrides) flags[pair.Key] = pair.Value;
            result.Add(new Preparation(group, new HomeInteractionTestScenario(Prefix + source.Id, source.Name, source.Description,
                source.Day, source.Flow, source.FocusId, source.ExpectedStories, flags: flags, runtimeScene: source.RuntimeScene, money: source.Money)));
        }

        void Add(string id, string group, string name, string description, string focus, int money = 1000,
            string[] enabled = null, int day = 2, EGameFlow flow = EGameFlow.CommuteIn, string scene = "OutSide", bool available = true)
            => result.Add(new Preparation(group, new HomeInteractionTestScenario(Prefix + id, name, description, day, flow, focus,
                flags: Flags(group, enabled ?? Array.Empty<string>()), runtimeScene: scene, money: money), available));

        Add("cat-feed-ready", "부비", "발견 완료 · 참치캔 주기 가능", "상호작용 메뉴에서 참치캔 주기·쓰다듬기·취소를 확인합니다.", "neighborhood-reveal",
            enabled: new[] { "boobi_revealed", "shiba_bought_tuna_can" });
        Add("cat-no-tuna", "부비", "발견 완료 · 참치캔 없음", "쓰다듬기는 가능하고 참치캔 주기는 잠겨야 합니다.", "neighborhood-reveal", enabled: new[] { "boobi_revealed" });
        Add("cat-adopted-outside", "부비", "입양 완료 · 외부 박스 숨김", "입양한 부비는 외부에서 사라지고 Home 프리셋에서 만납니다.", "neighborhood-reveal",
            enabled: new[] { "boobi_revealed", "boobi_adopted", "boobi_rest_seen" }, available: false);

        Add("shop-budget", "상점", "구매 가능 · 비터스 300 / 참치캔 50", "용돈 분기를 완료하여 구매 메뉴를 바로 확인합니다.", "shiba-shop-revisit",
            enabled: new[] { "shiba_shop_met", "shiba_allowance_received" });
        Add("shop-no-money", "상점", "구매 불가 · 49원", "용돈은 이미 수령했습니다. 참치캔·비터스의 잔액 부족 표시를 확인합니다.", "shiba-shop-revisit", 49,
            new[] { "shiba_shop_met", "shiba_allowance_received" });
        Add("shop-tuna-boundary", "상점", "참치캔 최소 금액 · 50원", "참치캔만 구매 가능하고 비터스는 잔액 부족입니다.", "shiba-shop-revisit", 50,
            new[] { "shiba_shop_met", "shiba_allowance_received" });
        Add("shop-allowance", "상점", "용돈 제안 · 500원 / 미수령", "비터스가 없고 2일차 이하이며 잔액 500원 이하인 용돈 분기를 확인합니다.", "shiba-shop-revisit", ShopStoryEvents.AllowanceMoneyLimit, new[] { "shiba_shop_met" });
        Add("shop-owned", "상점", "비터스·참치캔 구매 완료", "이미 산 상품은 다시 판매하지 않습니다.", "shiba-shop-revisit",
            enabled: new[] { "shiba_shop_met", "shiba_bought_bitters", "shiba_bought_tuna_can" });
        Add("shop-lesson", "상점", "올드 패션드 교육 조건 충족", "비터스·와일드 독을 준비합니다. 교육·퀴즈 정답·오답 반복을 확인합니다.", "shiba-shop-revisit",
            enabled: new[] { "shiba_shop_met", "shiba_bought_bitters", "gang_wild_dog_received" });
        Add("shop-lesson-done", "상점", "교육 완료 · 재교육 없음", "재료를 보유해도 교육 완료 기록이 있으면 구매 메뉴로 갑니다.", "shiba-shop-revisit",
            enabled: new[] { "shiba_shop_met", "shiba_bought_bitters", "gang_wild_dog_received", "shiba_old_fashioned_taught" });

        Add("repair-gear", "수리·엘리베이터", "기어 부품으로 수리 · 0원", "부품 건네주기만 가능하며 현금 지불은 잠깁니다.", "neighborhood-repairGreeting", 0, new[] { "repairman_met", "has_gear_part" });
        Add("repair-cash", "수리·엘리베이터", "현금으로 수리 · 400원", "최소 수리비를 준비하며 부품은 없습니다.", "neighborhood-repairGreeting", 400, new[] { "repairman_met" });
        Add("repair-short", "수리·엘리베이터", "수리 조건 부족 · 399원 / 부품 없음", "두 수리 선택지의 부족 안내와 취소를 확인합니다.", "neighborhood-repairGreeting", 399, new[] { "repairman_met" });
        Add("repair-done", "수리·엘리베이터", "수리 완료 · 수리공 퇴장", "수리공은 숨겨지고 엘리베이터의 완료 상태를 확인할 수 있습니다.", "neighborhood-repairGreeting",
            enabled: new[] { "repairman_met", "elevator_repair_paid", "elevator_repaired" }, available: false);
        Add("elevator-repaired", "수리·엘리베이터", "수리된 엘리베이터 탑승", "수리 완료 후 실제 탑승·취소·왕복을 확인합니다.", "outside_elevator", enabled: new[] { "elevator_repaired" });

        Add("guard-pass", "경비·보스", "통행 허가 완료 · 경비 숨김", "경비 대화는 끝나고 보스에게 진입할 수 있습니다.", "neighborhood-guardBlocked", enabled: new[] { "gang_pass_granted" }, available: false);
        Add("boss-cash", "경비·보스", "와일드 독 현금 거래 · 500원", "수리 보상은 불가하고 현금 구매만 가능합니다.", "neighborhood-bossIntro", 500, new[] { "gang_pass_granted", "gang_boss_met" });
        Add("boss-short", "경비·보스", "보스 거래 조건 부족 · 499원", "엘리베이터 미수리·잔액 부족으로 두 거래 선택지가 잠깁니다.", "neighborhood-bossIntro", 499, new[] { "gang_pass_granted", "gang_boss_met" });
        Add("boss-repair", "경비·보스", "수리 보상으로 와일드 독 · 0원", "승강기 수리를 완료하여 보상 거래만 가능합니다.", "neighborhood-bossIntro", 0, new[] { "gang_pass_granted", "gang_boss_met", "elevator_repaired" });
        Add("boss-paid", "경비·보스", "지불 완료 · 물건 수령 전", "현금 중복 차감 없이 중단된 인도 대화로 재진입합니다.", "neighborhood-bossIntro", enabled: new[] { "gang_pass_granted", "gang_boss_met", "gang_wild_dog_paid" });
        Add("boss-promised", "경비·보스", "보상 약속 완료 · 물건 수령 전", "보상 거래의 인도 대화를 다시 시작합니다.", "neighborhood-bossIntro", enabled: new[] { "gang_pass_granted", "gang_boss_met", "elevator_repaired", "gang_wild_dog_repair_promised" });
        Add("boss-received", "경비·보스", "와일드 독 수령 완료", "거래 완료 대화만 나오고 중복 지급하지 않습니다.", "neighborhood-bossIntro", enabled: new[] { "gang_pass_granted", "gang_boss_met", "gang_wild_dog_received" });

        Add("thug-short", "불량배·정보", "불량배 지불 부족 · 499원", "1일차 자동 사건에서 500원 선택지가 잠기는지 확인합니다.", "day1-corridor-thug", 499, day: 1);
        Add("thug-paid", "불량배·정보", "불량배에게 이미 지불함", "완료 플래그는 비워 사건을 재생하고 기존 지불 기록 분기를 확인합니다.", "day1-corridor-thug", enabled: new[] { "day1_thug_choice" }, day: 1);
        Add("information-short", "불량배·정보", "정보 구매 부족 · 49원", "2일차 조우 완료 후 50원 정보 선택지의 부족 안내를 확인합니다.", "day2-thug-information", 49, new[] { "day2_thug_done" });
        Add("information-budget", "불량배·정보", "정보 1건 구매 · 50원", "구매 후 잔액이 0원이 되어 다음 정보가 잠기는지 확인합니다.", "day2-thug-information", 50, new[] { "day2_thug_done" });

        for (int day = 0; day <= 3; day++)
            Add("tv-seen-" + day, "TV", day + "일차 · TV 시청 완료 / 재시청", "당일 시청 기록이 있는 상태에서 TV 메뉴·전원·재시청을 확인합니다.", "home_tv",
                enabled: new[] { "tv_news_d" + day + "_seen" }, day: day, scene: "Home");
        Add("wanted-poster", "거리·삼호", "시BAR 수배 전단 조사", "실제 조사 지점에서 반복·닫기를 확인합니다.", "outside_shiba_wanted_inspect");
        return result.AsReadOnly();
    }

    private static Dictionary<string, bool> Flags(string group, string[] enabled)
    {
        var flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (ResetFlags.TryGetValue(group, out var keys)) foreach (string key in keys) flags[key] = false;
        foreach (string key in enabled) flags[key] = true;
        return flags;
    }

    private static string GroupFor(string focus) => focus switch
    {
        "neighborhood-reveal" or "neighborhood-homeCat" => "부비",
        "shiba-shop" or "shiba-shop-revisit" => "상점",
        "neighborhood-repairGreeting" or "outside_elevator" => "수리·엘리베이터",
        "neighborhood-guardBlocked" or "neighborhood-bossIntro" => "경비·보스",
        "day1-corridor-thug" or "day2-corridor-thug" or "day2-thug-information" => "불량배·정보",
        "home_tv" or "home_tv_broadcast" => "TV",
        "home_sofa" => "취침·엔딩",
        "home_bed" or "home_book" or "home_wardrobe" or "home_exit_door" => "집 안 조사",
        _ => "거리·삼호"
    };
}
