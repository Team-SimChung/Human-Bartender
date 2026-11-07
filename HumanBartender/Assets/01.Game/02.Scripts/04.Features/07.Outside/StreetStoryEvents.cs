using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using static OutsideStorySteps;

/// <summary>거리 사건과 공통 선택지를 처리한다. 대본은 로더의 단일 원본에서 조회한다.</summary>
public sealed class StreetStoryEvents : IOutsideStoryEventHandler
{
    private readonly IStoryContentSource content;
    private readonly GameStateManager state;
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
                var home = await run.ChooseAsync(new[] { Option("press", "누른다"), Option("cancel", "취소") }, token);
                return home.Id == "press" ? content.GetSceneSteps("home-wardrobe-pressed") : Array.Empty<Step>();
            case OutsideStoryCommand.ThugChoice:
                if (run.Flag("day1_thug_choice")) return Branch("day1-corridor-thug", "thug-pay");
                var thug = await run.ChooseAsync(Choices(content, "day1-corridor-thug"), token);
                return Branch("day1-corridor-thug", thug.Id);
            case OutsideStoryCommand.BeggarChoice:
                var beggar = await run.ChooseAsync(new[] { Option("pay", "네, 믿을게요 · 200원", "money >= 200 && !flag.street_beggar_paid"),
                    Option("refuse", "싫어요."), Option("question", "혹시 사기꾼인가요?", "flag.street_beggar_warned") }, token);
                if (beggar.Id == "pay") run.Effect("money -= 200; flag.street_beggar_paid = true");
                else if (beggar.Id == "refuse") run.Effect($"flag.street_beggar_refused_day{state.CurrentDay} = true");
                return content.GetSceneSteps("shared-beggar-" + (beggar.Id == "pay" ? "paid" : beggar.Id == "refuse" ? "refused" : "questioned"));
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
