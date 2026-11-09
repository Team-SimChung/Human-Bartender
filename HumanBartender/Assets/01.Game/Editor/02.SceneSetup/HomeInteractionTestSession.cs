using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

/// <summary>Home/OutSide의 실제 흐름을 복제 플레이어·별도 저장 경로로 실행하는 공용 테스트 세션.</summary>
[InitializeOnLoad]
internal static class HomeInteractionTestSession
{
    private const string Prefix = "HumanBartender.HomeInteractionTest.";
    private const string MainPath = "Assets/01.Game/01.Scenes/Main.unity";
    private static IDisposable installation;
    private static CancellationTokenSource lifetime;
    private static CancellationTokenSource barPlayback;
    private static PlayerDataSO isolatedPlayer;
    private static bool busy;
    private static readonly List<Behaviour> heldControllers = new();

    public static bool IsActive => SessionState.GetBool(Prefix + "Active", false);
    public static bool IsBusy => busy;
    public static string Status { get; private set; } = "테스트를 시작하면 별도 플레이어와 저장 경로를 사용합니다.";
    public static string SaveDirectory => SessionState.GetString(Prefix + "SaveDirectory", "");
    public static PlayerDataSO Player => isolatedPlayer;
    public static bool IsBarPlaying => barPlayback != null;
    public static PlayPhaseController BarPhase => Application.isPlaying
        ? Object.FindObjectsByType<PlayPhaseController>(FindObjectsSortMode.None)
            .FirstOrDefault(value => value.gameObject.scene == SceneManager.GetActiveScene()) : null;
    public static StoryScriptRunner BarRunner => Application.isPlaying
        ? Object.FindObjectsByType<StoryScriptRunner>(FindObjectsSortMode.None)
            .FirstOrDefault(value => value.gameObject.scene == SceneManager.GetActiveScene()) : null;
    public static WorldInteractionSceneContext Context => Application.isPlaying
        ? Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .FirstOrDefault(value => value.gameObject.scene == SceneManager.GetActiveScene()) : null;
    public static WorldInteractionController Controller => Application.isPlaying
        ? WorldInteractionController.ForScene(SceneManager.GetActiveScene()) : null;

