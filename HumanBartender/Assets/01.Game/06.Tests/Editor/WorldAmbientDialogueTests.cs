using System;
using System.Collections;
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

public sealed class WorldAmbientDialogueTests
{
    [Test]
    public void EntryRequiresLeavingTheRangeBeforeRepeating()
    {
        var gate = new WorldProximityEntry();
        Assert.IsFalse(gate.CanStart("day0", 2, 1, .2f));
        Assert.IsTrue(gate.CanStart("day0", .9f, 1, .2f));
        gate.Consume();
        Assert.IsFalse(gate.CanStart("day0", .5f, 1, .2f));
        Assert.IsFalse(gate.CanStart("day0", 1.1f, 1, .2f));
        Assert.IsFalse(gate.CanStart("day0", .9f, 1, .2f));
        Assert.IsFalse(gate.CanStart("day0", 1.3f, 1, .2f));
        Assert.IsTrue(gate.CanStart("day0", .9f, 1, .2f));
        gate.Consume();
        Assert.IsTrue(gate.CanStart("day1", .9f, 1, .2f));
    }

    [TestCase(0, EGameFlow.CommuteOut, EActivationMode.Interact, "home_tv_cutscene")]
    [TestCase(1, EGameFlow.CommuteIn, EActivationMode.Proximity, "home_tv_morning")]
    [TestCase(1, EGameFlow.CommuteOut, EActivationMode.Interact, "home_tv_cutscene")]
    [TestCase(2, EGameFlow.CommuteIn, EActivationMode.Interact, "home_tv_cutscene")]
    public void TvModeAndScriptAreSelectedFromExistingCsv(int day, EGameFlow phase, EActivationMode mode, string id)
    {
        var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var state = GameStateManager.Instance;
        int previous = state.CurrentDay;
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            state.CurrentDay = day;
            var conditions = new ConditionUtil(state, wallet, wallet);
            var definitions = csv.Read<NewInteractPointData[]>("interact_points_home");
            var definition = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv", phase, conditions);
            Assert.IsTrue(definition.HasValue);
            Assert.AreEqual(mode, definition.Value.ActivationMode);
            var dialogue = WorldInteractionContentRules.SelectDialogue(definition.Value.DialogueFlows, day, conditions, new System.Collections.Generic.HashSet<string>());
            Assert.AreEqual(id, dialogue.Value.SceneId);
            var story = csv.Read<NewStreetData>("script/home").Scenes.Single(row => row.Id == id);
            Assert.IsNotEmpty(story.Steps);
            if (mode == EActivationMode.Proximity) Assert.IsTrue(story.Steps.All(step => step.Type == "say" && step.Sync == "auto"));
        }
        finally { state.CurrentDay = previous; UnityEngine.Object.DestroyImmediate(wallet); }
    }

    [TestCase("Home"), TestCase("OutSide")]
    public void SceneUsesIndependentAmbientLayerAndValidSpeakerBindings(string name)
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/" + name + ".unity", OpenSceneMode.Additive);
        try
        {
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.AreEqual(1, objects.OfType<WorldAmbientDialogueController>().Count());
            var view = objects.OfType<WorldInteractionView>().Single();
            var layer = (RectTransform)new SerializedObject(view).FindProperty("ambientLayer").objectReferenceValue;
            Assert.IsNotNull(layer);
            Assert.AreEqual(view.transform.parent, layer.parent);
            Assert.AreEqual(Vector2.zero, layer.anchorMin);
            Assert.AreEqual(Vector2.one, layer.anchorMax);
            var group = objects.OfType<WorldInteractionTarget>().Single(t => t.Content != null && t.Content.SourceId == (name == "Home" ? "home_tv" : "street_neighbours"));
            var csv = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
            var story = csv.Read<NewStreetData>(name == "Home" ? "script/home" : "script/street").Scenes
                .Single(row => row.Id == (name == "Home" ? "home_tv_morning" : "street_neighbours_ambient"));
            Assert.DoesNotThrow(() => WorldAmbientDialogueController.ValidateScript(group, story, null));
            Assert.Throws<InvalidOperationException>(() => group.ResolveSpeaker("unwired_actor", null));
            if (name == "OutSide") Assert.AreNotSame(group.ResolveSpeaker("citizen_a", null), group.ResolveSpeaker("citizen_b", null));
            story.Steps = new[] { new Step { Type = "choice" } };
            Assert.Throws<InvalidOperationException>(() => WorldAmbientDialogueController.ValidateScript(group, story, null));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest, Explicit("실제 Home·OutSide에서 자동 진행과 수동 대화 간 독립성을 확인함.")]
    public IEnumerator AmbientRuntime()
    {
        EditorSceneManager.OpenScene(WorldInteractionCutsceneTests.OutsidePath);
        yield return new EnterPlayMode();
        yield return WorldAmbientDialogueRuntime.ExerciseAsync().ToCoroutine();
        yield return new ExitPlayMode();
    }
}

