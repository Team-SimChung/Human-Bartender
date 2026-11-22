using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DayOneThugInteractionTests
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string TestActorName = "Day one thug integration test";
    private SceneSetup[] sceneSetup;
    private PlayerDataSO playerData;
    private IConditionUtil conditions;
    private NpcInteraction interaction;
    private NpcActor actor;
    private Transform luna;
    private NewSceneData story;
    private bool finished;
    private Exception failure;
    private StoryExecutionResult dialogueResult;
    private readonly List<OutsideStoryWorldAction> actions = new();

    private sealed class UnusedFade : ISceneFadeService
    {
        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The thug sequence must not fade the screen.");
        public UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The thug sequence must not fade the screen.");
    }

    private sealed class UnusedPopup : IPopupService
    {
        public UniTask<bool> Popup(PopupType type, string data)
            => throw new InvalidOperationException("The thug sequence must not add a completion popup.");
    }

    private sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        public string Choice = "thug-refuse";
        public string PauseText;
        public bool Paused;
        public int ChoiceCount;
        public readonly List<string> Lines = new();
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public async UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        {
            Lines.Add(text);
            if (text != PauseText) return;
            Paused = true;
            await UniTask.WaitUntil(() => false, cancellationToken: token);
        }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
        {
            ChoiceCount++;
            selected(options.Single(option => option.Id == Choice));
        }
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    private static WorldInteractionTarget FindThug(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
        .Single(target => target.StorySceneId == "day1-corridor-thug");

    private static NewSceneData ReadStory() => StoryCsvCompiler.Compile(
        CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
            .Read<NewStreetData>("script/street")).Scenes.Single(value => value.Id == "day1-corridor-thug");

    private void PrepareActor()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outside = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var source = FindThug(outside);
            Assert.That(source.GetComponent<NpcInteraction>(), Is.Not.Null, "Run DayOneThugNpcSetup before these tests.");
            var clone = Object.Instantiate(source.gameObject);
            clone.name = TestActorName;
            SceneManager.MoveGameObjectToScene(clone, isolated);
            clone.GetComponent<NpcMovement2D>().Configure(15);
            using var settings = new SerializedObject(clone.GetComponent<NpcInteraction>());
            var departure = NpcSequenceTestSettings.Action(settings, NpcActionKind.DepartWhileMoving);
            departure.FindPropertyRelative("offset").vector2Value = new Vector2(2, -.25f);
            departure.FindPropertyRelative("walkDuration").floatValue = .3f;
            departure.FindPropertyRelative("duration").floatValue = .1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { EditorSceneManager.CloseScene(outside, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private void Initialize(int day = 1)
    {
        Time.timeScale = 1;
        GameStateManager.Instance.CurrentDay = day;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        GameStateManager.Instance.Language = ELanguage.Ko;
        interaction = GameObject.Find(TestActorName).GetComponent<NpcInteraction>();
        actor = interaction.GetComponent<NpcActor>();
        actor.transform.position = new Vector3(2, 4, 3);
        luna = new GameObject("Test Luna").transform;
        luna.position = new Vector3(7, 9, 6);
        NpcSequenceTestSettings.CreateStagingCamera(luna);
        playerData = ScriptableObject.CreateInstance<PlayerDataSO>();
        conditions = new ConditionUtil(GameStateManager.Instance, playerData, playerData);
        story = ReadStory();
        story.RuntimeScene = interaction.gameObject.scene.name;
        interaction.Initialize(conditions, luna, story, new UnusedFade(), new UnusedPopup());
    }

    private async UniTask Observe(UniTask operation)
    {
        finished = false; failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private async UniTask PlayAction(OutsideStoryWorldAction action, CancellationToken token)
    {
        actions.Add(action);
        await interaction.PlayWorldActionAsync(action, token);
    }

    private async UniTask ObserveDialogue(DialogueRunner runner, CancellationToken token)
    {
        finished = false; failure = null;
        try { dialogueResult = await runner.PlayOutsideAsync(story.Steps, token, autoAdvance: true, playWorldAction: PlayAction); }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private DialogueRunner CreateRunner(Presenter presenter)
    {
        var runner = new GameObject("Thug CSV runner").AddComponent<DialogueRunner>();
        typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, conditions);
        typeof(DialogueRunner).GetField("storyEvents", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner,
            new OutsideStoryEvents(new[] { new StreetStoryEvents(new CsvStoryTestContent(), GameStateManager.Instance) }));
        runner.Bind(presenter);
        return runner;
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason)
    {
        float until = Time.realtimeSinceStartup + 8;
        while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private static IEnumerator Frames()
    {
        int frame = Time.frameCount;
        yield return WaitUntil(() => Time.frameCount >= frame + 2, "Game frames did not advance.");
    }

    private void AssertRestored(Vector3 position)
    {
        Assert.That(Vector3.Distance(actor.transform.position, position), Is.LessThan(.0001f));
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(interaction.CanInteract, Is.True);
        Assert.That(actor.GetComponentInChildren<SpriteRenderer>().color.a, Is.EqualTo(1).Within(.0001f));
        Assert.That(playerData.CheckFlag("day1_thug_done"), Is.False);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (playerData != null) Object.DestroyImmediate(playerData);
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void AuthoredDayOneThugUsesOneNpcOwnerAndPreservesItsSpeakerAndOtherThugs()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var target = FindThug(scene);
            Assert.That(target.Participant, Is.SameAs(target.GetComponent<NpcInteraction>()));
            Assert.That(target.GetComponent<NpcInteraction>(), Is.Not.Null);
            Assert.That(target.GetComponent<WorldStoryActors>(), Is.Null);
            Assert.That(target.GetComponent<NpcActor>(), Is.Not.Null);
            Assert.That(target.GetComponent<NpcMovement2D>(), Is.Not.Null);
            Assert.That(target.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
            Assert.That(target.GetComponent<Animator>().applyRootMotion, Is.False);
            Assert.That(target.GetComponent<WorldDialogueSpeakers>().TryResolve("day1-corridor-thug", out var anchor, out _, out _), Is.True);
            Assert.That(anchor.IsChildOf(target.transform), Is.True);
            foreach (var other in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Where(value => value.StorySceneId is "day2-corridor-thug" or "day2-thug-information"))
            {
                Assert.That(other.GetComponent<WorldStoryActors>(), Is.Null);
                Assert.That(other.Participant, Is.InstanceOf<NpcInteraction>());
                Assert.That(other.GetComponentInChildren<NpcActor>(true), Is.Not.Null);
                Assert.That(other.GetComponent<WorldDialogueSpeakers>()
                    .TryResolve(other.StorySceneId, out var otherAnchor, out _, out _), Is.True);
                Assert.That(otherAnchor.IsChildOf(other.transform), Is.True);
            }
            using var settings = new SerializedObject(target.GetComponent<NpcInteraction>());
            var punch = NpcSequenceTestSettings.WorldActions(settings, OutsideStoryWorldAction.ThugPunch);
            Assert.That(NpcSequenceTestSettings.Kinds(punch), Is.EqualTo(new[] { NpcActionKind.Face,
                NpcActionKind.MoveTo, NpcActionKind.SetPosition, NpcActionKind.MoveTo, NpcActionKind.Wait }));
            foreach (var spec in new[] { (1, .33f, .65f), (3, .63f, .8f) })
            {
                var move = punch.GetArrayElementAtIndex(spec.Item1);
                Assert.That(move.FindPropertyRelative("offset").vector2Value, Is.EqualTo(new Vector2(spec.Item2, 0)));
                Assert.That(move.FindPropertyRelative("duration").floatValue, Is.EqualTo(spec.Item3));
                Assert.That(move.FindPropertyRelative("keepFacing").boolValue, Is.True);
                Assert.That(move.FindPropertyRelative("horizontalOnly").boolValue, Is.True);
            }
            Assert.That(punch.GetArrayElementAtIndex(2).FindPropertyRelative("offset").vector2Value, Is.EqualTo(new Vector2(.23f, 0)));
            var departure = NpcSequenceTestSettings.Action(settings, NpcActionKind.DepartWhileMoving);
            Assert.That(departure.FindPropertyRelative("offset").vector2Value, Is.EqualTo(new Vector2(.845f, 0)));
            Assert.That(departure.FindPropertyRelative("walkDuration").floatValue, Is.EqualTo(1));
            Assert.That(departure.FindPropertyRelative("duration").floatValue, Is.EqualTo(.3f));
            Assert.That(settings.FindProperty("exitPoint"), Is.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator PresenceUsesCsvDayRouteConditionAndCompletionFlag()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyPresence();
    }

    [Test]
    public void ReapplyingThugSetupKeepsAuthoredActionOrderAndArguments()
    {
        PrepareActor();
        var target = GameObject.Find(TestActorName).GetComponent<WorldInteractionTarget>();
        var common = target.GetComponent<NpcInteraction>();
        using (var data = new SerializedObject(common))
        {
            NpcSequenceTestSettings.Action(data, NpcActionKind.MoveTo).FindPropertyRelative("offset").vector2Value = new Vector2(1.25f, 0);
            NpcSequenceTestSettings.Action(data, NpcActionKind.MoveTo).FindPropertyRelative("duration").floatValue = .9f;
            data.FindProperty("sequences").GetArrayElementAtIndex(0).FindPropertyRelative("actions").MoveArrayElement(0, 1);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        string authored = EditorJsonUtility.ToJson(common);
        Assert.That(NpcInteractionSetup.ConfigureThugTarget(target, target.GetComponent<NpcActor>()), Is.SameAs(common));
        Assert.That(EditorJsonUtility.ToJson(common), Is.EqualTo(authored));
    }

    private IEnumerator VerifyPresence()
    {
        Initialize(0);
        Assert.That(actor.IsVisible, Is.False);
        GameStateManager.Instance.CurrentDay = 1;
        yield return Frames();
        Assert.That(interaction.CanInteract, Is.True);
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        yield return Frames();
        Assert.That(actor.IsVisible, Is.False);
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        story.When = "flag.thug_test_allowed";
        story.CompletionFlag = "thug_test_complete";
        interaction.Initialize(conditions, luna, story, new UnusedFade(), new UnusedPopup());
        Assert.That(actor.IsVisible, Is.False);
        playerData.AddFlag("thug_test_allowed", true);
        yield return Frames();
        Assert.That(interaction.CanInteract, Is.True);
        playerData.AddFlag("thug_test_complete", true);
        yield return Frames();
        Assert.That(actor.IsVisible, Is.False);
        playerData.AddFlag("thug_test_complete", false);
        GameStateManager.Instance.CurrentDay = 2;
        yield return Frames();
        Assert.That(actor.IsVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator ApproachStopsOnEitherSideAndCancellationRestoresTheWholeSession()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyApproachAndCancellation();
    }

    private IEnumerator VerifyApproachAndCancellation()
    {
        Initialize();
        foreach (float direction in new[] { 1f, -1f })
        {
            var start = new Vector3(direction > 0 ? 2 : 12, 4, 3);
            actor.transform.position = start;
            interaction.BeginInteraction();
            float began = Time.time;
            Observe(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.ThugPunch, CancellationToken.None)).Forget();
            yield return WaitUntil(() => actor.GetComponent<NpcMovement2D>().IsMoving, "The web contact movement did not begin.");
            Assert.That(actor.GetComponentInChildren<SpriteRenderer>().flipX, Is.EqualTo(direction > 0),
                "Contact and recoil preserve the direction chosen when the attack starts.");
            yield return WaitUntil(() => finished, "Contact, recoil and hold did not finish.");
            Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(1.65f - .02f));
            Assert.That(actor.transform.position.x, Is.EqualTo(luna.position.x + .63f).Within(.0001f));
            Assert.That(actor.transform.position.y, Is.EqualTo(start.y));
            Assert.That(actor.transform.position.z, Is.EqualTo(start.z));
            Assert.That(failure, Is.Null);
            Assert.That(actor.IsVisible, Is.True);
            interaction.EndInteraction();
            yield return Frames();
            AssertRestored(start);
        }

        var sessionStart = actor.transform.position;
        interaction.BeginInteraction();
        using var cancellation = new CancellationTokenSource();
        Observe(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.ThugPunch, cancellation.Token)).Forget();
        yield return WaitUntil(() => actor.GetComponent<NpcMovement2D>().IsMoving, "The contact movement did not begin before cancellation.");
        cancellation.Cancel();
        yield return WaitUntil(() => finished, "Cancelled attack kept its lifetime.");
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        interaction.EndInteraction();
        yield return Frames();
        AssertRestored(sessionStart);
        Assert.That(actor.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
    }

    [UnityTest]
    public IEnumerator RefusalCsvWaitsForPunchThenReactionDialogueAndRelativeDeparture()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyRefusalCsv();
    }

    private IEnumerator VerifyRefusalCsv()
    {
        Initialize();
        playerData.SetMoney(1000);
        var refusal = story.Steps.SelectMany(step => step.Options ?? Array.Empty<NewStreetOptionData>())
            .Single(option => option.Id == "thug-refuse").ResultSteps;
        var presenter = new Presenter { PauseText = refusal.Single(step => step.Seq == 4).Text.Ko };
        var runner = CreateRunner(presenter);
        var start = actor.transform.position;
        interaction.BeginInteraction();
        using (var cancellation = new CancellationTokenSource())
        {
            ObserveDialogue(runner, cancellation.Token).Forget();
            yield return WaitUntil(() => presenter.Paused, "The actual refusal reaction was not reached.");
            Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.ThugPunch }));
            Assert.That(actor.IsVisible, Is.True);
            Assert.That(actor.IsBusy, Is.False);
            Assert.That(actor.transform.position.x, Is.EqualTo(luna.position.x + .63f).Within(.0001f));
            Assert.That(playerData.HasMoney(), Is.EqualTo(1000));
            cancellation.Cancel();
            yield return WaitUntil(() => finished, "Reaction cancellation did not end the dialogue.");
            Assert.That(dialogueResult.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
        }
        interaction.EndInteraction();
        yield return Frames();
        AssertRestored(start);

        actions.Clear();
        runner.Bind(new Presenter());
        interaction.BeginInteraction();
        ObserveDialogue(runner, CancellationToken.None).Forget();
        yield return WaitUntil(() => finished, "Refusal retry did not finish.");
        Assert.That(failure, Is.Null);
        Assert.That(dialogueResult.Completed, Is.True, dialogueResult.Error);
        Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.ThugPunch, OutsideStoryWorldAction.ThugDepart }));
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(actor.transform.position.x, Is.EqualTo(luna.position.x + .63f + 2).Within(.0001f));
        Assert.That(actor.transform.position.y, Is.EqualTo(start.y - .25f).Within(.0001f));
        Assert.That(playerData.CheckFlag("day1_thug_done"), Is.False, "Only the controller commits story completion.");
        interaction.CompleteInteractionAsync(CancellationToken.None).GetAwaiter().GetResult();
        playerData.AddFlag(story.CompletionFlag, true);
        var end = actor.transform.position;
        interaction.EndInteraction();
        yield return Frames();
        Assert.That(actor.transform.position, Is.EqualTo(end));
        Assert.That(actor.IsVisible, Is.False);
    }

    [UnityTest]
    public IEnumerator PaidCsvDepartsBeforeLunasLastLineAndCancelledPaidRetryNeverChargesAgain()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyPaidCsv();
    }

    private IEnumerator VerifyPaidCsv()
    {
        Initialize();
        playerData.SetMoney(1000);
        var pay = story.Steps.SelectMany(step => step.Options ?? Array.Empty<NewStreetOptionData>())
            .Single(option => option.Id == "thug-pay").ResultSteps;
        var presenter = new Presenter { Choice = "thug-pay", PauseText = pay.Last(step => step.Type == "say").Text.Ko };
        var runner = CreateRunner(presenter);
        var start = actor.transform.position;
        interaction.BeginInteraction();
        using (var cancellation = new CancellationTokenSource())
        {
            ObserveDialogue(runner, cancellation.Token).Forget();
            yield return WaitUntil(() => presenter.Paused, "Luna's last paid-branch line was not reached.");
            Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.ThugDepart }));
            Assert.That(actor.IsVisible, Is.False, "The authored departure precedes Luna's final line.");
            Assert.That(playerData.HasMoney(), Is.EqualTo(500));
            Assert.That(playerData.CheckFlag("day1_thug_choice"), Is.True);
            Assert.That(playerData.CheckFlag("day1_thug_done"), Is.False);
            cancellation.Cancel();
            yield return WaitUntil(() => finished, "Paid dialogue cancellation did not finish.");
        }
        interaction.EndInteraction();
        yield return Frames();
        AssertRestored(start);

        // Rehydrate the same save-format flags before retrying the interrupted paid story.
        playerData.ReplaceProgress(playerData.HasMoney(), playerData.GetSkillValue(), playerData.ReadAffinity(), playerData.ReadFlags());
        actions.Clear();
        var resumed = new Presenter { Choice = "thug-refuse" };
        runner.Bind(resumed);
        interaction.BeginInteraction();
        ObserveDialogue(runner, CancellationToken.None).Forget();
        yield return WaitUntil(() => finished, "Paid retry did not finish.");
        Assert.That(failure, Is.Null);
        Assert.That(dialogueResult.Completed, Is.True, dialogueResult.Error);
        Assert.That(resumed.ChoiceCount, Is.Zero, "The saved payment flag resumes its original branch without another choice.");
        Assert.That(playerData.HasMoney(), Is.EqualTo(500));
        Assert.That(actions, Is.EqualTo(new[] { OutsideStoryWorldAction.ThugDepart }));
        interaction.CompleteInteractionAsync(CancellationToken.None).GetAwaiter().GetResult();
        playerData.AddFlag(story.CompletionFlag, true);
        interaction.EndInteraction();
        yield return Frames();
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(interaction.CanInteract, Is.False);
    }
}
