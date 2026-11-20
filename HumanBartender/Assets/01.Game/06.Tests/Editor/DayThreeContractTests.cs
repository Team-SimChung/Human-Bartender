using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

/// <summary>웹 계약 연출의 배우·대본 순서·중단·완료 저장 경계를 실제 CSV와 씬으로 검증한다.</summary>
public sealed class DayThreeContractTests
{
    private const string Id = "day3-samho-contract";
    private const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private SceneSetup[] sceneSetup;
    private PlayerDataSO isolatedPlayer;
    private PlayerDataSO sharedPlayer;
    private int originalMoney, originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool finished, progressionFinished;
    private Exception failure;
    private StoryExecutionResult dialogueResult;
    private GameProgressionResult progressionResult;

    private sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        public readonly List<(string actor, string text)> Lines = new();
        public Action<string, string> OnLine;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Lines.Add((actor, text));
            OnLine?.Invoke(actor, text);
            return UniTask.CompletedTask;
        }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
            => throw new InvalidOperationException("The authored contract has no choice.");
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    private static NewSceneData ReadStory() => StoryCsvCompiler.Compile(
        CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
            .Read<NewStreetData>("script/street")).Scenes.Single(value => value.Id == Id);

    private static WorldInteractionTarget FindTarget(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
        .Single(target => target.StorySceneId == Id);

    private static NpcActor Actor(WorldInteractionTarget target, string role) => target.GetComponentsInChildren<NpcActor>(true)
        .Single(value => value.name.EndsWith("· " + role, StringComparison.Ordinal));

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason, float seconds = 10)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private static IEnumerator Frames(int count = 2)
    {
        int frame = Time.frameCount;
        yield return WaitUntil(() => Time.frameCount >= frame + count, "Game frames did not advance.");
    }

    private void PrepareClone()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outside = EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Additive);
        try
        {
            var clone = Object.Instantiate(FindTarget(outside).gameObject);
            SceneManager.MoveGameObjectToScene(clone, isolated);
        }
        finally { EditorSceneManager.CloseScene(outside, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private void RestorePlayerSnapshot()
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
            RestorePlayerSnapshot();
            yield return new ExitPlayMode();
        }
        if (isolatedPlayer != null) Object.DestroyImmediate(isolatedPlayer);
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void CsvRetainsContractDialogueCommandOrderAndCompletionFlag()
    {
        var story = ReadStory();
        Assert.That(story.CompletionFlag, Is.EqualTo("day3_contract_done"));
        Assert.That(story.Steps.Where(step => step.Type == "event").Select(step => step.Command), Is.EqualTo(new[]
        {
            OutsideStoryCommand.NotionDealerExit, OutsideStoryCommand.NotionApproach, OutsideStoryCommand.NotionSamhoExit
        }));
        Assert.That(story.Steps.Single(step => step.Seq == 12).Actor, Is.EqualTo("contract-samho"));
        Assert.That(story.Steps.Single(step => step.Seq == 12).Text.Ko, Is.EqualTo("루나? 퇴근했나 보네?"));
        Assert.That(story.Steps.Single(step => step.Seq == 22).Actor, Is.EqualTo("luna"));
    }

    [UnityTest]
    public IEnumerator ActualCsvWaitsForDealerExitThenSamhoApproachAndRightDeparture()
    {
        PrepareClone();
        yield return new EnterPlayMode();
        yield return VerifyCsvActions();
    }

    private IEnumerator VerifyCsvActions()
    {
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 3;
        state.GameFlow = EGameFlow.CommuteOut;
        state.Language = ELanguage.Ko;
        isolatedPlayer = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(state, isolatedPlayer, isolatedPlayer);
        var scene = SceneManager.GetActiveScene();
        var target = FindTarget(scene);
        var interaction = target.GetComponent<NpcInteraction>();
        var samho = Actor(target, "samho");
        var dealer = Actor(target, "dealer");
        var samhoStart = samho.transform.position;
        var dealerStart = dealer.transform.position;
        var luna = new GameObject("Contract test Luna").transform;
        luna.position = samhoStart + new Vector3(3, 5, 7);
        NpcSequenceTestSettings.CreateStagingCamera(luna);
        var story = ReadStory();
        story.RuntimeScene = scene.name;
        interaction.Initialize(conditions, luna, story, null, null);
        var actions = new List<OutsideStoryWorldAction>();
        var speakers = target.GetComponent<WorldDialogueSpeakers>();
        Assert.That(speakers.Resolve("contract-samho").IsChildOf(samho.transform), Is.True);
        Assert.That(speakers.Resolve("contract-smuggler").IsChildOf(dealer.transform), Is.True);
        var presenter = new Presenter();
        presenter.OnLine = (_, text) =>
        {
            if (text != story.Steps.Single(step => step.Seq == 12).Text.Ko) return;
            Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.NotionDealerExit, OutsideStoryWorldAction.NotionApproach }));
            Assert.That(dealer.IsVisible, Is.False, "The dealer must stay gone throughout the Luna dialogue.");
            Assert.That(samho.transform.position.x, Is.EqualTo(luna.position.x - .75f).Within(.0001f));
            Assert.That(samho.IsBusy, Is.False, "The next spoken line must wait for the complete .95-second approach.");
        };
        var runner = new GameObject("Contract CSV runner").AddComponent<DialogueRunner>();
        typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner, conditions);
        typeof(DialogueRunner).GetField("storyEvents", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner,
            new OutsideStoryEvents(new[] { new StreetStoryEvents(new CsvStoryTestContent(), state) }));
        runner.Bind(presenter);
        interaction.BeginInteraction();
        finished = false;
        RunContractCsv(runner, story, interaction, samho, dealer, luna, samhoStart, dealerStart, actions).Forget();
        yield return WaitUntil(() => finished, "The contract CSV did not finish.", 15);
        Assert.That(failure, Is.Null);
        Assert.That(dialogueResult.Completed, Is.True, dialogueResult.Error);
        Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.NotionDealerExit,
            OutsideStoryWorldAction.NotionApproach, OutsideStoryWorldAction.NotionSamhoExit }));
        Assert.That(presenter.Lines, Is.EqualTo(story.Steps.Where(step => step.Type == "say")
            .Select(step => (step.Actor, step.Text.Ko)).ToArray()));
        Assert.That(isolatedPlayer.CheckFlag("day3_contract_done"), Is.False, "Only the controller may commit scene completion.");
        interaction.EndInteraction();
    }

    private async UniTask RunContractCsv(DialogueRunner runner, NewSceneData story, NpcInteraction interaction,
        NpcActor samho, NpcActor dealer, Transform luna, Vector3 samhoStart, Vector3 dealerStart,
        List<OutsideStoryWorldAction> actions)
    {
        failure = null;
        try
        {
            await interaction.PrepareInteractionAsync(CancellationToken.None);
            Assert.That(luna.position.x, Is.EqualTo(samhoStart.x + 1).Within(.0001f),
                "The web prelude places Luna one unit to Samho's right before the NPC dialogue.");
            Assert.That(interaction.GetComponent<NpcSceneStaging>().HasCameraOverride, Is.True);
            dialogueResult = await runner.PlayOutsideAsync(story.Steps, CancellationToken.None, autoAdvance: true,
                playWorldAction: async (command, token) =>
                {
                    actions.Add(command);
                    float began = Time.time;
                    await interaction.PlayWorldActionAsync(command, token);
                    Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(command == OutsideStoryWorldAction.NotionApproach ? .90f : 1.25f));
                    Assert.That(dealer.transform.position.x, Is.EqualTo(dealerStart.x - .845f).Within(.0001f));
                    Assert.That(dealer.IsVisible, Is.False);
                    Assert.That(samho.transform.position.y, Is.EqualTo(samhoStart.y));
                    Assert.That(samho.transform.position.z, Is.EqualTo(samhoStart.z));
                    if (command == OutsideStoryWorldAction.NotionDealerExit)
                        Assert.That(samho.transform.position, Is.EqualTo(samhoStart));
                    if (command == OutsideStoryWorldAction.NotionApproach)
                    {
                        Assert.That(samho.transform.position.x, Is.EqualTo(luna.position.x - .75f).Within(.0001f));
                        Assert.That(samho.GetComponent<NpcMovement2D>().FacingRight, Is.True, "At Luna's left Samho must face Luna.");
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        Assert.That(samho.GetComponent<NpcMovement2D>().FacingRight, Is.True,
                            "The Luna conversation must not revert to facing the dealer who already left.");
                    }
                    if (command == OutsideStoryWorldAction.NotionSamhoExit)
                    {
                        Assert.That(samho.transform.position.x, Is.EqualTo(luna.position.x - .75f + .845f).Within(.0001f));
                        Assert.That(samho.IsVisible, Is.False);
                    }
                });
        }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private void CompleteProgression(GameProgressionResult result) { progressionResult = result; progressionFinished = true; }
    private IEnumerator WaitForProgression(string stage)
    {
        yield return WaitUntil(() => progressionFinished, stage + " timed out.", 30);
        Assert.That(progressionResult.Succeeded, Is.True, stage + ": " + progressionResult.Message);
    }

    [UnityTest]
    public IEnumerator RealAutomaticContractCancelsDuringFadeThenCompletesAndStaysSavedOnRevisit()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyRealContract();
    }

    private IEnumerator VerifyRealContract()
    {
        Time.timeScale = 1;
        yield return WaitUntil(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null, "CSV load timed out.", 30);
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None).First(scope => scope.Container != null);
        var progression = root.Container.Resolve<GameProgressionService>();
        var conditions = root.Container.Resolve<IConditionUtil>();
        sharedPlayer = root.Container.Resolve<PlayerDataSO>();
        originalMoney = sharedPlayer.HasMoney(); originalSkill = sharedPlayer.GetSkillValue();
        originalAffinity = sharedPlayer.ReadAffinity(); originalFlags = sharedPlayer.ReadFlags();
        progressionFinished = false;
        progression.StartNewGameAsync(() => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Home startup");
        GameStateManager.Instance.CurrentDay = 1;
        conditions.ApplyRequired("flag.day1_thug_done = true; flag.day3_samho_done = true; flag.day3_contract_done = false");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Outside entry");
        var scene = SceneManager.GetActiveScene();
        var controller = WorldInteractionController.ForScene(scene);
        yield return WaitUntil(() => controller != null && controller.IsReady, "Outside controller was not ready.", 30);
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None).Single(value => value.gameObject.scene == scene);
        GameStateManager.Instance.CurrentDay = 3;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        context.RefreshContents();
        yield return Frames();
        var target = FindTarget(scene);
        Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Automatic));
        var interaction = target.GetComponent<NpcInteraction>();
        var samho = Actor(target, "samho");
        var dealer = Actor(target, "dealer");
        var samhoStart = samho.transform.position;
        var dealerStart = dealer.transform.position;
        var dealerRenderer = dealer.GetComponentInChildren<SpriteRenderer>();
        var near = target.transform.position + Vector3.right * .8f;
        context.Player.transform.position = near;
        float deadline = Time.realtimeSinceStartup + 35;
        bool sawFade = false;
        while (!sawFade && Time.realtimeSinceStartup < deadline)
        {
            if (controller.IsRunning)
            {
                Assert.That(controller.ActiveTarget, Is.SameAs(target));
                controller.TryHandleInput();
                sawFade = dealer.IsBusy && dealerRenderer.color.a > .01f && dealerRenderer.color.a < .95f;
            }
            if (sawFade) break;
            yield return null;
        }
        Assert.That(sawFade, Is.True, "The real CSV must reach the dealer's moving fade before cancellation.");
        Assert.That(dealer.transform.position.x, Is.LessThan(dealerStart.x - .65f));
        Assert.That(samho.transform.position, Is.EqualTo(samhoStart));
        Assert.That(sharedPlayer.CheckFlag("day3_contract_done"), Is.False);
        // Keep the automatic trigger quiet while checking cancellation's restored entry pose.
        target.enabled = false;
        controller.Cancel();
        yield return WaitUntil(() => !controller.IsRunning && !dealer.IsBusy, "Cancelled contract did not release the session.");
        yield return Frames();
        Assert.That(controller.LastResult.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
        Assert.That(dealer.transform.position, Is.EqualTo(dealerStart));
        Assert.That(samho.transform.position, Is.EqualTo(samhoStart));
        Assert.That(dealer.IsVisible && samho.IsVisible, Is.True);
        Assert.That(dealerRenderer.color.a, Is.EqualTo(1).Within(.0001f));
        Assert.That(interaction.CanInteract, Is.True);
        Assert.That(sharedPlayer.CheckFlag("day3_contract_done"), Is.False);
        Assert.That(Vector3.Distance(context.Player.transform.position, near), Is.LessThan(.0001f));

        context.Player.transform.position = target.transform.position + Vector3.right * 10;
        target.enabled = true;
        yield return Frames();
        context.Player.transform.position = near;
        bool dealerGone = false, approachSettled = false, samhoFaded = false;
        float approachX = samhoStart.x + 1 - .75f;
        deadline = Time.realtimeSinceStartup + 35;
        while ((!sharedPlayer.CheckFlag("day3_contract_done") || controller.IsRunning) && Time.realtimeSinceStartup < deadline)
        {
            if (controller.IsRunning)
            {
                Assert.That(controller.ActiveTarget, Is.SameAs(target));
                Assert.That(sharedPlayer.CheckFlag("day3_contract_done"), Is.False, "Completion cannot be saved before the full sequence ends.");
                if (!dealer.IsVisible) dealerGone = true;
                if (dealerGone) Assert.That(dealer.IsVisible, Is.False, "Approach must never make the dealer reappear.");
                if (dealerGone && !samho.IsBusy && samho.IsVisible && Mathf.Abs(samho.transform.position.x - approachX) < .001f)
                    approachSettled = true;
                if (approachSettled && samho.IsBusy && samho.GetComponentInChildren<SpriteRenderer>().color.a < .95f)
                {
                    samhoFaded = true;
                    Assert.That(samho.transform.position.x, Is.GreaterThan(approachX + .65f));
                }
                controller.TryHandleInput();
            }
            yield return null;
        }
        Assert.That(controller.LastResult.Completed, Is.True, controller.LastResult.Error);
        Assert.That(dealerGone && approachSettled && samhoFaded, Is.True, "The real scene must show both exits with Samho's intervening Luna conversation.");
        Assert.That(sharedPlayer.CheckFlag("day3_contract_done"), Is.True);
        Assert.That(samho.IsVisible || dealer.IsVisible, Is.False);
        Assert.That(context.Player.transform.position.x, Is.EqualTo(samhoStart.x + 1).Within(.0001f));
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(context.IsSafeForSave, Is.True);
        sharedPlayer.ReplaceProgress(sharedPlayer.HasMoney(), sharedPlayer.GetSkillValue(), sharedPlayer.ReadAffinity(), sharedPlayer.ReadFlags());
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.HomeVisit).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Home revisit");
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Outside revisit");
        scene = SceneManager.GetActiveScene();
        controller = WorldInteractionController.ForScene(scene);
        yield return WaitUntil(() => controller != null && controller.IsReady, "Revisited controller was not ready.", 30);
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        yield return Frames();
        target = FindTarget(scene);
        Assert.That(sharedPlayer.CheckFlag("day3_contract_done"), Is.True);
        Assert.That(Actor(target, "samho").IsVisible || Actor(target, "dealer").IsVisible, Is.False);
        Assert.That(controller.TryResolve(target, out _), Is.False);
    }
}
