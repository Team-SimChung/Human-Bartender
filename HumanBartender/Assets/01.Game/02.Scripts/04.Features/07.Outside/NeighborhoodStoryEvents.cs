using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using static OutsideStorySteps;

/// <summary>동네 사건 규칙을 실행한다. 연출 성공을 기다린 뒤 기존 효과·진행 서비스에 상태 변경을 요청한다.</summary>
public sealed class NeighborhoodStoryEvents : IOutsideStoryEventHandler
{
    readonly IStoryContentSource content;
    public NeighborhoodStoryEvents(IStoryContentSource content) => this.content = content;

    public bool Handles(OutsideStoryCommand command) => command is OutsideStoryCommand.CatChoice or OutsideStoryCommand.HomeCatChoice
        or OutsideStoryCommand.RepairIntroChoice or OutsideStoryCommand.RepairChoice or OutsideStoryCommand.GuardChoice
        or OutsideStoryCommand.BossFirstChoice or OutsideStoryCommand.BossRepeatChoice or OutsideStoryCommand.MeetBoss
        or OutsideStoryCommand.GrantWildDog or OutsideStoryCommand.RepairElevator or OutsideStoryCommand.AdoptCat
        or OutsideStoryCommand.RevealCat or OutsideStoryCommand.PetCat or OutsideStoryCommand.FeedCat
        or OutsideStoryCommand.NeighborEnter or OutsideStoryCommand.NeighborExit or OutsideStoryCommand.ChrisEnter or OutsideStoryCommand.CatMeow;

