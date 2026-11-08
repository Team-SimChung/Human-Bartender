using System;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Diagnostics;
using VContainer.Unity;
using Object = UnityEngine.Object;

/// <summary>실제 홈 흐름을 복제 플레이어·별도 저장 경로로 실행하는 에디터 전용 세션.</summary>
[InitializeOnLoad]
internal static class HomeInteractionTestSession
{
    private const string Prefix = "HumanBartender.HomeInteractionTest.";
    private const string MainPath = "Assets/01.Game/01.Scenes/Main.unity";
    private static IDisposable installation;
    private static CancellationTokenSource lifetime;
    private static PlayerDataSO isolatedPlayer;
    private static bool busy;

    public static bool IsActive => SessionState.GetBool(Prefix + "Active", false);
    public static bool IsBusy => busy;
    public static string Status { get; private set; } = "테스트를 시작하면 별도 플레이어와 저장 경로를 사용합니다.";
    public static string SaveDirectory => SessionState.GetString(Prefix + "SaveDirectory", "");
    public static PlayerDataSO Player => isolatedPlayer;
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

    public static void Start(string scenarioId)
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
    {
        // 동일 구현의 Singleton을 추가하면 VContainer가 컬렉션 등록 중 충돌한다.
        // 새 등록을 별도 빌더에서 구성한 뒤 기존 슬롯 자체를 교체한다.
        var replacements = new ContainerBuilder();
        var playerRegistration = replacements.RegisterInstance(player).AsSelf().As<IPlayerDataReader>().As<IPlayerDataWriter>();
        var savesRegistration = replacements.Register(createSaves, Lifetime.Singleton);
        ReplaceRegistration(builder, typeof(PlayerDataSO), playerRegistration);
        ReplaceRegistration(builder, typeof(SaveManager), savesRegistration);
        if (builder.Diagnostics != null)
        {
            // 교체 전 등록 기록이 남으면 Diagnostics가 Build되지 않은 항목을 읽는다.
            builder.Diagnostics.Clear();
            for (int i = 0; i < builder.Count; i++)
                builder.Diagnostics.TraceRegister(new RegisterInfo(builder[i]));
        }
    }

    private static void ReplaceRegistration(IContainerBuilder builder, Type service, RegistrationBuilder replacement)
    {
        int index = -1;
        for (int i = 0; i < builder.Count; i++)
        {
            var registration = builder[i].Build();
            if (registration.ImplementationType != service && registration.InterfaceTypes?.Contains(service) != true) continue;
            if (index >= 0) throw new InvalidOperationException("[HomeTest] 교체할 서비스가 중복 등록되어 있습니다: " + service.Name);
            index = i;
        }
        if (index < 0) throw new InvalidOperationException("[HomeTest] 교체할 기존 서비스를 찾을 수 없습니다: " + service.Name);
        builder[index] = replacement;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode && IsActive) InstallOverrides();
        if (change == PlayModeStateChange.EnteredPlayMode && IsActive)
        {
            SessionState.SetBool(Prefix + "Running", true);
            lifetime = new CancellationTokenSource();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LoadScenarioAsync(SessionState.GetString(Prefix + "Scenario", "")).Forget(ReportFailure);
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
        if (!IsActive || !Application.isPlaying || isolatedPlayer == null || scene.name != "TempEnding") return;
        // 크레딧은 DI 대신 직렬화된 SO를 읽으므로, Start 이전에 씬 인스턴스의 연결만 바꾼다.
        foreach (var credits in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndingCredits>(true)))
        {
            using var serialized = new SerializedObject(credits);
            serialized.FindProperty("data").objectReferenceValue = isolatedPlayer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static async UniTask LoadScenarioAsync(string scenarioId)
    {
        RequireSession();
        if (busy) throw new InvalidOperationException("[HomeTest] 다른 테스트 준비가 진행 중입니다.");
        var scenario = HomeInteractionTestScenarios.Find(scenarioId);
        busy = true;
        try
        {
            Status = "홈 테스트 조건을 준비합니다.";
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
            var loaded = scenario.Opening
                ? await progression.StartNewGameAsync(() => scenario.Apply(player), RestorePlayer, token)
                : await progression.RestoreHomeAsync(scenario.Day, () => scenario.Apply(player), RestorePlayer, token);
            if (!loaded.Succeeded) throw new InvalidOperationException("[HomeTest] Home 준비 실패: " + loaded.Message);
            await WaitUntilAsync(() => Context != null && Context.IsReady && Controller != null && Controller.IsReady,
                "홈 상호작용 준비", token);
            if (!scenario.Opening)
            {
                GameStateManager.Instance.GameFlow = scenario.Flow;
                GameStateManager.Instance.CurrentGameState = GameState.Play;
                Context.RefreshContents();
                Context.Player.TrySetPosition();
                if (!string.IsNullOrEmpty(scenario.FocusId))
                {
                    var target = WorldInteractionTarget.InScene(SceneManager.GetActiveScene()).FirstOrDefault(value =>
                        value.Content?.SourceId == scenario.FocusId || value.StorySceneId == scenario.FocusId);
                    if (target != null) TeleportToTarget(target);
                }
            }
            Status = scenario.Opening ? "오프닝 실행 중입니다. Game 창에서 진행하세요." : "홈 테스트 준비 완료.";
        }
        catch (OperationCanceledException) { Status = "테스트 준비가 취소되었습니다."; throw; }
        catch (Exception error) { Status = error.Message; throw; }
        finally { busy = false; }
    }

    public static void TeleportToTarget(WorldInteractionTarget target)
    {
        RequireSession();
        var context = Context;
        if (target == null || context == null || !context.IsReady || target.gameObject.scene != context.gameObject.scene)
            throw new InvalidOperationException("[HomeTest] 현재 홈 씬의 대상을 선택하세요.");
        if (Controller?.IsRunning == true || context.Runner.IsRunning || context.Player.State != EInteractorState.None)
            throw new InvalidOperationException("[HomeTest] 현재 상호작용이 끝난 뒤 이동하세요.");
        context.Player.PrepareWorldInteraction();
        var position = target.transform.position;
        position.z = context.Player.transform.position.z;
        context.Player.transform.position = position;
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

    public static void Stop()
    {
        if (!IsActive) return;
        lifetime?.Cancel();
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else Cleanup();
    }

    private static void Cleanup()
    {
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
