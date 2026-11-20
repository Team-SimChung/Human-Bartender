using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using static OutsideStorySteps;

/// <summary>상점의 선택·거래 규칙만 담당한다. 화면과 재생기, 플레이어 값의 수명을 보유하지 않는다.</summary>
public sealed class ShopStoryEvents : IOutsideStoryEventHandler
{
    readonly IPlayerDataReader player;
    readonly GameStateManager state;
    readonly IStoryContentSource content;
    public ShopStoryEvents(IPlayerDataReader player, GameStateManager state, IStoryContentSource content)
    { this.player = player; this.state = state; this.content = content; }

    /// <summary>팔 물건이 없을 때의 웹 원문(outside-shop.js soldOut). 대본 CSV에는 없는 상점 규칙 대사다.</summary>
    public const string SoldOutLine = "이제 팔 거 없어. 꺼져, 시바.";
    const int AllowanceMoneyLimit = 300;

    public bool Handles(OutsideStoryCommand command) => command is OutsideStoryCommand.ShopIntroComplete or OutsideStoryCommand.ShopOpen
        or OutsideStoryCommand.ShopGreeting or OutsideStoryCommand.ShopItems or OutsideStoryCommand.ShopAllowance
        or OutsideStoryCommand.GrantAllowance or OutsideStoryCommand.ShopQuiz or OutsideStoryCommand.ShopSoldOut;

    /// <summary>참치캔은 2일차부터, 부비를 입양하지 않았고 아직 사지 않았을 때만 판다(웹 tunaAvailable).</summary>
    public static bool TunaAvailable(int day, IConditionUtil conditions) => day >= 2 &&
        !conditions.CheckRequired("flag.boobi_adopted") && !conditions.CheckRequired("flag.shiba_bought_tuna_can");

    /// <summary>지금 팔 수 있는 상품이 하나라도 있는지(웹 hasStock). 와일드 독은 팔지 않는다.</summary>
    public static bool HasStock(int day, IConditionUtil conditions) =>
        !conditions.CheckRequired("flag.shiba_bought_bitters") || TunaAvailable(day, conditions);

    /// <summary>비터스·와일드 독을 모두 갖고 아직 배우지 않았으면 올드 패션드 설명·퀴즈가 먼저다(웹 needsQuiz).</summary>
    public static bool NeedsQuiz(IConditionUtil conditions) => conditions.CheckRequired("flag.shiba_bought_bitters") &&
        CampaignStoryRules.HasWildDog(conditions) && !conditions.CheckRequired("flag.shiba_old_fashioned_taught");

    /// <summary>재고가 없고 퀴즈도 남지 않았으면 인사·목록 대신 판매 종료 한 줄로 끝낸다.</summary>
    public static bool IsSoldOut(int day, IConditionUtil conditions) => !NeedsQuiz(conditions) && !HasStock(day, conditions);

    public static Step[] SoldOut() => new[]
    {
        new Step { Type = "say", Actor = "shiba", Arg = "default", Text = new Texts { Ko = SoldOutLine, En = SoldOutLine }, Sync = "wait" }
    };

