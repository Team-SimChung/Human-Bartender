using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Assert.That(StudioPlaybackService.IsPrepared(sequence), Is.False,
                "A registered programme with missing actor bindings is still unavailable.");
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
}
