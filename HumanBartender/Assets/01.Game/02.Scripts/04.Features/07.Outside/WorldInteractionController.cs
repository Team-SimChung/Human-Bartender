using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>외부/홈의 새 상호작용 세션. 기존 CSV 로더와 DialogueRunner를 그대로 사용함.</summary>
public sealed class WorldInteractionController : MonoBehaviour
{
    [SerializeField] private WorldInteractionView view;
    [SerializeField] private HomeTerracePresentation homeTerrace;
    [Inject] private WorldInteractionSceneContext sceneContext;
    [Inject] private IConditionUtil conditions;
    [Inject] private IPlayerDataReader playerData;
    [Inject] private IPopupService popups;
    [Inject] private ISceneFadeService sceneFades;
    private readonly HashSet<string> notifiedEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NewSceneData> stories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> targetCounts = new(StringComparer.Ordinal);
    private int registryRevision = -1;
    private int indexedFrame = -1;
    private readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private Player player;
    private DialogueRunner runner;
    private WorldInteractionCutscenePlayer cutscenePlayer;
    private WorldInteractionTarget focused;
    private WorldInteractionTarget activeTarget;
    private CancellationTokenSource session;
    private bool ready;
    private int lastInputFrame = -1;
    private static int escapeConsumedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEscapeConsumption() => escapeConsumedFrame = -1;

    // Update와 옵션 메뉴가 같은 취소 처리를 사용함.
    // 취소가 즉시 끝나도 같은 프레임의 Esc가 옵션 메뉴까지 열지 않도록 기록함.
    public static bool TryCancelForEscape()
    {
        if (escapeConsumedFrame == Time.frameCount) return true;
        foreach (var controller in FindObjectsByType<WorldInteractionController>(FindObjectsSortMode.None))
        {
            if (!controller.isActiveAndEnabled || !controller.IsRunning) continue;
            escapeConsumedFrame = Time.frameCount;
            controller.RequestCancel();
            return true;
        }
        return false;
    }

    public bool IsRunning => session != null;
    public bool IsReady => ready;
    public bool CanSave => !IsRunning || activeTarget != null && activeTarget.Action is IWorldInteractionSavePolicy policy && policy.CanSave;
    public bool CanCancel => activeTarget == null || activeTarget.Action is not IWorldInteractionCancellationPolicy policy || policy.CanCancel;
    public WorldInteractionTarget FocusedTarget => focused;
    internal WorldInteractionTarget ActiveTarget => activeTarget;
    internal WorldInteractionView View => view;
    internal WorldInteractionCutscenePlayer CutscenePlayer => cutscenePlayer;
    public StoryExecutionResult LastResult { get; private set; }

    public static WorldInteractionController ForScene(Scene scene)
    {
        foreach (var controller in FindObjectsByType<WorldInteractionController>(FindObjectsSortMode.None))
            if (controller.gameObject.scene == scene) return controller;
        return null;
    }

    private void Awake() => WorldInteractionTarget.RegisterScene(gameObject.scene);

