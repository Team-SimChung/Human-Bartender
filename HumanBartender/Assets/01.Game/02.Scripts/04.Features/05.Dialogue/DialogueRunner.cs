using Cysharp.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using VContainer;

public enum DialogueState { Idle, Typing, WaitingForInput, WaitingForChoice }

/// <summary>대화 한 번의 표현기·조건·입력·취소를 함께 관리함.</summary>
public class DialogueRunner : MonoBehaviour
{
    [Inject] private IConditionUtil conditionUtil;
    [Inject] private IPopupService popupService;
    [Inject] private ICutScenePlayer cutscenes;
    [SerializeField] private NewStreetDataSO StreetDataSO;
    private IDialoguePresenter presenter;
    private Execution active;

    private sealed class Execution
    {
        public IDialoguePresenter Presenter;
        public IConditionUtil Conditions;
        public CancellationTokenSource Cancellation;
        public UniTaskCompletionSource Input;
        public readonly UniTaskCompletionSource Finished = new();
        public DialogueState State;
        public int VisitedSteps;
        public Func<string, CancellationToken, UniTask> PlayTimeline;
        public bool AutoAdvance;
    }

    public DialogueState CurrentState => active?.State ?? DialogueState.Idle;
    public bool IsRunning => active != null;
    public StoryExecutionResult LastResult { get; private set; }
    public ELanguage CurrentLanguage => GameStateManager.Instance.Language;

    public void Bind(IDialoguePresenter value)
    {
        if (IsRunning) throw new InvalidOperationException("Stop and await the previous dialogue before rebinding.");
        presenter = value;
    }

    public void Stop() => active?.Cancellation.Cancel();

    public async UniTask StopAsync()
    {
        var previous = active;
        if (previous == null) return;
        previous.Cancellation.Cancel();
        // 이전 UI 정리가 끝나야 다음 대화를 시작할 수 있음.
        await previous.Finished.Task;
    }

    private void OnDisable() => Stop();

