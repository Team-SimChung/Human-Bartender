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
    private IWorldInteractionParticipant sessionParticipant;
    private WorldDialogueSpeakers sessionSpeakersOverride;
    private WorldDialogueSpeakers SessionSpeakers => sessionSpeakersOverride != null ? sessionSpeakersOverride :
        activeTarget != null ? activeTarget.GetComponent<WorldDialogueSpeakers>() : null;
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
    private InteractionStateLease continuationLease;
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
#if UNITY_EDITOR
    // 대본 직접 테스트 중 기본 위치의 자동 사건·주변 대사가 먼저 시작하는 것을 막는다.
    internal bool SuppressDetectionForTest { get; set; }
    private bool testScriptOutput;
#endif
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
        if (target.Participant is { } participant && !participant.CanInteract) return false;
        string id = target.RegistryKey;
        if (target.Content != null)
        {
            if (!target.Content.TryResolve(out story)) return false;
            target.ApplyStartSettings(ref story);
            if (target.StartMode == WorldInteractionStartMode.ScriptOnly) return false;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.StorySceneId) || !stories.TryGetValue(target.StorySceneId, out story))
            { ReportOnce("CSV scene을 찾을 수 없습니다: " + target.StorySceneId); return false; }
            var state = GameStateManager.Instance;
            target.ApplyStartSettings(ref story);
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
#if UNITY_EDITOR
            if (SuppressDetectionForTest) { SetFocus(null); return; }
#endif
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
        view != null && view.isActiveAndEnabled &&
        (sessionParticipant == null || sessionParticipant is MonoBehaviour participant && participant != null && participant.isActiveAndEnabled);

    private bool CanAcquireSession => ready && view != null && view.isActiveAndEnabled && !IsRunning &&
        !runner.IsRunning && !UIDisplayOptions.IsOptionOpen && player.State == EInteractorState.None;
    private bool CanStartSession => CanAcquireSession &&
        !(progression is IGameProgressionContinuationService continuation && continuation.IsContinuingDialogue);

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
            if (target.Participant != null) continue; // 새 NPC의 출현은 대화 가능 여부와 독립적이다.
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
        return await PlayResolvedAsync(target, story, token => ExecuteOutputAsync(target, story, token));
    }

#if UNITY_EDITOR
    /// <summary>격리된 에디터 테스트에서만 호출한다. 대본 진입 조건을 생략하고 실제 세션·연출·정리를 사용한다.</summary>
    internal UniTask<StoryExecutionResult> PlayScriptForTestAsync(WorldInteractionTarget target, string scriptId)
    {
        if (target == null || !target.isActiveAndEnabled || target.gameObject.scene != gameObject.scene || !CanStartSession ||
            target.Participant is { CanInteract: false })
            return UniTask.FromResult(new StoryExecutionResult(StoryExecutionStatus.Failed,
                "현재 지점의 연출을 시작할 수 없습니다. 날짜·플래그로 지점과 NPC를 먼저 등장시키세요."));
        var data = gameObject.scene.name == "Home" ? NewDataLoadManager.HomeStory : NewDataLoadManager.StreetStory;
        var matches = (data.Scenes ?? Array.Empty<NewSceneData>()).Where(value => value.Id == scriptId).ToArray();
        if (matches.Length != 1 || matches[0].Steps == null || matches[0].Steps.Length == 0)
            return UniTask.FromResult(new StoryExecutionResult(StoryExecutionStatus.Failed,
                "현재 씬의 실행 가능한 대본 ID를 선택하세요: " + scriptId));
        var story = matches[0];
        story.StartMode = ENewSceneTrigger.Interact;
        return PlayResolvedAsync(target, story, token => PlayTestScriptOutputAsync(target, story, token));
    }

    private async UniTask<StoryExecutionResult> PlayTestScriptOutputAsync(WorldInteractionTarget target,
        NewSceneData story, CancellationToken token)
    {
        bool terrace = homeTerrace != null && homeTerrace.ContainsStory(story.Id);
        testScriptOutput = true;
        try
        {
            if (terrace) await homeTerrace.EnterAsync(sceneFades, token);
            view.Begin(target, terrace ? homeTerrace.LunaAnchor : player.transform,
                speakers: terrace ? homeTerrace.Speakers : sessionActors?.Speakers ?? SessionSpeakers);
            runner.Bind(view);
            var travel = target.GetComponent<WorldDialogueTravelSettings>();
            return await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync,
                playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync,
                continueTravel: travel != null && travel.enabled);
        }
        finally
        {
            try
            {
                if (view != null) view.EndScene();
                if (terrace && homeTerrace != null) await homeTerrace.ExitAsync(sceneFades, CancellationToken.None);
            }
            finally { testScriptOutput = false; }
        }
    }
