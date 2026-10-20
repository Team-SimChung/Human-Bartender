using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>실제 Home에서 공통 소파 메뉴와 격리된 저장 슬롯을 검증함.</summary>
public static class HomeSofaMenuRuntime
{
    private const string Folder = "Library/WorldInteractionValidation/sofa-choices/";

    public static async UniTask ExerciseAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(70));
        var token = timeout.Token;
        var scene = SceneManager.GetActiveScene();
        Assert.AreEqual("Home", scene.name);
        var controller = WorldInteractionController.ForScene(scene);
        var context = controller.GetComponent<WorldInteractionSceneContext>();
        var target = WorldInteractionTarget.InScene(scene).Single(value => value.Content != null && value.Content.SourceId == "home_sofa");
        var sofa = (HomeSofaAction)target.Action;
        var saves = Get<SaveManager>(sofa, "saves");
        var wallet = Get<PlayerDataSO>(saves, "player");
        int originalMoney = wallet.HasMoney();
        string directory = Path.GetFullPath(Folder + "slots-" + Guid.NewGuid().ToString("N"));
        var isolated = (SaveManager)Activator.CreateInstance(typeof(SaveManager), BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { wallet, Get<GameProgressionService>(saves, "progression"), GameStateManager.Instance, directory }, null);
        Set(sofa, "saves", isolated);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            var playerPosition = context.Player.transform.position;
            playerPosition.x = target.transform.position.x;
            context.Player.transform.position = playerPosition;
            for (int frame = 0; frame < 30; frame++) await UniTask.NextFrame(token);
            var session = controller.PlayAsync(target);
            await WaitForMenu(controller, "sleep", token);
            CollectionAssert.AreEqual(new[] { "수면", "저장", "취소" },
                controller.View.Choices.Rows.Where(row => row.gameObject.activeSelf).Select(row => row.Label.text).ToArray());
            Assert.IsFalse(controller.View.Bubble.gameObject.activeSelf);
            Assert.IsFalse(controller.View.Choices.MoneyLabel.gameObject.activeSelf);
            Assert.IsTrue(context.IsSafeForSave);
            Assert.AreNotEqual(EInteractorState.None, context.Player.State);
            Assert.Less(controller.View.Choices.Rect.position.x, Camera.main.WorldToScreenPoint(context.Player.transform.position).x);
            await Capture("sofa-menu", token);

            await Choose(controller, "save", token);
            await WaitForMenu(controller, "slot-1", token);
            Assert.AreEqual(4, controller.View.Choices.Rows.Count(row => row.gameObject.activeSelf));
            await Choose(controller, "next", token);
            await WaitForMenu(controller, "slot-3", token);
            await Choose(controller, "next", token);
            await WaitForMenu(controller, "slot-5", token);
            await Capture("sofa-save-slots", token);
            await Choose(controller, "slot-5", token);
            await WaitForMenu(controller, "ok", token);
            Assert.IsTrue(isolated.HasSlotFile(5), "5번 슬롯도 공통 선택창에서 저장할 수 있어야 함. 표시 결과: " +
                controller.View.Bubble.textLabel.text);
            string path = Path.Combine(directory, "slot-5.json");
            string saved = File.ReadAllText(path);
            Assert.AreEqual(GameStateManager.Instance.CurrentDay, (int)JObject.Parse(saved)["current_day"]);
            Assert.IsTrue(controller.View.Bubble.gameObject.activeSelf);
            await Capture("sofa-save-result", token);
            await Choose(controller, "ok", token);

            await Choose(controller, "save", token);
            await Choose(controller, "next", token);
            await Choose(controller, "next", token);
            await Choose(controller, "slot-5", token);
            await WaitForMenu(controller, "overwrite", token);
            await Capture("sofa-overwrite", token);
            await Choose(controller, "back", token);
            Assert.AreEqual(saved, File.ReadAllText(path), "덮어쓰기 취소 시 기존 저장을 보존해야 함.");
            wallet.SetMoney(originalMoney + 23);
            await Choose(controller, "slot-5", token);
            await Choose(controller, "overwrite", token);
            await WaitForMenu(controller, "ok", token);
            Assert.AreEqual(originalMoney + 23, (int)JObject.Parse(File.ReadAllText(path))["money"]);
            await Choose(controller, "ok", token);
            await Choose(controller, "cancel", token, mouse: true);
            Assert.IsTrue((await session).Completed);
            AssertClosed(controller, context, sofa);

            session = controller.PlayAsync(target);
            await WaitForMenu(controller, "sleep", token);
            await PressEscape(keyboard, token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await session).Status);
            AssertClosed(controller, context, sofa);

            session = controller.PlayAsync(target);
            await WaitForMenu(controller, "sleep", token);
            sofa.enabled = false;
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await session).Status);
            AssertClosed(controller, context, sofa);
            sofa.enabled = true;
            controller.View.Begin(target, context.Player.transform);
            controller.View.ShowOutsideChoices(new[] { new NewStreetOptionData { Id = "ordinary", Text = new Texts { Ko = "대화" } } }, _ => { });
            var prefab = Get<WorldInteractionChoiceView>(controller.View, "choicesPrefab");
            Assert.AreEqual(prefab.Rect.localScale, controller.View.Choices.Rect.localScale);
            Assert.IsTrue(controller.View.Choices.MoneyLabel.gameObject.activeSelf, "메뉴 종료 후 일반 대화의 소지금 표시를 복원해야 함.");
            Assert.That(controller.View.Choices.Rows[0].Rect.rect.width, Is.EqualTo(Get<float>(prefab, "minimumItemWidth")).Within(.01f));
            controller.View.EndScene();
            File.WriteAllText(Folder + "menu-runtime.txt", "PASS: left-side prefab menu, no bubble/money header, all 5 slots, save result, overwrite cancel/confirm, keyboard/mouse selection, Esc/component-disable cancellation and restored controls. Saves isolated at " + directory);
        }
        finally
        {
            controller.Cancel();
            sofa.enabled = true;
            Set(sofa, "saves", saves);
            wallet.SetMoney(originalMoney);
            InputSystem.RemoveDevice(keyboard);
        }
    }

    private static void AssertClosed(WorldInteractionController controller, WorldInteractionSceneContext context, HomeSofaAction sofa)
    {
        Assert.IsFalse(controller.IsRunning);
        Assert.IsFalse(controller.View.IsChoosing);
        Assert.IsFalse(controller.View.Bubble.gameObject.activeSelf);
        Assert.IsFalse(sofa.CanSave);
        Assert.AreEqual(EInteractorState.None, context.Player.State);
    }

    private static async UniTask WaitForMenu(WorldInteractionController controller, string id, CancellationToken token)
    {
        await UniTask.NextFrame(token);
        await UniTask.WaitUntil(() => controller.View.IsChoosing && !controller.View.Choices.IsRevealing, cancellationToken: token);
        Assert.IsTrue(controller.View.Choices.Rows.Any(row => row.gameObject.activeSelf && row.name == "Choice_" + id), id);
        Assert.LessOrEqual(controller.View.Choices.Rows.Count(row => row.gameObject.activeSelf), 4);
    }

    private static async UniTask Choose(WorldInteractionController controller, string id, CancellationToken token, bool mouse = false)
    {
        await WaitForMenu(controller, id, token);
        var row = controller.View.Choices.Rows.Single(item => item.gameObject.activeSelf && item.name == "Choice_" + id);
        if (mouse) row.Button.onClick.Invoke();
        else
        {
            for (int i = 0; i < 4 && !row.IsFocused; i++) controller.View.MoveChoice(1);
            Assert.IsTrue(row.IsFocused);
            controller.TryHandleInput();
        }
    }

    private static async UniTask PressEscape(Keyboard keyboard, CancellationToken token)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await UniTask.NextFrame(token);
    }

    private static async UniTask Capture(string name, CancellationToken token)
    {
        Directory.CreateDirectory(Folder);
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + name + ".png"));
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
    }

    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