    public async UniTask<StoryExecutionResult> PlayOutsideAsync(Step[] steps, CancellationToken token = default,
        Func<string, CancellationToken, UniTask> playTimeline = null, bool autoAdvance = false)
    {
        if (active != null) return Failed("A dialogue is already running.");
        if (!Alive(presenter) || conditionUtil == null) return Failed("Dialogue presenter or conditions are missing.");
        if (steps == null || steps.Length == 0) return Failed("Dialogue has no steps.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        var run = new Execution { Presenter=presenter, Conditions=conditionUtil.CreateExecutionScope(), Cancellation=source,
            PlayTimeline=playTimeline, AutoAdvance=autoAdvance };
        active = run;
        using var dialogueAudio = GameAudioContext.Dialogue(run.Presenter.GetPlayMode() == EActivationMode.Proximity);
        var result = new StoryExecutionResult(StoryExecutionStatus.Completed);
        try
        {
            await ExecuteAsync(steps, run, 0);
            source.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { result = new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception e) { result = Failed(e.Message); }
        finally
        {
            run.Input = null;
            try { if (Alive(run.Presenter)) run.Presenter.EndScene(); }
            catch (Exception e)
            {
                result = new StoryExecutionResult(result.Completed ? StoryExecutionStatus.Failed : result.Status,
                    result.Error + " Cleanup: " + e.Message);
            }
            if (ReferenceEquals(active, run)) { active=null; LastResult=result; }
            run.Finished.TrySetResult();
        }
        return result;
    }

    public async UniTask<StoryExecutionResult> PlayOutsideAsync(string sceneId, CancellationToken token = default)
    {
        try
        {
            await NewDataLoadManager.WaitUntilLoadedAsync(token);
            if (StreetDataSO == null || !StreetDataSO.TryGetSteps(sceneId, out var steps))
                return Failed("Missing street scene: " + sceneId);
            return await PlayOutsideAsync(steps, token);
        }
        catch (OperationCanceledException) { return new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception e) { return Failed(e.Message); }
    }

    public void AdvanceInputOutside()
    {
        var run = active;
        if (run == null || run.Cancellation.IsCancellationRequested || !Alive(run.Presenter)) return;
        if (run.Presenter.GetPlayMode() == EActivationMode.Proximity) return;
        if (run.State == DialogueState.Typing) run.Presenter.SkipTyping();
        else if (run.State == DialogueState.WaitingForInput)
        {
            if (run.Input != null) (run.Presenter as IDialogueInputFeedback)?.PlayAdvanceFeedback();
            run.Presenter.HideDialogue();
            run.Input?.TrySetResult();
        }
    }

    private async UniTask ExecuteAsync(Step[] steps, Execution run, int depth)
    {
        var token = run.Cancellation.Token;
        if (depth > 64) throw new InvalidOperationException("Street result_steps nesting exceeds 64.");
        if (steps == null) return;
        // seq가 없는 기존 결과 스텝은 CSV source_order 순서를 유지함.
        var ordered = steps.All(s => s.Seq == 0) ? steps : steps.OrderBy(s => s.Seq).ToArray();
        if (ordered.Where(s=>s.Seq != 0).GroupBy(s=>s.Seq).Any(g=>g.Count()>1)) throw new InvalidOperationException("Duplicate street step seq.");
        foreach (var step in ordered)
        {
            token.ThrowIfCancellationRequested();
            if (run.Presenter is IDialogueAutomaticPlayback automatic)
                await automatic.WaitUntilReadyAsync(token);
            if (++run.VisitedSteps > 10000) throw new InvalidOperationException("Street execution exceeded its step limit.");
            if (!run.Conditions.CheckRequired(step.When)) continue;
            bool branch = false;
            switch (step.Type?.ToLowerInvariant())
            {
                case "popup":
                    await PopupCsvStep.ExecuteAsync(popupService, step.Arg, Text(step.Text), step.Sync, token);
                    break;
                case "say":
                    await SayAsync(step.Actor, Text(step.Text), step.Arg, step.Sync, run);
                    break;
                case "timeline":
                    if (!string.IsNullOrEmpty(Text(step.Text)))
                        await SayAsync(step.Actor, Text(step.Text), step.Arg, step.Sync, run);
                    // 현재 세션의 재생기만 교체함. 바와 기존 호출자는 기본 재생 경로를 유지함.
                    else if (run.PlayTimeline != null) await run.PlayTimeline(step.Arg, token);
                    else await OptionalStoryCutscene.PlayAsync(cutscenes, step.Arg, token);
                    break;
                case "effect":
                case "set_state": break;
                case "choice":
                    branch = await PlayChoiceAsync(step, run, depth);
                    break;
                case "goto":
                    if (string.IsNullOrEmpty(step.SceneId) || StreetDataSO == null || !StreetDataSO.TryGetSteps(step.SceneId, out var next))
                        throw new InvalidOperationException("Missing street goto scene: " + step.SceneId);
                    // 퀴즈 재시도를 위해 재방문을 허용하되 깊이와 실행 단계 수를 제한함.
                    await ExecuteAsync(next, run, depth + 1);
                    branch = true;
                    break;
                default: throw new NotSupportedException("Unsupported street step: " + step.Type);
            }
            token.ThrowIfCancellationRequested();
            run.Conditions.ApplyRequired(step.Effects);
            if (branch) return;
        }
    }

    private async UniTask<bool> PlayChoiceAsync(Step step, Execution run, int depth)
    {
        var token = run.Cancellation.Token;
        if (step.Options == null || step.Options.Length == 0) throw new InvalidOperationException("Street choice has no options.");
        var choices = step.Options.OrderBy(o=>o.Seq).ToArray();
        for (int i=0; i<choices.Length; i++) choices[i].Selectable = run.Conditions.CheckRequired(choices[i].When);
        if (!choices.Any(o => o.Selectable == true)) throw new InvalidOperationException("Street choice has no selectable options.");
        run.State = DialogueState.WaitingForChoice;
        var picked = new UniTaskCompletionSource<NewStreetOptionData>();
        run.Presenter.ShowOutsideChoices(choices, option => CompleteChoice(run, picked, option));
        var choice = await picked.Task.AttachExternalCancellation(token);
        token.ThrowIfCancellationRequested();
        if (!choices.Contains(choice)) throw new InvalidOperationException("Unknown street choice result.");
        if (!run.Conditions.CheckRequired(choice.When))
            await SayAsync(null, Text(choice.LockReason), null, null, run);
        else if (choice.ResultSteps != null && choice.ResultSteps.Length > 0)
        {
            await ExecuteAsync(choice.ResultSteps, run, depth + 1);
            return true;
        }
        return false;
    }

    private void CompleteChoice(Execution run, UniTaskCompletionSource<NewStreetOptionData> completion, NewStreetOptionData option)
    {
        if (ReferenceEquals(active, run) && !run.Cancellation.IsCancellationRequested)
            completion.TrySetResult(option);
    }

    private async UniTask SayAsync(string actor, string text, string arg, string sync, Execution run)
    {
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Street dialogue text is empty.");
        var token = run.Cancellation.Token;
        run.State = DialogueState.Typing;
        await run.Presenter.ShowDialogueAsync(actor, text, arg, token);
        token.ThrowIfCancellationRequested();
        run.State = DialogueState.WaitingForInput;
        if (run.Presenter is IDialogueAutomaticPlayback automatic)
            await automatic.WaitAfterLineAsync(token);
        else if (sync == "auto" || run.AutoAdvance) await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken:token);
        else
        {
            var input = new UniTaskCompletionSource();
            run.Input = input;
            var prompt = run.Presenter as IDialogueInputPrompt;
            try
            {
                prompt?.SetWaitingForInput(run.Presenter.GetPlayMode() != EActivationMode.Proximity);
                await input.Task.AttachExternalCancellation(token);
            }
            finally
            {
                if (run.Input == input) run.Input=null;
                prompt?.SetWaitingForInput(false);
            }
        }
    }

    private string Text(Texts text) => CurrentLanguage == ELanguage.En && !string.IsNullOrEmpty(text?.En) ? text.En : text?.Ko;
    private static StoryExecutionResult Failed(string error) => new(StoryExecutionStatus.Failed,error);
    private static bool Alive(object value) => value != null && (value is not UnityEngine.Object obj || obj != null);
}
