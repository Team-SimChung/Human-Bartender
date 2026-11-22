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

public sealed class OutsideAmbientDialogueTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Ids =
    {
        "outside_day0_store_passers", "outside_day0_building_residents", "outside_day0_port_pair",
        "outside_day1_commute_passers2", "outside_day2_commute_reopening"
    };
    Scene scene;
    readonly List<Object> owned = new();
    readonly List<CancellationTokenSource> sources = new();
    CsvContentCatalog catalog;
    int previousDay;
    EGameFlow previousFlow;

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        previousDay = GameStateManager.Instance.CurrentDay;
        previousFlow = GameStateManager.Instance.GameFlow;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var source in sources) source.Cancel();
        if (scene.IsValid())
        {
            // 미리보기 씬은 일반 MonoBehaviour 수명주기를 실행하지 않는다.
            // 테스트가 명시적으로 등록한 대상만 같은 OnDestroy 경로로 해제한다.
            foreach (var target in WorldInteractionTarget.InScene(scene).ToArray()) InvokeLifecycle(target, "OnDestroy");
            EditorSceneManager.ClosePreviewScene(scene);
        }
        foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
        foreach (var source in sources) source.Dispose();
        sources.Clear(); owned.Clear();
        GameStateManager.Instance.CurrentDay = previousDay;
        GameStateManager.Instance.GameFlow = previousFlow;
    }

    GameObject Create(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    T Asset<T>() where T : ScriptableObject { var result = ScriptableObject.CreateInstance<T>(); owned.Add(result); return result; }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void InvokeLifecycle(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);

    [Test]
    public void StreetLoadIncludesFiveAmbientScriptsWithoutChangingExistingDialogueOrHomeBroadcasts()
    {
        var original = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/street"));
        var ambient = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/ambient"));
        var street = StoryCsvCompiler.CompileStreet(catalog);
        Assert.That(street.Scenes.Length, Is.EqualTo(original.Scenes.Length + Ids.Length));
        Assert.That(street.Scenes.Select(s => s.Id).Distinct().Count(), Is.EqualTo(street.Scenes.Length));
        foreach (var row in original.Scenes.Concat(ambient.Scenes.Where(s => Ids.Contains(s.Id))))
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(street.Scenes.Single(s => s.Id == row.Id)),
                Is.EqualTo(Newtonsoft.Json.JsonConvert.SerializeObject(row)), row.Id);
        Assert.That(Ids.Select(id => street.Scenes.Single(s => s.Id == id).Steps.Length), Is.EqualTo(new[] { 8, 4, 10, 6, 13 }));
        Assert.That(street.Scenes.Any(s => s.Id == "outside_day0_tv_news1" || s.Id == "outside_day1_tv_news2"), Is.False);
        var home = StoryCsvCompiler.CompileHome(catalog);
        Assert.That(home.Scenes.Any(s => s.Id == "outside_day0_tv_news1"), Is.True);
        Assert.That(home.Scenes.Any(s => s.Id == "outside_day1_tv_news2"), Is.True);
        Assert.That(WorldInteractionTestCatalog.LoadScripts("OutSide").Select(s => s.Id),
            Is.EquivalentTo(street.Scenes.Where(s => s.Steps?.Length > 0).Select(s => s.Id)),
            "Game Interaction Lab도 런타임과 같은 외부·주변 대본을 표시한다.");
    }

    [TestCase(0, EGameFlow.CommuteIn, 0)]
    [TestCase(0, EGameFlow.CommuteOut, 3)]
    [TestCase(1, EGameFlow.CommuteIn, 1)]
    [TestCase(1, EGameFlow.CommuteOut, 0)]
    [TestCase(2, EGameFlow.CommuteIn, 1)]
    [TestCase(2, EGameFlow.CommuteOut, 0)]
    [TestCase(3, EGameFlow.CommuteIn, 0)]
    [TestCase(3, EGameFlow.CommuteOut, 0)]
    public void ImportedConnectionsUseWebDayPhaseAndOnceRules(int day, EGameFlow phase, int count)
    {
        GameStateManager.Instance.CurrentDay = day;
        GameStateManager.Instance.GameFlow = phase;
        var playerData = Asset<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, playerData, playerData);
        var rows = catalog.Read<NewInteractPointData[]>("interact_points_outside");
        int available = 0;
        foreach (string id in Ids)
        {
            var row = rows.Single(r => r.Id == "p_" + id);
            Assert.That(row.SourceId, Is.EqualTo(id));
            Assert.That(row.ActivationMode, Is.EqualTo(EActivationMode.Proximity));
            Assert.That(row.DialogueFlows.Single().PlayType, Is.EqualTo(EPlayType.Once));
            var selected = WorldInteractionContentRules.SelectDefinition(rows, id, phase, conditions);
            if (!selected.HasValue) continue;
            var history = new HashSet<string>();
            Assert.That(WorldInteractionContentRules.SelectDialogue(row.DialogueFlows, day, conditions, history)?.SceneId, Is.EqualTo(id));
            history.Add(id);
            Assert.That(WorldInteractionContentRules.SelectDialogue(row.DialogueFlows, day, conditions, history), Is.Null);
            available++;
        }
        Assert.That(available, Is.EqualTo(count));
    }

    [Test]
    public void UnplacedExampleAndDeferredResidentsRemainValidDataWithoutCreatingTargets()
    {
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(
            @"^\[CSV\] \d+개 데이터 묶음 구조와 로더 타입 \d+개를 검증했습니다\.$"));
        CsvContentValidator.ValidateCatalog(catalog);
        var rows = catalog.Read<NewInteractPointData[]>("interact_points_outside");
        var example = rows.Single(r => r.Id == "p_street_neighbours");
        Assert.That(example.DialogueFlows.Single().SceneId, Is.EqualTo("street_neighbours_ambient"));
        Assert.That(StoryCsvCompiler.CompileStreet(catalog).Scenes.Any(s => s.Id == "street_neighbours_ambient"), Is.True);
        Assert.That(rows.Any(r => r.Id == "p_outside_day0_building_residents"), Is.True);
        Assert.That(WorldInteractionTarget.InScene(scene), Is.Empty);
        LogAssert.NoUnexpectedReceived();
    }

    WorldAmbientDialogueView MakeView(bool withGate, out Transform listener, out Transform speaker, Func<bool> paused = null)
    {
        var group = Create("AmbientGroup");
        var target = group.AddComponent<WorldInteractionTarget>();
        if (withGate) group.AddComponent<WorldAmbientDialogueDistanceGate>();
        listener = Create("Listener").transform;
        speaker = Create("SpeechAnchor").transform;
        var view = Create("AmbientView").AddComponent<WorldAmbientDialogueView>();
        view.Begin(target, listener, paused ?? (() => false), 1.2f, 4f, 22f);
        Set(view, "speaker", speaker);
        return view;
    }

    [TestCase(3.9f, 0f, true)]
    [TestCase(-3.9f, 0f, true)]
    [TestCase(4.1f, 0f, false)]
    [TestCase(0f, 1.5f, true)]
    [TestCase(0f, -1.5f, true)]
    [TestCase(0f, 1.7f, false)]
    public void ListeningRangeUsesHorizontalAndVerticalDistances(float x, float y, bool inside)
    {
        var view = MakeView(true, out var listener, out var speaker);
        speaker.position = new Vector3(10f, 20f, 100f);
        listener.position = new Vector3(10f + x, 20f + y, -100f);
        Assert.That(view.IsDistancePaused, Is.EqualTo(!inside));
    }

    [Test]
    public void ExitMarginPreventsChatterAndReturnRequiresBaseRange()
    {
        var view = MakeView(true, out var listener, out _);
        Assert.That(view.IsDistancePaused, Is.False);
        listener.position = new Vector3(4.1f, 1.7f);
        Assert.That(view.IsDistancePaused, Is.False);
        listener.position = new Vector3(4.3f, 0f);
        Assert.That(view.IsDistancePaused, Is.True);
        listener.position = new Vector3(4.1f, 0f);
        Assert.That(view.IsDistancePaused, Is.True);
        listener.position = new Vector3(3.9f, 1.5f);
        Assert.That(view.IsDistancePaused, Is.False);
    }

    [Test]
    public void SpeakerSwitchAndDisabledGateUseCurrentSpeakerAndKeepGlobalPause()
    {
        bool paused = false;
        var view = MakeView(true, out var listener, out _, () => paused);
        Assert.That(view.IsPlaybackPaused, Is.False);
        var next = Create("NextSpeaker").transform;
        next.position = new Vector3(10f, 0f);
        Set(view, "speaker", next);
        Assert.That(view.IsPlaybackPaused, Is.True);
        var target = (WorldInteractionTarget)typeof(WorldAmbientDialogueView).GetField("target", Private).GetValue(view);
        target.GetComponent<WorldAmbientDialogueDistanceGate>().enabled = false;
        Assert.That(view.IsPlaybackPaused, Is.False);
        paused = true;
        Assert.That(view.IsPlaybackPaused, Is.True);
    }

    [TestCase("TV")]
    [TestCase("Radio")]
    public void ExistingAmbientWithoutGateKeepsDistanceAndGlobalPauseBehavior(string kind)
    {
        bool paused = false;
        var view = MakeView(false, out var listener, out _, () => paused);
        view.name = kind;
        listener.position = new Vector3(100f, 100f);
        Assert.That(view.IsDistancePaused, Is.False);
        Assert.That(view.IsPlaybackPaused, Is.False);
        paused = true;
        Assert.That(view.IsPlaybackPaused, Is.True);
        paused = false;
        Assert.That(view.IsPlaybackPaused, Is.False);
    }

    [UnityTest]
    public IEnumerator DistancePauseHoldsReadyWaitAndReturnResumesSameView()
    {
        var view = MakeView(true, out var listener, out _);
        listener.position = new Vector3(10f, 0f);
        var source = new CancellationTokenSource(); sources.Add(source);
        var task = view.WaitUntilReadyAsync(source.Token).SuppressCancellationThrow().AsTask();
        for (int i = 0; i < 3; i++) yield return null;
        Assert.That(task.IsCompleted, Is.False);
        listener.position = Vector3.zero;
        for (int i = 0; i < 8 && !task.IsCompleted; i++) yield return null;
        Assert.That(task.IsCompletedSuccessfully, Is.True);
        Assert.That(task.Result, Is.False, "복귀는 세션 취소가 아니다.");
    }

    (WorldAmbientDialogueController ambient, WorldInteractionController foreground, WorldInteractionSceneContext context, Player player, CancellationTokenSource token)
        MakePausedSession(int day, EGameFlow phase)
    {
        GameStateManager.Instance.CurrentDay = day;
        GameStateManager.Instance.GameFlow = phase;
        var view = MakeView(true, out var listener, out _);
        var player = listener.gameObject.AddComponent<Player>();
        listener.position = new Vector3(20f, 0f);
        var system = Create("InteractionSystem");
        var foreground = system.AddComponent<WorldInteractionController>();
        var context = system.AddComponent<WorldInteractionSceneContext>();
        var ambient = system.AddComponent<WorldAmbientDialogueController>();
        var runner = system.AddComponent<DialogueRunner>();
        var frontView = Create("ForegroundView").AddComponent<WorldInteractionView>();
        var points = Asset<NewInteractPointDataSO>(); points.interactPointData = catalog.Read<NewInteractPointData[]>("interact_points_outside");
        var data = Asset<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, data, data);
        Set(context, "<IsReady>k__BackingField", true);
        Set(context, "player", player); Set(context, "runner", runner); Set(context, "controller", foreground);
        Set(context, "ambient", ambient); Set(context, "interactPointData", points); Set(context, "conditions", conditions);
        var stories = (Dictionary<string, NewSceneData>)typeof(WorldInteractionSceneContext).GetField("sceneStories", Private).GetValue(context);
        foreach (var story in StoryCsvCompiler.CompileStreet(catalog).Scenes) stories.Add(story.Id, story);
        Set(foreground, "ready", true); Set(foreground, "view", frontView); Set(foreground, "player", player); Set(foreground, "runner", runner);
        Set(ambient, "foreground", foreground); Set(ambient, "context", context);
        var type = typeof(WorldAmbientDialogueController).GetNestedType("Session", BindingFlags.NonPublic);
        var session = Activator.CreateInstance(type, true);
        var source = new CancellationTokenSource(); sources.Add(source);
        Set(session, "Cancellation", source); Set(session, "View", view);
        Set(session, "Runner", view.gameObject.AddComponent<DialogueRunner>());
        var target = (WorldInteractionTarget)typeof(WorldAmbientDialogueView).GetField("target", Private).GetValue(view);
        ((IDictionary)typeof(WorldAmbientDialogueController).GetField("sessions", Private).GetValue(ambient)).Add(target, session);
        Assert.That(view.IsDistancePaused, Is.True);
        Assert.That(ambient.IsRunning, Is.True);
        return (ambient, foreground, context, player, source);
    }

    [TestCase("p_enter_bar", 1, EGameFlow.CommuteIn)]
    [TestCase("p_enter_home", 0, EGameFlow.CommuteOut)]
    [TestCase("p_elevator_move", 1, EGameFlow.CommuteIn)]
    public void PausedAmbientDoesNotBlockBarHomeOrElevatorEntry(string id, int day, EGameFlow phase)
    {
        var env = MakePausedSession(day, phase);
        var row = catalog.Read<NewInteractPointData[]>("interact_points_outside").Single(r => r.Id == id);
        var go = Create(id);
        var content = go.AddComponent<WorldInteractionContent>(); Set(content, "sourceId", row.SourceId);
        content.Initialize(env.context); content.Refresh();
        var target = go.AddComponent<WorldInteractionTarget>(); Set(target, "content", content);
        target.ConfigureStart(WorldInteractionStartMode.Interact, 1.2f);
        if (id == "p_elevator_move")
        {
            var elevator = go.AddComponent<OutsideElevator>();
            Set(target, "output", WorldInteractionOutput.Action); Set(target, "action", elevator);
            Assert.That(elevator.CanExecute, Is.True);
        }
        Assert.That(env.player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(env.foreground.IsRunning, Is.False);
        Assert.That((bool)typeof(WorldInteractionController).GetProperty("CanStartSession", Private).GetValue(env.foreground), Is.True);
        // 실제 게임의 Controller 초기화처럼 이 미리보기 씬의 대상을 등록한다.
        WorldInteractionTarget.RegisterScene(scene);
        Assert.That(WorldInteractionTarget.InScene(scene).Count(t => t.Content?.SourceId == row.SourceId), Is.EqualTo(1));
        Assert.That(env.foreground.TryResolve(target, out _), Is.True);
        Assert.That(env.context.IsSafeForSave, Is.True);
        Assert.That(env.ambient.IsPaused, Is.False, "거리 정지는 전역 정지가 아니다.");
    }

    [Test]
    public void OtherGroupCanResolveWhileDistantSessionRemainsAndElevatorStillPausesAmbient()
    {
        var env = MakePausedSession(0, EGameFlow.CommuteOut);
        var go = Create("SecondGroup");
        var content = go.AddComponent<WorldInteractionContent>(); Set(content, "sourceId", "outside_day0_port_pair");
        content.Initialize(env.context); content.Refresh();
        var target = go.AddComponent<WorldInteractionTarget>(); Set(target, "content", content);
        target.ConfigureStart(WorldInteractionStartMode.Ambient, 1.2f);
        go.transform.position = env.player.transform.position;
        WorldInteractionTarget.RegisterScene(scene);
        Assert.That(WorldInteractionTarget.InScene(scene).Count(t => t == target), Is.EqualTo(1));
        Assert.That(env.foreground.TryResolve(target, out var story), Is.True);
        Assert.That(new WorldProximityEntry().CanStart(story.Id, 0f, story.Radius, .2f), Is.True);
        Assert.That(env.ambient.IsPaused, Is.False);
        env.player.State = EInteractorState.ForceMove;
        Assert.That(env.ambient.IsPaused, Is.True, "일반 엘리베이터 탑승 정지 규칙 유지");
        env.player.State = EInteractorState.None;
        Assert.That(env.ambient.IsPaused, Is.False);
    }

    [UnityTest]
    public IEnumerator SceneOwnerDisableCancelsDistantWaitInsteadOfBlockingTeardown()
    {
        var env = MakePausedSession(0, EGameFlow.CommuteOut);
        var sessions = (IDictionary)typeof(WorldAmbientDialogueController).GetField("sessions", Private).GetValue(env.ambient);
        var session = sessions.Values.Cast<object>().Single();
        var view = (WorldAmbientDialogueView)session.GetType().GetField("View", Private).GetValue(session);
        var task = view.WaitUntilReadyAsync(env.token.Token).SuppressCancellationThrow().AsTask();
        Assert.That(task.IsCompleted, Is.False);
        // 씬을 떠날 때 실행되는 실제 정리 콜백을 검증한다.
        // PreviewScene에서 DestroyImmediate만으로 OnDisable 호출을 기대하지 않는다.
        env.ambient.enabled = false;
        InvokeLifecycle(env.ambient, "OnDisable");
        Assert.That(env.token.IsCancellationRequested, Is.True);
        for (int i = 0; i < 8 && !task.IsCompleted; i++) yield return null;
        Assert.That(task.IsCompletedSuccessfully, Is.True);
        Assert.That(task.Result, Is.True);
    }
}