    public async UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution run, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        switch (command)
        {
            case OutsideStoryCommand.ShopIntroComplete: run.Effect("flag.shiba_shop_met = true"); return null;
            case OutsideStoryCommand.GrantAllowance:
                if (string.IsNullOrEmpty(argument)) throw new InvalidOperationException("용돈의 원본 CSV 효과가 없습니다.");
                if (!run.Flag("shiba_allowance_received")) run.Effect(argument + "; flag.shiba_allowance_received = true");
                return null;
            case OutsideStoryCommand.ShopSoldOut: return SoldOut();
            case OutsideStoryCommand.ShopOpen:
                // 웹 openStock 순서: 퀴즈 → 판매 종료 → 용돈 → 목록.
                if (CanTeach(run)) return Lesson();
                if (!HasStock(state.CurrentDay, run.Conditions)) return SoldOut();
                if (!run.Flag("shiba_allowance_received") && state.CurrentDay <= 2 && player.HasMoney() <= AllowanceMoneyLimit)
                    return content.GetSceneSteps("shiba-allowance");
                return new[] { Command(OutsideStoryCommand.ShopItems) };
            case OutsideStoryCommand.ShopGreeting:
                var greeting = await run.ChooseAsync(Choices(content, "shiba-shop-revisit"), token);
                return ProjectShopBranch(greeting.ResultSteps);
            case OutsideStoryCommand.ShopAllowance:
                var options = Choices(content, "shiba-allowance");
                for (int i = 0; i < options.Length; i++)
                {
                    if (options[i].Id != "allow-yes") continue;
                    options[i].When = string.IsNullOrEmpty(options[i].When) ? "!flag.shiba_allowance_received" : "(" + options[i].When + ") && !flag.shiba_allowance_received";
                }
                var allowance = await run.ChooseAsync(options, token);
                return ProjectShopBranch(allowance.ResultSteps);
            case OutsideStoryCommand.ShopQuiz:
                var answer = await run.ChooseAsync(new[] { Option("correct", "옛날 방식의 칵테일"), Option("wrong", "옛날 옛적 이야기"), Option("wrong2", "뭘 봐, 시바.") }, token);
                if (answer.Id == "correct") { run.Effect("flag.shiba_old_fashioned_taught = true"); return Array.Empty<Step>(); }
                return content.GetSceneSteps("shared-shop-lesson").Concat(new[] { Command(OutsideStoryCommand.ShopQuiz) }).ToArray();
            case OutsideStoryCommand.ShopItems:
                var choices = new List<NewStreetOptionData> { Option("cancel", "구매하지 않는다.") };
                if (!run.Flag("shiba_bought_bitters")) choices.Add(Option("bitters", "비터스 · 300원", "money >= 300"));
                if (TunaAvailable(state.CurrentDay, run.Conditions))
                    choices.Add(Option("tuna_can", "참치캔 구매 · 50원", "money >= 50"));
                var selected = await run.ChooseAsync(choices.ToArray(), token);
                if (selected.Id == "cancel") return Array.Empty<Step>();
                int cost = selected.Id == "bitters" ? 300 : 50;
                if (player.HasMoney() < cost) throw new InvalidOperationException("돈이 부족합니다.");
                run.Effect($"money -= {cost}; flag.shiba_bought_{selected.Id} = true");
                return CanTeach(run) ? Lesson() : Array.Empty<Step>();
            default: throw new NotSupportedException("상점 명령이 아닙니다: " + command);
        }
    }

    static bool CanTeach(OutsideStoryExecution run) => NeedsQuiz(run.Conditions);
    Step[] Lesson() => content.GetSceneSteps("shared-shop-quizIntro").Concat(content.GetSceneSteps("shared-shop-lesson"))
        .Concat(new[] { Command(OutsideStoryCommand.ShopQuiz) }).ToArray();

    // 오래된 stock 분기를 Unity 상점 처리기에 연결하되 문구·원본 효과는 CSV에서 그대로 사용한다.
    static Step[] ProjectShopBranch(Step[] source) => (source ?? Array.Empty<Step>()).Select(step =>
    {
        if (step.Type == "goto" && step.SceneId is "shiba-stock" or "shiba-allowance")
        {
            step.Command = step.SceneId == "shiba-stock" ? OutsideStoryCommand.ShopItems : OutsideStoryCommand.ShopOpen;
            step.Type = "event";
        }
        else if (!string.IsNullOrEmpty(step.Effects) && step.Effects.Contains("money +="))
        {
            step.Command = OutsideStoryCommand.GrantAllowance;
            step.Type = "event"; step.Arg = step.Effects; step.Effects = null;
        }
        return step;
    }).ToArray();
}
