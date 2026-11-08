using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>실제 Home 상호작용을 재현하는 에디터 전용 시작 상태. CSV 대본을 복제하지 않는다.</summary>
internal sealed class HomeInteractionTestScenario
{
    private const int TestMoney = 1000;
    private readonly IReadOnlyDictionary<string, bool> flagOverrides;

    internal string Id { get; }
    internal string Name { get; }
    internal string Description { get; }
    internal int Day { get; }
    internal EGameFlow Flow { get; }
    internal bool Opening { get; }
    internal string FocusId { get; }
    internal string[] ExpectedStories { get; }

    internal HomeInteractionTestScenario(string id, string name, string description, int day,
        EGameFlow flow, string focusId, string[] expectedStories = null, bool opening = false,
        IReadOnlyDictionary<string, bool> flags = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Day = day;
        Flow = flow;
        FocusId = focusId;
        ExpectedStories = expectedStories ?? Array.Empty<string>();
        Opening = opening;
        flagOverrides = flags ?? new Dictionary<string, bool>();
    }

    internal void Apply(PlayerDataSO player) => Apply(player, null);

    /// <summary>이전 시험의 돈·호감도·플래그를 모두 지우고 선택한 상황만 재현한다.</summary>
    internal void Apply(PlayerDataSO player, bool? televisionWatched)
    {
        if (player == null) throw new ArgumentNullException(nameof(player), "[Home Test] 시험용 플레이어가 없습니다.");
        var flags = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["opening_seen"] = !Opening,
            ["workshop_seen"] = Day > 1,
            ["boobi_adopted"] = false,
            ["boobi_rest_seen"] = false,
            ["gang_wild_dog_received"] = false,
            ["shiba_bought_wild_dog"] = false,
            ["shiba_bought_bitters"] = false,
            ["campaign_observation"] = false
        };
        for (int day = 0; day <= 3; day++)
        {
            flags["home_night" + day + "_seen"] = day < Day;
            flags["tv_news_d" + day + "_seen"] = false;
            flags["day" + day + "_bar_completed"] = day < Day || day == Day && Flow == EGameFlow.CommuteOut;
        }
        foreach (var pair in flagOverrides) flags[pair.Key] = pair.Value;
        if (televisionWatched.HasValue) flags["tv_news_d" + Day + "_seen"] = televisionWatched.Value;
        player.ReplaceProgress(TestMoney, 0, new Dictionary<string, int>(StringComparer.Ordinal), flags);
    }
}

/// <summary>홈 씬에 실제 연결된 현관·소파·TV·부비의 수동 검증 목록.</summary>
internal static class HomeInteractionTestScenarios
{
    internal const string CoverageNote = "홈 씬의 실제 대상: 현관, 소파, TV, 부비. " +
        "침대·옷장 대본은 CSV에만 있고 대상 연결이 없으며, 테라스 문은 예약 위치입니다. " +
        "테라스는 소파의 수면으로 실행합니다. TV 재시청은 당일 시청 플래그를 바꿔 확인합니다.";

