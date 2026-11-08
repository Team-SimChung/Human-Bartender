using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>실제 수리공 선택창에서 거부 표시·입력·재화 보존·취소·재방문을 확인한다. CSV와 저장 슬롯은 쓰지 않는다.</summary>
public sealed class ChoiceRejectionRuntimeTests
{
    private SceneSetup[] sceneSetup;
    private PlayerDataSO sharedPlayer;
    private IConditionUtil conditions;
    private int originalMoney;
    private int originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool progressionFinished;
    private GameProgressionResult progressionResult;
    private bool interactionFinished;
    private StoryExecutionResult interactionResult;
    private Exception interactionFailure;

    [UnityTest]
    public IEnumerator RepairChoicesRejectWithoutSpendingAndRestoreOnCancelAndReopen()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyRealChoices();
    }

    private IEnumerator VerifyRealChoices()
    {
        Time.timeScale = 1;
        float deadline = Time.realtimeSinceStartup + 30;
        while (!NewDataLoadManager.IsLoaded && NewDataLoadManager.LastLoadError == null &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        Assert.That(NewDataLoadManager.IsLoaded, Is.True, "실제 CSV 초기화 시간이 초과되었습니다.");
        yield return null;

        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
            .FirstOrDefault(value => value.Container != null);
        Assert.That(root, Is.Not.Null);
        var progression = root.Container.Resolve<GameProgressionService>();
        conditions = root.Container.Resolve<IConditionUtil>();
        sharedPlayer = root.Container.Resolve<PlayerDataSO>();
        originalMoney = sharedPlayer.HasMoney();
        originalSkill = sharedPlayer.GetSkillValue();
        originalAffinity = sharedPlayer.ReadAffinity();
        originalFlags = sharedPlayer.ReadFlags();

        progressionFinished = false;
        progression.StartNewGameAsync(SkipOpening, RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("새 게임의 Home 진입");
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.Language = ELanguage.Ko;
        sharedPlayer.SetMoney(300);
        conditions.ApplyRequired("flag.repairman_met = true; flag.has_gear_part = false; " +
            "flag.elevator_repair_paid = false; flag.elevator_repaired = false");
        SceneManager.sceneLoaded += SuspendUnrelatedInteractions;
        progressionFinished = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitForProgression("OutSide 진입");
        SceneManager.sceneLoaded -= SuspendUnrelatedInteractions;
        var scene = SceneManager.GetActiveScene();
        Assert.That(scene.name, Is.EqualTo("OutSide"));
        var controller = WorldInteractionController.ForScene(scene);
        Assert.That(controller, Is.Not.Null);
        deadline = Time.realtimeSinceStartup + 30;
        while (!controller.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(controller.IsReady, Is.True);
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == scene);
        Assert.That(context.IsReady, Is.True, context.InitializationError);
        Assert.That(controller.IsRunning, Is.False);
        var target = WorldInteractionTarget.InScene(scene)
            .Single(value => value.StorySceneId == "neighborhood-repairGreeting");
        Assert.That(target.GetComponent<NpcActor>().IsVisible, Is.True);
        context.Player.GetComponent<PlayerMovement2D>().GetMoveInput(Vector2.zero);
        context.Player.transform.position = target.transform.position + Vector3.right * .3f;
        Object.FindObjectsByType<OutsideCamera>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == scene).SnapToFollow();
        var flagsBefore = sharedPlayer.ReadFlags();
        var historyBefore = ReadHistory(context);

        ObserveInteraction(controller, target).Forget();
        yield return AdvanceUntilMenu(controller);
        var view = controller.View;
        var rows = view.Choices.Rows.Where(value => value.gameObject.activeSelf).ToArray();
        Assert.That(rows.Select(value => value.name), Is.EqualTo(new[] { "Choice_gear", "Choice_pay", "Choice_cancel" }));
        Assert.That(rows[0].IsFocused, Is.True, "불가 항목도 처음부터 포커스로 가리킬 수 있어야 합니다.");
        Assert.That(rows[0].CanExecute, Is.False);
        Assert.That(rows[1].CanExecute, Is.False);
        Assert.That(rows[2].CanExecute, Is.True);
        string gearLabel = rows[0].Label.text;
        string payLabel = rows[1].Label.text;
        Assert.That(gearLabel, Is.EqualTo("기어 부품 건네주기"));
        Assert.That(payLabel, Is.EqualTo("비용 건네주기 · 400원"));
        Assert.That(rows.All(value => value.transform.Find("LockReason") == null), Is.True);

        // 수리공 선택지는 기존 사건 코드가 만드는 메뉴다. CSV 사유 검사는 별도 실제 CSV 회귀 검사에서 수행한다.
        string expectedReason = OutsideStorySteps.Option("gear", gearLabel).LockReason.Ko;
        yield return RejectAndInspect(controller, rows[0], expectedReason);
        AssertUnchanged(controller, context, flagsBefore, historyBefore);

        view.MoveChoice(1);
        Assert.That(rows[0].IsRejected, Is.False);
        Assert.That(rows[0].Label.text, Is.EqualTo(gearLabel));
        Assert.That(rows[1].IsFocused, Is.True);
        yield return RejectAndInspect(controller, rows[1], expectedReason);
        AssertUnchanged(controller, context, flagsBefore, historyBefore);
        view.MoveChoice(1);
        Assert.That(rows[1].IsRejected, Is.False);
        Assert.That(rows[1].Label.text, Is.EqualTo(payLabel));
        view.ConfirmChoice();
        yield return WaitForInteraction();
        AssertCompleted(controller, context);
        Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(300));
        Assert.That(sharedPlayer.ReadFlags(), Is.EquivalentTo(flagsBefore));
        Assert.That(target.GetComponent<NpcActor>().IsVisible, Is.True);

        ObserveInteraction(controller, target).Forget();
        yield return AdvanceUntilMenu(controller);
        rows = view.Choices.Rows.Where(value => value.gameObject.activeSelf).ToArray();
        Assert.That(rows[0].Label.text, Is.EqualTo(gearLabel));
        Assert.That(rows[1].Label.text, Is.EqualTo(payLabel));
        Assert.That(rows.All(value => !value.IsRejected), Is.True, "재방문 시 이전 거부 표시가 남으면 안 됩니다.");
        view.MoveChoice(1);
        view.MoveChoice(1);
        view.ConfirmChoice();
        yield return WaitForInteraction();
        AssertCompleted(controller, context);
        Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(300));
        Assert.That(sharedPlayer.ReadFlags(), Is.EquivalentTo(flagsBefore));
    }

    private IEnumerator RejectAndInspect(WorldInteractionController controller, WorldInteractionChoiceItem row,
        string expectedReason)
    {
        var originalMaterial = row.Label.fontSharedMaterial;
        controller.View.ConfirmChoice();
        Assert.That(row.IsRejected, Is.True);
        Assert.That(row.Label.text, Is.EqualTo(expectedReason));
        using (var settings = new SerializedObject(row))
        {
            Assert.That(row.Background.color, Is.EqualTo(settings.FindProperty("rejectedBackgroundColor").colorValue));
            Assert.That(row.Label.color, Is.EqualTo(settings.FindProperty("rejectedTextColor").colorValue));
        }
        bool sawShader = false;
        float deadline = Time.realtimeSinceStartup + .5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            var material = row.Label.fontSharedMaterial;
            if (material != null && material.shader.name == "Custom/TMPGlitch" && material.GetFloat("_GlitchStrength") > .5f)
                sawShader = true;
            yield return null;
        }
        Assert.That(sawShader, Is.True, "실제 월드 선택지에서 기존 TMP 글리치 셰이더가 재생되어야 합니다.");
        Assert.That(row.Label.fontSharedMaterial, Is.SameAs(originalMaterial));
        Assert.That(row.IsRejected, Is.True);
        Assert.That(row.Label.text, Is.EqualTo(expectedReason));

        // 같은 항목 재확정은 대화 결과를 실행하지 않고 효과를 다시 시작한다.
        controller.View.ConfirmChoice();
        Assert.That(row.IsRejected, Is.True);
        Assert.That(interactionFinished, Is.False);
        deadline = Time.realtimeSinceStartup + .5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.View.IsChoosing, Is.True);
            yield return null;
        }
        Assert.That(row.Label.fontSharedMaterial, Is.SameAs(originalMaterial));
        Assert.That(row.Label.text, Is.EqualTo(expectedReason));
    }

    private void AssertUnchanged(WorldInteractionController controller, WorldInteractionSceneContext context,
        Dictionary<string, bool> flags, string[] history)
    {
        Assert.That(interactionFinished, Is.False);
        Assert.That(controller.IsRunning, Is.True);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.Interct));
        Assert.That(sharedPlayer.HasMoney(), Is.EqualTo(300));
        Assert.That(sharedPlayer.ReadFlags(), Is.EquivalentTo(flags));
        Assert.That(ReadHistory(context), Is.EquivalentTo(history));
    }

    private static string[] ReadHistory(WorldInteractionSceneContext context)
    {
        var field = typeof(WorldInteractionSceneContext).GetField("onceHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((HashSet<string>)field.GetValue(context)).OrderBy(value => value).ToArray();
    }

    private static void SuspendUnrelatedInteractions(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "OutSide") return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var target in root.GetComponentsInChildren<WorldInteractionTarget>(true))
                if (target.StorySceneId != "neighborhood-repairGreeting") target.enabled = false;
            foreach (var ambient in root.GetComponentsInChildren<WorldAmbientDialogueController>(true)) ambient.enabled = false;
        }
    }

    private void SkipOpening() => conditions.ApplyRequired("flag.opening_seen = true");

    private void CompleteProgression(GameProgressionResult value)
    {
        progressionResult = value;
        progressionFinished = true;
    }

    private IEnumerator WaitForProgression(string stage)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!progressionFinished && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(progressionFinished, Is.True, stage + " 시간이 초과되었습니다.");
        Assert.That(progressionResult.Succeeded, Is.True, stage + ": " + progressionResult.Message);
    }

    private async UniTask ObserveInteraction(WorldInteractionController controller, WorldInteractionTarget target)
    {
        interactionFinished = false;
        interactionFailure = null;
        try { interactionResult = await controller.PlayAsync(target); }
        catch (Exception error) { interactionFailure = error; }
        finally { interactionFinished = true; }
    }

    private IEnumerator AdvanceUntilMenu(WorldInteractionController controller)
    {
        float deadline = Time.realtimeSinceStartup + 20;
        while (!controller.View.IsChoosing && !interactionFinished && Time.realtimeSinceStartup < deadline)
        {
            controller.TryHandleInput();
            yield return null;
        }
        Assert.That(controller.View.IsChoosing, Is.True, interactionFailure?.ToString() ?? interactionResult.Error);
        while (controller.View.Choices.IsRevealing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(controller.View.Choices.IsRevealing, Is.False);
        yield return null;
    }

    private IEnumerator WaitForInteraction()
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (!interactionFinished && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(interactionFinished, Is.True, "취소 선택 이후 대화 완료 시간이 초과되었습니다.");
    }

    private void AssertCompleted(WorldInteractionController controller, WorldInteractionSceneContext context)
    {
        Assert.That(interactionFailure, Is.Null);
        Assert.That(interactionResult.Completed, Is.True, interactionResult.Error);
        Assert.That(controller.IsRunning, Is.False);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
    }

    private void RestorePlayerSnapshot()
    {
        if (sharedPlayer != null && originalFlags != null)
            sharedPlayer.ReplaceProgress(originalMoney, originalSkill, originalAffinity, originalFlags);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        SceneManager.sceneLoaded -= SuspendUnrelatedInteractions;
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
        if (sceneSetup != null && sceneSetup.Any(value => value.isLoaded && value.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }
}
