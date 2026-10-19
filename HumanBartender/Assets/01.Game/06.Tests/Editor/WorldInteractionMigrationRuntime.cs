using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldInteractionMigrationRuntime
{
    public static async UniTask ExerciseAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        var token = timeout.Token;
        var scene = SceneManager.GetActiveScene();
        Assert.AreEqual("Home", scene.name);
        var controller = WorldInteractionController.ForScene(scene);
        var context = controller.GetComponent<WorldInteractionSceneContext>();
        var runner = context.Runner;
        Assert.IsNotNull(Get<ICameraImpulse>(controller.View, "cameraImpulse"));
        var data = Get<IPlayerDataReader>(controller, "playerData") as PlayerDataSO;
        GameStateManager.Instance.CurrentDay = 0;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        data.AddFlag("home_night0_seen", false);
        context.RefreshContents();
        await HomeSofaMenuRuntime.ExerciseAsync();
        var sofa = WorldInteractionTarget.InScene(scene).Single(target => target.Content != null && target.Content.SourceId == "home_sofa");
        context.Player.transform.position = sofa.transform.position;
        await UniTask.NextFrame(token);
        var session = controller.PlayAsync(sofa);
        Assert.IsTrue(controller.IsRunning);
        Assert.IsTrue(context.IsSafeForSave, "소파 메뉴가 열려 있어도 수동 저장이 가능해야 함.");
        await SelectSleepAsync(controller, token);
        await UniTask.WaitUntil(() => runner.IsRunning, cancellationToken: token);
        Assert.IsFalse(context.IsSafeForSave, "취침 대화 중에는 저장할 수 없어야 함.");
        Assert.IsTrue(controller.View.Bubble.gameObject.activeSelf);
        controller.Cancel();
        Assert.AreEqual(StoryExecutionStatus.Cancelled, (await session).Status);
        Assert.AreEqual(0, GameStateManager.Instance.CurrentDay);
        Assert.IsFalse(data.CheckFlag("home_night0_seen"));
        await UniTask.NextFrame(token);
        session = controller.PlayAsync(sofa);
        await SelectSleepAsync(controller, token);
        while (controller.IsRunning)
        {
            token.ThrowIfCancellationRequested();
            if (controller.View.IsChoosing)
            {
                if (!controller.View.Choices.IsRevealing) controller.View.ConfirmChoice();
            }
            else if (runner.IsRunning) runner.AdvanceInputOutside();
            await UniTask.NextFrame(token);
        }
        Assert.IsTrue((await session).Completed, "소파가 다음 날 비활성화될 때 취침을 취소하면 안 됨.");
        Assert.IsTrue(data.CheckFlag("home_night0_seen"));
        Assert.AreEqual(1, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteIn, GameStateManager.Instance.GameFlow);
        Assert.IsTrue(context.IsReady);
        var door = WorldInteractionTarget.InScene(scene).Single(target => target.Content != null && target.Content.SourceId == "home_exit_door");
        Assert.IsTrue(controller.TryResolve(door, out _));
        context.Player.transform.position = door.transform.position;
        await UniTask.NextFrame(token);
        controller.PlayAsync(door).Forget();
        await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "OutSide" &&
            WorldInteractionController.ForScene(SceneManager.GetActiveScene())?.IsReady == true, cancellationToken: token);
        controller = WorldInteractionController.ForScene(SceneManager.GetActiveScene());
        context = controller.GetComponent<WorldInteractionSceneContext>();
        runner = context.Runner;
        var automatic = WorldInteractionTarget.InScene(context.gameObject.scene).Single(target => target.StorySceneId == "day1-corridor-thug");
        context.Player.transform.position = automatic.transform.position;
        await UniTask.WaitUntil(() => controller.IsRunning, cancellationToken: token);
        Assert.IsTrue(runner.IsRunning, "기존 Auto CSV 이벤트를 새 실행기에서 재생해야 함.");
        context.Player.transform.position = automatic.transform.position + Vector3.right * 10;
        controller.Cancel();
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.IsFalse(data.CheckFlag("day1_thug_done"));
        Assert.AreEqual(EInteractorState.None, context.Player.State);
        await UniTask.WaitUntil(() => !SceneTransitionManager.Instance.IsBusy, cancellationToken: token);
        await UniTask.NextFrame(token);
        File.WriteAllText("Library/WorldInteractionValidation/unification/migration-runtime.txt",
            "PASS: sofa save gate, night dialogue cancel/completion, sleep day transition, Home exit, CSV Auto event, restored controls.");
    }
    private static async UniTask SelectSleepAsync(WorldInteractionController controller, CancellationToken token)
    {
        await UniTask.WaitUntil(() => controller.View.IsChoosing && !controller.View.Choices.IsRevealing, cancellationToken: token);
        Assert.AreEqual("수면", controller.View.Choices.Rows[0].Label.text);
        controller.TryHandleInput();
    }
    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
}
