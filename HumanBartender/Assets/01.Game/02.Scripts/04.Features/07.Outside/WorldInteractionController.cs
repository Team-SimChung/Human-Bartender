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
    [Inject] private IStoryContentSource storyContent;
    [Inject] private IPlayerDataReader playerData;
    [Inject] private IPopupService popups;
    [Inject] private ISceneFadeService sceneFades;
    [Inject] private IGameProgressionService progression;
    [Inject] private ISoundEffectPlayer sound;
    private WorldStoryActors sessionActors;
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
    private bool handingOff;
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

    public bool IsRunning => session != null || handingOff;
    public bool IsReady => ready;
    public bool CanSave => !handingOff && (!IsRunning || activeTarget != null && activeTarget.Action is IWorldInteractionSavePolicy policy && policy.CanSave);
    public bool CanCancel => !handingOff && (activeTarget == null || activeTarget.Action is not IWorldInteractionCancellationPolicy policy || policy.CanCancel);
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
            foreach (var original in data.Scenes ?? Array.Empty<NewSceneData>())
            {
                var story = original;
                if (OutsideStorySceneRules.TryBind(original, gameObject.scene.name, out var bound)) story = bound;
                if (story.RuntimeScene != gameObject.scene.name) continue;
                stories.Add(story.Id, story);
                if (OutsideStorySceneRules.IsNativeEntry(story.Id) && !OwnsStory(story.Id))
                    throw new InvalidOperationException("Unity 씬에 사건 대상을 배치하세요: " + story.Id);
            }
            view.Initialize(HandleInput, RequestCancel, () => playerData.HasMoney());
            foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
            {
                if (target.Content == null && target.StorySceneId == "bd-vendor" && !stories.ContainsKey("bd-vendor"))
                {
                    target.SetFocus(false);
                    target.enabled = false;
                    Debug.LogWarning("[Story] 웹 정본에서 제거된 BD 판매상 진입을 비활성화했습니다.", target);
                }
            }
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
            var state = GameStateManager.Instance;
            if (OutsideStorySceneRules.IsNativeEntry(story.Id)) story.Radius = target.InteractionRadius;
            if (!OutsideStorySceneRules.IsAvailable(story.Id, state.CurrentDay, state.GameFlow, conditions) || !Eligible(story)) return false;
            story = OutsideStorySceneRules.ResolveEntry(story, state.CurrentDay, conditions, storyContent);
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
        if (!ready || handingOff) return;
        if (!IsRunning)
        {
            RefreshStoryActors();
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

    private void RefreshStoryActors()
    {
        foreach (var target in WorldInteractionTarget.InScene(gameObject.scene))
        {
            var actors = target.GetComponent<WorldStoryActors>();
            if (actors != null) actors.RefreshVisibility(TryResolve(target, out _), conditions);
        }
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
        if (handingOff) return true;
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
        var lease = InteractionStateLease.Acquire(player);
        try
        {
            player.PrepareWorldInteraction();
            sessionActors = target.GetComponent<WorldStoryActors>();
            sessionActors?.BeginSession();
            SetFocus(null);
            if (story.StartMode == ENewSceneTrigger.Auto && notifiedEvents.Add(story.Id))
                await popups.Popup(PopupType.EventOccurred, "");
            source.Token.ThrowIfCancellationRequested();
            LastResult = await ExecuteOutputAsync(target, story, source.Token);
            if (target.Content != null) sceneContext.CompleteDialogue(story.Id, LastResult);
            if (LastResult.Completed && !string.IsNullOrEmpty(story.CompletionFlag))
                conditions.ApplyRequired("flag." + story.CompletionFlag + " = true");
            if (LastResult.Status == StoryExecutionStatus.Failed) Debug.LogError("[WorldInteraction] " + LastResult.Error, this);
        }
        catch (OperationCanceledException) { return LastResult = new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, e.Message);
        }
        finally
        {
            try
            {
                sessionActors?.EndSession();
                sessionActors = null;
                if (view != null) view.EndScene();
            }
            finally
            {
                activeTarget = null;
                session = null;
                lease.Dispose();
                if (sceneContext != null && sceneContext.IsReady)
                {
                    try { sceneContext.RefreshContents(); }
                    catch (Exception error) { ReportFailure(error); }
                }
            }
        }
        if (LastResult.Completed && LastResult.Destination.HasValue)
        {
            // 이후의 전환 수명은 전역 진행 서비스가 소유한다. 원래 씬의 파괴 토큰을 넘기지 않는다.
            handingOff = true;
            try
            {
                var moved = await progression.EnterAsync(LastResult.Destination.Value, CancellationToken.None);
                if (!moved.Succeeded) LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, moved.Message);
            }
            catch (Exception error) { LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, error.Message); }
            finally { handingOff = false; }
        }
        return LastResult;
    }

    private async UniTask<StoryExecutionResult> ExecuteOutputAsync(WorldInteractionTarget target, NewSceneData story, CancellationToken token)
    {
        switch (target.Output)
        {
            case WorldInteractionOutput.Cutscene:
                await PlayTimelineAsync(null, null, token);
                break;
            case WorldInteractionOutput.Action:
                var context = new WorldInteractionActionContext(player, PlayAutomaticDialogueAsync,
                    PlayNightAsync, sceneContext.RefreshContents, ChooseMenuAsync,
                    () => homeTerrace?.PrepareMorning(player), FinishNightAsync);
                await target.Action.ExecuteAsync(context, token);
                break;
            case WorldInteractionOutput.Image:
            case WorldInteractionOutput.SpeechBubble:
                view.Begin(target, player.transform, speakers: sessionActors?.Speakers);
                runner.Bind(view);
                return await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync, playWorldAction: PlayWorldActionAsync);
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

    private UniTask<bool> PlayTimelineAsync(string id, string memoryKey, CancellationToken token) => PlayStudioAsync(id, memoryKey, token);

    private async UniTask<bool> PlayStudioAsync(string id, string memoryKey, CancellationToken token)
    {
        var target = activeTarget;
        if (target == null) throw new InvalidOperationException("컷씬 상호작용 대상이 사라졌습니다.");
        // Inspector의 명시적 연결을 우선하고, 나머지는 기존 StudioCatalog에서 조회한다.
        bool local = target.TryResolveTimeline(id, out var sequence);
        view.EndScene();
        if (cutscenePlayer == null)
            cutscenePlayer = GetComponent<WorldInteractionCutscenePlayer>() ?? gameObject.AddComponent<WorldInteractionCutscenePlayer>();
        try
        {
            bool played;
            if (local) { await cutscenePlayer.PlayAsync(sequence, token); played = true; }
            else
            {
                played = await cutscenePlayer.TryPlayAsync(id, token);
                if (!played && CsvStoryCutsceneProjection.TryGetSource(id, memoryKey, out var sourceId))
                {
                    using var projection = new CsvStoryCutsceneProjection(id, storyContent.GetCutsceneSteps(sourceId), GameStateManager.Instance.Language);
                    await cutscenePlayer.PlayAsync(projection.Sequence, token);
                    played = true;
                }
            }
            token.ThrowIfCancellationRequested();
            if (!played)
            {
                if (!target.IsOptionalTimeline(id))
                    throw new InvalidOperationException("필수 Cutscene Studio 에셋을 Target.Timelines 또는 StudioCatalog에 연결하세요: " + id);
                Debug.LogWarning("[WorldInteraction] 선택적 미제작 컷씬: " + id, target);
            }
            return played;
        }
        finally
        {
            if (target != null && view != null && !token.IsCancellationRequested && target.Output != WorldInteractionOutput.Cutscene)
            {
                if (homeTerrace != null && homeTerrace.IsActive)
                    view.Begin(target, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
                else if (player != null) view.Begin(target, player.transform, speakers: sessionActors?.Speakers);
            }
        }
    }

    private UniTask PlayWorldActionAsync(OutsideStoryWorldAction action, CancellationToken token)
    {
        if (sessionActors == null) throw new InvalidOperationException("사건 대상에 WorldStoryActors를 연결하세요: " + action);
        return sessionActors.PlayAsync(action, player.transform, sound, token);
    }

    private async UniTask<StoryExecutionResult> PlayAutomaticDialogueAsync(Step[] steps, Transform anchor, CancellationToken token)
    {
        view.Begin(activeTarget, player.transform, anchor, true, sessionActors?.Speakers);
        runner.Bind(view);
        return await runner.PlayOutsideAsync(steps, token, PlayTimelineAsync, autoAdvance: true, playWorldAction: PlayWorldActionAsync);
    }

    private async UniTask PlayNightAsync(CancellationToken token)
    {
        var state = GameStateManager.Instance;
        if (gameObject.scene.name != "Home" || state.GameFlow != EGameFlow.CommuteOut)
            throw new InvalidOperationException("귀가 후 Home에서만 취침 대화를 진행할 수 있습니다.");
        bool completed = false;
        try
        {
            if (conditions.CheckRequired("flag.boobi_adopted && !flag.boobi_rest_seen"))
            {
                var previous = sessionActors;
                var cat = WorldInteractionTarget.InScene(gameObject.scene).Single(target => target.StorySceneId == "neighborhood-homeCat");
                var restActors = cat.GetComponent<WorldStoryActors>();
                if (restActors == null) throw new InvalidOperationException("집 안 부비의 월드 화자를 연결하세요.");
                restActors.BeginSession();
                sessionActors = restActors;
                try
                {
                    view.Begin(activeTarget, player.transform, speakers: restActors.Speakers);
                    runner.Bind(view);
                    var rest = await runner.PlayOutsideAsync(storyContent.GetSceneSteps("neighborhood-rest"), token,
                        PlayTimelineAsync, playWorldAction: PlayWorldActionAsync);
                    if (rest.Status == StoryExecutionStatus.Cancelled) throw new OperationCanceledException(token);
                    if (!rest.Completed) throw new InvalidOperationException(rest.Error);
                    conditions.ApplyRequired("flag.boobi_rest_seen = true");
                }
                finally { restActors.EndSession(); sessionActors = previous; }
            }
            foreach (var story in stories.Values.OrderBy(StoryOrder))
            {
                if (story.StartMode != ENewSceneTrigger.Manual ||
                    !WorldInteractionSceneRules.IsEligible(story, gameObject.scene.name, state.CurrentDay, state.GameFlow, conditions)) continue;
                bool terraceStory = homeTerrace != null && homeTerrace.ContainsStory(story.Id);
                view.EndScene();
                if (terraceStory) await homeTerrace.EnterAsync(sceneFades, token);
                else if (homeTerrace != null) await homeTerrace.ExitAsync(sceneFades, token);
                if (terraceStory) view.Begin(activeTarget, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
                else view.Begin(activeTarget, player.transform, player.transform, speakers: sessionActors?.Speakers);
                runner.Bind(view);
                var result = await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync, playWorldAction: PlayWorldActionAsync);
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
