using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public struct CutsceneSpeakerBinding
{
    public string actorId;
    public InteractiveEntity target;
}

/// <summary>Signal은 재생 시점만 정하고, 대사와 화자 정보는 CSV에서 읽는다.</summary>
public class CutsceneDialogueHandler : MonoBehaviour
{
    [SerializeField] PlayableDirector director;
    [SerializeField] UIDialogueTextView view;
    [SerializeField] UIOutsideTracker playerTracker;
    [SerializeField] UIOutsideTracker npcTracker;
    [SerializeField] NewStreetDataSO scriptData;
    [SerializeField] NewCharacterDataSO characters;
    [SerializeField] string dialogueSceneId;
    [SerializeField] CutsceneSpeakerBinding[] speakerBindings = Array.Empty<CutsceneSpeakerBinding>();
    IConditionUtil conditions;
    int index;
    public Exception LastError { get; private set; }

    sealed class LineRun
    {
        public CancellationTokenSource Source;
        public readonly UniTaskCompletionSource Finished = new();
    }
    LineRun active;
    void OnEnable() { if (director != null) director.stopped += OnStopped; }
    void OnStopped(PlayableDirector value) => active?.Source.Cancel();
    void OnDisable()
    {
        if (director != null) director.stopped -= OnStopped;
        active?.Source.Cancel();
    }

    public async UniTask BeginTimelineAsync(string id, IConditionUtil scope)
    {
        await StopAsync();
        dialogueSceneId = id;
        conditions = scope?.CreateExecutionScope();
        index = 0;
        LastError = null;
    }
    public async UniTask StopAsync()
    {
        var run = active;
        if (run == null) return;
        run.Source.Cancel();
        await run.Finished.Task;
    }
    public void PlayNextLine() => Next(false);
    public void PlayNextLinePause() => Next(true);
    void Next(bool pause) => RunAsync(pause).Forget(e =>
    {
        if (e is OperationCanceledException) return;
        LastError = e;
        Debug.LogError("[Timeline Dialogue] " + e.Message);
        if (director != null) director.Stop();
    });

    async UniTask RunAsync(bool pause)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var previous = active;
        var run = new LineRun { Source = source };
        active = run;
        previous?.Source.Cancel();
        try
        {
            if (previous != null) await previous.Finished.Task;
            await NewDataLoadManager.WaitUntilLoadedAsync(source.Token);
            source.Token.ThrowIfCancellationRequested();
            if (director == null || view == null || scriptData == null ||
                !scriptData.TryGetSteps(dialogueSceneId, out var steps))
                throw new InvalidOperationException("Missing Timeline CSV dialogue: " + dialogueSceneId);
            var ordered = steps.OrderBy(s => s.Seq).ToArray();
            while (index < ordered.Length && conditions != null && !conditions.CheckRequired(ordered[index].When)) index++;
            if (index >= ordered.Length) throw new InvalidOperationException("Timeline has more dialogue signals than CSV lines: " + dialogueSceneId);
            var step = ordered[index++];
            if (step.Type != "say") throw new InvalidOperationException("Timeline signal dialogue must be say: " + step.Type);
            if (conditions == null && (!string.IsNullOrEmpty(step.When) || !string.IsNullOrEmpty(step.Effects)))
                throw new InvalidOperationException("Timeline condition scope is missing.");
            bool english = GameStateManager.Instance.Language == ELanguage.En;
            string text = english && !string.IsNullOrEmpty(step.Text?.En) ? step.Text.En : step.Text?.Ko;
            if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Timeline dialogue text is empty.");
            var character = characters?.characterData?.FirstOrDefault(c => c.Id == step.Actor);
            string name = character.HasValue ? (english ? character.Value.Name.En : character.Value.Name.Ko) : step.Actor;
            if (string.IsNullOrEmpty(name)) name = step.Actor;
            bool player = step.Actor == "luna";
            var speaker = speakerBindings.FirstOrDefault(b => b.actorId == step.Actor).target;
            if (pause) director.Pause();
            if (!player && speaker != null && npcTracker != null)
            {
                npcTracker.SetTrackedTarget(speaker);
                npcTracker.gameObject.SetActive(true);
            }
            await view.StartType(new TypingData(text, name, Vector3.zero, Color.white, player), token: source.Token);
            source.Token.ThrowIfCancellationRequested();
            conditions?.ApplyRequired(step.Effects);
            if (pause && ReferenceEquals(active, run)) director.Resume();
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException) LastError = e;
            throw;
        }
        finally
        {
            if (ReferenceEquals(active, run))
            {
                if (npcTracker != null) npcTracker.StopTracking();
                if (playerTracker != null) playerTracker.StopTracking();
                if (view != null) view.ClearText();
                active = null;
            }
            run.Finished.TrySetResult();
        }
    }
}