#endif

    private async UniTask<StoryExecutionResult> PlayResolvedAsync(WorldInteractionTarget target, NewSceneData story,
        Func<CancellationToken, UniTask<StoryExecutionResult>> execute)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        lastInputFrame = Time.frameCount;
        session = source;
        activeTarget = target;
        var lease = InteractionStateLease.Acquire(player);
        WorldDialogueTravelSettings.Snapshot? travel = null;
        try
        {
            var travelSettings = target.GetComponent<WorldDialogueTravelSettings>();
            if (travelSettings != null && travelSettings.enabled) travel = travelSettings.Capture();
            player.PrepareWorldInteraction();
            sessionParticipant = target.Participant;
            sessionParticipant?.BeginInteraction();
            if (sessionParticipant is IWorldInteractionPreparationParticipant preparation)
                await preparation.PrepareInteractionAsync(source.Token);
            sessionActors = sessionParticipant == null ? target.GetComponent<WorldStoryActors>() : null;
            sessionActors?.BeginSession();
            SetFocus(null);
            if (story.StartMode == ENewSceneTrigger.Auto && notifiedEvents.Add(story.Id))
                await popups.Popup(PopupType.EventOccurred, "");
            source.Token.ThrowIfCancellationRequested();
            LastResult = await execute(source.Token);
            if (LastResult.Completed && LastResult.Continuation == null && sessionParticipant != null)
            {
                view.EndScene();
                await sessionParticipant.CompleteInteractionAsync(source.Token);
            }
            if (LastResult.Continuation == null && target.Content != null) sceneContext.CompleteDialogue(story.Id, LastResult);
            if (LastResult.Completed && LastResult.Continuation == null && !string.IsNullOrEmpty(story.CompletionFlag))
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
                try
                {
                    if (sessionParticipant is MonoBehaviour participant && participant != null)
                        sessionParticipant.EndInteraction();
                }
                finally
                {
                    sessionParticipant = null;
                    sessionActors?.EndSession();
                    sessionActors = null;
                    sessionSpeakersOverride = null;
                    if (view != null) view.EndScene();
                }
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
                if (LastResult.Continuation != null)
                {
                    if (!travel.HasValue || progression is not IGameProgressionContinuationService continuationService)
                        throw new InvalidOperationException("대화 장소 이동 설정이 연결되지 않았습니다.");
                    LastResult = await continuationService.ContinueDialogueAsync(LastResult.Destination.Value,
                        LastResult.Continuation, travel.Value, CancellationToken.None);
                    if (LastResult.Completed && !string.IsNullOrEmpty(story.CompletionFlag))
                        conditions.ApplyRequired("flag." + story.CompletionFlag + " = true");
                }
                else
                {
                    var moved = await progression.EnterAsync(LastResult.Destination.Value, CancellationToken.None);
                    if (!moved.Succeeded) LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, moved.Message);
                }
            }
            catch (Exception error) { LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, error.Message); }
            finally { handingOff = false; }
        }
        return LastResult;
    }

    internal async UniTask PrepareSceneContinuationAsync(WorldDialogueTravelSettings.Snapshot settings)
    {
        if (!CanAcquireSession) throw new InvalidOperationException("도착한 씬에서 대화를 이어 갈 수 없습니다.");
        var target = WorldInteractionTarget.InScene(gameObject.scene)
            .SingleOrDefault(value => value.StorySceneId == settings.DestinationTargetId);
        if (target == null || !target.isActiveAndEnabled)
            throw new InvalidOperationException("도착한 씬의 대화 대상이 없습니다: " + settings.DestinationTargetId);
        session = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        activeTarget = target;
        handingOff = true;
        try
        {
            continuationLease = InteractionStateLease.Acquire(player);
            player.PrepareWorldInteraction();
            var position = player.transform.position;
            player.transform.position = new Vector3(settings.PlayerPosition.x, settings.PlayerPosition.y, position.z);
            player.GetComponent<PlayerMovement2D>()?.Face(settings.FaceRight);
            SetFocus(null);
            sessionParticipant = target.Participant;
            sessionParticipant?.BeginInteraction();
            if (sessionParticipant is IWorldInteractionPreparationParticipant preparation)
                await preparation.PrepareInteractionAsync(session.Token);
            sessionActors = sessionParticipant == null ? target.GetComponent<WorldStoryActors>() : null;
            sessionActors?.BeginSession();
        }
        catch { ReleaseSceneContinuation(); throw; }
    }

    internal async UniTask<StoryExecutionResult> PlaySceneContinuationAsync(Step[] steps)
    {
        if (session == null || continuationLease == null)
            return new StoryExecutionResult(StoryExecutionStatus.Failed, "이어질 대화가 준비되지 않았습니다.");
        handingOff = false;
        var token = session.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            view.Begin(activeTarget, player.transform, speakers: sessionActors?.Speakers ?? SessionSpeakers);
            runner.Bind(view);
            LastResult = steps == null || steps.Length == 0 ? new StoryExecutionResult(StoryExecutionStatus.Completed) :
                await runner.PlayOutsideAsync(steps, token, PlayTimelineAsync,
                    playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync);
            if (LastResult.Completed && sessionParticipant != null)
            {
                view.EndScene();
                await sessionParticipant.CompleteInteractionAsync(token);
            }
            return LastResult;
        }
        catch (OperationCanceledException) { return LastResult = new StoryExecutionResult(StoryExecutionStatus.Cancelled); }
        catch (Exception error) { return LastResult = new StoryExecutionResult(StoryExecutionStatus.Failed, error.Message); }
        finally { ReleaseSceneContinuation(); }
    }

    internal void ReleaseSceneContinuation()
    {
        if (continuationLease == null) return;
        var lease = continuationLease;
        continuationLease = null;
        var source = session;
        session = null;
        try
        {
            if (sessionParticipant is MonoBehaviour participant && participant != null) sessionParticipant.EndInteraction();
        }
        finally
        {
            sessionParticipant = null;
            try
            {
                try { sessionActors?.EndSession(); }
                finally
                {
                    sessionActors = null;
                    sessionSpeakersOverride = null;
                    if (view != null) view.EndScene();
                }
            }
            finally
            {
                activeTarget = null;
                handingOff = false;
                source?.Dispose();
                lease.Dispose();
                if (sceneContext != null && sceneContext.IsReady) sceneContext.RefreshContents();
            }
        }
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
                view.Begin(target, player.transform, speakers: sessionActors?.Speakers ?? SessionSpeakers);
                runner.Bind(view);
                var travel = target.GetComponent<WorldDialogueTravelSettings>();
                return await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync,
                    playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync,
                    continueTravel: travel != null && travel.enabled);
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
            bool restoreDialogue = target != null && target.Output != WorldInteractionOutput.Cutscene;