    static HomeInteractionTestSession()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        if (IsActive && SessionState.GetBool(Prefix + "ReloadInterrupted", false))
        {
            Status = "실행 중 스크립트가 다시 로드되어 테스트를 종료합니다. 창에서 다시 시작하세요.";
            EditorApplication.delayCall += Stop;
        }
        else if (IsActive && EditorApplication.isPlayingOrWillChangePlaymode)
        {
            InstallOverrides();
        }
        else if (SessionState.GetBool(Prefix + "RestorePending", false))
        {
            Cleanup();
        }
    }

    public static void Start(string scenarioId, string focusKey = null, string scriptId = null)
    {
        HomeInteractionTestScenarios.Find(scenarioId);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[HomeTest] 현재 Play 모드를 종료한 뒤 테스트를 시작하세요.");
        var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath);
        if (main == null) throw new InvalidOperationException("[HomeTest] Main 씬을 찾을 수 없습니다.");
        var state = GameStateManager.Instance;
        SessionState.SetInt(Prefix + "PreviousDay", state.CurrentDay);
        SessionState.SetInt(Prefix + "PreviousFlow", (int)state.GameFlow);
        SessionState.SetInt(Prefix + "PreviousGameState", (int)state.CurrentGameState);
        SessionState.SetFloat(Prefix + "PreviousTimeScale", Time.timeScale);
        SessionState.SetString(Prefix + "PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Prefix + "Scenario", scenarioId);
        SessionState.SetString(Prefix + "FocusKey", focusKey ?? "");
        SessionState.SetString(Prefix + "ScriptId", scriptId ?? "");
        var saveDirectory = Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "Library", "HomeInteractionTests", "ManualSaves", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(saveDirectory);
        SessionState.SetString(Prefix + "SaveDirectory", saveDirectory);
        SessionState.SetBool(Prefix + "RestorePending", true);
        SessionState.SetBool(Prefix + "ReloadInterrupted", false);
        SessionState.SetBool(Prefix + "Running", false);
        SessionState.SetBool(Prefix + "Active", true);
        Status = "테스트용 Main 부트스트랩을 시작합니다.";
        EditorSceneManager.playModeStartScene = main;
        InstallOverrides();
        EditorApplication.isPlaying = true;
    }

    public static void StartBarDialogue(int day, string sceneId, int stepSeq, ENewGrade grade, bool orderMatch)
    {
        SelectBarLine(day, sceneId, stepSeq, grade, orderMatch);
        Start(BarDialogueTestCatalog.Scenario(day).Id);
    }

    public static UniTask ReloadBarDialogueAsync(int day, string sceneId, int stepSeq, ENewGrade grade, bool orderMatch)
    {
        SelectBarLine(day, sceneId, stepSeq, grade, orderMatch);
        return LoadScenarioAsync(BarDialogueTestCatalog.Scenario(day).Id);
    }

    private static void SelectBarLine(int day, string sceneId, int stepSeq, ENewGrade grade, bool orderMatch)
    {
        if (!BarDialogueTestCatalog.LoadLines(day).Any(value => value.SceneId == sceneId && value.Step.Seq == stepSeq))
            throw new ArgumentException("[BarTest] 현재 CSV에서 선택 대사를 찾을 수 없습니다.");
        SessionState.SetString(Prefix + "BarSceneId", sceneId);
        SessionState.SetInt(Prefix + "BarStepSeq", stepSeq);
        SessionState.SetInt(Prefix + "BarGrade", (int)grade);
        SessionState.SetBool(Prefix + "BarOrderMatch", orderMatch);
    }

    private static void InstallOverrides()
    {
        if (installation != null) return;
        installation = LifetimeScope.Enqueue(builder =>
        {
            if (!IsActive || builder.ApplicationOrigin is not ProjectLifetimeScope scope) return;
            if (isolatedPlayer != null)
                throw new InvalidOperationException("[HomeTest] 테스트 프로젝트 스코프가 중복 생성되었습니다.");
            using var serialized = new SerializedObject(scope);
            var original = serialized.FindProperty("playerData").objectReferenceValue as PlayerDataSO;
            if (original == null) throw new InvalidOperationException("[HomeTest] 프로젝트의 PlayerData 연결이 없습니다.");
            isolatedPlayer = Object.Instantiate(original);
            isolatedPlayer.name = "HomeInteractionTestPlayer";
            isolatedPlayer.hideFlags = HideFlags.DontSave;
            HomeInteractionTestScenarios.Find(SessionState.GetString(Prefix + "Scenario", "")).Apply(isolatedPlayer);
            ReplaceTestServices(builder, isolatedPlayer, resolver => new SaveManager(resolver.Resolve<PlayerDataSO>(),
                resolver.Resolve<GameProgressionService>(), resolver.Resolve<GameStateManager>(), SaveDirectory));
        });
    }

    internal static void ReplaceTestServices(IContainerBuilder builder, PlayerDataSO player,
        Func<IObjectResolver, SaveManager> createSaves)
        => EditorPlayTestServices.Replace(builder, player, createSaves);

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode && IsActive) InstallOverrides();
        if (change == PlayModeStateChange.EnteredPlayMode && IsActive)
        {
            SessionState.SetBool(Prefix + "Running", true);
            lifetime = new CancellationTokenSource();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadScenarioAsync(SessionState.GetString(Prefix + "Scenario", ""),
                SessionState.GetString(Prefix + "FocusKey", ""),
                SessionState.GetString(Prefix + "ScriptId", "")).Forget(ReportFailure);
        }
        if (change == PlayModeStateChange.ExitingPlayMode && IsActive)
        {
            lifetime?.Cancel();
            installation?.Dispose();
            installation = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SessionState.SetBool(Prefix + "Active", false);
            SessionState.SetBool(Prefix + "Running", false);
        }
        if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "RestorePending", false)) Cleanup();
    }

    private static void BeforeAssemblyReload()
    {
        // Play 진입의 정상 도메인 리로드와, 이미 실행 중인 스크립트 재컴파일을 구분한다.
        if (!IsActive || !SessionState.GetBool(Prefix + "Running", false)) return;
        SessionState.SetBool(Prefix + "ReloadInterrupted", true);
        lifetime?.Cancel();
        installation?.Dispose();
        installation = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsActive || !Application.isPlaying || isolatedPlayer == null) return;
        var scenario = HomeInteractionTestScenarios.Find(SessionState.GetString(Prefix + "Scenario", ""));
        if (scene.name == "Play" && scenario.RuntimeScene == "Play")
            foreach (var phase in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayPhaseController>(true)))
                phase.enabled = false; // 실제 영업·개점 대사가 Start에서 먼저 실행되는 것을 막는다.
        if (scenario.IsWorldTest && WorldInteractionTestCatalog.Scenes.Contains(scene.name))
        {
            foreach (var controller in scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<WorldInteractionController>(true)))
                controller.SuppressDetectionForTest = !string.IsNullOrEmpty(SessionState.GetString(Prefix + "ScriptId", ""));
            // 지점 테스트에서는 첫 출근 강제 보행·경계를 생략한다. 저장된 씬은 수정하지 않는다.
            if (scene.name == "OutSide")
                foreach (var context in scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<WorldInteractionSceneContext>(true)))
                {
                    using var serialized = new SerializedObject(context);
                    serialized.FindProperty("firstCommuteArrival").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            if (busy)
            {
                // 기본 스폰 위치의 자동 사건보다 선택 지점의 배치를 먼저 완료한다.
                foreach (var component in scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<MonoBehaviour>(true)).Where(value => value.enabled &&
                        (value is WorldInteractionController || value is WorldAmbientDialogueController)))
                {
                    heldControllers.Add(component);
                    component.enabled = false;
                }
            }
        }
        if (scene.name != "TempEnding") return;
        // 크레딧은 DI 대신 직렬화된 SO를 읽으므로, Start 이전에 씬 인스턴스의 연결만 바꾼다.
        foreach (var credits in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndingCredits>(true)))
        {
            using var serialized = new SerializedObject(credits);
            serialized.FindProperty("data").objectReferenceValue = isolatedPlayer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ReleaseControllers()
    {
        foreach (var controller in heldControllers) if (controller != null) controller.enabled = true;
        heldControllers.Clear();
    }

    public static async UniTask LoadScenarioAsync(string scenarioId, string focusKey = null, string scriptId = null)
    {
        RequireSession();
        if (busy) throw new InvalidOperationException("[HomeTest] 다른 테스트 준비가 진행 중입니다.");
        var scenario = HomeInteractionTestScenarios.Find(scenarioId);
        busy = true;
        try
        {
            Status = scenario.RuntimeScene + " 테스트 조건을 준비합니다.";
            Time.timeScale = 1;
            var token = lifetime.Token;
            await WaitUntilAsync(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null,
                "CSV 로딩", token);
            if (NewDataLoadManager.LastLoadError != null)
                throw new InvalidOperationException("[HomeTest] CSV 로딩 실패: " + NewDataLoadManager.LastLoadError);
            ProjectLifetimeScope root = null;
            await WaitUntilAsync(() => (root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
                .FirstOrDefault(value => value.Container != null)) != null, "프로젝트 컨테이너 준비", token);
            if (isolatedPlayer == null || root.Container.Resolve<PlayerDataSO>() != isolatedPlayer)
                throw new InvalidOperationException("[HomeTest] 테스트용 PlayerData 주입이 확인되지 않아 실행을 중단합니다.");
            var transitions = root.Container.Resolve<ISceneTransitionService>();
            var progression = root.Container.Resolve<GameProgressionService>();
            root.Container.Resolve<UIDisplayOptions>().Hide();
            if (barPlayback != null)
            {
                barPlayback.Cancel();
                await WaitUntilAsync(() => barPlayback == null, "바 대사 테스트 정리", token);
            }
            var current = Controller;
            if (current != null && current.IsRunning)
            {
                if (!current.CanCancel) throw new InvalidOperationException("[HomeTest] 취침 연출이 끝난 뒤 시나리오를 바꾸세요.");
                current.Cancel();
                await WaitUntilAsync(() => current == null || !current.IsRunning, "기존 상호작용 정리", token);
            }
            if (Context?.Runner.IsRunning == true)
                throw new InvalidOperationException("[HomeTest] 현재 대화가 끝난 뒤 시나리오를 바꾸세요.");
            await WaitUntilAsync(() => !transitions.IsBusy, "기존 화면 전환 완료", token);
            GameStateManager.Instance.CurrentGameState = GameState.Play;
            // 모든 장면에서 재시작할 수 있고 Once 기록도 초기화되도록 Main을 경유한다.
            if (SceneManager.GetActiveScene().name != "Main")
            {
                var returned = await transitions.RequestLoadScene("Main", cancellationToken: token).Completion;
                if (!returned.Succeeded) throw new InvalidOperationException("[HomeTest] Main 복귀 실패: " + returned.Message);
            }
            var player = isolatedPlayer;
            int oldMoney = player.HasMoney(), oldSkill = player.GetSkillValue();
            var oldAffinity = player.ReadAffinity();
            var oldFlags = player.ReadFlags();
            void RestorePlayer() => player.ReplaceProgress(oldMoney, oldSkill, oldAffinity, oldFlags);
            SessionState.SetString(Prefix + "Scenario", scenarioId);
            SessionState.SetString(Prefix + "FocusKey", focusKey ?? "");
            SessionState.SetString(Prefix + "ScriptId", scriptId ?? "");
            if (scenario.RuntimeScene == "Play")
            {
                scenario.Apply(player);
                GameStateManager.Instance.CurrentDay = scenario.Day;
                GameStateManager.Instance.GameFlow = EGameFlow.Bar;
                var moved = await transitions.RequestLoadScene("Play", cancellationToken: token).Completion;
                if (!moved.Succeeded) throw new InvalidOperationException("[BarTest] 바 준비 실패: " + moved.Message);
                await WaitUntilAsync(() => BarPhase != null && BarRunner != null, "바 실행기 준비", token);
                Status = "바 내부 대사 테스트 준비 완료.";
            }
            else
            {
                var loaded = scenario.Opening
                    ? await progression.StartNewGameAsync(() => scenario.Apply(player), RestorePlayer, token)
                    : await progression.RestoreHomeAsync(scenario.Day, () => scenario.Apply(player), RestorePlayer, token);
                if (!loaded.Succeeded) throw new InvalidOperationException("[HomeTest] Home 준비 실패: " + loaded.Message);
                await WaitForWorldAsync("Home", token, !scenario.IsWorldTest);
                if (!scenario.Opening)
                {
                    GameStateManager.Instance.GameFlow = scenario.Flow;
                    GameStateManager.Instance.CurrentGameState = GameState.Play;
                    if (scenario.RuntimeScene != "Home")
                    {
                        var moved = await transitions.RequestLoadScene(scenario.RuntimeScene, cancellationToken: token).Completion;
                        if (!moved.Succeeded) throw new InvalidOperationException("[WorldTest] 씬 준비 실패: " + moved.Message);
                        await WaitForWorldAsync(scenario.RuntimeScene, token, !scenario.IsWorldTest);
                    }
                    Context.RefreshContents();
                    Context.Player.TrySetPosition();
                    if (!string.IsNullOrEmpty(focusKey))
                    {
                        var target = WorldInteractionTestCatalog.FindTarget(SceneManager.GetActiveScene(), focusKey);
                        if (target == null) throw new InvalidOperationException("[WorldTest] 선택 지점을 찾을 수 없습니다: " + focusKey);
                        TeleportToTarget(target);
                    }
                    else if (!string.IsNullOrEmpty(scenario.FocusId))
                    {
                        var target = WorldInteractionTarget.InScene(SceneManager.GetActiveScene()).FirstOrDefault(value =>
                            value.Content?.SourceId == scenario.FocusId || value.StorySceneId == scenario.FocusId);
                        if (target != null) TeleportToTarget(target);
                    }
                }
                ReleaseControllers();
                await WaitForWorldAsync(scenario.RuntimeScene, token);
                Status = scenario.Opening ? "오프닝 실행 중입니다. Game 창에서 진행하세요." : scenario.RuntimeScene + " 테스트 준비 완료.";
            }
        }
        catch (OperationCanceledException) { Status = "테스트 준비가 취소되었습니다."; throw; }
        catch (Exception error) { Status = error.Message; throw; }
        finally { ReleaseControllers(); busy = false; }
        if (scenario.RuntimeScene == "Play")
        {
            await PlayBarDialogueAsync();
            return;
        }
        // 준비 잠금은 여기서 끝내고, 재생 중 입력·선택·취소는 실제 상호작용 세션에 맡긴다.
        if (!string.IsNullOrEmpty(scriptId))
        {
            await UniTask.NextFrame(PlayerLoopTiming.Update, lifetime.Token);
            await PlayScriptAsync(WorldInteractionTestCatalog.FindTarget(SceneManager.GetActiveScene(), focusKey), scriptId);
        }
    }

    public static void TeleportToTarget(WorldInteractionTarget target)
    {
        RequireSession();
        var context = Context;
        if (target == null || context == null || !context.IsReady || target.gameObject.scene != context.gameObject.scene)
            throw new InvalidOperationException("[WorldTest] 현재 게임 씬의 대상을 선택하세요.");
        if (Controller?.IsRunning == true || context.Runner.IsRunning || context.Player.State != EInteractorState.None)
            throw new InvalidOperationException("[HomeTest] 현재 상호작용이 끝난 뒤 이동하세요.");
        context.Player.PrepareWorldInteraction();
        var position = target.transform.position;
        position.z = context.Player.transform.position.z;
        context.Player.transform.position = position;
        foreach (var camera in Object.FindObjectsByType<OutsideCamera>(FindObjectsSortMode.None))
            if (camera.gameObject.scene == context.gameObject.scene) camera.SnapToFollow();
        Status = "대상 위치로 이동: " + target.name;
    }

    public static async UniTask<StoryExecutionResult> PlayTargetAsync(WorldInteractionTarget target)
    {
        RequireSession();
        if (busy) throw new InvalidOperationException("[HomeTest] 시나리오 준비가 끝난 뒤 실행하세요.");
        TeleportToTarget(target);
        if (target.IsAmbient)
            return new StoryExecutionResult(StoryExecutionStatus.Failed, "주변 대화는 이동한 위치에서 자동으로 실행됩니다.");
        Status = "상호작용 실행: " + target.name;
        var result = await Controller.PlayAsync(target);
        Status = "상호작용 결과: " + result.Status + (string.IsNullOrEmpty(result.Error) ? "" : " · " + result.Error);
        return result;
    }

    public static void Cancel()
    {
        if (IsActive && Controller != null && Controller.CanCancel) Controller.Cancel();
    }

    public static async UniTask<StoryExecutionResult> PlayScriptAsync(WorldInteractionTarget target, string scriptId)
    {
        RequireEditableState();
        if (string.IsNullOrWhiteSpace(scriptId)) throw new ArgumentException("[WorldTest] 시작할 대본 ID를 선택하세요.");
        TeleportToTarget(target);
        Controller.SuppressDetectionForTest = true;
        SessionState.SetString(Prefix + "ScriptId", scriptId);
        Status = "대본 직접 실행: " + scriptId;
        var result = await Controller.PlayScriptForTestAsync(target, scriptId);
        Status = "대본 " + scriptId + " · " + result.Status +
            (string.IsNullOrEmpty(result.Error) ? "" : " · " + result.Error);
        return result;
    }

    public static void ResumeDetection()
    {
        RequireEditableState();
        Controller.SuppressDetectionForTest = false;
        SessionState.SetString(Prefix + "ScriptId", "");
        Status = "접근 자동 감지를 재개했습니다.";
    }

    public static void CancelBarDialogue() => barPlayback?.Cancel();

    private static async UniTask PlayBarDialogueAsync()
    {
        RequireSession();
        if (busy || barPlayback != null || BarPhase == null)
            throw new InvalidOperationException("[BarTest] 다른 대사 테스트가 진행 중이거나 바 씬이 아닙니다.");
        using var playback = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        barPlayback = playback;
        string sceneId = SessionState.GetString(Prefix + "BarSceneId", "");
        int stepSeq = SessionState.GetInt(Prefix + "BarStepSeq", -1);
        try
        {
            Status = $"바 대사 실행: {sceneId} #{stepSeq}";
            var result = await BarPhase.RunDialogueForTestAsync(sceneId, stepSeq,
                (ENewGrade)SessionState.GetInt(Prefix + "BarGrade", (int)ENewGrade.Excellent),
                SessionState.GetBool(Prefix + "BarOrderMatch", true), playback.Token);
            Status = "바 대사 테스트 · " + result.Status + (string.IsNullOrEmpty(result.Error) ? "" : " · " + result.Error);
            if (result.Status == StoryExecutionStatus.Failed) Debug.LogError("[BarTest] " + result.Error);
        }
        finally { if (ReferenceEquals(barPlayback, playback)) barPlayback = null; }
    }

    public static void SetFlag(string key, bool value)
    {
        RequireEditableState();
        isolatedPlayer.AddFlag(key, value);
        Context.RefreshContents();
    }

    public static void SetMoney(int amount)
    {
        RequireEditableState();
        isolatedPlayer.SetMoney(amount);
        Context.RefreshContents();
    }

    private static void RequireEditableState()
    {
        RequireSession();
        if (busy || isolatedPlayer == null || Context == null || !Context.IsReady ||
            Controller == null || Controller.IsRunning || Context.Runner.IsRunning ||
            Context.Player.State != EInteractorState.None)
            throw new InvalidOperationException("[WorldTest] 준비·대화·연출이 끝난 뒤 조건을 바꾸세요.");
    }

    private static async UniTask WaitForWorldAsync(string sceneName, CancellationToken token, bool requireController = true)
    {
        await WaitUntilAsync(() =>
        {
            var context = Context;
            if (context == null || context.gameObject.scene.name != sceneName) return false;
            if (!string.IsNullOrEmpty(context.InitializationError))
                throw new InvalidOperationException("[WorldTest] " + context.InitializationError);
            return context.IsReady && (!requireController || Controller != null && Controller.IsReady);
        }, sceneName + " 상호작용 준비", token);
    }

    public static void Stop()
    {
        if (!IsActive) return;
        lifetime?.Cancel();
        barPlayback?.Cancel();
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else Cleanup();
    }

    private static void Cleanup()
    {
        barPlayback?.Cancel();
        barPlayback = null;
        ReleaseControllers();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        installation?.Dispose();
        installation = null;
        if (isolatedPlayer != null) Object.DestroyImmediate(isolatedPlayer);
        isolatedPlayer = null;
        var previous = SessionState.GetString(Prefix + "PreviousStartScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        var state = GameStateManager.Instance;
        state.CurrentDay = SessionState.GetInt(Prefix + "PreviousDay", 0);
        state.GameFlow = (EGameFlow)SessionState.GetInt(Prefix + "PreviousFlow", (int)EGameFlow.Bar);
        state.CurrentGameState = (GameState)SessionState.GetInt(Prefix + "PreviousGameState", (int)GameState.Play);
        Time.timeScale = SessionState.GetFloat(Prefix + "PreviousTimeScale", 1);
        SessionState.SetBool(Prefix + "Active", false);
        SessionState.SetBool(Prefix + "RestorePending", false);
        SessionState.SetBool(Prefix + "ReloadInterrupted", false);
        SessionState.SetBool(Prefix + "Running", false);
        busy = false;
        Status = "테스트가 종료되었습니다. 시작 씬 설정을 복원했습니다.";
    }

    private static void RequireSession()
    {
        if (!IsActive || !Application.isPlaying || lifetime == null)
            throw new InvalidOperationException("[HomeTest] 창의 테스트 시작 버튼으로 Play 모드를 시작하세요.");
    }

    private static async UniTask WaitUntilAsync(Func<bool> ready, string operation, CancellationToken token)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!ready())
        {
            token.ThrowIfCancellationRequested();
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("[HomeTest] 시간 초과: " + operation);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private static void ReportFailure(Exception error)
    {
        if (error is OperationCanceledException) return;
        Status = error.Message;
        Debug.LogError("[HomeTest] " + error.Message);
    }
}
