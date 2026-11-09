using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>실제 거리 지점의 등장·분기 조건을 준비하는 Home 테스트와 같은 형식의 프리셋.</summary>
internal static class OutsideInteractionTestScenarios
{
    internal const string Prefix = "outside-test-";
    internal const string MenuPath = "Tools/Human Bartender/Tests/Outside Interaction Lab";
    internal const string CoverageNote = "거리의 문·라디오·불량배·삼호·시BAR·부비·수리공·경비·보스·엘리베이터를 확인합니다. " +
        "구형 bubi/shiba/포스터 Source 대상 일부는 현재 CSV의 0~3일차 등장 조건이 닫혀 있어 비활성으로 표시됩니다. " +
        "활성 NPC는 실제 사건 지점을 사용하며, 조건을 강제로 바꾸지 않습니다.";

    internal static IReadOnlyList<HomeInteractionTestScenario> All { get; } = Array.AsReadOnly(new[]
    {
        Case("bar-door", "0일차 · 출근 / 바 입장", 0, EGameFlow.CommuteIn, "street_bar_door"),
        Case("home-door", "0일차 · 퇴근 / 집 입장", 0, EGameFlow.CommuteOut, "street_home_door"),
        Case("radio", "0일차 · 퇴근 / 엘리베이터 라디오", 0, EGameFlow.CommuteOut, "elevator_radio"),
        Case("thug-first", "1일차 · 불량배 첫 조우", 1, EGameFlow.CommuteIn, "day1-corridor-thug"),
        Case("thug-repeat", "2일차 · 불량배 재회", 2, EGameFlow.CommuteIn, "day2-corridor-thug"),
        Case("thug-information", "2일차 · 불량배 정보 구매", 2, EGameFlow.CommuteIn, "day2-thug-information", "day2_thug_done"),
        Case("samho-day2", "2일차 · 삼호 출근길", 2, EGameFlow.CommuteIn, "commute2", "day2_thug_done"),
        Case("samho-day3", "3일차 · 삼호 출근길", 3, EGameFlow.CommuteIn, "commute3"),
        Case("samho-trade", "1일차 · 퇴근 / 삼호와 밀수업자", 1, EGameFlow.CommuteOut, "outside_day1_samho_smuggler"),
        Case("samho-contract", "3일차 · 퇴근 / 삼호 계약 종료", 3, EGameFlow.CommuteOut, "day3-samho-contract"),
        Case("shop-first", "1일차 · 퇴근 / 시BAR 첫 방문", 1, EGameFlow.CommuteOut, "shiba-shop"),
        Case("shop-repeat", "2일차 · 시BAR 재방문", 2, EGameFlow.CommuteIn, "shiba-shop-revisit", "shiba_shop_met"),
        Case("cat-reveal", "2일차 · 부비 발견 / 입양", 2, EGameFlow.CommuteIn, "neighborhood-reveal"),
        Case("cat-feed", "2일차 · 부비 먹이 주기", 2, EGameFlow.CommuteIn, "neighborhood-reveal", "boobi_revealed", "boobi_feeding_pending"),
        Case("repair-first", "1일차 · 수리공 첫 대화", 1, EGameFlow.CommuteIn, "neighborhood-repairGreeting"),
        Case("repair-paid", "1일차 · 수리비 지불 후", 1, EGameFlow.CommuteIn, "neighborhood-repairGreeting", "repairman_met", "elevator_repair_paid"),
        Case("guard-first", "1일차 · 경비 통행 제한", 1, EGameFlow.CommuteIn, "neighborhood-guardBlocked"),
        Case("guard-menu", "2일차 · 경비 통행 선택지", 2, EGameFlow.CommuteIn, "neighborhood-guardBlocked"),
        Case("boss-first", "2일차 · 보스 첫 거래", 2, EGameFlow.CommuteIn, "neighborhood-bossIntro", "gang_pass_granted"),
        Case("boss-repeat", "2일차 · 보스 재거래", 2, EGameFlow.CommuteIn, "neighborhood-bossIntro", "gang_pass_granted", "gang_boss_met"),
        Case("elevator", "1일차 · 엘리베이터 탑승", 1, EGameFlow.CommuteIn, "outside_elevator")
    });

    internal static HomeInteractionTestScenario Find(string id) => All.FirstOrDefault(value => value.Id == id)
        ?? throw new KeyNotFoundException("[OutsideTest] 준비 상황을 찾을 수 없습니다: " + id);

    private static HomeInteractionTestScenario Case(string id, string name, int day, EGameFlow flow,
        string focus, params string[] enabledFlags)
    {
        var flags = enabledFlags.ToDictionary(key => key, _ => true, StringComparer.Ordinal);
        return new HomeInteractionTestScenario(Prefix + id, name,
            "실제 거리 씬의 대상 위치에서 시작합니다. 접근 자동 사건은 Game 창에서 진행하고, " +
            "대화·선택·취소 후 같은 상황을 다시 불러와 반복합니다.", day, flow, focus,
            flags: flags, runtimeScene: "OutSide");
    }
}
