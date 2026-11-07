using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

public enum OutsideStoryWorldAction
{
    RepairElevator, AdoptCat, RevealCat, PetCat, FeedCat, NeighborEnter, NeighborExit, ChrisEnter, CatMeow,
    SamhoDepart, NotionApproach, NotionDealerExit, NotionSamhoExit, ThugPunch, ThugDepart, DoorCue
}

/// <summary>한 대화 실행 동안만 유효한 UI·효과·연출 경계. 처리기는 실행 객체를 저장하지 않는다.</summary>
public sealed class OutsideStoryExecution
{
    public IConditionUtil Conditions { get; }
    public GameProgressionDestination? Destination { get; private set; }
    public bool SupportsSceneContinuation { get; }
    internal bool ContinueAfterTravel { get; private set; }
    internal StorySceneContinuation Continuation { get; private set; }
    private string departureEffects, rollbackEffects;
    private readonly Func<NewStreetOptionData[], CancellationToken, UniTask<NewStreetOptionData>> choose;
    private readonly Func<OutsideStoryWorldAction, CancellationToken, UniTask> playWorldAction;
    private readonly Func<string, CancellationToken, UniTask> playNpcSequence;

    public OutsideStoryExecution(IConditionUtil conditions,
        Func<NewStreetOptionData[], CancellationToken, UniTask<NewStreetOptionData>> choose,
        Func<OutsideStoryWorldAction, CancellationToken, UniTask> playWorldAction,
        Func<string, CancellationToken, UniTask> playNpcSequence = null, bool supportsSceneContinuation = false)
    {
        Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        this.choose = choose ?? throw new ArgumentNullException(nameof(choose));
        this.playWorldAction = playWorldAction;
        this.playNpcSequence = playNpcSequence;
        SupportsSceneContinuation = supportsSceneContinuation;
    }

    public bool Flag(string key) => Conditions.CheckRequired("flag." + key);
    public void Effect(string effect) => Conditions.ApplyRequired(effect);
    public void RequestTravel(GameProgressionDestination destination)
    {
        if (Destination.HasValue && Destination != destination) throw new InvalidOperationException("대화의 이동 요청이 중복되었습니다.");
        Destination = destination;
    }
    public void RequestTravelAndContinue(GameProgressionDestination destination, string effects, string rollback)
    {
        if (!SupportsSceneContinuation) throw new InvalidOperationException("대화의 씬 이동 이어읽기가 연결되지 않았습니다.");
        RequestTravel(destination);
        ContinueAfterTravel = true;
        departureEffects = effects;
        rollbackEffects = rollback;
    }
    internal void CaptureContinuation(Step[] steps) =>
        Continuation = new StorySceneContinuation(steps, departureEffects, rollbackEffects);
    public async UniTask<NewStreetOptionData> ChooseAsync(NewStreetOptionData[] options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var selected = await choose(options, token);
        token.ThrowIfCancellationRequested();
        if (!options.Contains(selected) || !Conditions.CheckRequired(selected.When))
            throw new InvalidOperationException("선택할 수 없는 사건 선택지입니다.");
        return selected;
    }

    public async UniTask PlayAsync(OutsideStoryWorldAction cutscene, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (playWorldAction == null) throw new InvalidOperationException("사건의 월드 행동 담당자가 연결되지 않았습니다: " + cutscene);
        await playWorldAction(cutscene, token);
        token.ThrowIfCancellationRequested();
    }

    public async UniTask PlayNpcSequenceAsync(string sequenceId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(sequenceId)) throw new ArgumentException("NPC 연출 이름이 비어 있습니다.", nameof(sequenceId));
        if (playNpcSequence == null) throw new InvalidOperationException("사건의 NPC 연출 담당자가 연결되지 않았습니다: " + sequenceId);
        await playNpcSequence(sequenceId, token);
        token.ThrowIfCancellationRequested();
    }
}

public interface IOutsideStoryEvents
{
    // null은 부모 대본 계속 실행, 배열은 현재 대본을 해당 후속 대본으로 교체한다.
    UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution execution, CancellationToken token);
}

public interface IOutsideStoryEventHandler
{
    bool Handles(OutsideStoryCommand command);
    UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution execution, CancellationToken token);
}

/// <summary>DI에 등록된 담당자를 선택한다. 선택 UI, 보상 규칙, 컷신 수명을 소유하지 않는다.</summary>
public sealed class OutsideStoryEvents : IOutsideStoryEvents
{
    private readonly IOutsideStoryEventHandler[] handlers;
    public OutsideStoryEvents(IEnumerable<IOutsideStoryEventHandler> handlers) => this.handlers = handlers.ToArray();
    public UniTask<Step[]> ExecuteAsync(OutsideStoryCommand command, string argument, OutsideStoryExecution execution, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        IOutsideStoryEventHandler owner = null;
        foreach (var handler in handlers)
            if (handler.Handles(command))
            {
                if (owner != null) throw new InvalidOperationException("사건 명령 담당자가 중복되었습니다: " + command);
                owner = handler;
            }
        if (owner == null) throw new NotSupportedException("사건 명령 담당자가 없습니다: " + command);
        return owner.ExecuteAsync(command, argument, execution, token);
    }
}

internal static class OutsideStorySteps
{
    internal static NewStreetOptionData[] Choices(IStoryContentSource content, string sceneId)
    {
        var groups = content.GetSceneSteps(sceneId).Where(step => step.Options?.Length > 0).ToArray();
        if (groups.Length != 1) throw new InvalidOperationException("사건의 CSV 선택지 묶음을 하나로 지정하세요: " + sceneId);
        return groups[0].Options.ToArray();
    }
    internal static Step Command(OutsideStoryCommand command) => new() { Type = "event", Command = command };
    internal static Step Effect(string effects) => new() { Type = "effect", Effects = effects };
    internal static NewStreetOptionData Option(string id, string text, string when = null) => new()
    {
        Id = id, Text = new Texts { Ko = text, En = text }, When = when,
        LockReason = new Texts { Ko = "조건이 충족되지 않았습니다." }
    };
}