    private async void Start()
    {
        try
        {
            await NewDataLoadManager.WaitUntilLoadedAsync(this.GetCancellationTokenOnDestroy());
            if (!await sceneContext.WaitUntilReadyAsync()) throw new InvalidOperationException(sceneContext.InitializationError);
            if (view == null) throw new InvalidOperationException("InteractionLayer의 View가 연결되지 않았습니다.");
            player = sceneContext.Player;
            runner = sceneContext.Runner;
            if (player == null || runner == null) throw new InvalidOperationException("상호작용 Player/Runner가 없습니다.");
            var data = gameObject.scene.name == "Home" ? NewDataLoadManager.HomeStory : NewDataLoadManager.StreetStory;
            foreach (var story in data.Scenes ?? Array.Empty<NewSceneData>())
                if (story.RuntimeScene == gameObject.scene.name) stories.Add(story.Id, story);
            view.Initialize(HandleInput, RequestCancel, () => playerData.HasMoney());
            ready = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    public bool OwnsStory(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
            if (target.StorySceneId == id) return true;
        return false;
    }

    private void RefreshTargetIndex()
    {
        if (registryRevision == WorldInteractionTarget.RegistryRevision && indexedFrame == Time.frameCount) return;
        targetCounts.Clear();
        foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
        {
            string id = target.RegistryKey;
            if (string.IsNullOrWhiteSpace(id)) continue;
            targetCounts.TryGetValue(id, out int count);
            targetCounts[id] = count + 1;
        }
        registryRevision = WorldInteractionTarget.RegistryRevision;
        indexedFrame = Time.frameCount;
    }

    private bool Eligible(NewSceneData story)
    {
        var state = GameStateManager.Instance;
        return (story.StartMode == ENewSceneTrigger.Interact || story.StartMode == ENewSceneTrigger.Auto) &&
            WorldInteractionSceneRules.IsEligible(story, gameObject.scene.name, state.CurrentDay, state.GameFlow, conditions);
    }

    public bool TryResolve(WorldInteractionTarget target, out NewSceneData story)
    {
        story = default;
        if (target == null || !target.isActiveAndEnabled || target.gameObject.scene != gameObject.scene) return false;
        string id = target.RegistryKey;
        if (target.Content != null)
        {
            if (!target.Content.TryResolve(target.InteractionRadius, out story)) return false;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.StorySceneId) || !stories.TryGetValue(target.StorySceneId, out story))
            { ReportOnce("CSV scene을 찾을 수 없습니다: " + target.StorySceneId); return false; }
            if (!Eligible(story)) return false;
        }
        RefreshTargetIndex();
        if (!targetCounts.TryGetValue(id, out int count) || count != 1)
        { ReportOnce("중복된 상호작용 scene ID: " + id); return false; }
        return target.Output != WorldInteractionOutput.Action || target.Action?.CanExecute == true;
    }

    private void ReportOnce(string message)
    {
        if (reported.Add(message)) Debug.LogError("[WorldInteraction] " + message, this);
    }

    private void Update()
    {
        if (!ready) return;
        if (!IsRunning)
        {
            RefreshFocus();
            return;
        }
        if (WorldInteractionInput.EscapePressed)
        {
            TryCancelForEscape();
            return;
        }
        if (!SessionAvailable)
        {
            Cancel();
            return;
        }
        if (WorldInteractionInput.CancelPressed)
        {
            RequestCancel();
            return;
        }
        if (UIDisplayOptions.IsOptionOpen) return;
        view.MoveChoice(WorldInteractionInput.ReadChoiceDirection());
        if (WorldInteractionInput.AdvancePressed) TryHandleInput();
    }

    private bool SessionAvailable => activeTarget != null && activeTarget.isActiveAndEnabled &&
        view != null && view.isActiveAndEnabled;

    private bool CanStartSession => ready && view != null && view.isActiveAndEnabled && !IsRunning &&
        !runner.IsRunning && !UIDisplayOptions.IsOptionOpen && player.State == EInteractorState.None;

    private void RefreshFocus()
    {
        WorldInteractionTarget best = null;
        float nearest = float.PositiveInfinity;
        if (CanStartSession)
            foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
            {
                if (!TryResolve(target, out var story)) continue;
                if (target.IsAmbient) continue;
                // 씬에서 옮긴 오브젝트의 현재 위치를 기준으로 감지함.
                float distance = Vector2.Distance(player.transform.position, target.transform.position);
                if (story.StartMode == ENewSceneTrigger.Auto && distance <= story.Radius)
                {
                    PlayAsync(target).Forget(ReportFailure);
                    return;
                }
                if (distance > story.Radius || distance >= nearest) continue;
                nearest = distance;
                best = target;
            }
        SetFocus(best);
    }

    private void SetFocus(WorldInteractionTarget target)
    {
        if (focused != target)
        {
            focused?.SetFocus(false);
            focused = target;
            focused?.SetFocus(true);
        }
        if (view != null) view.ShowHint(focused);
    }

