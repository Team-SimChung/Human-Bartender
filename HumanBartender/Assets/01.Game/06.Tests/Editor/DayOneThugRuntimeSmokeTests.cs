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

/// <summary>실제 CSV·씬 주입·자동 대화·입력 해제와 완료 후 재방문을 검사한다. 저장 슬롯은 쓰지 않는다.</summary>
public sealed class DayOneThugRuntimeSmokeTests
{
    private SceneSetup[] sceneSetup;
    private PlayerDataSO sharedPlayer;
    private int originalMoney;
    private int originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool progressionFinished;
    private GameProgressionResult progressionResult;

    private void CompleteProgression(GameProgressionResult value)
    {
        progressionResult = value;
        progressionFinished = true;
    }

    private IEnumerator WaitForProgression(string stage)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!progressionFinished && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(progressionFinished, Is.True, stage + " timed out.");
        Assert.That(progressionResult.Succeeded, Is.True, stage + ": " + progressionResult.Message);
    }

    private static WorldInteractionTarget FindThug(Scene scene) => WorldInteractionTarget.InScene(scene)
        .Single(target => target.StorySceneId == "day1-corridor-thug");

    private static IEnumerator WaitForController(WorldInteractionController controller)
    {
        Assert.That(controller, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 30;
        while (!controller.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(controller.IsReady, Is.True, "The real outside interaction controller did not initialize.");
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
    public IEnumerator RefusalApproachesPunchesDepartsAndRemainsCompletedOnARealSceneRevisit()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        // Runtime captures must be created after the domain reload.
        yield return VerifyInjectedRefusalAndRevisit();
    }

    private IEnumerator VerifyInjectedRefusalAndRevisit()
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
        progression.StartNewGameAsync(() => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("New game to Home");
        GameStateManager.Instance.CurrentDay = 1;
        conditions.ApplyRequired("flag.day1_thug_done = false; flag.day1_thug_choice = false");
        sharedPlayer.SetMoney(0); // Only the refusal option is selectable in the existing choice UI.
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
        var target = FindThug(outside);
        var interaction = target.GetComponent<NpcInteraction>();
        var actor = target.GetComponent<NpcActor>();
        Assert.That(interaction, Is.Not.Null);
        Assert.That(actor, Is.Not.Null);
        Vector2 departureOffset;
        using (var settings = new SerializedObject(interaction))
        {
            departureOffset = NpcSequenceTestSettings.Action(settings, NpcActionKind.DepartWhileMoving)
                .FindPropertyRelative("offset").vector2Value;
        }
        var start = actor.transform.position;
        var playerPosition = context.Player.transform.position;
        float expectedAttackX = playerPosition.x + .63f;
        bool sawSession = false;
        bool sawContact = false, sawRecoil = false, sawMovingFade = false;
        float previousX = actor.transform.position.x;
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        deadline = Time.realtimeSinceStartup + 30;
        while ((!conditions.CheckRequired("flag.day1_thug_done") || controller.IsRunning) && Time.realtimeSinceStartup < deadline)
        {
            if (controller.IsRunning)
            {
                sawSession = true;
                Assert.That(controller.ActiveTarget, Is.SameAs(target));
                Assert.That(context.Player.State, Is.EqualTo(EInteractorState.Interct));
                if (actor.IsBusy && actor.IsVisible)
                {
                    float x = actor.transform.position.x;
                    if (x >= playerPosition.x + .2f && x <= playerPosition.x + .34f) sawContact = true;
                    if (sawContact && x > previousX + .0001f && x <= expectedAttackX + .001f) sawRecoil = true;
                    if (renderer.color.a < .95f && renderer.color.a > .01f)
                    {
                        sawMovingFade = true;
                        Assert.That(actor.GetComponent<NpcMovement2D>().IsMoving, Is.True);
                    }
                    Assert.That(actor.transform.position.y, Is.EqualTo(start.y).Within(.0001f));
                }
                previousX = actor.transform.position.x;
                controller.TryHandleInput();
            }
            yield return null;
        }
        Assert.That(sawSession, Is.True, "The authored automatic entry did not begin through scene DI.");
        Assert.That(sawContact, Is.True, "The real refusal branch must reach the web contact offset.");
        Assert.That(sawRecoil, Is.True, "The contact snap must be followed by the web recoil movement.");
        Assert.That(sawMovingFade, Is.True, "The departure must fade while continuing to move.");
        Assert.That(controller.LastResult.Completed, Is.True, controller.LastResult.Error);
        Assert.That(conditions.CheckRequired("flag.day1_thug_done"), Is.True);
        Assert.That(sharedPlayer.CheckFlag("day1_thug_choice"), Is.False);
        Assert.That(sharedPlayer.HasMoney(), Is.Zero);
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(actor.transform.position.x, Is.EqualTo(expectedAttackX + departureOffset.x).Within(.0001f));
        Assert.That(actor.transform.position.y, Is.EqualTo(start.y + departureOffset.y).Within(.0001f));
        Assert.That(context.Player.transform.position, Is.EqualTo(playerPosition), "The authored hard-body reaction must not introduce player knockback.");
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(controller.IsRunning, Is.False);
        Assert.That(controller.TryResolve(target, out _), Is.False);

        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.HomeVisit).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Home revisit");
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("Second outside visit");
        var revisited = SceneManager.GetActiveScene();
        var newController = WorldInteractionController.ForScene(revisited);
        yield return WaitForController(newController);
        var returnedTarget = FindThug(revisited);
        Assert.That(conditions.CheckRequired("flag.day1_thug_done"), Is.True);
        Assert.That(returnedTarget.GetComponent<NpcActor>().IsVisible, Is.False);
        Assert.That(returnedTarget.GetComponent<NpcInteraction>().CanInteract, Is.False);
        Assert.That(newController.TryResolve(returnedTarget, out _), Is.False);
    }
}
