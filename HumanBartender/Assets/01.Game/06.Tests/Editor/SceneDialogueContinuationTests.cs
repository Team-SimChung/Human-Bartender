using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

public sealed class SceneDialogueContinuationTests
{
    private sealed class FailingTransition : ISceneTransitionService, IPresentedSceneTransitionService, ISceneFadeService
    {
        public bool IsBusy => false;
        public SceneTransitionState CurrentState => SceneTransitionState.Idle;
        public long CurrentOperationId => 0;
        public SceneTransitionRequest RequestPresentedScene(string sceneName, SceneTransitionPresentation presentation,
            CancellationToken cancellationToken = default)
        {
            presentation.BeforeLoad();
            return new SceneTransitionRequest(true, UniTask.FromResult(new SceneTransitionResult(
                SceneTransitionOutcome.Failed, sceneName, "Simulated failure before scene activation.")));
        }
        public SceneTransitionRequest RequestLoadScene(string sceneName, LoadSceneMode sceneMode = LoadSceneMode.Single,
            CancellationToken cancellationToken = default, SceneTransitionStyle style = SceneTransitionStyle.Fade)
            => throw new NotSupportedException();
        public SceneTransitionRequest RequestUnloadScene(string sceneName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        public UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }
    private sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        public readonly List<string> Lines = new();
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Lines.Add(text); return UniTask.CompletedTask; }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
            => throw new InvalidOperationException("The feed continuation has no choice.");
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    [Test]
    public void OptInTravelKeepsLastCsvLineForHomeAndDefersAdoptionUntilDeparture()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        var owner = new GameObject("Scene continuation runner");
        var originalLanguage = GameStateManager.Instance.Language;
        try
        {
            GameStateManager.Instance.Language = ELanguage.Ko;
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
            conditions.ApplyRequired("flag.boobi_feeding_pending = true");
            var runner = owner.AddComponent<DialogueRunner>();
            typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, conditions);
            typeof(DialogueRunner).GetField("storyEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner,
                new OutsideStoryEvents(new[] { new NeighborhoodStoryEvents(new CsvStoryTestContent()) }));
            var presenter = new Presenter();
            runner.Bind(presenter);
            var steps = new CsvStoryTestContent().GetSceneSteps("neighborhood-feed");
            var original = steps.ToArray();
            int command = Array.FindIndex(steps, step => step.Command == OutsideStoryCommand.AdoptCat);
            Assert.That(command, Is.GreaterThanOrEqualTo(0));
            var result = runner.PlayOutsideAsync(steps, autoAdvance: true,
                playWorldAction: (_, _) => UniTask.CompletedTask, continueTravel: true).GetAwaiter().GetResult();
            Assert.That(result.Completed, Is.True, result.Error);
            Assert.That(result.Destination, Is.EqualTo(GameProgressionDestination.HomeVisit));
            Assert.That(result.Continuation, Is.Not.Null);
            Assert.That(result.Continuation.Steps.Select(step => step.Text.Ko),
                Is.EqualTo(steps.Skip(command + 1).Select(step => step.Text.Ko)));
            Assert.That(result.Continuation.Steps.Last().Actor, Is.EqualTo("luna"));
            Assert.That(presenter.Lines, Does.Not.Contain(result.Continuation.Steps.Last().Text.Ko));
            Assert.That(steps, Is.EqualTo(original), "Capturing a continuation must not modify loaded CSV rows.");
            Assert.That(player.CheckFlag("boobi_adopted"), Is.False);
            Assert.That(player.CheckFlag("boobi_feeding_pending"), Is.True);
            conditions.ApplyRequired(result.Continuation.DepartureEffects);
            Assert.That(player.CheckFlag("boobi_adopted"), Is.True);
            Assert.That(player.CheckFlag("boobi_feeding_pending"), Is.False);
            conditions.ApplyRequired(result.Continuation.RollbackEffects);
            Assert.That(player.CheckFlag("boobi_adopted"), Is.False);
            Assert.That(player.CheckFlag("boobi_feeding_pending"), Is.True);
            Assert.That(new StoryExecutionResult(StoryExecutionStatus.Cancelled, destination: result.Destination,
                continuation: result.Continuation).Continuation, Is.Null);
        }
        finally
        {
            GameStateManager.Instance.Language = originalLanguage;
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(player);
        }
    }

    private SceneSetup[] sceneSetup;
    private PlayerDataSO sharedPlayer;
    private int originalMoney, originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool progressionFinished, dialogueFinished;
    private GameProgressionResult progressionResult;
    private StoryExecutionResult dialogueResult;

    private static IEnumerator WaitUntil(Func<bool> predicate, string message, float seconds = 30)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, message);
    }
    private void Progressed(GameProgressionResult result) { progressionResult = result; progressionFinished = true; }
    private IEnumerator WaitProgression()
    {
        yield return WaitUntil(() => progressionFinished, "Progression timed out.");
        Assert.That(progressionResult.Succeeded, Is.True, progressionResult.Message);
    }
    private void RestorePlayer()
    {
        if (sharedPlayer != null && originalFlags != null)
            sharedPlayer.ReplaceProgress(originalMoney, originalSkill, originalAffinity, originalFlags);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying)
        {
            var controller = WorldInteractionController.ForScene(SceneManager.GetActiveScene());
            if (controller != null)
            {
                controller.Cancel();
                float deadline = Time.realtimeSinceStartup + 5;
                while (controller != null && controller.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            }
            RestorePlayer();
            yield return new ExitPlayMode();
        }
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
    }

    [UnityTest]
    public IEnumerator RealAdoptionSurvivesSourceDestructionKeepsHomeLineLockedAndStaysAdoptedOnRevisit()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyRealAdoption();
    }

    private IEnumerator VerifyRealAdoption()
    {
        Time.timeScale = 1;
        yield return WaitUntil(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null, "CSV load timed out.");
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None).First(scope => scope.Container != null);
        var progression = root.Container.Resolve<GameProgressionService>();
        var conditions = root.Container.Resolve<IConditionUtil>();
        sharedPlayer = root.Container.Resolve<PlayerDataSO>();
        originalMoney = sharedPlayer.HasMoney(); originalSkill = sharedPlayer.GetSkillValue();
        originalAffinity = sharedPlayer.ReadAffinity(); originalFlags = sharedPlayer.ReadFlags();
        progressionFinished = false;
        progression.StartNewGameAsync(() => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayer)
            .ContinueWith(Progressed).Forget();
        yield return WaitProgression();
        GameStateManager.Instance.CurrentDay = 1;
        conditions.ApplyRequired("flag.day1_thug_done = true; flag.boobi_revealed = true; flag.boobi_adopted = false; flag.boobi_feeding_pending = true");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(Progressed).Forget();
        yield return WaitProgression();
        var scene = SceneManager.GetActiveScene();
        var source = WorldInteractionController.ForScene(scene);
        yield return WaitUntil(() => source != null && source.IsReady, "Outside controller was not ready.");
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None).Single(value => value.gameObject.scene == scene);
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        context.RefreshContents();
        var cat = WorldInteractionTarget.InScene(scene).Single(value => value.StorySceneId == "neighborhood-reveal");
        var travelSettings = cat.GetComponent<WorldDialogueTravelSettings>();
        Assert.That(travelSettings, Is.Not.Null);
        var tail = new StorySceneContinuation(Array.Empty<Step>(),
            "flag.boobi_adopted = true; flag.boobi_feeding_pending = false",
            "flag.boobi_adopted = false; flag.boobi_feeding_pending = true");
        var failingTransition = new FailingTransition();
        var failingProgression = new GameProgressionService(GameStateManager.Instance, failingTransition, failingTransition, conditions);
        var failedTransfer = failingProgression.ContinueDialogueAsync(GameProgressionDestination.HomeVisit, tail,
            travelSettings.Capture()).GetAwaiter().GetResult();
        Assert.That(failedTransfer.Status, Is.EqualTo(StoryExecutionStatus.Failed));
        Assert.That(sharedPlayer.CheckFlag("boobi_adopted"), Is.False, "A failed load must roll back the departure commit.");
        Assert.That(sharedPlayer.CheckFlag("boobi_feeding_pending"), Is.True);
        Assert.That(failingProgression.IsContinuingDialogue, Is.False);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        // Cancel a real fade before Unity starts loading, then retry through the actual NPC dialogue.
        using (var canceledTravel = new CancellationTokenSource())
        {
            dialogueFinished = false;
            progression.ContinueDialogueAsync(GameProgressionDestination.HomeVisit, tail, travelSettings.Capture(), canceledTravel.Token)
                .ContinueWith(result => { dialogueResult = result; dialogueFinished = true; }).Forget();
            yield return null;
            canceledTravel.Cancel();
            yield return WaitUntil(() => dialogueFinished, "Canceled transfer did not release its session.");
            Assert.That(dialogueResult.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
            Assert.That(sharedPlayer.CheckFlag("boobi_adopted"), Is.False);
            Assert.That(sharedPlayer.CheckFlag("boobi_feeding_pending"), Is.True);
            Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        }
        context.Player.transform.position = cat.transform.position + Vector3.right * .5f;
        yield return null;
        dialogueFinished = false;
        source.PlayAsync(cat).ContinueWith(result => { dialogueResult = result; dialogueFinished = true; }).Forget();
        float deadline = Time.realtimeSinceStartup + 30;
        while (SceneManager.GetActiveScene().name != "Home" && !dialogueFinished && Time.realtimeSinceStartup < deadline)
        {
            source.TryHandleInput();
            Assert.That(progression.TryBeginManualSave(out _), Is.False);
            yield return null;
        }
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"), dialogueResult.Error);
        yield return WaitUntil(() => !SceneTransitionManager.Instance.IsBusy, "Adoption fade did not finish.");
        Assert.That(source == null, Is.True, "The original scene controller must actually have been destroyed.");
        var home = WorldInteractionController.ForScene(SceneManager.GetActiveScene());
        yield return WaitUntil(() => home != null && home.IsReady && home.IsRunning, "The remaining Luna line did not start in Home.");
        var homeContext = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene.name == "Home");
        Assert.That(home.ActiveTarget.StorySceneId, Is.EqualTo("neighborhood-homeCat"));
        Assert.That(homeContext.Player.transform.position.x, Is.EqualTo(2.02f).Within(.001f));
        Assert.That(homeContext.Player.transform.position.y, Is.EqualTo(-.7f).Within(.001f));
        Assert.That(homeContext.Player.GetComponent<PlayerMovement2D>().IsFacingRight, Is.True);
        Assert.That(sharedPlayer.CheckFlag("boobi_adopted"), Is.True);
        Assert.That(sharedPlayer.CheckFlag("boobi_feeding_pending"), Is.False);
        Assert.That(progression.TryBeginManualSave(out _), Is.False, "The final Home line still owns the save boundary.");
        deadline = Time.realtimeSinceStartup + 15;
        while (!dialogueFinished && Time.realtimeSinceStartup < deadline) { home.TryHandleInput(); yield return null; }
        Assert.That(dialogueFinished && dialogueResult.Completed, Is.True, dialogueResult.Error);
        Assert.That(homeContext.Player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(progression.TryBeginManualSave(out _), Is.True);
        progression.EndManualSave();
        sharedPlayer.ReplaceProgress(sharedPlayer.HasMoney(), sharedPlayer.GetSkillValue(), sharedPlayer.ReadAffinity(), sharedPlayer.ReadFlags());
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(Progressed).Forget();
        yield return WaitProgression();
        var revisited = WorldInteractionController.ForScene(SceneManager.GetActiveScene());
        yield return WaitUntil(() => revisited != null && revisited.IsReady, "Revisited controller was not ready.");
        cat = WorldInteractionTarget.InScene(SceneManager.GetActiveScene()).Single(value => value.StorySceneId == "neighborhood-reveal");
        Assert.That(revisited.TryResolve(cat, out _), Is.False);
        Assert.That(sharedPlayer.CheckFlag("boobi_adopted"), Is.True);
    }
}