internal static class WorldAmbientDialogueRuntime
{
    private static WorldInteractionController Controller => WorldInteractionController.ForScene(SceneManager.GetActiveScene());
    private static WorldInteractionTarget Find(string source) => WorldInteractionTarget.InScene(SceneManager.GetActiveScene())
        .Single(target => target.Content != null && target.Content.SourceId == source);
    private static bool Ready(string name) => SceneManager.GetActiveScene().name == name && Controller?.IsReady == true;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    internal static async UniTask ExerciseAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        var token = timeout.Token;
        await UniTask.WaitUntil(() => Ready("OutSide"), cancellationToken: token);
        var state = GameStateManager.Instance;
        state.CurrentDay = 0;
        state.GameFlow = EGameFlow.CommuteOut;
        var context = Controller.GetComponent<WorldInteractionSceneContext>();
        context.RefreshContents();
        await ExerciseGroup("street_neighbours", "citizen_a", "citizen_b", token);
        var entrance = Find("street_home_door");
        context.Player.transform.position = entrance.transform.position;
        Controller.PlayAsync(entrance).Forget();
        await UniTask.WaitUntil(() => Ready("Home") && !SceneTransitionManager.Instance.IsBusy, cancellationToken: token);
        context = Controller.GetComponent<WorldInteractionSceneContext>();
        var tv = Find("home_tv");
        Assert.IsTrue(Controller.TryResolve(tv, out var manual));
        Assert.AreEqual("home_tv_cutscene", manual.Id);
        Assert.IsFalse(tv.IsAmbient);
        context.Player.transform.position = tv.transform.position + new Vector3(2, 0);
        state.CurrentDay = 1;
        state.GameFlow = EGameFlow.CommuteIn;
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        Assert.IsTrue(tv.IsAmbient, "일차와 시간대가 바뀌면 별도 수동 Refresh 없이 CSV를 다시 선택해야 함.");
        await ExerciseGroup("home_tv", "tv_announcer", "tv_announcer", token);
        state.GameFlow = EGameFlow.CommuteOut;
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        Assert.IsTrue(Controller.TryResolve(tv, out manual));
        Assert.AreEqual("home_tv_cutscene", manual.Id);
        Assert.IsFalse(tv.IsAmbient);
    }

    private static async UniTask ExerciseGroup(string source, string first, string second, CancellationToken token)
    {
        var controller = Controller;
        var context = controller.GetComponent<WorldInteractionSceneContext>();
        var ambient = controller.GetComponent<WorldAmbientDialogueController>();
        var target = Find(source);
        Set(ambient, "minimumReadTime", .2f);
        Set(ambient, "maximumReadTime", .4f);
        Set(controller.View, "typingDelay", .01f);
        ambient.enabled = false;
        await UniTask.WaitUntil(() => !ambient.IsRunning, cancellationToken: token);
        context.Player.transform.position = target.transform.position + new Vector3(3, 0);
        ambient.enabled = true;
        await UniTask.NextFrame(token);
        Assert.IsFalse(ambient.IsRunning);
        context.Player.transform.position = target.transform.position;
        await UniTask.WaitUntil(() => ambient.RunnerFor(target)?.IsRunning == true, cancellationToken: token);
        var runner = ambient.RunnerFor(target);
        var view = ambient.ViewFor(target);
        Assert.AreEqual(EInteractorState.None, context.Player.State);
        Assert.IsFalse(context.Runner.IsRunning);
        Assert.AreNotSame(target, controller.FocusedTarget);
        Assert.AreEqual(target.ResolveSpeaker(first, context.Player.transform), view.Speaker);
        Assert.IsNull(view.GetComponentInChildren<UnityEngine.UI.Button>(true), "자동 말풍선에는 입력 버튼이 없어야 함.");
        int session = view.Bubble.TextPlayer.CurrentSessionId;
        runner.AdvanceInputOutside();
        Assert.AreEqual(session, view.Bubble.TextPlayer.CurrentSessionId);
        Assert.IsFalse(view.Bubble.TextPlayer.IsHolding, "E를 보내도 타이핑이 즉시 완료되면 안 됨.");

        // 수동 대화가 플레이어를 점유한 동안 숨기고 타이핑 시계도 멈춤.
        var previous = context.Player.State;
        SetPlayerState(context.Player, EInteractorState.Interct);
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        int visible = view.Bubble.textLabel.maxVisibleCharacters;
        await UniTask.Delay(150, cancellationToken: token);
        Assert.IsFalse(view.IsVisible);
        Assert.AreEqual(visible, view.Bubble.textLabel.maxVisibleCharacters);
        SetPlayerState(context.Player, previous);
        if (source == "street_neighbours")
        {
            var manual = WorldInteractionTarget.InScene(SceneManager.GetActiveScene())
                .Single(candidate => candidate.StorySceneId == "outside_interaction_money_example");
            manual.gameObject.SetActive(true); // Explicit tests opt into the disabled development target.
            await UniTask.NextFrame(token);
            context.Player.transform.position = manual.transform.position;
            controller.PlayAsync(manual).Forget();
            await UniTask.WaitUntil(() => controller.IsRunning, cancellationToken: token);
            await UniTask.NextFrame(token);
            await UniTask.NextFrame(token);
            visible = view.Bubble.textLabel.maxVisibleCharacters;
            Assert.IsFalse(view.IsVisible);
            await UniTask.Delay(150, cancellationToken: token);
            Assert.AreEqual(visible, view.Bubble.textLabel.maxVisibleCharacters);
            controller.Cancel();
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.AreEqual(EInteractorState.None, context.Player.State);
            context.Player.transform.position = target.transform.position;
        }
        await UniTask.WaitUntil(() => view.Bubble.TextPlayer.IsHolding, cancellationToken: token);
        await UniTask.NextFrame(token);
        Assert.IsTrue(view.IsVisible, "화자가 화면 안에 있으면 자동 말풍선이 보여야 함.");
        string preview = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(preview)) ScreenCapture.CaptureScreenshot(Path.Combine(preview, source + ".png"));
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        var anchor = view.Speaker;
        Vector3 position = anchor.position;
        anchor.position += Vector3.right * 10000;
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        Assert.IsFalse(view.IsVisible, "화자가 화면 밖으로 나가면 말풍선을 숨겨야 함.");
        anchor.position = position;
        if (first != second)
            await UniTask.WaitUntil(() => view.Speaker == target.ResolveSpeaker(second, context.Player.transform), cancellationToken: token);
        int completed = ambient.CompletedCount;
        context.Player.transform.position = target.transform.position + new Vector3(3, 0);
        await UniTask.WaitUntil(() => !ambient.IsRunning, cancellationToken: token);
        Assert.AreEqual(completed + 1, ambient.CompletedCount, "범위를 벗어나도 시작한 대화는 끝까지 진행해야 함.");
        await UniTask.NextFrame(token);
        context.Player.transform.position = target.transform.position;
        await UniTask.WaitUntil(() => ambient.RunnerFor(target)?.IsRunning == true, cancellationToken: token);
        await UniTask.WaitUntil(() => !ambient.IsRunning, cancellationToken: token);
        completed = ambient.CompletedCount;
        await UniTask.Delay(250, cancellationToken: token);
        Assert.IsFalse(ambient.IsRunning, "범위에 머무르면 자동 반복하면 안 됨.");
        Assert.AreEqual(completed, ambient.CompletedCount);
        context.Player.transform.position = target.transform.position + new Vector3(3, 0);
        await UniTask.NextFrame(token);
        context.Player.transform.position = target.transform.position;
        await UniTask.WaitUntil(() => ambient.IsRunning, cancellationToken: token);
        target.enabled = false;
        await UniTask.WaitUntil(() => !ambient.IsRunning, cancellationToken: token);
        Assert.AreEqual(completed, ambient.CompletedCount, "대상 비활성으로 취소된 대화는 완료 처리하면 안 됨.");
        context.Player.transform.position = target.transform.position + new Vector3(3, 0);
        target.enabled = true;
    }

    private static void SetPlayerState(Player player, EInteractorState state) => player.State = state;
}