    internal static IReadOnlyList<HomeInteractionTestScenario> All { get; } = Array.AsReadOnly(new[]
    {
        new HomeInteractionTestScenario("opening-full", "0일차 · 오프닝부터",
            "새 게임의 도입·조작 안내를 생략하지 않고 본 뒤 현관·TV를 확인합니다.",
            0, EGameFlow.CommuteIn, "home_exit_door", opening: true),
        new HomeInteractionTestScenario("morning-day0", "0일차 · 출근 / TV",
            "오프닝을 생략한 출근 상태입니다. 현관과 당일 TV의 최초 시청·재시청을 확인합니다.",
            0, EGameFlow.CommuteIn, "home_tv", new[] { "home_tv_cutscene" }),
        new HomeInteractionTestScenario("morning-day1", "1일차 · TV 자동 방송",
            "TV 범위에 접근하면 자동 방송이 실행됩니다. 이 상황은 수동 TV보다 자동 방송 정의가 우선합니다.",
            1, EGameFlow.CommuteIn, "home_tv", new[] { "home_tv_morning" }),
        new HomeInteractionTestScenario("morning-day2", "2일차 · 출근 / TV",
            "현관과 2일차 TV를 확인합니다. 시청 상태를 바꾸면 다시 본다·보지 않는다 선택지를 확인할 수 있습니다.",
            2, EGameFlow.CommuteIn, "home_tv", new[] { "home_tv_cutscene" }),
        new HomeInteractionTestScenario("morning-day3", "3일차 · 출근 / TV",
            "현관과 3일차 TV를 확인합니다. TV 최초 시청 및 재시청 분기를 반복할 수 있습니다.",
            3, EGameFlow.CommuteIn, "home_tv", new[] { "home_tv_cutscene" }),
        new HomeInteractionTestScenario("night-day0", "0일차 · 테라스와 다음 날",
            "소파에서 수면을 고르면 night0와 회상을 재생한 뒤 1일차 아침으로 돌아갑니다. 퇴근 TV도 수동 시청할 수 있습니다.",
            0, EGameFlow.CommuteOut, "home_sofa", new[] { "night0" }),
        new HomeInteractionTestScenario("night-day1", "1일차 · 작업장 → 테라스",
            "소파의 수면에서 workshop 다음 night1 순서와 테라스 진입 이동을 확인합니다. 1일차 수동 TV도 이 상황에서 확인합니다.",
            1, EGameFlow.CommuteOut, "home_sofa", new[] { "workshop", "night1" }),
        new HomeInteractionTestScenario("terrace-day1", "1일차 · 테라스만 반복",
            "작업장만 완료한 상태입니다. 소파 수면으로 night1과 Terrace의 로컬 X 진입 이동부터 확인합니다.",
            1, EGameFlow.CommuteOut, "home_sofa", new[] { "night1" }, flags: Flags("workshop_seen")),
        new HomeInteractionTestScenario("night-day2", "2일차 · 조니 기억 획득",
            "와일드 독·비터스는 있고 조니의 기억은 미확인입니다. night2의 기억 재생이 성공하면 다음 날로 진행됩니다.",
            2, EGameFlow.CommuteOut, "home_sofa", new[] { "night2" },
            flags: Flags("gang_wild_dog_received", "shiba_bought_bitters")),
        new HomeInteractionTestScenario("night-day2-blocked", "2일차 · 취침 조건 부족",
            "night2는 이미 봤지만 재료·기억 조건이 없습니다. 소파 수면에서 진행 거부 안내와 실내 복귀를 확인합니다.",
            2, EGameFlow.CommuteOut, "home_sofa", flags: Flags("home_night2_seen")),
        new HomeInteractionTestScenario("night-day2-cleared", "2일차 · 조건 충족 / 다음 날",
            "야간 대화와 재료·기억 조건을 모두 충족했습니다. 대화 재생 없이 3일차 퇴근길로 진행하는지 확인합니다.",
            2, EGameFlow.CommuteOut, "home_sofa",
            flags: Flags("home_night2_seen", "gang_wild_dog_received", "shiba_bought_bitters", "campaign_observation")),
        new HomeInteractionTestScenario("night-day3", "3일차 · 마지막 테라스 / 생존 엔딩",
            "ending과 마지막 테라스 회상을 본 뒤 삼호 생존 크레딧으로 이어지는 수면 흐름을 확인합니다.",
            3, EGameFlow.CommuteOut, "home_sofa", new[] { "ending" },
            flags: Flags("gang_wild_dog_received", "shiba_bought_bitters", "campaign_observation")),
        new HomeInteractionTestScenario("night-day3-death", "3일차 · 마지막 테라스 / 사망 엔딩",
            "삼호 사망 경로입니다. 같은 마지막 테라스 대화에서 TempEnding의 사망 크레딧으로 이어지는지 확인합니다.",
            3, EGameFlow.CommuteOut, "home_sofa", new[] { "ending" },
            flags: Flags("gang_wild_dog_received", "shiba_bought_bitters", "campaign_observation", "samho_death_route")),
        new HomeInteractionTestScenario("cat-adopted", "부비 · 쓰다듬기 / 취소",
            "입양과 첫 휴식이 끝난 부비입니다. 쓰다듬기·취소·반복 상호작용 및 크리스가 남지 않는지 확인합니다.",
            2, EGameFlow.CommuteIn, "neighborhood-homeCat", new[] { "neighborhood-homeCat" },
            flags: Flags("boobi_adopted", "boobi_rest_seen")),
        new HomeInteractionTestScenario("cat-first-rest", "부비 · 입양 후 첫 취침",
            "1일차 야간 이야기는 완료한 상태입니다. 소파 수면에서 부비·크리스 휴식 대화와 등장·퇴장만 분리해 확인합니다.",
            1, EGameFlow.CommuteOut, "home_sofa", new[] { "neighborhood-rest" },
            flags: Flags("boobi_adopted", "workshop_seen", "home_night1_seen")),
        new HomeInteractionTestScenario("sofa-menu", "소파 · 저장 / 취소 / 수면",
            "야간 이야기가 완료된 귀가 상태입니다. 저장 슬롯 페이지·취소와 다음 날 수면을 바로 확인합니다. 부비는 입양 전이라 숨겨집니다.",
            1, EGameFlow.CommuteOut, "home_sofa", flags: Flags("workshop_seen", "home_night1_seen"))
    });

    internal static HomeInteractionTestScenario Find(string id) => All.FirstOrDefault(value => value.Id == id)
        ?? throw new KeyNotFoundException("[Home Test] 시작 상황을 찾을 수 없습니다: " + id);

    private static IReadOnlyDictionary<string, bool> Flags(params string[] enabled)
    {
        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string key in enabled) values.Add(key, true);
        return values;
    }
}
