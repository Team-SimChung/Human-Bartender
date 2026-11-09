#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>에디터의 대사 시작점 준비. 앞 대사·컷씬·선택 입력을 재생하지 않는다.</summary>
internal sealed class BarDialogueTestStartPlan
{
    internal NewScriptSceneData ResumeScene;
    internal NewDialogueStepData[] Seats;
    internal NewDialogueStepData? OpenOrder;
    internal NewScriptSceneData OrderScene;
    internal bool CraftWasOpened;
    internal StoryResultContext PreviousResult;

    internal static BarDialogueTestStartPlan Build(NewDayScriptBase source, string sceneId, int stepSeq,
        IConditionUtil conditions, ENewGrade grade, bool orderMatch)
    {
        var scene = (source.Scenes ?? Array.Empty<NewScriptSceneData>()).Single(value => value.Id == sceneId);
        var steps = scene.Steps ?? Array.Empty<NewDialogueStepData>();
        int index = Array.FindIndex(steps, value => value.Seq == stepSeq);
        if (index < 0 || steps[index].Type is not (ENewStepType.Say or ENewStepType.Order) ||
            string.IsNullOrWhiteSpace(steps[index].Text?.Ko))
            throw new InvalidOperationException("[BarTest] 본문이 있는 대사 또는 주문 문구를 선택하세요.");
        var plan = new BarDialogueTestStartPlan();
        var seats = new Dictionary<string, NewDialogueStepData>(StringComparer.Ordinal);
        void Prepare(NewScriptSceneData owner, IEnumerable<NewDialogueStepData> prefix)
        {
            foreach (var step in prefix)
            {
                if (!conditions.CheckRequired(step.When)) continue;
                switch (step.Type)
                {
                    case ENewStepType.Enter:
                        foreach (string key in seats.Where(pair => pair.Value.Actor == step.Actor).Select(pair => pair.Key).ToArray()) seats.Remove(key);
                        seats[step.Arg] = step;
                        conditions.ApplyRequired("flag." + step.Actor + "_met = true");
                        break;
                    case ENewStepType.Exit:
                        foreach (string key in seats.Where(pair => pair.Value.Actor == step.Actor).Select(pair => pair.Key).ToArray()) seats.Remove(key);
                        break;
                    case ENewStepType.Order:
                        plan.OpenOrder = step;
                        plan.OrderScene = owner;
                        plan.CraftWasOpened = false;
                        conditions.Result = null;
                        break;
                    case ENewStepType.Craft:
                        plan.CraftWasOpened = true;
                        break;
                    case ENewStepType.Serve:
                        string cocktail = plan.OpenOrder?.Arg;
                        if (cocktail?.StartsWith("exact:", StringComparison.Ordinal) == true) cocktail = cocktail.Substring(6);
                        conditions.Result = new StoryResultContext(grade, grade, orderMatch, cocktail, cocktail);
                        plan.OpenOrder = null;
                        plan.CraftWasOpened = false;
                        break;
                }
                // 선택 항목의 결과는 고르지 않는다. 확정된 스텝의 효과만 테스트 복사본에 반영한다.
                conditions.ApplyRequired(step.Effects, owner.Id + "#" + step.Seq);
                CampaignStoryRules.TryUnlockJohnnyRecipe(owner.Day, conditions);
            }
        }
        if (scene.Phase == ENewScenePhase.Bar)
        {
            var opening = new StorySceneCursor(source, conditions, ENewScenePhase.BarOpen);
            while (opening.TryTakeNext(out var previous)) Prepare(previous, previous.Steps ?? Array.Empty<NewDialogueStepData>());
            // 개점 국면이 끝나면 실제 Runner도 좌석·주문·서빙 문맥을 정리한다.
            seats.Clear();
            plan.OpenOrder = null;
            plan.CraftWasOpened = false;
            conditions.Result = null;
        }
        var prefixSource = new NewDayScriptBase { Scenes = source.Scenes.Where(value => value.Seq < scene.Seq).ToArray() };
        var cursor = new StorySceneCursor(prefixSource, conditions, scene.Phase);
        while (cursor.TryTakeNext(out var previous))
        {
            Prepare(previous, previous.Steps ?? Array.Empty<NewDialogueStepData>());
        }
        Prepare(scene, steps.Take(index));
        plan.Seats = seats.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select((pair, i) =>
        {
            var entry = pair.Value;
            entry.Seq = i + 1;
            entry.Entrance = "seated";
            entry.When = null;
            entry.Effects = null;
            return entry;
        }).ToArray();
        var resume = steps.Skip(index).ToArray();
        var first = resume[0];
        first.When = null; // 고른 첫 대사는 표시하고, 이후 단계는 원래 조건을 따른다.
        resume[0] = first;
        scene.Steps = resume;
        plan.ResumeScene = scene;
        plan.PreviousResult = conditions.Result;
        return plan;
    }
}

public partial class StoryScriptRunner
{
    private sealed class DialogueTestStart
    {
        internal string SceneId;
        internal int StepSeq;
        internal ENewGrade Grade;
        internal bool OrderMatch;
    }
    private DialogueTestStart testStart;

    internal async UniTask<StoryExecutionResult> RunFromDialogueForTestAsync(NewDayScriptBase dayScript,
        string sceneId, int stepSeq, ENewGrade grade, bool orderMatch, CancellationToken token)
    {
        if (IsRunning || testStart != null)
            return new StoryExecutionResult(StoryExecutionStatus.Failed, "[BarTest] 앞 대사 테스트가 아직 진행 중입니다.");
        testStart = new DialogueTestStart { SceneId = sceneId, StepSeq = stepSeq, Grade = grade, OrderMatch = orderMatch };
        try { return await RunAsync(dayScript, token); }
        finally { testStart = null; }
    }

    private async UniTask RunFromTestDialogueAsync(CancellationToken token)
    {
        var plan = BarDialogueTestStartPlan.Build(script, testStart.SceneId, testStart.StepSeq,
            conditions, testStart.Grade, testStart.OrderMatch);
        var setup = plan.ResumeScene;
        setup.Steps = plan.Seats;
        walkedIn = false;
        foreach (var seat in plan.Seats) await EnterAsync(setup, seat, token);
        if (plan.Seats.Length == 0) await ApplyFramingAsync(token);
        if (plan.OpenOrder.HasValue)
        {
            var order = plan.OpenOrder.Value;
            order.Text = null;
            if (order.Arg == "free")
            {
                var recipe = (NewDataLoadManager.StoryCocktails ?? Array.Empty<NewCocktailData>()).FirstOrDefault(value =>
                    value.Status == ENewDataStatus.Confirmed && value.UnlockDay <= plan.ResumeScene.Day && conditions.CheckRequired(value.UnlockWhen));
                if (string.IsNullOrEmpty(recipe.Id)) throw new InvalidOperationException("[BarTest] 복원할 자유 주문의 레시피가 없습니다.");
                order.Arg = "exact:" + recipe.Id;
            }
            await OrderAsync(plan.OrderScene, order, token);
            if (plan.CraftWasOpened) SetCraftAuthorization(true);
        }
        conditions.Result = plan.PreviousResult;
        bool ended = await PlayFromAsync(plan.ResumeScene, token);
        if (ended) return;
        var remainder = new NewDayScriptBase { Scenes = script.Scenes.Where(value => value.Seq > plan.ResumeScene.Seq).ToArray() };
        var cursor = new StorySceneCursor(remainder, conditions, plan.ResumeScene.Phase);
        while (cursor.TryTakeNext(out var next))
        {
            if (await PlayFromAsync(next, token)) break;
        }
    }
}
#endif
