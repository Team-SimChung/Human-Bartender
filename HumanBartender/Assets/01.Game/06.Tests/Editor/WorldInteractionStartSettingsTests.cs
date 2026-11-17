using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class WorldInteractionStartSettingsTests
{
    [TestCase(WorldInteractionStartMode.Automatic, ENewSceneTrigger.Manual, ENewSceneTrigger.Auto, true)]
    [TestCase(WorldInteractionStartMode.Interact, ENewSceneTrigger.Auto, ENewSceneTrigger.Interact, true)]
    [TestCase(WorldInteractionStartMode.ScriptOnly, ENewSceneTrigger.Auto, ENewSceneTrigger.Manual, false)]
    public void ControllerUsesInspectorModeAndRadiusWhileKeepingCsvConditions(WorldInteractionStartMode mode,
        ENewSceneTrigger csvMode, ENewSceneTrigger expectedMode, bool eligible)
    {
        var state = GameStateManager.Instance;
        int previousDay = state.CurrentDay;
        var previousFlow = state.GameFlow;
        var host = new GameObject("Start policy test target");
        var controllerObject = new GameObject("Start policy test controller");
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            state.CurrentDay = 1;
            state.GameFlow = EGameFlow.CommuteIn;
            var target = host.AddComponent<WorldInteractionTarget>();
            target.ConfigureStory("test-inspector-start-policy", .75f, "test");
            target.ConfigureStart(mode, .75f);
            var controller = controllerObject.AddComponent<WorldInteractionController>();
            var conditions = new ConditionUtil(state, player, player);
            typeof(WorldInteractionController).GetField("conditions", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, conditions);
            var stories = (Dictionary<string, NewSceneData>)typeof(WorldInteractionController)
                .GetField("stories", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var authored = new NewSceneData
            {
                Id = target.StorySceneId, RuntimeScene = host.scene.name, Day = 1, Route = "in",
                StartMode = csvMode, Radius = 99, When = "flag.start_test_allowed", CompletionFlag = "start_test_done"
            };
            stories.Add(authored.Id, authored);
            WorldInteractionTarget.RegisterScene(host.scene);
            Assert.That(controller.TryResolve(target, out _), Is.False, "Inspector settings must not bypass the CSV When condition.");
            player.AddFlag("start_test_allowed", true);
            Assert.That(controller.TryResolve(target, out var resolved), Is.EqualTo(eligible));
            Assert.That(resolved.StartMode, Is.EqualTo(expectedMode));
            Assert.That(resolved.Radius, Is.EqualTo(.75f), "The stale CSV radius must not win over the Inspector.");
            Assert.That(resolved.Day, Is.EqualTo(1));
            Assert.That(resolved.CompletionFlag, Is.EqualTo(authored.CompletionFlag));
            state.CurrentDay = 2;
            Assert.That(controller.TryResolve(target, out _), Is.False);
            state.CurrentDay = 1;
            state.GameFlow = EGameFlow.CommuteOut;
            Assert.That(controller.TryResolve(target, out _), Is.False);
            state.GameFlow = EGameFlow.CommuteIn;
            player.AddFlag("start_test_done", true);
            Assert.That(controller.TryResolve(target, out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(player);
            state.CurrentDay = previousDay;
            state.GameFlow = previousFlow;
        }
    }

    [Test]
    public void MigrationCopiesOldEffectiveRadiusOnceAndPreservesLaterInspectorEdits()
    {
        var host = new GameObject("Start migration test");
        try
        {
            var target = host.AddComponent<WorldInteractionTarget>();
            target.ConfigureStory("day1-corridor-thug", 1.5f, "test");
            MarkUnmigrated(target);
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
            var stories = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/street")).Scenes;
            Assert.That(WorldInteractionStartSettingsSetup.MigrateTarget(target, stories, null), Is.True);
            Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Automatic));
            Assert.That(target.InteractionRadius, Is.EqualTo(2));
            target.ConfigureStart(WorldInteractionStartMode.Interact, .6f);
            Assert.That(WorldInteractionStartSettingsSetup.MigrateTarget(target, stories, null), Is.False);
            Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Interact));
            Assert.That(target.InteractionRadius, Is.EqualTo(.6f));

            target.ConfigureStory("neighborhood-guardBlocked", 1.4f, "test");
            MarkUnmigrated(target);
            Assert.That(WorldInteractionStartSettingsSetup.MigrateTarget(target, stories, null), Is.True);
            Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Interact));
            Assert.That(target.InteractionRadius, Is.EqualTo(1.4f));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void TvManualMenuRemainsSeparateFromItsMorningBroadcast()
    {
        var host = new GameObject("TV start migration test");
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        var state = GameStateManager.Instance;
        int previousDay = state.CurrentDay;
        try
        {
            var target = host.AddComponent<WorldInteractionTarget>();
            var content = host.AddComponent<WorldInteractionContent>();
            using (var data = new SerializedObject(content))
            {
                data.FindProperty("sourceId").stringValue = "home_tv";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var data = new SerializedObject(target))
            {
                data.FindProperty("content").objectReferenceValue = content;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            MarkUnmigrated(target);
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
            var definitions = catalog.Read<NewInteractPointData[]>("interact_points_home");
            WorldInteractionStartSettingsSetup.MigrateTarget(target, null, definitions);
            var conditions = new ConditionUtil(state, player, player);
            state.CurrentDay = 1;
            var morning = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv", EGameFlow.CommuteIn, conditions);
            Assert.That(morning.Value.Id, Is.EqualTo("p_home_tv_manual"));
            Assert.That(target.ResolveStartMode(morning.Value.Id), Is.EqualTo(WorldInteractionStartMode.Interact));
            var broadcast = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv_broadcast", EGameFlow.CommuteIn, conditions);
            Assert.That(broadcast.Value.Id, Is.EqualTo("p_home_tv_morning"));
            var evening = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv", EGameFlow.CommuteOut, conditions);
            Assert.That(target.ResolveStartMode(evening.Value.Id), Is.EqualTo(WorldInteractionStartMode.Interact));
            state.CurrentDay = 2;
            var tomorrow = WorldInteractionContentRules.SelectDefinition(definitions, "home_tv", EGameFlow.CommuteIn, conditions);
            Assert.That(target.ResolveStartMode(tomorrow.Value.Id), Is.EqualTo(WorldInteractionStartMode.Interact));
            target.ConfigureStart(WorldInteractionStartMode.Automatic, .9f);
            Assert.That(target.ResolveStartMode(morning.Value.Id), Is.EqualTo(WorldInteractionStartMode.Automatic),
                "Removing Inspector overrides must stop consulting CSV activation_mode.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(player); state.CurrentDay = previousDay; }
    }

    [Test]
    public void AmbientValidationRejectsNpcSequencesAndKeepsDialogueOnlyTargets()
    {
        var host = new GameObject("Ambient NPC validation test");
        var player = new GameObject("Ambient test player");
        try
        {
            var target = host.AddComponent<WorldInteractionTarget>();
            target.ConfigureStart(WorldInteractionStartMode.Ambient, 1.5f);
            var interaction = host.AddComponent<NpcInteraction>();
            var story = new NewSceneData
            {
                Id = "ambient-npc-test",
                Steps = new[] { new Step { Type = "say", Actor = "luna", Text = new Texts { Ko = "주변 대화" } } }
            };

            var error = Assert.Throws<InvalidOperationException>(() =>
                WorldAmbientDialogueController.ValidateScript(target, story, player.transform));
            Assert.That(error.Message, Does.Contain("주변 대화는 대사 전용"));

            Object.DestroyImmediate(interaction);
            Assert.DoesNotThrow(() => WorldAmbientDialogueController.ValidateScript(target, story, player.transform),
                "Dialogue-only ambient targets must continue to use their existing validation path.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(player); }
    }

    private static void MarkUnmigrated(WorldInteractionTarget target)
    {
        using var data = new SerializedObject(target);
        data.FindProperty("startSettingsVersion").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
