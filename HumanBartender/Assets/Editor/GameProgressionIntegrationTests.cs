using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameProgressionIntegrationTests
{
    sealed class Transitions : ISceneTransitionService
    {
        public UniTaskCompletionSource<SceneTransitionResult> completion = new();
        public int Loads;
        public bool Reject;
        public bool IsBusy => false;
        public SceneTransitionState CurrentState => SceneTransitionState.Idle;
        public long CurrentOperationId => 0;

        public SceneTransitionRequest RequestLoadScene(string sceneName,
            LoadSceneMode sceneMode = LoadSceneMode.Single, CancellationToken cancellationToken = default)
        {
            Loads++;
            if (Reject) return Request(false, UniTask.FromResult(Result(SceneTransitionOutcome.Rejected, sceneName)));
            return Request(true, completion.Task);
        }

        public SceneTransitionRequest RequestUnloadScene(string sceneName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    sealed class Fades : ISceneFadeService
    {
        public int FadeIns;
        public bool FailFirstFadeIn;
        public bool CancelFadeOut;
        public UniTask FadeOutAsync(float duration = -1f, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancelFadeOut) throw new OperationCanceledException();
            return UniTask.CompletedTask;
        }
        public UniTask FadeInAsync(float duration = -1f, CancellationToken cancellationToken = default)
        {
            FadeIns++;
            if (FailFirstFadeIn && FadeIns == 1) throw new InvalidOperationException("fade failed");
            return UniTask.CompletedTask;
        }
    }

    sealed class Departure : IGameProgressionService
    {
        public int Calls;
        public bool FailFirst;
        public UniTask<GameProgressionResult> WaitForBarEntryAsync(CancellationToken cancellationToken = default)
            => UniTask.FromResult(new GameProgressionResult(GameProgressionOutcome.Succeeded));
        public UniTask<GameProgressionResult> CompleteBarAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return UniTask.FromResult(new GameProgressionResult(
                FailFirst && Calls == 1 ? GameProgressionOutcome.Failed : GameProgressionOutcome.Succeeded,
                FailFirst && Calls == 1 ? "scene load failed" : null));
        }
        public UniTask<GameProgressionResult> EnterAsync(GameProgressionDestination destination,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public UniTask<GameProgressionResult> SleepAsync(Action refreshConditions,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    Scene originalScene;
    Scene testScene;
    EGameFlow originalFlow;
    int originalDay;
    object originalDays;
    object originalLoaded;
    object originalCompletion;

    static FieldInfo Field(Type type, string name) => type.GetField(name,
        BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

    static SceneTransitionResult Result(SceneTransitionOutcome outcome, string sceneName)
        => (SceneTransitionResult)Activator.CreateInstance(typeof(SceneTransitionResult),
            BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { outcome, sceneName, null, null }, null);

    static SceneTransitionRequest Request(bool accepted, UniTask<SceneTransitionResult> completion)
        => (SceneTransitionRequest)Activator.CreateInstance(typeof(SceneTransitionRequest),
            BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { accepted, completion }, null);

    void Activate(string name)
    {
        if (testScene.IsValid()) EditorSceneManager.CloseScene(testScene, true);
        testScene = EditorSceneManager.OpenScene($"Assets/00.Scenes/{name}.unity", OpenSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
    }

    [SetUp]
    public void SetUp()
    {
        originalScene = SceneManager.GetActiveScene();
        originalFlow = GameStateManager.Instance.GameFlow;
        originalDay = GameStateManager.Instance.CurrentDay;
        originalDays = Field(typeof(NewDataLoadManager), "loadedDays").GetValue(null);
        originalLoaded = Field(typeof(NewDataLoadManager), "<IsLoaded>k__BackingField").GetValue(null);
        originalCompletion = Field(typeof(NewDataLoadManager), "loadCompletion").GetValue(null);
    }

    [TearDown]
    public void TearDown()
    {
        Field(typeof(NewDataLoadManager), "loadedDays").SetValue(null, originalDays);
        Field(typeof(NewDataLoadManager), "<IsLoaded>k__BackingField").SetValue(null, originalLoaded);
        Field(typeof(NewDataLoadManager), "loadCompletion").SetValue(null, originalCompletion);
        GameStateManager.Instance.GameFlow = originalFlow;
        GameStateManager.Instance.CurrentDay = originalDay;
        if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
        if (testScene.IsValid()) EditorSceneManager.CloseScene(testScene, true);
    }

    static void LoadDays(params NewDayInfoData[] days)
    {
        var loaded = new UniTaskCompletionSource();
        loaded.TrySetResult();
        Field(typeof(NewDataLoadManager), "loadedDays").SetValue(null, days);
        Field(typeof(NewDataLoadManager), "loadCompletion").SetValue(null, loaded);
        Field(typeof(NewDataLoadManager), "<IsLoaded>k__BackingField").SetValue(null, true);
    }

    [Test]
    public void ProjectDaysDeclareHomeThenDayThreeCommuteOut()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var days = catalog.Read<NewDayInfoData[]>("days");
        Assert.IsTrue(days.Where(day => day.Day <= 2).All(day => day.StartPhase == "home"));
        Assert.AreEqual("commute_out", days.Single(day => day.Day == 3).StartPhase);
    }

    [Test]
    public async Task BarEntryWaitsForFadeCompletionAndRejectsDuplicate()
    {
        Activate("OutSide");
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());

        UniTask<GameProgressionResult> entry = service.EnterAsync(GameProgressionDestination.Bar);
        Assert.AreEqual(GameProgressionOutcome.Rejected,
            (await service.EnterAsync(GameProgressionDestination.Bar)).Outcome);
        Activate("Play");
        UniTask<GameProgressionResult> ready = service.WaitForBarEntryAsync();
        Assert.AreEqual(UniTaskStatus.Pending, ready.Status);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "Play"));

        Assert.IsTrue((await entry).Succeeded);
        Assert.IsTrue((await ready).Succeeded);
        Assert.AreEqual(1, transitions.Loads);
        Assert.AreEqual(EGameFlow.Bar, GameStateManager.Instance.GameFlow);

        transitions.completion = new UniTaskCompletionSource<SceneTransitionResult>();
        UniTask<GameProgressionResult> departure = service.CompleteBarAsync();
        Assert.AreEqual(GameProgressionOutcome.Rejected, (await service.CompleteBarAsync()).Outcome);
        Activate("OutSide");
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "OutSide"));
        Assert.IsTrue((await departure).Succeeded);
        Assert.AreEqual(2, transitions.Loads);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
    }

    [Test]
    public async Task NewGameEntersDayZeroPlayAfterTransitionCompletes()
    {
        Activate("Main");
        LoadDays(new NewDayInfoData { Day = 0, StartPhase = "home" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
        int initialized = 0;
        UniTask<GameProgressionResult> start = service.StartNewGameAsync(() => initialized++, () => initialized--);
        Assert.AreEqual(1, initialized);
        Assert.AreEqual(0, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.Bar, GameStateManager.Instance.GameFlow);
        Activate("Play");
        UniTask<GameProgressionResult> ready = service.WaitForBarEntryAsync();
        Assert.AreEqual(UniTaskStatus.Pending, ready.Status);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "Play"));
        Assert.IsTrue((await start).Succeeded);
        Assert.IsTrue((await ready).Succeeded);
        Assert.IsTrue((await service.WaitForBarEntryAsync()).Succeeded);
        Assert.AreEqual(1, transitions.Loads);
    }

    [Test]
    public async Task FailedNewGameBeforePlayRestoresPreviousSession()
    {
        Activate("Main");
        LoadDays(new NewDayInfoData { Day = 0, StartPhase = "home" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
        int changed = 0;
        UniTask<GameProgressionResult> start = service.StartNewGameAsync(() => changed++, () => changed--);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "Play"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await start).Outcome);
        Assert.AreEqual(0, changed);
        Assert.AreEqual(2, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.AreEqual(GameProgressionOutcome.Failed, (await service.WaitForBarEntryAsync()).Outcome);
    }

    [Test]
    public async Task CanceledEntryDoesNotReleasePlayForLatePhases()
    {
        Activate("OutSide");
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
        UniTask<GameProgressionResult> entry = service.EnterAsync(GameProgressionDestination.Bar);
        UniTask<GameProgressionResult> ready = service.WaitForBarEntryAsync();
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Canceled, "Play"));

        Assert.AreEqual(GameProgressionOutcome.Canceled, (await entry).Outcome);
        Assert.AreEqual(GameProgressionOutcome.Canceled, (await ready).Outcome);
        Assert.AreEqual(EGameFlow.CommuteIn, GameStateManager.Instance.GameFlow);
    }

    [Test]
    public async Task LateEntryWaiterSeesCompletedFailureAndNextEntryGetsFreshResult()
    {
        Activate("OutSide");
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
        Assert.AreEqual(GameProgressionOutcome.Rejected, (await service.WaitForBarEntryAsync()).Outcome);

        UniTask<GameProgressionResult> first = service.EnterAsync(GameProgressionDestination.Bar);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "Play"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await first).Outcome);
        Assert.AreEqual(GameProgressionOutcome.Failed, (await service.WaitForBarEntryAsync()).Outcome);

        transitions.completion = new UniTaskCompletionSource<SceneTransitionResult>();
        UniTask<GameProgressionResult> second = service.EnterAsync(GameProgressionDestination.Bar);
        Activate("Play");
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "Play"));
        Assert.IsTrue((await second).Succeeded);
        Assert.IsTrue((await service.WaitForBarEntryAsync()).Succeeded);
        Assert.AreEqual(2, transitions.Loads);
    }

    [Test]
    public async Task FailedDepartureCanRetryWithoutRunningPhases()
    {
        Activate("Play");
        GameStateManager.Instance.GameFlow = EGameFlow.Bar;
        var actor = new GameObject("phase-test");
        try
        {
            var controller = actor.AddComponent<PlayPhaseController>();
            var departure = new Departure { FailFirst = true };
            Field(typeof(PlayPhaseController), "progression").SetValue(controller, departure);
            Field(typeof(PlayPhaseController), "departurePending").SetValue(controller, true);
            typeof(PlayPhaseController).GetProperty("State").SetValue(controller, PlayPhaseRunState.Failed);

            PlayPhaseRunRequest retry = controller.RequestRetryDeparture();
            Assert.IsTrue(retry.Accepted);
            Assert.AreEqual(PlayPhaseRunOutcome.Failed, (await retry.Completion).Outcome);
            retry = controller.RequestRetryDeparture();
            Assert.IsTrue(retry.Accepted);
            Assert.AreEqual(PlayPhaseRunOutcome.Succeeded, (await retry.Completion).Outcome);
            Assert.AreEqual(2, departure.Calls);
            Assert.AreEqual(PlayPhaseRunState.Completed, controller.State);
            Assert.IsFalse(controller.RequestRetryDeparture().Accepted);
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
    }

    [Test]
    public async Task DepartureFailureAfterSceneChangeDoesNotClaimPlayWasRestored()
    {
        Activate("Play");
        GameStateManager.Instance.GameFlow = EGameFlow.Bar;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());

        UniTask<GameProgressionResult> departure = service.CompleteBarAsync();
        Activate("OutSide");
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "OutSide"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await departure).Outcome);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
    }

    [Test]
    public async Task DayTwoSleepUsesCommuteOutDataOnce()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 3, StartPhase = "commute_out" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());

        UniTask<GameProgressionResult> sleep = service.SleepAsync(null);
        Assert.AreEqual(3, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.AreEqual(GameProgressionOutcome.Rejected, (await service.SleepAsync(null)).Outcome);
        Activate("OutSide");
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "OutSide"));
        Assert.IsTrue((await sleep).Succeeded);
        Assert.AreEqual(3, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(1, transitions.Loads);
    }

    [Test]
    public async Task UnknownNextDayLeavesCurrentContextAndScreenVisible()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 3, StartPhase = "unknown" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var fades = new Fades();
        var service = new GameProgressionService(GameStateManager.Instance, new Transitions(), fades);

        Assert.AreEqual(GameProgressionOutcome.Rejected, (await service.SleepAsync(null)).Outcome);
        Assert.AreEqual(2, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.AreEqual(0, fades.FadeIns);
    }

    [Test]
    public async Task MissingNextDayDoesNotAdvance()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 3, StartPhase = "commute_out" });
        GameStateManager.Instance.CurrentDay = 3;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());

        Assert.AreEqual(GameProgressionOutcome.Rejected, (await service.SleepAsync(null)).Outcome);
        Assert.AreEqual(3, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.AreEqual(0, transitions.Loads);
    }

    [Test]
    public async Task DayThreeTransitionFailureBeforeLoadRestoresDayAndFlow()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 3, StartPhase = "commute_out" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
        Field(typeof(GameProgressionService), "homeReturned").SetValue(service, true);

        UniTask<GameProgressionResult> sleep = service.SleepAsync(null);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "OutSide"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await sleep).Outcome);
        Assert.AreEqual(2, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.IsTrue((bool)Field(typeof(GameProgressionService), "homeReturned").GetValue(service));
    }

    [Test]
    public async Task DayThreeFailureAfterSceneChangeKeepsActualContext()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 3, StartPhase = "commute_out" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var transitions = new Transitions();
        var service = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());

        UniTask<GameProgressionResult> sleep = service.SleepAsync(null);
        Activate("OutSide");
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "OutSide"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await sleep).Outcome);
        Assert.AreEqual(3, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
    }

    [Test]
    public async Task HomeSleepFadeFailureRestoresDayFlowAndVisibleScreen()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 2, StartPhase = "home" });
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var fades = new Fades { FailFirstFadeIn = true };
        var service = new GameProgressionService(GameStateManager.Instance, new Transitions(), fades);
        Field(typeof(GameProgressionService), "homeReturned").SetValue(service, true);
        int refreshes = 0;

        Assert.AreEqual(GameProgressionOutcome.Failed,
            (await service.SleepAsync(() => refreshes++)).Outcome);
        Assert.AreEqual(1, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.IsTrue((bool)Field(typeof(GameProgressionService), "homeReturned").GetValue(service));
        Assert.AreEqual(2, refreshes);
        Assert.AreEqual(2, fades.FadeIns);
    }

    [Test]
    public async Task CanceledHomeSleepKeepsDayAndRestoresScreen()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 2, StartPhase = "home" });
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var fades = new Fades { CancelFadeOut = true };
        var service = new GameProgressionService(GameStateManager.Instance, new Transitions(), fades);

        Assert.AreEqual(GameProgressionOutcome.Canceled, (await service.SleepAsync(null)).Outcome);
        Assert.AreEqual(1, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
        Assert.AreEqual(1, fades.FadeIns);
    }

    [Test]
    public void PlayerProgressReplacementUsesCanonicalFlagsAndRemovesPreviousKeys()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.Init();
            player.AddFlag("flag.samho_death_route", true);
            player.SetCharacterAffinityAmount("old", 8);
            Assert.IsTrue(player.CheckFlag("samho_death_route"));
            player.ReplaceProgress(42, 7,
                new System.Collections.Generic.Dictionary<string, int> { ["new"] = -3 },
                new System.Collections.Generic.Dictionary<string, bool> { ["route.part.one"] = true });
            Assert.AreEqual(42, player.HasMoney());
            Assert.AreEqual(7, player.GetSkillValue());
            Assert.AreEqual(-3, player.GetCurCharacterAffinityValue("new"));
            Assert.IsFalse(player.ReadAffinity().ContainsKey("old"));
            Assert.IsFalse(player.CheckFlag("samho_death_route"));
            Assert.IsTrue(player.CheckFlag("flag.route.part.one"));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void CorruptSlotDoesNotHideValidNeighborOrChangePlayer()
    {
        LoadDays(new NewDayInfoData { Day = 2, StartPhase = "home" });
        string directory = Path.Combine(Path.GetTempPath(), "hb-manual-save-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.SetMoney(73);
            var progression = new GameProgressionService(GameStateManager.Instance, new Transitions(), new Fades());
            var saves = (SaveManager)Activator.CreateInstance(typeof(SaveManager),
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { player, progression, GameStateManager.Instance, directory }, null);
            const string valid = "{\"save_version\":1,\"saved_at\":\"2026-09-24T00:00:00.0000000Z\",\"current_day\":2,\"money\":73,\"skill_amount\":0,\"affinity\":{},\"flags\":{}}";
            File.WriteAllText(Path.Combine(directory, "slot-1.json"), valid.Replace("\"money\":73", "\"money\":73,\"money\":74"));
            File.WriteAllText(Path.Combine(directory, "slot-2.json"), valid);
            Assert.IsTrue(saves.TryDescribeSlot(1, out _, out bool firstCanLoad));
            Assert.IsFalse(firstCanLoad);
            Assert.IsTrue(saves.TryDescribeSlot(2, out _, out bool secondCanLoad));
            Assert.IsTrue(secondCanLoad);
            Assert.AreEqual(73, player.HasMoney());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            Directory.Delete(directory, true);
        }
    }

    [TestCase("\"money\":73", "\"money\":-1")]
    [TestCase("\"money\":73", "\"money\":\"73\"")]
    [TestCase("\"save_version\":1", "\"save_version\":2")]
    [TestCase("\"current_day\":2", "\"current_day\":9")]
    [TestCase("\"affinity\":{}", "\"affinity\":[]")]
    [TestCase("\"flags\":{}", "\"flags\":{\"flag.route\":true}")]
    public void InvalidSlotValuesAreRejectedBeforeLoad(string original, string replacement)
    {
        LoadDays(new NewDayInfoData { Day = 2, StartPhase = "home" });
        string directory = Path.Combine(Path.GetTempPath(), "hb-invalid-save-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.SetMoney(73);
            var progression = new GameProgressionService(GameStateManager.Instance, new Transitions(), new Fades());
            var saves = (SaveManager)Activator.CreateInstance(typeof(SaveManager),
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { player, progression, GameStateManager.Instance, directory }, null);
            const string valid = "{\"save_version\":1,\"saved_at\":\"2026-09-24T00:00:00.0000000Z\",\"current_day\":2,\"money\":73,\"skill_amount\":0,\"affinity\":{},\"flags\":{}}";
            File.WriteAllText(Path.Combine(directory, "slot-1.json"), valid.Replace(original, replacement));
            Assert.IsTrue(saves.TryDescribeSlot(1, out _, out bool canLoad));
            Assert.IsFalse(canLoad);
            Assert.AreEqual(73, player.HasMoney());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task ManualSlotRoundTripsProgressThroughTemporaryDirectory()
    {
        Activate("Home");
        LoadDays(new NewDayInfoData { Day = 2, StartPhase = "home" });
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        var manager = testScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<InteractiveEntityManager>(true)).Single();
        typeof(InteractiveEntityManager).GetProperty("IsReady").SetValue(manager, true);
        ((UniTaskCompletionSource<bool>)Field(typeof(InteractiveEntityManager), "readiness")
            .GetValue(manager)).TrySetResult(true);

        string directory = Path.Combine(Path.GetTempPath(), "hb-save-roundtrip-" + Guid.NewGuid().ToString("N"));
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.ReplaceProgress(73, 4,
                new System.Collections.Generic.Dictionary<string, int> { ["chris"] = 9 },
                new System.Collections.Generic.Dictionary<string, bool> { ["route.open"] = true });
            var transitions = new Transitions();
            var progression = new GameProgressionService(GameStateManager.Instance, transitions, new Fades());
            Field(typeof(GameProgressionService), "homeReturned").SetValue(progression, true);
            var saves = (SaveManager)Activator.CreateInstance(typeof(SaveManager),
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { player, progression, GameStateManager.Instance, directory }, null);
            Assert.IsTrue(saves.TrySaveSlot(1, out string message), message);
            var document = JObject.Parse(File.ReadAllText(Path.Combine(directory, "slot-1.json")));
            CollectionAssert.AreEquivalent(new[]
                { "save_version", "saved_at", "current_day", "money", "skill_amount", "affinity", "flags" },
                document.Properties().Select(property => property.Name).ToArray());
            Assert.IsFalse(saves.HasSlotFile(2));

            player.ReplaceProgress(1, 0,
                new System.Collections.Generic.Dictionary<string, int> { ["old"] = 5 },
                new System.Collections.Generic.Dictionary<string, bool> { ["stale"] = true });
            UniTask<GameProgressionResult> load = saves.LoadSlotAsync(1);
            transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Succeeded, "Home"));
            Assert.IsTrue((await load).Succeeded);
            Assert.AreEqual(2, GameStateManager.Instance.CurrentDay);
            Assert.AreEqual(73, player.HasMoney());
            Assert.AreEqual(4, player.GetSkillValue());
            Assert.AreEqual(9, player.GetCurCharacterAffinityValue("chris"));
            Assert.IsFalse(player.ReadAffinity().ContainsKey("old"));
            Assert.IsTrue(player.CheckFlag("flag.route.open"));
            Assert.IsFalse(player.CheckFlag("stale"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
