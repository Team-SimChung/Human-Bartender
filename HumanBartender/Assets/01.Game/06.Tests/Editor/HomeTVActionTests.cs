using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public sealed class HomeTVActionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void MenuRetainsAllChoicesAndUsesTheExistingRejectionReason(bool ready)
    {
        var content = new CsvStoryTestContent();
        var steps = content.GetSceneSteps("home_tv_menu");
        var menu = WorldInteractionController.PrepareOutsideChoices(content,
            new ConditionUtil(GameStateManager.Instance, null, null), "home_tv_menu", id => id != "watch" || ready);
        CollectionAssert.AreEqual(new[] { "watch", "off", "cancel" }, menu.Select(value => value.Id));
        CollectionAssert.AreEqual(new[] { "본다", "끈다", "취소" }, menu.Select(value => value.Text.Ko));
        Assert.That(menu[0].Selectable, Is.EqualTo(ready));
        Assert.That(menu[0].LockReason.Ko, Is.EqualTo("볼만한 방송이 없습니다."));
        Assert.That(menu.Skip(1).All(value => value.Selectable != false), Is.True);
        Assert.That(steps.Single().Options[0].Selectable, Is.Null, "Shared CSV data must remain unchanged.");
    }

    [Test]
    public void PowerResetsOnDayChangeWithoutReloadingTheScene()
    {
        int day = GameStateManager.Instance.CurrentDay;
        var root = new GameObject("TVTest");
        var screen = new GameObject("Screen");
        var broadcast = new GameObject("Broadcast");
        try
        {
            screen.transform.SetParent(root.transform);
            broadcast.transform.SetParent(root.transform);
            var target = broadcast.AddComponent<WorldInteractionTarget>();
            var action = root.AddComponent<HomeTVAction>();
            using var data = new SerializedObject(action);
            data.FindProperty("powerVisual").objectReferenceValue = screen;
            data.FindProperty("ambientBroadcast").objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
            action.SetPower(true);
            action.SynchronizeDay();
            Assert.That(action.IsPoweredOn && screen.activeSelf && broadcast.activeSelf, Is.True);
            GameStateManager.Instance.CurrentDay = day + 1;
            action.SynchronizeDay();
            Assert.That(action.IsPoweredOn || screen.activeSelf || broadcast.activeSelf, Is.False);
            action.SetPower(true);
            action.enabled = false;
            Assert.That(screen.activeSelf || broadcast.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(root); GameStateManager.Instance.CurrentDay = day; }
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    // 웹 outside-tv.js open(): 꺼져 있던 TV를 켤 때만 방송 채널을 초기화한다.
    [Test]
    public void ReopeningThePoweredMenuKeepsTheCurrentBroadcast()
    {
        var root = new GameObject("TVTest");
        var screen = new GameObject("Screen");
        var broadcast = new GameObject("Broadcast");
        var system = new GameObject("AmbientSystem");
        var running = new CancellationTokenSource();
        try
        {
            screen.transform.SetParent(root.transform);
            broadcast.transform.SetParent(root.transform);
            var target = broadcast.AddComponent<WorldInteractionTarget>();
            var ambient = system.AddComponent<WorldAmbientDialogueController>();
            var action = root.AddComponent<HomeTVAction>();
            using (var data = new SerializedObject(action))
            {
                data.FindProperty("powerVisual").objectReferenceValue = screen;
                data.FindProperty("ambientBroadcast").objectReferenceValue = target;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            typeof(HomeTVAction).GetField("ambient", Private).SetValue(action, ambient);
            var entry = new WorldProximityEntry();
            ((IDictionary)typeof(WorldAmbientDialogueController).GetField("entries", Private).GetValue(ambient)).Add(target, entry);
            const string rule = "home_tv_broadcast";
            action.SetPower(false);

            entry.Consume();
            action.TurnOnForMenu();
            Assert.That(action.IsPoweredOn && screen.activeSelf && broadcast.activeSelf, Is.True);
            Assert.That(entry.CanStart(rule, 0f, .9f, .3f), Is.True, "꺼진 TV를 켜면 방송을 처음부터 다시 듣는다.");

            // 켜진 TV에서 방송이 재생 중일 때 메뉴를 다시 연다.
            entry.Consume();
            var sessionType = typeof(WorldAmbientDialogueController).GetNestedType("Session", BindingFlags.NonPublic);
            var session = Activator.CreateInstance(sessionType, true);
            sessionType.GetField("Cancellation", Private).SetValue(session, running);
            ((IDictionary)typeof(WorldAmbientDialogueController).GetField("sessions", Private).GetValue(ambient)).Add(target, session);
            action.TurnOnForMenu();
            Assert.That(running.IsCancellationRequested, Is.False, "켜진 TV의 메뉴를 다시 열어도 방송을 끊지 않는다.");
            Assert.That(entry.CanStart(rule, 0f, .9f, .3f), Is.False, "켜진 TV의 메뉴를 다시 열어도 방송을 처음부터 다시 시작하지 않는다.");
            Assert.That(action.IsPoweredOn, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(system);
            running.Dispose();
        }
    }

    [Test]
    public void OnlyValidNonemptyTimelinesArePrepared()
    {
        Assert.That(StudioPlaybackService.IsPrepared(null), Is.False);
        var sequence = ScriptableObject.CreateInstance<StudioSequence>();
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        try
        {
            Assert.That(StudioPlaybackService.IsPrepared(sequence), Is.False);
            using var data = new SerializedObject(sequence);
            data.FindProperty("id").stringValue = "empty-programme";
            data.FindProperty("timeline").objectReferenceValue = timeline;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(StudioPlaybackService.IsPrepared(sequence), Is.False);
            var track = timeline.CreateTrack<StudioTrack>(null, "Programme");
            var clip = track.CreateClip<StudioClip>();
            var asset = (StudioClip)clip.asset;
            asset.Kind = StudioKind.Effect;
            Assert.That(StudioPlaybackService.IsPrepared(sequence), Is.True);
            asset.Kind = StudioKind.Visual;
            asset.ActorId = "missing-board";
            // #133 이후 그림 연결 누락은 제작 경고일 뿐 시청을 막지 않는다.
            Assert.That(StudioPlaybackService.IsPrepared(sequence), Is.True);
            Assert.That(StudioSequenceWarnings.CollectMissingVisuals(sequence), Has.Some.Contains("missing-board"));
        }
        finally { Object.DestroyImmediate(sequence); Object.DestroyImmediate(timeline); }
    }

    [Test]
    public void ManualTvRemainsAvailableAndExistingNewsIsSeparate()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var definitions = catalog.Read<NewInteractPointData[]>("interact_points_home");
        var stories = StoryCsvCompiler.CompileHome(catalog).Scenes.ToDictionary(value => value.Id);
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        int day = GameStateManager.Instance.CurrentDay;
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
            for (int d = 0; d <= 3; d++)
                foreach (var flow in new[] { EGameFlow.CommuteIn, EGameFlow.CommuteOut })
                {
                    GameStateManager.Instance.CurrentDay = d;
                    var manual = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv", flow, conditions);
                    Assert.That(manual?.Id, Is.EqualTo("p_home_tv_manual"));
                    Assert.That(manual?.ActionType, Is.EqualTo(EActionType.System));
                    var ambient = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv_broadcast", flow, conditions);
                    if (!ambient.HasValue) continue;
                    var dialogue = WorldInteractionContentRules.SelectDialogue(ambient.Value.DialogueFlows, d, conditions, new HashSet<string>());
                    Assert.That(stories[dialogue.Value.SceneId].Steps.All(step => step.Type == "say" && step.Actor == "tv"), Is.True);
                }
        }
        finally { Object.DestroyImmediate(player); GameStateManager.Instance.CurrentDay = day; }
    }

    [Test]
    public void HomeUsesTheAuthoredPowerImageAndActionWithoutPlaceholderTimelines()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            var tv = targets.Single(value => value.Content != null && value.Content.SourceId == "home_tv");
            Assert.That(tv.Action, Is.TypeOf<HomeTVAction>());
            Assert.That(tv.Output, Is.EqualTo(WorldInteractionOutput.Action));
            Assert.That(tv.Label, Is.EqualTo("TV 상호작용"));
            Assert.That(tv.ResolveStartMode("p_home_tv_morning"), Is.EqualTo(WorldInteractionStartMode.Interact));
            Assert.That(tv.CutsceneSequence, Is.Null);
            Assert.That(tv.TryResolveTimeline("tv_news_d1", out _), Is.False);
            using var data = new SerializedObject((HomeTVAction)tv.Action);
            var screen = (GameObject)data.FindProperty("powerVisual").objectReferenceValue;
            var ambient = (WorldInteractionTarget)data.FindProperty("ambientBroadcast").objectReferenceValue;
            Assert.That(screen.name, Is.EqualTo("IntractObjects_TV_on"));
            Assert.That(screen.activeSelf || ambient.gameObject.activeSelf, Is.False);
            Assert.That(ambient.StartMode, Is.EqualTo(WorldInteractionStartMode.Ambient));
            Assert.That(ambient.Content.SourceId, Is.EqualTo("home_tv_broadcast"));
            Assert.That(ambient.ResolveSpeaker("tv", null), Is.EqualTo(tv.ResolveSpeaker("tv_announcer", null)));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // 웹 outside-tv.js tick·BackgroundStories.nearby: 0.9 안에서 시작하고 1.2를 넘어 벗어난 뒤에만 다시 시작하며,
    // 재생 중에는 화자와 가로 4·세로 1.6(듣는 중 여유 0.2)을 벗어나면 멈춘다.
    [Test]
    public void HomeBroadcastUsesTheWebStartRearmAndListeningRanges()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var targets = roots.SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            var broadcast = targets.Single(value => value.Content != null && value.Content.SourceId == "home_tv_broadcast");
            var controller = roots.SelectMany(root => root.GetComponentsInChildren<WorldAmbientDialogueController>(true)).Single();
            float exitMargin;
            using (var data = new SerializedObject(controller)) exitMargin = data.FindProperty("exitMargin").floatValue;
            float radius = broadcast.InteractionRadius;
            Assert.That(radius, Is.EqualTo(.9f).Within(.0001f));
            Assert.That(radius + exitMargin, Is.EqualTo(1.2f).Within(.0001f));
            Assert.That(targets.Count(value => value.IsAmbient), Is.EqualTo(1),
                "Home의 재시작 여유는 TV 방송 기준이다. 주변 대화를 추가하면 여유 값을 다시 검토한다.");

            const string rule = "home_tv_broadcast";
            var entry = new WorldProximityEntry();
            Assert.That(entry.CanStart(rule, .85f, radius, exitMargin), Is.True);
            entry.Consume();
            Assert.That(entry.CanStart(rule, 1.15f, radius, exitMargin), Is.False);
            Assert.That(entry.CanStart(rule, .5f, radius, exitMargin), Is.False, "1.2를 넘기 전에 돌아오면 다시 시작하지 않는다.");
            Assert.That(entry.CanStart(rule, 1.25f, radius, exitMargin), Is.False);
            Assert.That(entry.CanStart(rule, .85f, radius, exitMargin), Is.True);

            var gate = broadcast.GetComponent<WorldAmbientDialogueDistanceGate>();
            Assert.That(gate, Is.Not.Null, "멀어지면 방송 말풍선과 진행을 멈춘다.");
            Assert.That(gate.Contains(new Vector3(3.9f, 1.5f), Vector3.zero, false), Is.True);
            Assert.That(gate.Contains(new Vector3(4.1f, 0f), Vector3.zero, false), Is.False);
            Assert.That(gate.Contains(new Vector3(0f, 1.7f), Vector3.zero, false), Is.False);
            Assert.That(gate.Contains(new Vector3(4.1f, 0f), Vector3.zero, true), Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
