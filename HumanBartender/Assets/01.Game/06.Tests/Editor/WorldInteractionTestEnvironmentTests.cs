using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public sealed class WorldInteractionTestEnvironmentTests
{
    [Test]
    public void PointCatalogIncludesBothSavedScenesWithoutChangingOpenScenes()
    {
        var before = EditorSceneManager.GetSceneManagerSetup();
        var points = WorldInteractionTestCatalog.LoadPoints();
        var after = EditorSceneManager.GetSceneManagerSetup();
        CollectionAssert.AreEqual(before.Select(value => (value.path, value.isLoaded, value.isActive)),
            after.Select(value => (value.path, value.isLoaded, value.isActive)));
        foreach (string scene in WorldInteractionTestCatalog.Scenes)
        {
            var entries = points.Where(value => value.SceneName == scene).ToArray();
            Assert.That(entries, Is.Not.Empty, scene);
            Assert.That(entries.Select(value => value.Key).Distinct().Count(), Is.EqualTo(entries.Length),
                scene + " contains ambiguous point IDs.");
            Assert.That(entries.All(value => !string.IsNullOrEmpty(value.HierarchyPath)), Is.True);
        }
        Assert.That(points.Any(value => value.SceneName == "Home" && value.Key == "source:home_exit_door"), Is.True);
        Assert.That(points.Any(value => value.SceneName == "OutSide" && value.Key == "source:street_bar_door"), Is.True);
    }

    [Test]
    public void WorldScenariosRoundTripEverySupportedSceneDayAndFlowAndResetProgress()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            foreach (string scene in WorldInteractionTestCatalog.Scenes)
            for (int day = 0; day <= 3; day++)
            foreach (var flow in new[] { EGameFlow.CommuteIn, EGameFlow.CommuteOut })
            {
                string id = WorldInteractionTestCatalog.ScenarioId(scene, day, flow);
                var scenario = HomeInteractionTestScenarios.Find(id);
                Assert.That(scenario.RuntimeScene, Is.EqualTo(scene));
                Assert.That(scenario.Day, Is.EqualTo(day));
                Assert.That(scenario.Flow, Is.EqualTo(flow));
                Assert.That(scenario.IsWorldTest, Is.True);
                player.AddFlag("previous_point_test", true);
                player.SetMoney(-10);
                scenario.Apply(player);
                Assert.That(player.ReadFlags().ContainsKey("previous_point_test"), Is.False);
                Assert.That(player.HasMoney(), Is.Positive);
                Assert.That(player.CheckFlag("opening_seen"), Is.True);
                Assert.That(player.CheckFlag("day" + day + "_bar_completed"), Is.EqualTo(flow == EGameFlow.CommuteOut));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [TestCase("Play", 0, EGameFlow.CommuteIn)]
    [TestCase("Home", -1, EGameFlow.CommuteIn)]
    [TestCase("OutSide", 4, EGameFlow.CommuteOut)]
    [TestCase("OutSide", 1, EGameFlow.Bar)]
    public void UnsupportedStartsAreRejected(string scene, int day, EGameFlow flow) =>
        Assert.Throws<ArgumentException>(() => WorldInteractionTestCatalog.ScenarioId(scene, day, flow));

    [TestCase("Home", "home-wardrobe-pressed")]
    [TestCase("Home", "night2")]
    [TestCase("OutSide", "shiba-allowance")]
    [TestCase("OutSide", "neighborhood-bossPayRepeat")]
    public void ScriptCatalogIncludesInternalBranchesAndUsesCompiledSteps(string scene, string id)
    {
        var scripts = WorldInteractionTestCatalog.LoadScripts(scene);
        Assert.That(scripts.Select(value => value.Id).Distinct().Count(), Is.EqualTo(scripts.Length));
        Assert.That(scripts.All(value => value.Steps?.Length > 0), Is.True);
        Assert.That(scripts.Any(value => value.Id == id), Is.True, "Internal branches must be selectable without replaying their entry menus.");
        Assert.That(scripts.SelectMany(value => value.Steps).All(step => step.WebFields == null), Is.True,
            "The test tool must expose the same compiled commands as the runtime loader.");
        Assert.That(scripts.Any(value => value.Id == "campaign_d1_bar"), Is.False, "World scripts must not use the bar runner's schema.");
    }

    [Test]
    public void OutsidePresetsPrepareActualTargetsAndCsvEntryConditions()
    {
        var state = GameStateManager.Instance;
        int previousDay = state.CurrentDay;
        var previousFlow = state.GameFlow;
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        var preview = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/OutSide.unity");
        try
        {
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
            var definitions = catalog.Read<NewInteractPointData[]>("interact_points");
            var stories = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/street")).Scenes;
            var targets = WorldInteractionTestCatalog.Targets(preview);
            foreach (var scenario in OutsideInteractionTestScenarios.All)
            {
                scenario.Apply(player);
                state.CurrentDay = scenario.Day;
                state.GameFlow = scenario.Flow;
                var conditions = new ConditionUtil(state, player, player);
                var target = targets.Single(value => value.Content?.SourceId == scenario.FocusId || value.StorySceneId == scenario.FocusId);
                Assert.That(HomeInteractionTestScenarios.Find(scenario.Id), Is.SameAs(scenario));
                Assert.That(scenario.IsWorldTest, Is.True);
                if (target.Content != null)
                {
                    var definition = WorldInteractionContentRules.SelectDefinition(definitions, target.Content.SourceId, scenario.Flow, conditions);
                    Assert.That(definition.HasValue, Is.True, scenario.Name);
                    Assert.That(conditions.CheckRequired(definition.Value.InteractWhen), Is.True, scenario.Name);
                }
                else
                {
                    var story = stories.Single(value => value.Id == target.StorySceneId);
                    if (OutsideStorySceneRules.TryBind(story, "OutSide", out var bound)) story = bound;
                    target.ApplyStartSettings(ref story);
                    Assert.That(OutsideStorySceneRules.IsAvailable(story.Id, scenario.Day, scenario.Flow, conditions), Is.True, scenario.Name);
                    Assert.That(WorldInteractionSceneRules.IsEligible(story, "OutSide", scenario.Day, scenario.Flow, conditions), Is.True, scenario.Name);
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            UnityEngine.Object.DestroyImmediate(player);
            state.CurrentDay = previousDay;
            state.GameFlow = previousFlow;
        }
    }
}
