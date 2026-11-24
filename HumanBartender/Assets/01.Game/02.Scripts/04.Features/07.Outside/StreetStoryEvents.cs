using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using static OutsideStorySteps;

/// <summary>거리 사건과 공통 선택지를 처리한다. 대본은 로더의 단일 원본에서 조회한다.</summary>
public sealed class StreetStoryEvents : IOutsideStoryEventHandler
{
    internal const string WardrobePressId = "press";
    private readonly IStoryContentSource content;
    private readonly GameStateManager state;

    /// <summary>
    /// 옷장 버튼 선택지(웹 home-interactions.js choiceView). 본편의 HomeWardrobeAction과
    /// 대본 직접 테스트의 옛 CSV 흐름(home-wardrobe 장면의 choice 행)이 같이 쓴다.
    /// </summary>
    internal static NewStreetOptionData[] WardrobeOptions() =>
        new[] { Option(WardrobePressId, "누른다"), Option("cancel", "취소", isCancel: true) };
    public StreetStoryEvents(IStoryContentSource content, GameStateManager state) { this.content = content; this.state = state; }
    public bool Handles(OutsideStoryCommand command) => command is OutsideStoryCommand.SharedChoice or OutsideStoryCommand.WardrobeChoice
        or OutsideStoryCommand.ThugChoice or OutsideStoryCommand.BeggarChoice or OutsideStoryCommand.SamhoDepart
        or OutsideStoryCommand.NotionApproach or OutsideStoryCommand.NotionDealerExit or OutsideStoryCommand.NotionSamhoExit
        or OutsideStoryCommand.ThugPunch or OutsideStoryCommand.ThugDepart or OutsideStoryCommand.DoorCue
        or OutsideStoryCommand.NpcSequence;

    public async UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution run, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        switch (command)
        {
            case OutsideStoryCommand.SharedChoice:
                var shared = content.GetSharedChoices(argument);
                var projected = shared.Select((value, i) => new NewStreetOptionData
                {
                    Id = i.ToString(), Seq = value.Seq, Text = new Texts { Ko = value.Text?.Ko, En = value.Text?.En }, When = value.When,
                    LockReason = new Texts { Ko = value.LockReason?.Ko, En = value.LockReason?.En }
                }).ToArray();
                var selected = await run.ChooseAsync(projected, token);
                var original = shared[int.Parse(selected.Id)];
                run.Effect(original.Effects);
                return string.IsNullOrEmpty(original.Goto) ? null : content.GetSceneSteps(original.Goto);
            case OutsideStoryCommand.WardrobeChoice:
                // 본편 옷장은 HomeWardrobeAction이 처리한다. 이 경로는 CSV 장면을 직접 재생할 때만 탄다(그림·기록 변화 없음).
                var home = await run.ChooseAsync(WardrobeOptions(), token);
                return home.Id == WardrobePressId ? content.GetSceneSteps("home-wardrobe-pressed") : Array.Empty<Step>();
            case OutsideStoryCommand.ThugChoice:
                if (run.Flag("day1_thug_choice")) return Branch("day1-corridor-thug", "thug-pay");
                var thug = await run.ChooseAsync(Choices(content, "day1-corridor-thug"), token);
                return Branch("day1-corridor-thug", thug.Id);
            case OutsideStoryCommand.BeggarChoice:
                // 웹 outside-beggar.js choiceView: 두 번째 만남부터 "아니요.", 경고를 듣기 전에는 질문이 "???"로 잠긴다.
                bool repeat = run.Flag("street_beggar_met");
                run.Effect("flag.street_beggar_met = true");
                string refuseText = repeat ? "아니요." : "싫어요.";
                var beggar = await run.ChooseAsync(new[]
                {
                    Locked(Option("pay", "네, 믿을게요 · 200원", "money >= 200 && !flag.street_beggar_paid"), "돈이 부족합니다."),
                    Option("refuse", refuseText),
                    Locked(Option("question", run.Flag("street_beggar_warned") ? "혹시 사기꾼인가요?" : "???", "flag.street_beggar_warned"),
                        "관련 정보가 필요합니다.")
                }, token);
                if (beggar.Id == "pay") run.Effect("money -= 200; flag.street_beggar_paid = true");
                else if (beggar.Id == "refuse") run.Effect($"flag.street_beggar_refused_day{state.CurrentDay} = true");
                var reply = content.GetSceneSteps("shared-beggar-" + (beggar.Id == "pay" ? "paid" : beggar.Id == "refuse" ? "refused" : "questioned"));
                // 거절 대본의 첫 줄은 루나가 고른 말 그대로 보여 준다.
                return beggar.Id != "refuse" ? reply : reply.Select((step, i) =>
                {
                    if (i == 0 && step.Actor == "luna") step.Text = new Texts { Ko = refuseText, En = refuseText };
                    return step;
                }).ToArray();
            case OutsideStoryCommand.SamhoDepart: await run.PlayAsync(OutsideStoryWorldAction.SamhoDepart, token); return null;
            case OutsideStoryCommand.NotionApproach: await run.PlayAsync(OutsideStoryWorldAction.NotionApproach, token); return null;
            case OutsideStoryCommand.NotionDealerExit: await run.PlayAsync(OutsideStoryWorldAction.NotionDealerExit, token); return null;
            case OutsideStoryCommand.NotionSamhoExit: await run.PlayAsync(OutsideStoryWorldAction.NotionSamhoExit, token); return null;
            case OutsideStoryCommand.ThugPunch: await run.PlayAsync(OutsideStoryWorldAction.ThugPunch, token); return null;
            case OutsideStoryCommand.ThugDepart: await run.PlayAsync(OutsideStoryWorldAction.ThugDepart, token); return null;
            case OutsideStoryCommand.DoorCue: await run.PlayAsync(OutsideStoryWorldAction.DoorCue, token); return null;
            case OutsideStoryCommand.NpcSequence: await run.PlayNpcSequenceAsync(argument, token); return null;
            default: throw new NotSupportedException("거리 명령이 아닙니다: " + command);
        }
    }

    private static NewStreetOptionData Locked(NewStreetOptionData option, string reason)
    {
        option.LockReason = new Texts { Ko = reason, En = reason };
        return option;
    }

    private Step[] Branch(string sceneId, string optionId)
    {
        foreach (var step in content.GetSceneSteps(sceneId))
            foreach (var option in step.Options ?? Array.Empty<NewStreetOptionData>())
                if (option.Id == optionId)
                    // struct 복사본의 실행 조건만 보강한다. CSV 결과와 결제는 기존 효과 처리기가 소유한다.
                    return (option.ResultSteps ?? Array.Empty<Step>()).Select(value =>
                    {
                        if (optionId == "thug-pay" && !string.IsNullOrEmpty(value.Effects) && value.Effects.Contains("flag.day1_thug_choice"))
                            value.When = string.IsNullOrEmpty(value.When) ? "!flag.day1_thug_choice" : "(" + value.When + ") && !flag.day1_thug_choice";
                        return value;
                    }).ToArray();
        throw new InvalidOperationException("사건 선택 결과가 없습니다: " + sceneId + "/" + optionId);
    }
}