    public async UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution run, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        switch (command)
        {
            case OutsideStoryCommand.MeetBoss: run.Effect("flag.gang_boss_met = true"); return null;
            case OutsideStoryCommand.GrantWildDog:
                if (!CampaignStoryRules.HasWildDog(run.Conditions))
                {
                    if (!run.Flag("gang_wild_dog_paid") && !run.Flag("gang_wild_dog_repair_promised"))
                        throw new InvalidOperationException("와일드 독 거래가 확정되지 않았습니다.");
                    run.Effect("flag.gang_wild_dog_received = true; flag.gang_wild_dog_paid = false; flag.gang_wild_dog_repair_promised = false; flag.gang_wild_dog_repeat_deal = false");
                }
                return null;
            case OutsideStoryCommand.RepairElevator:
                if (run.Flag("elevator_repaired")) return null;
                if (!run.Flag("elevator_repair_paid"))
                    throw new InvalidOperationException("돈이나 부품을 전달하기 전에는 수리를 완료할 수 없습니다.");
                await run.PlayAsync(OutsideStoryWorldAction.RepairElevator, token);
                run.Effect("flag.elevator_repaired = true"); return null;
            case OutsideStoryCommand.RevealCat:
                if (run.Flag("boobi_revealed")) return null;
                await run.PlayAsync(OutsideStoryWorldAction.RevealCat, token);
                run.Effect("flag.boobi_revealed = true"); return null;
            case OutsideStoryCommand.AdoptCat:
                bool wasAdopted = run.Flag("boobi_adopted");
                bool wasPending = run.Flag("boobi_feeding_pending");
                if (!wasAdopted)
                {
                    await run.PlayAsync(OutsideStoryWorldAction.AdoptCat, token);
                    if (!run.SupportsSceneContinuation)
                        run.Effect("flag.boobi_adopted = true; flag.boobi_feeding_pending = false");
                }
                if (run.SupportsSceneContinuation)
                {
                    // 표시 전환이 암전 중 성공해야 입양 상태를 기록한다. 원래 대본의 뒷부분은 새 씬에서 읽는다.
                    run.RequestTravelAndContinue(GameProgressionDestination.HomeVisit,
                        "flag.boobi_adopted = true; flag.boobi_feeding_pending = false",
                        "flag.boobi_adopted = " + (wasAdopted ? "true" : "false") +
                        "; flag.boobi_feeding_pending = " + (wasPending ? "true" : "false"));
                    return null;
                }
                // UI·입력 lease·씬 CTS 정리 후 Controller가 전역 진행 서비스에 인계한다.
                run.RequestTravel(GameProgressionDestination.HomeVisit); return null;
            case OutsideStoryCommand.PetCat: await run.PlayAsync(OutsideStoryWorldAction.PetCat, token); return null;
            case OutsideStoryCommand.FeedCat: await run.PlayAsync(OutsideStoryWorldAction.FeedCat, token); return null;
            case OutsideStoryCommand.NeighborEnter: await run.PlayAsync(OutsideStoryWorldAction.NeighborEnter, token); return null;
            case OutsideStoryCommand.NeighborExit: await run.PlayAsync(OutsideStoryWorldAction.NeighborExit, token); return null;
            case OutsideStoryCommand.ChrisEnter: await run.PlayAsync(OutsideStoryWorldAction.ChrisEnter, token); return null;
            case OutsideStoryCommand.CatMeow: await run.PlayAsync(OutsideStoryWorldAction.CatMeow, token); return null;
            default: return await ChooseAsync(command, run, token);
        }
    }

    async UniTask<Step[]> ChooseAsync(OutsideStoryCommand command, OutsideStoryExecution run, CancellationToken token)
    {
        var choices = new List<NewStreetOptionData>();
        switch (command)
        {
            case OutsideStoryCommand.CatChoice:
                choices.Add(Option("feed", "참치캔 주기", "flag.shiba_bought_tuna_can && !flag.boobi_adopted && !flag.boobi_feeding_pending"));
                goto case OutsideStoryCommand.HomeCatChoice;
            case OutsideStoryCommand.HomeCatChoice: choices.Add(Option("pet", "쓰다듬기")); break;
            case OutsideStoryCommand.RepairIntroChoice: choices.Add(Option("talk", "대화한다.")); break;
            case OutsideStoryCommand.RepairChoice:
                choices.Add(Option("gear", "기어 부품 건네주기", "flag.has_gear_part && !flag.elevator_repair_paid"));
                choices.Add(Option("pay", "비용 건네주기 · 400원", "money >= 400 && !flag.elevator_repair_paid")); break;
            case OutsideStoryCommand.GuardChoice: choices.Add(Option("ask", "술을 찾고 있어요.")); break;
            case OutsideStoryCommand.BossFirstChoice: case OutsideStoryCommand.BossRepeatChoice:
                choices.Add(Option("wait", "( 대화 그만두기 )"));
                choices.Add(Option("repair", command == OutsideStoryCommand.BossFirstChoice ? "승강기는 이미 고쳤습니다." : "승강기 고쳤습니다.", "flag.elevator_repaired && !flag.gang_wild_dog_received && !flag.shiba_bought_wild_dog && !flag.gang_wild_dog_paid && !flag.gang_wild_dog_repair_promised"));
                choices.Add(Option("pay", "돈으로 살게요 · 500원", "money >= 500 && !flag.gang_wild_dog_received && !flag.shiba_bought_wild_dog && !flag.gang_wild_dog_paid && !flag.gang_wild_dog_repair_promised")); break;
            default: throw new NotSupportedException("동네 선택 명령이 아닙니다: " + command);
        }
        choices.Add(Option("cancel", "취소"));
        var selected = await run.ChooseAsync(choices.ToArray(), token);
        string id = selected.Id;
        if (id == "cancel") return Array.Empty<Step>();
        if (command is OutsideStoryCommand.CatChoice or OutsideStoryCommand.HomeCatChoice)
        {
            if (id == "pet") return new[] { Command(OutsideStoryCommand.PetCat) };
            run.Effect("flag.boobi_feeding_pending = true"); return content.GetSceneSteps("neighborhood-feed");
        }
        if (command == OutsideStoryCommand.RepairIntroChoice)
            return content.GetSceneSteps("neighborhood-repairIntro").Concat(new[] { Effect("flag.repairman_met = true") }).ToArray();
        if (command == OutsideStoryCommand.RepairChoice)
        {
            run.Effect((id == "pay" ? "money -= 400; " : "flag.has_gear_part = false; ") + "flag.elevator_repair_paid = true");
            return content.GetSceneSteps("neighborhood-repairPaid");
        }
        if (command == OutsideStoryCommand.GuardChoice)
            return content.GetSceneSteps("neighborhood-guardPass").Concat(new[] { Effect("flag.gang_pass_granted = true") }).ToArray();
        bool first = command == OutsideStoryCommand.BossFirstChoice;
        if (id == "wait") return first ? content.GetSceneSteps("neighborhood-bossWait") : Array.Empty<Step>();
        run.Effect("flag.gang_wild_dog_repeat_deal = " + (first ? "false; " : "true; ") +
            (id == "pay" ? "money -= 500; flag.gang_wild_dog_paid = true" : "flag.gang_wild_dog_repair_promised = true"));
        return content.GetSceneSteps("neighborhood-boss" + (id == "pay" ? "Pay" : "Repair") + (first ? "First" : "Repeat"));
    }
}
