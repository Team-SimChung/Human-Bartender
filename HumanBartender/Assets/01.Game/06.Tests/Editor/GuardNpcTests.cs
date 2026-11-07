using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>경비원의 공통 NPC 연결, 상주 조건, 직접 입력 대화와 통과 기록을 검사한다. 저장 슬롯은 쓰지 않는다.</summary>
public sealed class GuardNpcTests
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string StoryId = "neighborhood-guardBlocked";
    private const string ActorName = "Guard common NPC test";
    private SceneSetup[] sceneSetup;
    private PlayerDataSO isolatedPlayer;
    private PlayerDataSO sharedPlayer;
    private int originalMoney;
    private int originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool progressionFinished;
    private GameProgressionResult progressionResult;

    private static WorldInteractionTarget FindGuard(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
        .Single(target => target.StorySceneId == StoryId);

    private void PrepareIsolatedGuard()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outside = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var clone = Object.Instantiate(FindGuard(outside).gameObject);
            clone.name = ActorName;
            SceneManager.MoveGameObjectToScene(clone, isolated);
        }
        finally { EditorSceneManager.CloseScene(outside, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason, float seconds = 10)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private static IEnumerator Frames(int count = 2)
    {
        int frame = Time.frameCount;
        yield return WaitUntil(() => Time.frameCount >= frame + count, "Game frames did not advance.");
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
    public void AuthoredGuardUsesTheCommonNpcAndKeepsManualDialogueBindings()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var target = FindGuard(scene);
            var interaction = target.GetComponent<NpcInteraction>();
            Assert.That(interaction, Is.Not.Null, "Run NPC Interaction Setup before testing the authored scene.");
            Assert.That(target.Participant, Is.SameAs(interaction));
            Assert.That(target.GetComponent<WorldStoryActors>(), Is.Null);
            Assert.That(target.GetComponent<NpcActor>(), Is.Not.Null);
            Assert.That(target.GetComponent<NpcMovement2D>(), Is.Not.Null);
            var animator = target.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Interact));
            Assert.That(target.InteractionRadius, Is.EqualTo(1.4f));
            Assert.That(target.GetComponent<WorldDialogueSpeakers>().TryResolve("guard", out var anchor, out _, out _), Is.True);
            Assert.That(anchor.IsChildOf(target.transform), Is.True);
            using var data = new SerializedObject(interaction);
            Assert.That(data.FindProperty("actor").objectReferenceValue, Is.SameAs(target.GetComponent<NpcActor>()));
            Assert.That(data.FindProperty("useStoryPresence").boolValue, Is.False,
                "Camp occupancy persists independently of whether the guard still offers dialogue.");
            Assert.That(data.FindProperty("minimumDay").intValue, Is.Zero);
            Assert.That(data.FindProperty("minimumDayStartsOnReturn").boolValue, Is.True);
            Assert.That(data.FindProperty("presenceCondition").stringValue, Is.Empty);
            Assert.That(data.FindProperty("committedCondition").stringValue, Is.Empty);
            Assert.That(data.FindProperty("sequences").arraySize, Is.Zero, "A standing conversation must not add a departure.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ReapplyingMigrationPreservesInspectorChangesAndSpeakerPlacement()
    {
        PrepareIsolatedGuard();
        var target = GameObject.Find(ActorName).GetComponent<WorldInteractionTarget>();
        var interaction = target.GetComponent<NpcInteraction>();
        Assert.That(interaction, Is.Not.Null);
        target.ConfigureStart(WorldInteractionStartMode.Automatic, .9f);
        target.transform.position += new Vector3(2, 3, 0);
        target.GetComponent<NpcMovement2D>().Configure(3.7f);
        var speakers = target.GetComponent<WorldDialogueSpeakers>();
        Assert.That(speakers.TryResolve("guard", out var anchor, out _, out _), Is.True);
        anchor.localPosition += Vector3.up * .2f;
        using (var data = new SerializedObject(interaction))
        {
            var sequences = data.FindProperty("sequences");
            sequences.arraySize = 1;
            var sequence = sequences.GetArrayElementAtIndex(0);
            sequence.FindPropertyRelative("id").stringValue = "authored-greeting";
            sequence.FindPropertyRelative("trigger").enumValueIndex = (int)NpcSequenceTrigger.Named;
            sequence.FindPropertyRelative("requiredCondition").stringValue = "flag.authored_guard_condition";
            var actions = sequence.FindPropertyRelative("actions");
            actions.arraySize = 1;
            actions.GetArrayElementAtIndex(0).FindPropertyRelative("kind").enumValueIndex = (int)NpcActionKind.Wait;
            actions.GetArrayElementAtIndex(0).FindPropertyRelative("duration").floatValue = .75f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        string authored = EditorJsonUtility.ToJson(interaction);
        string targetSettings = EditorJsonUtility.ToJson(target);
        string movement = EditorJsonUtility.ToJson(target.GetComponent<NpcMovement2D>());
        var position = target.transform.position;
        var anchorPosition = anchor.position;
        var animatorController = target.GetComponent<Animator>().runtimeAnimatorController;
        NpcInteractionSetup.ConfigureGuardTarget(target);
        Assert.That(target.GetComponent<NpcInteraction>(), Is.SameAs(interaction));
        Assert.That(EditorJsonUtility.ToJson(interaction), Is.EqualTo(authored));
        Assert.That(EditorJsonUtility.ToJson(target), Is.EqualTo(targetSettings));
        Assert.That(EditorJsonUtility.ToJson(target.GetComponent<NpcMovement2D>()), Is.EqualTo(movement));
        Assert.That(target.transform.position, Is.EqualTo(position));
        Assert.That(anchor.position, Is.EqualTo(anchorPosition));
        Assert.That(target.GetComponent<Animator>().runtimeAnimatorController, Is.SameAs(animatorController));
        Assert.That(target.GetComponents<NpcInteraction>().Length, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PresenceAndIdleFollowExistingDayRouteAndRestoredProgress()
    {
        PrepareIsolatedGuard();
        yield return new EnterPlayMode();
        yield return VerifyPresenceAndIdle();
    }

    private IEnumerator VerifyPresenceAndIdle()
    {
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 0;
        state.GameFlow = EGameFlow.CommuteIn;
        var target = GameObject.Find(ActorName).GetComponent<WorldInteractionTarget>();
        var interaction = target.GetComponent<NpcInteraction>();
        var actor = target.GetComponent<NpcActor>();
        var original = StoryCsvCompiler.Compile(CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
            .Read<NewStreetData>("script/street")).Scenes.Single(value => value.Id == StoryId);
        Assert.That(OutsideStorySceneRules.TryBind(original, "OutSide", out var story), Is.True);
        story.RuntimeScene = target.gameObject.scene.name;
        isolatedPlayer = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(state, isolatedPlayer, isolatedPlayer);
        interaction.Initialize(conditions, null, story, null, null);
        Assert.That(actor.IsVisible, Is.False, "The original guard is absent on the initial outbound commute.");
        state.GameFlow = EGameFlow.CommuteOut;
        yield return Frames();
        Assert.That(actor.IsVisible, Is.True, "The guard starts appearing on day zero's return commute.");
        var renderer = target.GetComponentInChildren<SpriteRenderer>();
        var firstSprite = renderer.sprite;
        yield return WaitUntil(() => renderer.sprite != firstSprite, "The guard's idle animation is not driven by Animator.", 3);
        Assert.That(target.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        for (int day = 1; day <= 3; day++)
            foreach (var flow in new[] { EGameFlow.CommuteIn, EGameFlow.CommuteOut })
            {
                state.CurrentDay = day;
                state.GameFlow = flow;
                yield return Frames();
                Assert.That(actor.IsVisible, Is.True, "The guard must remain until the pass condition changes.");
                Assert.That(interaction.CanInteract, Is.True);
            }
        isolatedPlayer.AddFlag("gang_pass_granted", true);
        yield return Frames();
        Assert.That(actor.IsVisible, Is.True, "The web guard stays in camp after granting passage.");
        Assert.That(interaction.CanInteract, Is.True, "The participant remains available; CSV resolution suppresses the completed guard dialogue.");
        // SaveManager uses these progress fields; check restore without writing any user's save slot.
        var restored = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            restored.ReplaceProgress(isolatedPlayer.HasMoney(), isolatedPlayer.GetSkillValue(),
                isolatedPlayer.ReadAffinity(), isolatedPlayer.ReadFlags());
            interaction.Initialize(new ConditionUtil(state, restored, restored), null, story, null, null);
            Assert.That(actor.IsVisible, Is.True, "Restoring the pass keeps the guard physically in camp.");
            Assert.That(restored.CheckFlag("gang_pass_granted"), Is.True);
        }
        finally
        {
            interaction.Initialize(conditions, null, story, null, null);
            Object.DestroyImmediate(restored);
        }
    }

    private void CompleteProgression(GameProgressionResult result)
    {
        progressionResult = result;
        progressionFinished = true;
    }

    private IEnumerator WaitForProgression(string stage)
    {
        yield return WaitUntil(() => progressionFinished, stage + " timed out.", 30);
        Assert.That(progressionResult.Succeeded, Is.True, stage + ": " + progressionResult.Message);
    }

    private static IEnumerator FinishDialogue(WorldInteractionController controller, WorldInteractionTarget target)
    {
        yield return WaitUntil(() => controller.IsRunning, "The direct interaction did not begin.");
        float deadline = Time.realtimeSinceStartup + 30;
        while (controller.IsRunning && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(controller.ActiveTarget, Is.SameAs(target));
            controller.TryHandleInput();
            yield return null;
        }
        Assert.That(controller.IsRunning, Is.False, "The real guard dialogue did not finish.");
        Assert.That(controller.LastResult.Completed, Is.True, controller.LastResult.Error);
    }

    [UnityTest]
    public IEnumerator DirectInputRepeatsDialogueAndTheDayTwoPassPersistsOnRevisit()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        // Closures and scene references must be created after the play-mode domain reload.
        yield return VerifyRealManualDialogueAndRevisit();
    }

    private IEnumerator VerifyRealManualDialogueAndRevisit()
    {
        Time.timeScale = 1;
        yield return WaitUntil(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null,
            "Real CSV load timed out.", 30);
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        Assert.That(NewDataLoadManager.IsLoaded, Is.True);
        yield return null;
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope.Container != null);
        Assert.That(root, Is.Not.Null);
        var progression = root.Container.Resolve<GameProgressionService>();
        var conditions = root.Container.Resolve<IConditionUtil>();
        sharedPlayer = root.Container.Resolve<PlayerDataSO>();
        originalMoney = sharedPlayer.HasMoney();
        originalSkill = sharedPlayer.GetSkillValue();
        originalAffinity = sharedPlayer.ReadAffinity();
        originalFlags = sharedPlayer.ReadFlags();
        progressionFinished = false;
        progression.StartNewGameAsync(() => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("New game to Home");
        GameStateManager.Instance.CurrentDay = 1;
        conditions.ApplyRequired("flag.day1_thug_done = true; flag.gang_pass_granted = false");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Outside entry");
        var outside = SceneManager.GetActiveScene();
        var controller = WorldInteractionController.ForScene(outside);
        Assert.That(controller, Is.Not.Null);
        yield return WaitUntil(() => controller.IsReady, "Outside interactions did not initialize.", 30);
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == outside);
        Assert.That(context.IsReady, Is.True, context.InitializationError);
        var target = FindGuard(outside);
        var actor = target.GetComponent<NpcActor>();
        var interaction = target.GetComponent<NpcInteraction>();
        var origin = actor.transform.position;
        context.Player.transform.position = target.transform.position;
        yield return WaitUntil(() => controller.FocusedTarget == target, "The nearby guard did not receive manual focus.");
        yield return Frames(8);
        Assert.That(controller.IsRunning, Is.False, "Entering the guard's radius must not start an automatic dialogue.");
        Assert.That(actor.IsVisible, Is.True);
        int money = sharedPlayer.HasMoney();
        for (int visit = 0; visit < 2; visit++)
        {
            yield return Frames();
            Assert.That(controller.TryHandleInput(), Is.True);
            yield return FinishDialogue(controller, target);
            Assert.That(actor.IsVisible, Is.True, "A normal blocked dialogue must keep the guard in the world.");
            Assert.That(actor.transform.position, Is.EqualTo(origin));
            Assert.That(interaction.CanInteract, Is.True);
            Assert.That(conditions.CheckRequired("flag.gang_pass_granted"), Is.False);
            Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(money));
            Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
            Assert.That(context.IsSafeForSave, Is.True);
        }
        yield return Frames();
        Assert.That(controller.TryHandleInput(), Is.True);
        yield return WaitUntil(() => controller.IsRunning, "The cancellable guard dialogue did not begin.");
        controller.Cancel();
        yield return WaitUntil(() => !controller.IsRunning, "Guard cancellation did not release the session.");
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(interaction.CanInteract, Is.True);
        Assert.That(conditions.CheckRequired("flag.gang_pass_granted"), Is.False);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        GameStateManager.Instance.CurrentDay = 2;
        yield return Frames();
        Assert.That(controller.TryResolve(target, out var dayTwo), Is.True);
        Assert.That(dayTwo.Steps.Any(step => step.Command == OutsideStoryCommand.GuardChoice), Is.True);
        Assert.That(controller.TryHandleInput(), Is.True);
        yield return FinishDialogue(controller, target); // Existing first choice is "술을 찾고 있어요.".
        Assert.That(conditions.CheckRequired("flag.gang_pass_granted"), Is.True);
        Assert.That(actor.IsVisible, Is.True, "Granting passage must not remove the camp actor.");
        Assert.That(interaction.CanInteract, Is.True);
        Assert.That(controller.TryResolve(target, out _), Is.False);
        Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(money));
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(context.IsSafeForSave, Is.True);
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.HomeVisit).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Home revisit");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Outside revisit");
        var revisited = SceneManager.GetActiveScene();
        var revisitedController = WorldInteractionController.ForScene(revisited);
        Assert.That(revisitedController, Is.Not.Null);
        yield return WaitUntil(() => revisitedController.IsReady, "Revisited outside interactions did not initialize.", 30);
        var returned = FindGuard(revisited);
        Assert.That(conditions.CheckRequired("flag.gang_pass_granted"), Is.True);
        Assert.That(returned.GetComponent<NpcActor>().IsVisible, Is.True);
        Assert.That(returned.GetComponent<NpcInteraction>().CanInteract, Is.True);
        Assert.That(revisitedController.TryResolve(returned, out _), Is.False);
    }
}
