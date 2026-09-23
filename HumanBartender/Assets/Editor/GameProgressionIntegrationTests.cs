using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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

        UniTask<GameProgressionResult> sleep = service.SleepAsync(null);
        transitions.completion.TrySetResult(Result(SceneTransitionOutcome.Failed, "OutSide"));
        Assert.AreEqual(GameProgressionOutcome.Failed, (await sleep).Outcome);
        Assert.AreEqual(2, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
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
        int refreshes = 0;

        Assert.AreEqual(GameProgressionOutcome.Failed,
            (await service.SleepAsync(() => refreshes++)).Outcome);
        Assert.AreEqual(1, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteOut, GameStateManager.Instance.GameFlow);
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
}
