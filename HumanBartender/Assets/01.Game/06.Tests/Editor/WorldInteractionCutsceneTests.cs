using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WorldInteractionCutsceneTests
{
    public const string HomePath = "Assets/01.Game/01.Scenes/Home.unity";
    public const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    public const string SequencePath = "Assets/03.Dev/01.Scenes/06.Cutscenes/CutsceneStudio_Test.asset";
    public const string StoryId = "home_tv_cutscene";
    const string Evidence = "Library/WorldInteractionValidation/timeline-unification/";

    sealed class Presenter : IDialoguePresenter
    {
        public readonly List<string> Lines = new();
        public int ChoiceIndex;
        public int ChoiceCount;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
        { Lines.Add(text); return UniTask.CompletedTask; }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
        { ChoiceCount++; selected(options[ChoiceIndex]); }
    }

    static void Set(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    static T Get<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

    static Step[] TvSteps() => CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
        .Read<NewStreetData>("script/home").Scenes.Single(s => s.Id == StoryId).Steps;

    static StoryExecutionResult Drain(DialogueRunner runner, UniTask<StoryExecutionResult> task)
    {
        for (int i = 0; i < 100 && task.Status == UniTaskStatus.Pending; i++)
            runner.AdvanceInputOutside();
        Assert.AreNotEqual(UniTaskStatus.Pending, task.Status, "A nonvisual CSV execution should finish after advancing its lines.");
        return task.GetAwaiter().GetResult();
    }

    [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
    public void CsvSelectsTodaysTimelineAndOffersReplayOnlyAfterCompletion(int day)
    {
        var root = new GameObject("TV CSV test");
        var data = ScriptableObject.CreateInstance<PlayerDataSO>();
        var previousDay = GameStateManager.Instance.CurrentDay;
        try
        {
            GameStateManager.Instance.CurrentDay = day;
            var runner = root.AddComponent<DialogueRunner>();
            Set(runner, "conditionUtil", new ConditionUtil(GameStateManager.Instance, data, data));
            var presenter = new Presenter();
            runner.Bind(presenter);
            var ids = new List<string>();
            UniTask Play(string id, CancellationToken token) { ids.Add(id); return UniTask.CompletedTask; }
            Assert.IsTrue(Drain(runner, runner.PlayOutsideAsync(TvSteps(), default, Play)).Completed);
            Assert.AreEqual(new[] { "tv_news_d" + day }, ids);
            Assert.AreEqual(0, presenter.ChoiceCount);
            Assert.IsTrue(data.CheckFlag("tv_news_d" + day + "_seen"));
            foreach (int other in Enumerable.Range(0, 4).Where(d => d != day))
                Assert.IsFalse(data.CheckFlag("tv_news_d" + other + "_seen"));
            Assert.AreEqual("방송이 끝났다.", presenter.Lines.Last());

            presenter.ChoiceIndex = 1; // 거절하면 해당 분기가 끝나야 함.
            Assert.IsTrue(Drain(runner, runner.PlayOutsideAsync(TvSteps(), default, Play)).Completed);
            Assert.AreEqual(1, presenter.ChoiceCount);
            Assert.AreEqual(1, ids.Count);
            presenter.ChoiceIndex = 0;
            Assert.IsTrue(Drain(runner, runner.PlayOutsideAsync(TvSteps(), default, Play)).Completed);
            Assert.AreEqual(2, presenter.ChoiceCount);
            Assert.AreEqual(2, ids.Count);
        }
        finally
        {
            GameStateManager.Instance.CurrentDay = previousDay;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [TestCase(false), TestCase(true)]
    public void FailedOrCancelledTimelineDoesNotRecordViewing(bool cancel)
    {
        var root = new GameObject("TV failure test");
        var data = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var runner = root.AddComponent<DialogueRunner>();
            Set(runner, "conditionUtil", new ConditionUtil(GameStateManager.Instance, data, data));
            runner.Bind(new Presenter());
            var steps = new[] { new Step { Type="timeline", Arg="unavailable", Effects="flag.viewed = true" },
                new Step { Type="effect", Effects="flag.following_step = true" } };
            UniTask Play(string id, CancellationToken token) =>
                cancel ? UniTask.FromCanceled(new CancellationToken(true)) :
                    UniTask.FromException(new InvalidOperationException("unavailable timeline"));
            var result = Drain(runner, runner.PlayOutsideAsync(steps, default, Play));
            Assert.AreEqual(cancel ? StoryExecutionStatus.Cancelled : StoryExecutionStatus.Failed, result.Status);
            Assert.IsFalse(data.CheckFlag("viewed"));
            Assert.IsFalse(data.CheckFlag("following_step"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test]
    public void HomeBindingsAndElevatorActionAreWiredWithoutChangingStudioInputDefaults()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            // Home의 기존 패널에 남아 있는 삭제된 프리팹 참조 경고를 구분함.
            // 확인된 두 경고만 허용하며 다른 씬·실행 오류는 검사에 실패하도록 유지함.
            if (File.ReadAllText(HomePath).Contains("7d0c7bc10184af04998b0dbb17460797"))
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Problem detected while opening the Scene file: 'Assets/01.Game/01.Scenes/Home.unity'"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Prefab instance problem:.*7d0c7bc10184af04998b0dbb17460797"));
            }
            var home = EditorSceneManager.OpenScene(HomePath, OpenSceneMode.Single);
            var tv = home.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(t => t.Content != null && t.Content.SourceId == "home_tv");
            Assert.AreEqual(WorldInteractionOutput.SpeechBubble, tv.Output);
            var sequence = AssetDatabase.LoadAssetAtPath<StudioSequence>(SequencePath);
            Assert.IsEmpty(sequence.Validate());
            foreach (int day in Enumerable.Range(0, 4)) Assert.AreSame(sequence, tv.ResolveTimeline("tv_news_d" + day));
            Assert.Throws<InvalidOperationException>(() => tv.ResolveTimeline("unknown"));
            var outside = EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Single);
            var elevator = outside.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OutsideElevator>(true)).Single();
            var target = elevator.GetComponent<WorldInteractionTarget>();
            Assert.AreEqual("outside_elevator", target.StorySceneId);
            Assert.AreSame(elevator, target.Action);
            Assert.IsTrue(WorldInteractionLegacyBridge.OwnsAction(elevator));
            var host = new GameObject("Studio default");
            try { Assert.IsTrue(host.AddComponent<StudioPlayer>().HandleKeyboardInput); }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        finally
        {
            if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [UnityTest, Explicit("Enters Play Mode and verifies TV, timeline input, elevator travel, cancellation and radio tracking.")]
    public IEnumerator TimelineTvAndElevatorRuntime()
    {
        EditorSceneManager.OpenScene(OutsidePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return ExerciseAsync().ToCoroutine();
        yield return new ExitPlayMode();
    }

    public static async UniTask ExerciseAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var token = timeout.Token;
        Directory.CreateDirectory(Evidence);
        var previousSettings = InputSystem.settings;
        var settings = UnityEngine.Object.Instantiate(previousSettings);
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = settings;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        WorldInteractionController controller = null;
        try
        {
            await UniTask.WaitUntil(() => WorldInteractionController.ForScene(SceneManager.GetActiveScene())?.IsReady == true,
                cancellationToken: token);
            var outside = SceneManager.GetActiveScene();
            controller = WorldInteractionController.ForScene(outside);
            var player = Get<Player>(controller, "player");
            var input = UnityEngine.Object.FindFirstObjectByType<OutsideInputHandler>().GetComponent<PlayerInput>();
            input.enabled = false;
            input.enabled = true;
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            var elevator = outside.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OutsideElevator>(true)).Single();
            var elevatorTarget = elevator.GetComponent<WorldInteractionTarget>();
            var before = player.transform.position;
            GameStateManager.Instance.IsOutsideLogo = true; // 타이틀 연출을 기다리지 않고 이동 동작을 검사함.
            Set(elevator, "duration", 2f);
            Set(elevator, "delayDuration", 0f);
            var radio = Get<OutsideElevatorRadio>(elevator, "radio");
            Assert.IsTrue(radio.gameObject.activeInHierarchy, "실제 CSV의 Day 0 방송 조건이 활성화돼야 함.");
            Assert.AreEqual("d1_elevator", radio.DialogueSceneId);
            Assert.IsNotEmpty(radio.Steps);
            string firstBroadcast = radio.Steps.First(step => step.Type == "say").Text.Ko;
            player.transform.position = elevator.transform.position + Vector3.up * .2f;
            await Frames(3, token);
            Assert.AreEqual(elevatorTarget, controller.FocusedTarget,
                $"ready={controller.IsReady}, running={controller.IsRunning}, runner={Get<DialogueRunner>(controller, "runner").IsRunning}, " +
                $"options={UIDisplayOptions.IsOptionOpen}, controller={controller.isActiveAndEnabled}, view={controller.View.isActiveAndEnabled}, player={player.State}, active={elevatorTarget.isActiveAndEnabled}, " +
                $"action={elevator.CanExecute}, resolve={controller.TryResolve(elevatorTarget, out var elevatorStory)}, " +
                $"distance={Vector2.Distance(player.transform.position, elevator.transform.position)}, radius={elevatorStory.Radius}");
            Assert.AreEqual("탑승", controller.View.Hint.Label.text);
            Assert.IsNull(Get<InteractionDetector>(player, "detector"));

            bool initialTop = elevator.IsTop;
            await Press(keyboard, Key.E, token);
            await UniTask.WaitUntil(() => elevator.IsMoving, cancellationToken: token);
            Assert.IsTrue(controller.IsRunning);
            Assert.IsTrue(controller.View.Bubble.gameObject.activeSelf);
            Assert.AreEqual(firstBroadcast, controller.View.Bubble.textLabel.text, "실제 CSV에서 읽은 방송 대사를 표시함.");
            var bubbleBefore = controller.View.Bubble.bubble.anchoredPosition;
            await UniTask.WaitUntil(() => Get<DialogueRunner>(controller, "runner").CurrentState == DialogueState.WaitingForInput,
                cancellationToken: token);
            Assert.IsTrue(controller.View.Bubble.gameObject.activeSelf);
            Assert.AreSame(radio.transform, Get<Transform>(controller.View, "speaker"));
            await Capture("elevator-radio", token);
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.AreNotEqual(initialTop, elevator.IsTop);
            Assert.AreEqual(EInteractorState.None, player.State);
            Assert.IsFalse(controller.View.Bubble.gameObject.activeSelf);
            Assert.IsFalse(elevator.IsMoving);

            // 다른 층에서도 엘리베이터를 따라 감지하며 취소하면 출발 위치로 복원되는지 확인함.
            player.transform.position = elevator.transform.position + Vector3.up * .2f;
            await Frames(3, token);
            Assert.AreEqual(elevatorTarget, controller.FocusedTarget);
            var elevatorStart = elevator.transform.position;
            var playerStart = player.transform.position;
            var cancelTravel = controller.PlayAsync(elevatorTarget);
            await Frames(3, token);
            await Press(keyboard, Key.Escape, token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await cancelTravel).Status);
            Assert.That(Vector3.Distance(elevatorStart, elevator.transform.position), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(playerStart, player.transform.position), Is.LessThan(.01f));
            Assert.AreEqual(EInteractorState.None, player.State);
            Assert.IsFalse(controller.View.Bubble.gameObject.activeSelf);

            // 직접 재생도 CSV 요청과 같은 Timeline 재생기를 사용하는지 확인함.
            var direct = WorldInteractionTarget.InScene(outside).Single(t => t.StorySceneId == "outside_interaction_cutscene_example");
            player.transform.position = direct.transform.position;
            await Frames(3, token);
            var directRun = controller.PlayAsync(direct);
            await UniTask.WaitUntil(() => controller.CutscenePlayer?.ActivePlayer?.IsWaiting == true, cancellationToken: token);
            await Press(keyboard, Key.Escape, token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await directRun).Status);

            GameStateManager.Instance.CurrentDay = 0;
            GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
            var transition = await SceneTransitionManager.Instance.LoadSceneAsync("Home", cancellationToken: token);
            Assert.IsTrue(transition.Succeeded, transition.Message);
            var home = SceneManager.GetSceneByPath(HomePath);
            controller = WorldInteractionController.ForScene(home);
            await UniTask.WaitUntil(() => controller.IsReady, cancellationToken: token);
            player = Get<Player>(controller, "player");
            var runner = Get<DialogueRunner>(controller, "runner");
            var data = Get<IPlayerDataReader>(controller, "playerData") as PlayerDataSO;
            Assert.NotNull(data);
            var tv = WorldInteractionTarget.InScene(home).Single(t => t.Content != null && t.Content.SourceId == "home_tv");
            data.AddFlag("tv_news_d0_seen", false);
            data.AddFlag("tv_news_d1_seen", false);
            player.transform.position = tv.transform.position;
            await Frames(3, token);
            float scale = Time.timeScale;
            var gameState = GameStateManager.Instance.CurrentGameState;

            var first = controller.PlayAsync(tv);
            await AdvanceLine(controller, runner, keyboard, token);
            await UniTask.WaitUntil(() => controller.CutscenePlayer?.ActivePlayer?.IsWaiting == true, cancellationToken: token);
            Assert.IsFalse(data.CheckFlag("tv_news_d0_seen"));
            await Capture("tv-timeline", token);
            await Press(keyboard, Key.Escape, token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await first).Status);
            Assert.IsFalse(data.CheckFlag("tv_news_d0_seen"));
            AssertRestored(controller, player, scale, gameState);

            await Frames(2, token);
            var complete = controller.PlayAsync(tv);
            await AdvanceLine(controller, runner, keyboard, token);
            await FinishTimeline(controller, keyboard, token);
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            Assert.IsTrue(data.CheckFlag("tv_news_d0_seen"));
            Assert.AreEqual("방송이 끝났다.", controller.View.Bubble.textLabel.text);
            await AdvanceLine(controller, runner, keyboard, token);
            Assert.IsTrue((await complete).Completed);
            AssertRestored(controller, player, scale, gameState);

            await Frames(2, token);
            var repeat = controller.PlayAsync(tv);
            await AdvanceLine(controller, runner, keyboard, token);
            await UniTask.WaitUntil(() => controller.View.IsChoosing, cancellationToken: token);
            // 같은 입력 경로로 첫 선택지를 확정한 뒤 재시청을 취소함.
            await UniTask.WaitUntil(() => !controller.View.Choices.IsRevealing, cancellationToken: token);
            await Capture("tv-replay-choice", token);
            await Press(keyboard, Key.E, token);
            await UniTask.WaitUntil(() => controller.CutscenePlayer?.ActivePlayer?.IsWaiting == true, cancellationToken: token);
            await Press(keyboard, Key.Escape, token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await repeat).Status);
            Assert.IsTrue(data.CheckFlag("tv_news_d0_seen"), "Cancelling replay must not erase an earlier successful viewing.");

            GameStateManager.Instance.CurrentDay = 1;
            await Frames(3, token);
            var nextDay = controller.PlayAsync(tv);
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            Assert.That(controller.View.Bubble.textLabel.text, Does.Contain("Day 1"));
            Assert.IsFalse(controller.View.IsChoosing);
            controller.Cancel();
            Assert.AreEqual(StoryExecutionStatus.Cancelled, (await nextDay).Status);
            Assert.IsFalse(data.CheckFlag("tv_news_d1_seen"));
            AssertRestored(controller, player, scale, gameState);
            File.WriteAllText(Evidence+"runtime-result.txt",
                "PASS: shared Timeline player; CSV dialogue -> Timeline -> dialogue; E/Esc; replay choice; per-day viewed flags; elevator hint/input/travel/arrival/cancel; radio via common UI; player/world restored.");
        }
        finally
        {
            controller?.Cancel();
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings = previousSettings;
            UnityEngine.Object.Destroy(settings);
        }
    }

    static async UniTask AdvanceLine(WorldInteractionController controller, DialogueRunner runner, Keyboard keyboard, CancellationToken token)
    {
        await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
        await Press(keyboard, Key.E, token);
    }

    static async UniTask FinishTimeline(WorldInteractionController controller, Keyboard keyboard, CancellationToken token)
    {
        await UniTask.WaitUntil(() => controller.CutscenePlayer?.IsPlaying == true, cancellationToken: token);
        while (controller.CutscenePlayer.IsPlaying)
        {
            token.ThrowIfCancellationRequested();
            if (controller.CutscenePlayer.ActivePlayer?.IsWaiting == true) await Press(keyboard, Key.E, token);
            await UniTask.NextFrame(token);
        }
    }

    static void AssertRestored(WorldInteractionController controller, Player player, float scale, GameState state)
    {
        Assert.IsFalse(controller.IsRunning);
        Assert.IsFalse(controller.CutscenePlayer.IsPlaying);
        Assert.AreEqual(EInteractorState.None, player.State);
        Assert.AreEqual(scale, Time.timeScale);
        Assert.AreEqual(state, GameStateManager.Instance.CurrentGameState);
        Assert.AreEqual("Home", SceneManager.GetActiveScene().name);
    }

    static async UniTask Frames(int count, CancellationToken token)
    { for (int i=0; i<count; i++) await UniTask.NextFrame(token); }

    static async UniTask Press(Keyboard keyboard, Key key, CancellationToken token)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await Frames(2, token);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Frames(1, token);
    }

    static async UniTask Capture(string name, CancellationToken token)
    {
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence+name+".png"));
        await Frames(2, token);
    }
}