    // 한 번의 입력이 시작과 진행에 중복 적용되지 않게 함.
    public bool TryHandleInput()
    {
        if (!ready) return false;
        if (UIDisplayOptions.IsOptionOpen) return true;
        if (IsRunning)
        {
            if (lastInputFrame != Time.frameCount)
            {
                lastInputFrame = Time.frameCount;
                if (cutscenePlayer?.IsPlaying == true) cutscenePlayer.Continue();
                else if (view.IsChoosing) view.ConfirmChoice();
                else runner.AdvanceInputOutside();
            }
            return true;
        }
        if (lastInputFrame == Time.frameCount) return true;
        RefreshFocus();
        if (focused == null) return false;
        lastInputFrame = Time.frameCount;
        PlayAsync(focused).Forget(ReportFailure);
        return true;
    }

    private void HandleInput() => TryHandleInput();
    private void RequestCancel() { if (CanCancel) Cancel(); }
    public void Cancel() => session?.Cancel();

    public async UniTask<StoryExecutionResult> PlayAsync(WorldInteractionTarget target)
    {
        if (target == null || target.IsAmbient || !CanStartSession ||
            !TryResolve(target, out var story) || Vector2.Distance(player.transform.position, target.transform.position) > story.Radius)
            return new StoryExecutionResult(StoryExecutionStatus.Failed, "현재 상호작용할 수 없습니다.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        lastInputFrame = Time.frameCount;
        session = source;
        activeTarget = target;
        using var lease = InteractionStateLease.Acquire(player);
        try
        {
            player.PrepareWorldInteraction();
            SetFocus(null);
            if (story.StartMode == ENewSceneTrigger.Auto && notifiedEvents.Add(story.Id))
                await popups.Popup(PopupType.EventOccurred, "");
            source.Token.ThrowIfCancellationRequested();
            LastResult = await ExecuteOutputAsync(target, story, source.Token);
            if (target.Content != null) sceneContext.CompleteDialogue(story.Id, LastResult);
            if (LastResult.Completed && !string.IsNullOrEmpty(story.CompletionFlag))
                conditions.ApplyRequired("flag." + story.CompletionFlag + " = true");
            if (LastResult.Status == StoryExecutionStatus.Failed) Debug.LogError("[WorldInteraction] " + LastResult.Error, this);
            return LastResult;
        }
        catch (OperationCanceledException) { return LastResult = new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, e.Message);
        }
        finally
        {
            if (view != null) view.EndScene();
            activeTarget = null;
            session = null;
            if (sceneContext != null && sceneContext.IsReady)
            {
                try { sceneContext.RefreshContents(); }
                catch (Exception error) { ReportFailure(error); }
            }
        }
    }

