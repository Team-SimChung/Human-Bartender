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

/// <summary>실제 Play 실행기로 개별 기믹 또는 한 잔을 실행하며 영업과 원본 저장 데이터 사용을 차단한다.</summary>
[InitializeOnLoad]
internal static class GimmickPlayTestSession
{
    private const string Prefix = "HumanBartender.GimmickPlayTest.";
    private static IDisposable installation;
    private static CancellationTokenSource lifetime, operation;
    private static PlayerDataSO player;
    private static GimmickRunner runner;
    private static readonly List<CraftSession> quickSessions = new();
    internal static bool IsActive => SessionState.GetBool(Prefix + "Active", false);
    internal static bool IsReady { get; private set; }
    internal static bool IsBusy { get; private set; }
    internal static string Status { get; private set; } = "기믹과 레시피를 선택하세요.";
    internal static CraftSession LastSession { get; private set; }
    internal static CraftJudgement LastJudgement { get; private set; }
    internal static GimmickResult LastResult => LastSession?.Actual.GimmickResults.LastOrDefault();
    internal static bool IsWholeCocktail { get; private set; }
    internal static bool IsQuickTest { get; private set; }
    internal static IReadOnlyList<CraftSession> QuickSessions => quickSessions;

    static GimmickPlayTestSession()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        if (IsActive && SessionState.GetBool(Prefix + "ReloadInterrupted", false))
            EditorApplication.delayCall += Stop;
        else if (IsActive && EditorApplication.isPlayingOrWillChangePlaymode) Install();
        else if (SessionState.GetBool(Prefix + "RestorePending", false)) Cleanup();
    }

    internal static void Start(string key, bool wholeCocktail = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[GimmickPlayTest] 현재 Play 모드를 종료한 뒤 시작하세요.");
        if (key == GimmickPlayTestCatalog.QuickTestKey) GimmickPlayTestCatalog.ValidateQuickTest();
        else if (wholeCocktail) GimmickPlayTestCatalog.ValidateRecipe(GimmickPlayTestCatalog.FindRecipe(key));
        else GimmickPlayTestCatalog.ValidatePrefab(GimmickPlayTestCatalog.Find(key).Step.Type);
        var main = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/01.Game/01.Scenes/Main.unity");
        if (main == null) throw new InvalidOperationException("[GimmickPlayTest] Main 씬이 없습니다.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var state = GameStateManager.Instance;
        SessionState.SetInt(Prefix + "Day", state.CurrentDay);
        SessionState.SetInt(Prefix + "Flow", (int)state.GameFlow);
        SessionState.SetInt(Prefix + "GameState", (int)state.CurrentGameState);
        SessionState.SetFloat(Prefix + "TimeScale", Time.timeScale);
        SessionState.SetString(Prefix + "StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Prefix + "Case", key);
        SessionState.SetBool(Prefix + "WholeCocktail", wholeCocktail);
        SessionState.SetString(Prefix + "SaveDirectory", Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "Library", "GimmickPlayTests", "ManualSaves", Guid.NewGuid().ToString("N"))));
        SessionState.SetBool(Prefix + "Active", true);
        SessionState.SetBool(Prefix + "RestorePending", true);
        SessionState.SetBool(Prefix + "Running", false);
        SessionState.SetBool(Prefix + "ReloadInterrupted", false);
        SessionState.SetBool(Prefix + "BarBlocked", false);
        LastSession = null;
        LastJudgement = null;
        quickSessions.Clear();
        IsQuickTest = key == GimmickPlayTestCatalog.QuickTestKey;
        Status = "실제 기믹 실행 환경을 준비합니다.";
        try
        {
            EditorSceneManager.playModeStartScene = main;
            Install();
            EditorApplication.isPlaying = true;
        }
        catch { Cleanup(); throw; }
    }

    private static void Install()
    {
        if (installation != null) return;
        installation = LifetimeScope.Enqueue(builder =>
        {
            if (!IsActive) return;
            if (builder.ApplicationOrigin is ProjectLifetimeScope scope)
            {
                if (player != null) throw new InvalidOperationException("[GimmickPlayTest] 프로젝트 스코프가 중복 생성되었습니다.");
                using var data = new SerializedObject(scope);
                var original = data.FindProperty("playerData").objectReferenceValue as PlayerDataSO;
                if (original == null) throw new InvalidOperationException("[GimmickPlayTest] PlayerData 연결이 없습니다.");
                player = Object.Instantiate(original);
                player.name = "GimmickPlayTestPlayer";
                player.hideFlags = HideFlags.DontSave;
                EditorPlayTestServices.Replace(builder, player, resolver => new SaveManager(resolver.Resolve<PlayerDataSO>(),
                    resolver.Resolve<GameProgressionService>(), resolver.Resolve<GameStateManager>(),
                    SessionState.GetString(Prefix + "SaveDirectory", "")));
            }
            if (builder.ApplicationOrigin is InGameLifetimeScope play)
            {
                var phases = play.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlayPhaseController>(true)).ToArray();
                if (phases.Length != 1) throw new InvalidOperationException("[GimmickPlayTest] PlayPhaseController가 하나여야 합니다.");
                // Start 이전에 씬 인스턴스만 끈다. 영업·대사·튜토리얼 없이 실행기를 사용한다.
                phases[0].enabled = false;
                SessionState.SetBool(Prefix + "BarBlocked", true);
            }
        });
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode && IsActive) Install();
        if (change == PlayModeStateChange.EnteredPlayMode && IsActive)
        {
            SessionState.SetBool(Prefix + "Running", true);
            lifetime = new CancellationTokenSource();
            PrepareAsync().Forget(Report);
        }
        if (change == PlayModeStateChange.ExitingPlayMode && IsActive)
        {
            lifetime?.Cancel();
            operation?.Cancel();
            installation?.Dispose();
            installation = null;
            IsReady = false;
            SessionState.SetBool(Prefix + "Active", false);
        }
        if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "RestorePending", false)) Cleanup();
    }

    private static async UniTask PrepareAsync()
    {
        var token = lifetime.Token;
        await WaitAsync(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null, "CSV 로딩", token);
        if (NewDataLoadManager.LastLoadError != null)
            throw new InvalidOperationException("[GimmickPlayTest] CSV 로딩 실패: " + NewDataLoadManager.LastLoadError);
        ProjectLifetimeScope root = null;
        await WaitAsync(() => (root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.Container != null)) != null, "프로젝트 컨테이너", token);
        if (player == null || root.Container.Resolve<PlayerDataSO>() != player)
            throw new InvalidOperationException("[GimmickPlayTest] 테스트 플레이어 격리가 확인되지 않았습니다.");
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 0;
        state.GameFlow = EGameFlow.Bar;
        state.CurrentGameState = GameState.Play;
        root.Container.Resolve<UIDisplayOptions>().Hide();
        var transitions = root.Container.Resolve<ISceneTransitionService>();
        await WaitAsync(() => !transitions.IsBusy, "기존 화면 전환", token);
        var loaded = await transitions.RequestLoadScene("Play", cancellationToken: token).Completion;
        if (!loaded.Succeeded) throw new InvalidOperationException("[GimmickPlayTest] Play 진입 실패: " + loaded.Message);
        await WaitAsync(() => (runner = Object.FindObjectsByType<GimmickRunner>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.gameObject.scene.name == "Play")) != null, "기믹 실행기", token);
        if (!SessionState.GetBool(Prefix + "BarBlocked", false))
            throw new InvalidOperationException("[GimmickPlayTest] 영업 시작을 차단하지 못했습니다.");
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
        IsReady = true;
        await PlayAsync(SessionState.GetString(Prefix + "Case", ""), SessionState.GetBool(Prefix + "WholeCocktail", false));
    }

    internal static async UniTask PlayAsync(string key, bool wholeCocktail = false)
    {
        if (!IsActive || !Application.isPlaying || !IsReady || runner == null || lifetime == null)
            throw new InvalidOperationException("[GimmickPlayTest] 테스트 시작 버튼으로 실행 환경을 준비하세요.");
        if (IsBusy) throw new InvalidOperationException("[GimmickPlayTest] 현재 기믹을 마치거나 중단한 뒤 다시 실행하세요.");
        if (key == GimmickPlayTestCatalog.QuickTestKey)
        {
            await PlayQuickTestAsync();
            return;
        }
        var single = wholeCocktail ? null : GimmickPlayTestCatalog.Find(key);
        var recipe = wholeCocktail ? GimmickPlayTestCatalog.FindRecipe(key) : single.Recipe;
        var steps = wholeCocktail ? recipe.Steps : new[] { single.Step };
        using var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operation = source;
        IsBusy = true;
        LastJudgement = null;
        IsWholeCocktail = wholeCocktail;
        IsQuickTest = false;
        quickSessions.Clear();
        SessionState.SetString(Prefix + "Case", key);
        SessionState.SetBool(Prefix + "WholeCocktail", wholeCocktail);
        var session = recipe.CreateSession();
        LastSession = session;
        try
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, source.Token);
            await ExecutePlanAsync(runner, session, recipe.Cocktail, steps, (index, step) =>
                Status = (wholeCocktail ? recipe.Label + " · " + (index + 1) + "/" + steps.Length + " " : "") +
                    GimmickPlayTestCatalog.Name(step.Type) + " 플레이 중 — Game 창에서 조작하세요.", source.Token);
            if (wholeCocktail) LastJudgement = Judge(session, recipe.Cocktail);
            Status = wholeCocktail ? recipe.Label + " 제조를 완료했습니다. 단계별 결과와 제조 판정을 확인하세요."
                : "완료했습니다. 다시 플레이하거나 다른 기믹을 선택할 수 있습니다.";
        }
        catch (OperationCanceledException) { session.Cancel(); Status = "기믹을 중단했습니다."; }
        catch (Exception error) { session.Fail(error.Message); throw; }
        finally
        {
            session.Timer.Stop();
            operation = null;
            IsBusy = false;
        }
    }

    private static async UniTask PlayQuickTestAsync()
    {
        var selected = GimmickPlayTestCatalog.SelectQuickCases(GimmickPlayTestCatalog.Load());
        using var source = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        operation = source;
        IsBusy = true;
        IsQuickTest = true;
        IsWholeCocktail = false;
        LastSession = null;
        LastJudgement = null;
        quickSessions.Clear();
        SessionState.SetString(Prefix + "Case", GimmickPlayTestCatalog.QuickTestKey);
        SessionState.SetBool(Prefix + "WholeCocktail", false);
        try
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, source.Token);
            await ExecuteQuickCasesAsync(runner, selected, (item, session, index) =>
            {
                LastSession = session;
                quickSessions.Add(session);
                Status = $"간단 테스트 {index + 1}/{selected.Length} · {GimmickPlayTestCatalog.Name(item.Step.Type)}" +
                    " — Game 창에서 조작하세요.";
            }, source.Token);
            Status = "간단 테스트 4종을 모두 완료했습니다. 각 기믹의 결과를 확인하거나 다시 실행하세요.";
        }
        catch (OperationCanceledException) { Status = "간단 테스트를 중단했습니다. 남은 기믹은 실행하지 않습니다."; }
        finally { operation = null; IsBusy = false; }
    }

    internal static async UniTask ExecuteQuickCasesAsync(ICraftExecutor executor,
        IReadOnlyList<GimmickPlayTestCatalog.Case> selected, Action<GimmickPlayTestCatalog.Case, CraftSession, int> onStart,
        CancellationToken token)
    {
        for (int i = 0; i < selected.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var item = selected[i];
            var session = item.CreateSession();
            try
            {
                onStart?.Invoke(item, session, i);
                await ExecutePlanAsync(executor, session, item.Cocktail, new[] { item.Step }, null, token);
            }
            catch (OperationCanceledException) { session.Cancel(); throw; }
            catch (Exception error) { session.Fail(error.Message); throw; }
        }
    }

    internal static async UniTask ExecutePlanAsync(ICraftExecutor executor, CraftSession session, NewCocktailData cocktail,
        IReadOnlyList<GimmickStep> steps, Action<int, GimmickStep> onStep, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        session.BeginGimmicks();
        try
        {
            try
            {
                executor.Begin(session.Timer, new CraftRunDisplay(cocktail.TimeLimitSec, false));
                var context = CraftContext.From(session);
                for (int i = 0; i < steps.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    onStep?.Invoke(i, steps[i]);
                    var result = await executor.ExecuteAsync(steps[i], context, session.Timer, token);
                    token.ThrowIfCancellationRequested();
                    if (result == null) throw new InvalidOperationException("[GimmickPlayTest] 기믹 결과가 없습니다: " + steps[i]);
                    session.Actual.Record(result);
                    session.AdvanceGimmick();
                }
            }
            finally { executor.End(); }
            token.ThrowIfCancellationRequested();
            session.Complete();
        }
        finally { session.Timer.Stop(); }
    }

    private static CraftJudgement Judge(CraftSession session, NewCocktailData cocktail)
    {
        var flow = Object.FindObjectsByType<CraftFlowController>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == runner.gameObject.scene);
        using var data = new SerializedObject(flow);
        var shelf = data.FindProperty("shelfData").objectReferenceValue as NewShelfItemDataSO;
        var balance = data.FindProperty("balanceData").objectReferenceValue as NewBalanceDataSO;
        return CraftJudge.Evaluate(session, cocktail, shelf, balance != null ? balance.balanceData : null);
    }

    internal static void CancelGimmick() => operation?.Cancel();

    internal static void Stop()
    {
        if (!IsActive && !SessionState.GetBool(Prefix + "RestorePending", false)) return;
        lifetime?.Cancel();
        operation?.Cancel();
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else Cleanup();
    }

    private static void BeforeReload()
    {
        if (!IsActive || !SessionState.GetBool(Prefix + "Running", false)) return;
        SessionState.SetBool(Prefix + "ReloadInterrupted", true);
        lifetime?.Cancel();
        installation?.Dispose();
        installation = null;
    }

    private static void Cleanup()
    {
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        installation?.Dispose();
        installation = null;
        if (player != null) Object.DestroyImmediate(player);
        player = null;
        runner = null;
        operation = null;
        IsReady = IsBusy = false;
        var previous = SessionState.GetString(Prefix + "StartScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
        var state = GameStateManager.Instance;
        state.CurrentDay = SessionState.GetInt(Prefix + "Day", 0);
        state.GameFlow = (EGameFlow)SessionState.GetInt(Prefix + "Flow", (int)EGameFlow.Bar);
        state.CurrentGameState = (GameState)SessionState.GetInt(Prefix + "GameState", (int)GameState.Play);
        Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
        SessionState.SetBool(Prefix + "Active", false);
        SessionState.SetBool(Prefix + "RestorePending", false);
        SessionState.SetBool(Prefix + "Running", false);
        SessionState.SetBool(Prefix + "ReloadInterrupted", false);
        Status = "테스트를 종료하고 시작 씬·진행 상태·시간 배율을 복원했습니다.";
    }

    private static async UniTask WaitAsync(Func<bool> ready, string operationName, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        float deadline = Time.realtimeSinceStartup + 30;
        while (!ready())
        {
            token.ThrowIfCancellationRequested();
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("[GimmickPlayTest] 준비 시간 초과: " + operationName);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    internal static void Report(Exception error)
    {
        if (error is OperationCanceledException) return;
        Status = error.Message;
        Debug.LogError("[GimmickPlayTest] " + error.Message);
    }
}