#if UNITY_EDITOR
            restoreDialogue |= testScriptOutput;
#endif
            if (target != null && view != null && !token.IsCancellationRequested && restoreDialogue)
            {
                if (homeTerrace != null && homeTerrace.IsActive)
                    view.Begin(target, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
                else if (player != null) view.Begin(target, player.transform, speakers: sessionActors?.Speakers ?? SessionSpeakers);
            }
        }
    }

    private UniTask PlayWorldActionAsync(OutsideStoryWorldAction action, CancellationToken token)
    {
        if (sessionParticipant != null) return sessionParticipant.PlayWorldActionAsync(action, token);
        if (action == OutsideStoryWorldAction.DoorCue)
        {
            token.ThrowIfCancellationRequested();
            sound.PlaySE("SFX_door_open_close");
            return UniTask.CompletedTask;
        }
        if (sessionActors == null) throw new InvalidOperationException("사건 대상에 WorldStoryActors를 연결하세요: " + action);
        return sessionActors.PlayAsync(action, player.transform, sound, token);
    }

    private UniTask PlayNpcSequenceAsync(string sequenceId, CancellationToken token)
    {
        if (sessionParticipant is IWorldInteractionSequenceParticipant participant)
            return participant.PlaySequenceAsync(sequenceId, token);
        throw new InvalidOperationException("사건 대상에 NPC 연출 목록을 연결하세요: " + sequenceId);
    }

    private async UniTask<StoryExecutionResult> PlayAutomaticDialogueAsync(Step[] steps, Transform anchor, CancellationToken token)
    {
        view.Begin(activeTarget, player.transform, anchor, true, sessionActors?.Speakers ?? SessionSpeakers);
        runner.Bind(view);
        return await runner.PlayOutsideAsync(steps, token, PlayTimelineAsync, autoAdvance: true,
            playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync);
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
                var previousParticipant = sessionParticipant;
                var previousSpeakers = sessionSpeakersOverride;
                var cat = WorldInteractionTarget.InScene(gameObject.scene).Single(target => target.StorySceneId == "neighborhood-homeCat");
                var restParticipant = cat.Participant;
                var restSpeakers = cat.GetComponent<WorldDialogueSpeakers>();
                if (restParticipant == null || restSpeakers == null)
                    throw new InvalidOperationException("집 안 부비의 NPC Interaction과 월드 화자를 연결하세요.");
                restParticipant.BeginInteraction();
                sessionActors = null;
                sessionParticipant = restParticipant;
                sessionSpeakersOverride = restSpeakers;
                try
                {
                    if (restParticipant is IWorldInteractionPreparationParticipant preparation)
                        await preparation.PrepareInteractionAsync(token);
                    view.Begin(activeTarget, player.transform, speakers: restSpeakers);
                    runner.Bind(view);
                    var rest = await runner.PlayOutsideAsync(storyContent.GetSceneSteps("neighborhood-rest"), token,
                        PlayTimelineAsync, playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync);
                    if (rest.Status == StoryExecutionStatus.Cancelled) throw new OperationCanceledException(token);
                    if (!rest.Completed) throw new InvalidOperationException(rest.Error);
                    await restParticipant.CompleteInteractionAsync(token);
                    token.ThrowIfCancellationRequested();
                    conditions.ApplyRequired("flag.boobi_rest_seen = true");
                }
                finally
                {
                    try { restParticipant.EndInteraction(); }
                    finally
                    {
                        sessionActors = previous;
                        sessionParticipant = previousParticipant;
                        sessionSpeakersOverride = previousSpeakers;
                    }
                }
            }
            foreach (var story in stories.Values.OrderBy(StoryOrder))
            {
                if (OutsideStorySceneRules.IsNativeEntry(story.Id) || story.StartMode != ENewSceneTrigger.Manual ||
                    !WorldInteractionSceneRules.IsEligible(story, gameObject.scene.name, state.CurrentDay, state.GameFlow, conditions)) continue;
                bool terraceStory = homeTerrace != null && homeTerrace.ContainsStory(story.Id);
                view.EndScene();
                if (terraceStory) await homeTerrace.EnterAsync(sceneFades, token);
                else if (homeTerrace != null) await homeTerrace.ExitAsync(sceneFades, token);
                if (terraceStory) view.Begin(activeTarget, homeTerrace.LunaAnchor, speakers: homeTerrace.Speakers);
                else view.Begin(activeTarget, player.transform, player.transform, speakers: sessionActors?.Speakers);
                runner.Bind(view);
                var result = await runner.PlayOutsideAsync(story.Steps, token, PlayTimelineAsync,
                    playWorldAction: PlayWorldActionAsync, playNpcSequence: PlayNpcSequenceAsync);
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