    private async UniTask<StoryExecutionResult> ExecuteOutputAsync(WorldInteractionTarget target, NewSceneData story, CancellationToken token)
    {
        switch (target.Output)
        {
            case WorldInteractionOutput.Cutscene:
                await PlayTimelineAsync(null, token);
                break;
            case WorldInteractionOutput.Action:
                var context = new WorldInteractionActionContext(player, PlayAutomaticDialogueAsync,
                    PlayNightAsync, sceneContext.RefreshContents, ChooseMenuAsync,
                    () => homeTerrace?.PrepareMorning(player), FinishNightAsync);
                await target.Action.ExecuteAsync(context, token);
                break;
            case WorldInteractionOutput.Image:
            case WorldInteractionOutput.SpeechBubble:
                view.Begin(target, player.transform);
                runner.Bind(view);
                return await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync);
            default:
                throw new NotSupportedException("지원하지 않는 상호작용 출력: " + target.Output);
        }
        token.ThrowIfCancellationRequested();
        return new StoryExecutionResult(StoryExecutionStatus.Completed);
    }

    private UniTask<NewStreetOptionData> ChooseMenuAsync(NewStreetOptionData[] options, string message, CancellationToken token)
    {
        view.Begin(activeTarget, player.transform, player.transform);
        return view.ChooseMenuAsync(options, message, token);
    }

    private async UniTask PlayTimelineAsync(string id, CancellationToken token)
    {
        var target = activeTarget;
        if (target == null) throw new InvalidOperationException("컷씬 상호작용 대상이 사라졌습니다.");
        if (!target.TryResolveTimeline(id, out var sequence))
        {
            if (!target.IsOptionalTimeline(id))
                throw new InvalidOperationException("Target의 Timeline 목록에서 컷씬을 찾을 수 없습니다: " + id);
            Debug.LogWarning("[WorldInteraction] 미제작 컷씬을 건너뜀. Target의 Timelines에 연결하면 재생됨: " + id, target);
            return;
        }
        view.EndScene();
        if (cutscenePlayer == null)
            cutscenePlayer = GetComponent<WorldInteractionCutscenePlayer>() ?? gameObject.AddComponent<WorldInteractionCutscenePlayer>();
        await cutscenePlayer.PlayAsync(sequence, token);
        token.ThrowIfCancellationRequested();
        // 컷씬이 월드를 복원하면 같은 CSV 실행과 대화 표현기로 돌아감.
        if (target.Output != WorldInteractionOutput.Cutscene)
        {
            if (homeTerrace != null && homeTerrace.IsActive)
                view.Begin(target, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
            else view.Begin(target, player.transform);
        }
    }

    private async UniTask<StoryExecutionResult> PlayAutomaticDialogueAsync(Step[] steps, Transform anchor, CancellationToken token)
    {
        view.Begin(activeTarget, player.transform, anchor, true);
        runner.Bind(view);
        return await runner.PlayOutsideAsync(steps, token, PlayTimelineAsync, autoAdvance: true);
    }

    private async UniTask PlayNightAsync(CancellationToken token)
    {
        var state = GameStateManager.Instance;
        if (gameObject.scene.name != "Home" || state.GameFlow != EGameFlow.CommuteOut)
            throw new InvalidOperationException("귀가 후 Home에서만 취침 대화를 진행할 수 있습니다.");
        bool completed = false;
        try
        {
            foreach (var story in stories.Values.OrderBy(StoryOrder))
            {
                if (story.StartMode != ENewSceneTrigger.Manual ||
                    !WorldInteractionSceneRules.IsEligible(story, gameObject.scene.name, state.CurrentDay, state.GameFlow, conditions)) continue;
                bool terraceStory = homeTerrace != null && homeTerrace.ContainsStory(story.Id);
                view.EndScene();
                if (terraceStory) await homeTerrace.EnterAsync(sceneFades, token);
                else if (homeTerrace != null) await homeTerrace.ExitAsync(sceneFades, token);
                if (terraceStory) view.Begin(activeTarget, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
                else view.Begin(activeTarget, player.transform, player.transform);
                runner.Bind(view);
                var result = await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync);
                if (result.Status == StoryExecutionStatus.Cancelled) throw new OperationCanceledException(token);
                if (!result.Completed) throw new InvalidOperationException(result.Error);
                if (!string.IsNullOrEmpty(story.CompletionFlag)) conditions.ApplyRequired("flag." + story.CompletionFlag + " = true");
            }
            completed = true;
        }
        finally
        {
            view.EndScene();
            if (!completed) await FinishNightAsync(CancellationToken.None);
        }
    }

    private async UniTask FinishNightAsync(CancellationToken token)
    {
        if (view != null) view.EndScene();
        if (homeTerrace != null) await homeTerrace.ExitAsync(sceneFades, token);
    }

    private static int StoryOrder(NewSceneData story) => story.Seq;
    private void ReportFailure(Exception error)
    {
        if (error is not OperationCanceledException) Debug.LogException(error, this);
    }

    private void OnDisable()
    {
        Cancel();
        SetFocus(null);
        if (view != null) view.EndScene();
    }
}
