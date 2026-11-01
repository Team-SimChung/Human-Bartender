using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>proximity 대화를 그룹별로 실행함. 수동 상호작용의 입력과 플레이어 상태를 점유하지 않음.</summary>
[DisallowMultipleComponent]
public sealed class WorldAmbientDialogueController : MonoBehaviour
{
    [SerializeField, Min(0)] private float exitMargin = .2f;
    [SerializeField, Min(0)] private float minimumReadTime = 1.2f;
    [SerializeField, Min(0)] private float maximumReadTime = 4f;
    [SerializeField, Min(1)] private float charactersPerSecond = 22;
    [Inject] private WorldInteractionSceneContext context;
    [Inject] private IObjectResolver resolver;
    [Inject] private IConditionUtil conditions;
    private WorldInteractionController foreground;
    private readonly Dictionary<WorldInteractionTarget, WorldProximityEntry> entries = new();
    private readonly Dictionary<WorldInteractionTarget, Session> sessions = new();
    private readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private readonly List<Session> cancelledSessions = new();

    private sealed class Session
    {
        internal string Rule;
        internal DialogueRunner Runner;
        internal WorldAmbientDialogueView View;
        internal CancellationTokenSource Cancellation;
    }

    public bool IsRunning => sessions.Count > 0;
    public bool IsPaused => !isActiveAndEnabled || context == null || !context.IsReady ||
        UIDisplayOptions.IsOptionOpen || foreground == null || foreground.IsRunning ||
        context.Runner.IsRunning || context.Player.State != EInteractorState.None;
    internal int CompletedCount { get; private set; }

    public bool IsRunningFor(WorldInteractionContent content)
    {
        foreach (var target in sessions.Keys)
            if (target != null && target.Content == content) return true;
        return false;
    }

    internal WorldAmbientDialogueView ViewFor(WorldInteractionTarget target)
        => sessions.TryGetValue(target, out var session) ? session.View : null;
    internal DialogueRunner RunnerFor(WorldInteractionTarget target)
        => sessions.TryGetValue(target, out var session) ? session.Runner : null;

    private void Update()
    {
        if (foreground == null) foreground = GetComponent<WorldInteractionController>();
        if (foreground == null || !foreground.IsReady || context == null || !context.IsReady) return;
        cancelledSessions.Clear();
        foreach (var pair in sessions)
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Value.Rule != RuleKey(pair.Key))
                cancelledSessions.Add(pair.Value);
        foreach (var session in cancelledSessions) session.Cancellation.Cancel();
        foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
        {
            if (sessions.ContainsKey(target)) continue;
            if (!entries.TryGetValue(target, out var entry))
            {
                entry = new WorldProximityEntry();
                entries.Add(target, entry);
            }
            if (!target.IsAmbient || !foreground.TryResolve(target, out var story))
            {
                entry.Reset();
                continue;
            }
            float distance = Vector2.Distance(context.Player.transform.position, target.transform.position);
            bool entered = entry.CanStart(RuleKey(target), distance, story.Radius, exitMargin);
            if (!entered || IsPaused) continue;
            // 잘못된 대본도 같은 범위 안에서 매 프레임 다시 시도하지 않게 함.
            entry.Consume();
            PlayAsync(target, story).Forget(ReportFailure);
        }
    }

    private static string RuleKey(WorldInteractionTarget target)
    {
        var state = GameStateManager.Instance;
        return target.Content?.Definition?.Id + ":" + target.Content?.Dialogue?.Id + ":" + state.CurrentDay + ":" + state.GameFlow;
    }

    internal static void ValidateScript(WorldInteractionTarget target, NewSceneData story, Transform player)
    {
        if (target.Output != WorldInteractionOutput.SpeechBubble)
            throw new InvalidOperationException("접근 자동 대화의 Output은 SpeechBubble이어야 함: " + target.name);
        if (story.Steps == null || story.Steps.Length == 0) throw new InvalidOperationException("자동 대화의 대본이 비어 있음.");
        foreach (var step in story.Steps)
        {
            switch (step.Type?.ToLowerInvariant())
            {
                case "say":
                    if (target.ResolveSpeaker(step.Actor, player) == null)
                        throw new InvalidOperationException("자동 대화의 화자를 연결하세요: " + step.Actor);
                    break;
                case "effect":
                case "set_state":
                    break;
                default:
                    throw new InvalidOperationException("주변 자동 대화에는 say/effect/set_state만 사용 가능함: " + step.Type);
            }
        }
    }

    private async UniTask PlayAsync(WorldInteractionTarget target, NewSceneData story)
    {
        ValidateScript(target, story, context.Player.transform);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var session = new Session { Rule = RuleKey(target), Cancellation = source };
        sessions.Add(target, session);
        try
        {
            session.View = foreground.View.CreateAmbientView();
            session.View.name = "Ambient_" + target.name;
            session.View.Begin(target, context.Player.transform, ReadPaused, minimumReadTime, maximumReadTime, charactersPerSecond);
            session.Runner = session.View.gameObject.AddComponent<DialogueRunner>();
            resolver.Inject(session.Runner);
            session.Runner.Bind(session.View);
            var result = await session.Runner.PlayOutsideAsync(story.Steps, source.Token, autoAdvance: true);
            source.Token.ThrowIfCancellationRequested();
            if (result.Status == StoryExecutionStatus.Failed) throw new InvalidOperationException(result.Error);
            if (result.Completed && target != null && session.Rule == RuleKey(target))
            {
                context.CompleteDialogue(story.Id, result);
                if (!string.IsNullOrEmpty(story.CompletionFlag)) conditions.ApplyRequired("flag." + story.CompletionFlag + " = true");
                CompletedCount++;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            sessions.Remove(target);
            if (session.View != null) Destroy(session.View.gameObject);
            if (context != null && context.IsReady && isActiveAndEnabled) context.RefreshContents();
        }
    }

    private bool ReadPaused() => IsPaused;
    private void ReportFailure(Exception error)
    {
        if (error is not OperationCanceledException && reported.Add(error.Message)) Debug.LogException(error, this);
    }

    private void OnDisable()
    {
        foreach (var session in new List<Session>(sessions.Values)) session.Cancellation.Cancel();
        foreach (var entry in entries.Values) entry.Reset();
    }
}
