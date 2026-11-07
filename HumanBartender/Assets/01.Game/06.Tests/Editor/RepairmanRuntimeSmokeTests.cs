using System;
using System.Collections;
using System.Collections.Generic;
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

/// <summary>실제 씬 주입·대화 컨트롤러·화면 전환으로 수리와 재방문을 검증한다. 저장 슬롯은 쓰지 않는다.</summary>
public sealed class RepairmanRuntimeSmokeTests
{
    private SceneSetup[] sceneSetup;
    private PlayerDataSO sharedPlayer;
    private int originalMoney;
    private int originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool progressionFinished;
    private GameProgressionResult progressionResult;
    private bool interactionFinished;
    private StoryExecutionResult interactionResult;
    private Exception interactionFailure;

    private void CompleteProgression(GameProgressionResult value)
    {
        progressionResult = value;
        progressionFinished = true;
    }

    private async UniTask ObserveInteraction(WorldInteractionController controller, WorldInteractionTarget target)
    {
        interactionFinished = false;
        interactionFailure = null;
        try { interactionResult = await controller.PlayAsync(target); }
        catch (Exception error) { interactionFailure = error; }
        finally { interactionFinished = true; }
    }

    private IEnumerator WaitForProgression(string stage)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!progressionFinished && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(progressionFinished, Is.True, stage + " timed out.");
        Assert.That(progressionResult.Succeeded, Is.True, stage + ": " + progressionResult.Message);
    }

    private static WorldInteractionTarget FindRepairman(Scene scene) => WorldInteractionTarget.InScene(scene)
        .Single(target => target.StorySceneId == "neighborhood-repairGreeting");

    private static IEnumerator WaitForController(WorldInteractionController controller)
    {
        Assert.That(controller, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 30;
        while (!controller.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(controller.IsReady, Is.True, "The real outside interaction controller did not initialize.");
    }

    private static IEnumerator FinishArrivalDialogue(WorldInteractionController controller, WorldInteractionSceneContext context)
    {
        // Day 1's home doorway starts the existing automatic thug dialogue on Update.
        // Advance that real session before beginning a separate repair interaction.
        yield return null;
        float deadline = Time.realtimeSinceStartup + 30;
        while ((controller.IsRunning || context.Player.State != EInteractorState.None) &&
            Time.realtimeSinceStartup < deadline)
        {
            if (controller.IsRunning) controller.TryHandleInput();
            yield return null;
        }
        Assert.That(controller.IsRunning, Is.False,
            "Arrival dialogue did not finish: " + controller.ActiveTarget?.StorySceneId);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None),
            "Arrival dialogue did not return player input: " + controller.ActiveTarget?.StorySceneId);
        Assert.That(controller.LastResult.Completed, Is.True, controller.LastResult.Error);
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
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator PaidRepairCompletesThroughInjectedControllerAndDoesNotReappearOnARealSceneRevisit()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        // 재로드 이전 iterator closure에 런타임 컨테이너나 지역 변수를 보관하지 않는다.
        yield return VerifyInjectedRepairAndSceneRevisit();
    }

    private IEnumerator VerifyInjectedRepairAndSceneRevisit()
    {
        Time.timeScale = 1;
        float deadline = Time.realtimeSinceStartup + 30;
        while (!NewDataLoadManager.IsLoaded && NewDataLoadManager.LastLoadError == null &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        Assert.That(NewDataLoadManager.IsLoaded, Is.True, "Real CSV load timed out.");
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
        // The opening tutorial is unrelated to the repair; skip it using its existing progress flag.
        progression.StartNewGameAsync(
            () => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("New game to Home");
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"));

        GameStateManager.Instance.CurrentDay = 1;
        conditions.ApplyRequired("flag.repairman_met = true; flag.elevator_repair_paid = true; flag.elevator_repaired = false");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("First outside visit");
        var outside = SceneManager.GetActiveScene();
        Assert.That(outside.name, Is.EqualTo("OutSide"));
        var controller = WorldInteractionController.ForScene(outside);
        yield return WaitForController(controller);
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == outside);
        Assert.That(context.IsReady, Is.True, context.InitializationError);
        yield return FinishArrivalDialogue(controller, context);
        int moneyBeforeRepair = sharedPlayer.HasMoney();

        var target = FindRepairman(outside);
        var participant = target.GetComponent<NpcInteraction>();
        var actor = target.GetComponent<NpcActor>();
        Assert.That(participant, Is.Not.Null);
        Assert.That(actor, Is.Not.Null);
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(participant.CanInteract, Is.True, "Scene DI must initialize the participant without manual test injection.");
        Assert.That(controller.TryResolve(target, out var story), Is.True);
        Assert.That(story.Steps.Any(step => step.Command == OutsideStoryCommand.RepairElevator), Is.True);
        Assert.That(story.Steps.Any(step => step.Command == OutsideStoryCommand.RepairChoice), Is.False);

        var player = context.Player;
        Assert.That(player.State, Is.EqualTo(EInteractorState.None));
        player.transform.position = target.transform.position + Vector3.right * .3f;
        var start = actor.transform.position;
        Vector2 departureOffset;
        using (var repairSettings = new SerializedObject(participant))
            departureOffset = NpcSequenceTestSettings.Action(repairSettings, NpcActionKind.DepartWhileMoving)
                .FindPropertyRelative("offset").vector2Value;
        ObserveInteraction(controller, target).Forget();
        Assert.That(controller.IsRunning, Is.True);
        Assert.That(player.State, Is.EqualTo(EInteractorState.Interct));
        bool sawMovingFade = false;
        var repairRenderer = actor.GetComponentInChildren<SpriteRenderer>();
        deadline = Time.realtimeSinceStartup + 20;
        while (!interactionFinished && Time.realtimeSinceStartup < deadline)
        {
            if (actor.IsBusy && actor.IsVisible && repairRenderer.color.a > .01f && repairRenderer.color.a < .95f)
            {
                sawMovingFade = true;
                Assert.That(actor.GetComponent<NpcMovement2D>().IsMoving, Is.True,
                    "Repair departure keeps moving during its separate NPC fade.");
            }
            if (controller.IsRunning) controller.TryHandleInput();
            yield return null;
        }
        Assert.That(interactionFinished, Is.True, "The real paid dialogue, fade, or departure timed out.");
        Assert.That(interactionFailure, Is.Null);
        Assert.That(interactionResult.Completed, Is.True, interactionResult.Error);
        Assert.That(sawMovingFade, Is.True);
        Assert.That(conditions.CheckRequired("flag.elevator_repaired"), Is.True);
        Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(moneyBeforeRepair), "Resuming a paid repair must not charge again.");
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(Vector3.Distance(actor.transform.position,
            start + new Vector3(departureOffset.x, departureOffset.y, 0)), Is.LessThan(.0001f));
        var departedPosition = actor.transform.position;
        Assert.That(player.State, Is.EqualTo(EInteractorState.None), "The interaction lease must restore player input.");
        Assert.That(controller.IsRunning, Is.False);
        yield return null;
        yield return null;
        Assert.That(actor.transform.position, Is.EqualTo(departedPosition));
        Assert.That(controller.TryResolve(target, out _), Is.False);

        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.HomeVisit).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Home revisit");
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"));
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Second outside visit");
        var revisited = SceneManager.GetActiveScene();
        var newController = WorldInteractionController.ForScene(revisited);
        yield return WaitForController(newController);
        var returnedTarget = FindRepairman(revisited);
        Assert.That(conditions.CheckRequired("flag.elevator_repaired"), Is.True);
        Assert.That(returnedTarget.GetComponent<NpcActor>().IsVisible, Is.False);
        Assert.That(returnedTarget.GetComponent<NpcInteraction>().CanInteract, Is.False);
        Assert.That(newController.TryResolve(returnedTarget, out _), Is.False);
    }
}
