using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using VContainer;

public enum DialogueState { Idle, Typing, WaitingForInput, WaitingForChoice }

/// <summary>대화 한 번의 표현기·조건·입력·취소를 함께 관리한다.</summary>
public class DialogueRunner : MonoBehaviour
{
    [Inject] IConditionUtil conditionUtil;
    [SerializeField] NewStreetDataSO StreetDataSO;
    IDialoguePresenter presenter;
    Execution active;

    sealed class Execution
    {
        public IDialoguePresenter Presenter;
        public IConditionUtil Conditions;
        public CancellationTokenSource Cancellation;
        public UniTaskCompletionSource Input;
        public readonly UniTaskCompletionSource Finished = new();
        public DialogueState State;
        public int VisitedSteps;
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
        // 이전 UI 정리가 끝나야 다음 대화를 시작할 수 있다.
        await previous.Finished.Task;
    }

    void OnDisable() => Stop();

    public async UniTask<StoryExecutionResult> PlayOutsideAsync(Step[] steps, CancellationToken token = default)
    {
        if (active != null) return Failed("A dialogue is already running.");
        if (!Alive(presenter) || conditionUtil == null) return Failed("Dialogue presenter or conditions are missing.");
        if (steps == null || steps.Length == 0) return Failed("Dialogue has no steps.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        var run = new Execution { Presenter=presenter, Conditions=conditionUtil.CreateExecutionScope(), Cancellation=source };
        active = run;
        var result = new StoryExecutionResult(StoryExecutionStatus.Completed);
        try
        {
            await ExecuteAsync(steps, run, new HashSet<string>(), 0);
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
        else if (run.State == DialogueState.WaitingForInput) run.Input?.TrySetResult();
    }

    async UniTask ExecuteAsync(Step[] steps, Execution run, HashSet<string> gotoPath, int depth)
    {
        var token = run.Cancellation.Token;
        if (depth > 64) throw new InvalidOperationException("Street result_steps nesting exceeds 64.");
        if (steps == null) return;
        // seq가 없는 기존 결과 스텝은 CSV source_order 순서를 유지한다.
        var ordered = steps.All(s => s.Seq == 0) ? steps : steps.OrderBy(s => s.Seq).ToArray();
        if (ordered.Where(s=>s.Seq != 0).GroupBy(s=>s.Seq).Any(g=>g.Count()>1)) throw new InvalidOperationException("Duplicate street step seq.");
        foreach (var step in ordered)
        {
            token.ThrowIfCancellationRequested();
            if (++run.VisitedSteps > 10000) throw new InvalidOperationException("Street execution exceeded its step limit.");
            if (!run.Conditions.CheckRequired(step.When)) continue;
            bool branch = false;
            switch (step.Type?.ToLowerInvariant())
            {
                case "say":
                case "timeline": // Existing street Timeline dialogue markers use the dialogue presenter.
                    await SayAsync(step.Actor, Text(step.Text), step.Arg, step.Sync, run);
                    break;
                case "effect":
                case "set_state": break;
                case "choice":
                    if (step.Options == null || step.Options.Length == 0) throw new InvalidOperationException("Street choice has no options.");
                    var choices = step.Options.OrderBy(o=>o.Seq).ToArray();
                    run.State = DialogueState.WaitingForChoice;
                    var picked = new UniTaskCompletionSource<NewStreetOptionData>();
                    run.Presenter.ShowOutsideChoices(choices, option =>
                    {
                        if (ReferenceEquals(active,run) && !token.IsCancellationRequested) picked.TrySetResult(option);
                    });
                    var choice = await picked.Task.AttachExternalCancellation(token);
                    token.ThrowIfCancellationRequested();
                    if (!choices.Contains(choice)) throw new InvalidOperationException("Unknown street choice result.");
                    if (!run.Conditions.CheckRequired(choice.When))
                        await SayAsync(null, Text(choice.LockReason), null, null, run);
                    else if (choice.ResultSteps != null && choice.ResultSteps.Length > 0)
                    {
                        await ExecuteAsync(choice.ResultSteps, run, gotoPath, depth+1);
                        branch = true;
                    }
                    break;
                case "goto":
                    if (string.IsNullOrEmpty(step.SceneId) || StreetDataSO == null || !StreetDataSO.TryGetSteps(step.SceneId, out var next))
                        throw new InvalidOperationException("Missing street goto scene: " + step.SceneId);
                    if (!gotoPath.Add(step.SceneId)) throw new InvalidOperationException("Street goto cycle: " + step.SceneId);
                    await ExecuteAsync(next,run,gotoPath,depth+1);
                    gotoPath.Remove(step.SceneId);
                    branch = true;
                    break;
                default: throw new NotSupportedException("Unsupported street step: " + step.Type);
            }
            token.ThrowIfCancellationRequested();
            run.Conditions.ApplyRequired(step.Effects);
            if (branch) return;
        }
    }

    async UniTask SayAsync(string actor, string text, string arg, string sync, Execution run)
    {
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Street dialogue text is empty.");
        var token = run.Cancellation.Token;
        run.State = DialogueState.Typing;
        await run.Presenter.ShowDialogueAsync(actor, text, arg, token);
        token.ThrowIfCancellationRequested();
        run.State = DialogueState.WaitingForInput;
        if (sync == "auto") await UniTask.Delay(TimeSpan.FromSeconds(2), cancellationToken:token);
        else
        {
            var input = new UniTaskCompletionSource();
            run.Input = input;
            try { await input.Task.AttachExternalCancellation(token); }
            finally { if (run.Input == input) run.Input=null; }
        }
    }

    string Text(Texts text) => CurrentLanguage == ELanguage.En && !string.IsNullOrEmpty(text?.En) ? text.En : text?.Ko;
    static StoryExecutionResult Failed(string error) => new(StoryExecutionStatus.Failed,error);
    static bool Alive(object value) => value != null && (value is not UnityEngine.Object obj || obj != null);
}
