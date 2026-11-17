using System;
using System.Linq;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WorldInteractionPreparationTests
{
    private PlayerDataSO player;
    private int previousDay;
    private EGameFlow previousFlow;

    [SetUp]
    public void SetUp()
    {
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        previousDay = GameStateManager.Instance.CurrentDay;
        previousFlow = GameStateManager.Instance.GameFlow;
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(player);
        GameStateManager.Instance.CurrentDay = previousDay;
        GameStateManager.Instance.GameFlow = previousFlow;
    }

    private IConditionUtil Prepare(string id)
    {
        var scenario = HomeInteractionTestScenarios.Find(WorldInteractionTestPreparationCatalog.Prefix + id);
        scenario.Apply(player);
        GameStateManager.Instance.CurrentDay = scenario.Day;
        GameStateManager.Instance.GameFlow = scenario.Flow;
        return new ConditionUtil(GameStateManager.Instance, player, player);
    }

    [Test]
    public void EveryAuthoredPointHasPreparationOrAnExplicitClosedCondition()
    {
        var points = WorldInteractionTestCatalog.LoadPoints();
        foreach (var point in points)
            Assert.That(WorldInteractionTestPreparationCatalog.ForTarget(point.SceneName, point.Key).Any() ||
                !string.IsNullOrEmpty(WorldInteractionTestPreparationCatalog.ClosedReason(point.SceneName, point.Key)), Is.True,
                point.SceneName + "/" + point.Key + " needs a preparation or a closed-condition explanation.");
        TestContext.WriteLine($"{WorldInteractionTestPreparationCatalog.All.Count} preparations; {points.Length} authored points; " +
            $"{points.Count(point => WorldInteractionTestPreparationCatalog.ClosedReason(point.SceneName, point.Key) != null)} explicitly closed points.");
    }

    [TestCase("Home")]
    [TestCase("OutSide")]
    public void EveryPreparationMatchesTheActualCsvAndNativeEntryConditions(string scene)
    {
        var preview = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/" + scene + ".unity");
        try
        {
            var targets = WorldInteractionTestCatalog.Targets(preview);
            var definitions = WorldInteractionTestCatalog.LoadDefinitions();
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
            var stories = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>(scene == "Home" ? "script/home" : "script/street")).Scenes;
            foreach (var preparation in WorldInteractionTestPreparationCatalog.All.Where(value => value.Scenario.RuntimeScene == scene))
            {
                var scenario = preparation.Scenario;
                Assert.That(HomeInteractionTestScenarios.Find(scenario.Id), Is.SameAs(scenario));
                Assert.That(scenario.IsWorldTest, Is.True);
                var conditions = Prepare(scenario.Id.Substring(WorldInteractionTestPreparationCatalog.Prefix.Length));
                var target = targets.Single(value => value.Content?.SourceId == scenario.FocusId || value.StorySceneId == scenario.FocusId);
                bool available;
                if (target.Content != null)
                {
                    var definition = WorldInteractionContentRules.SelectDefinition(definitions, target.Content.SourceId, scenario.Flow, conditions);
                    available = definition.HasValue && conditions.CheckRequired(definition.Value.InteractWhen);
                }
                else
                {
                    Assert.That(stories.Count(value => value.Id == target.StorySceneId), Is.EqualTo(1), scenario.Id + "/" + target.StorySceneId);
                    var story = stories.Single(value => value.Id == target.StorySceneId);
                    if (OutsideStorySceneRules.TryBind(story, scene, out var bound)) story = bound;
                    target.ApplyStartSettings(ref story);
                    available = OutsideStorySceneRules.IsAvailable(story.Id, scenario.Day, scenario.Flow, conditions) &&
                        WorldInteractionSceneRules.IsEligible(story, scene, scenario.Day, scenario.Flow, conditions);
                }
                Assert.That(available, Is.EqualTo(preparation.TargetAvailable), scenario.Id);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    [Test]
    public void FullPreparationResetsProgressButConditionOnlyApplicationPreservesUnrelatedProgress()
    {
        var catalog = WorldInteractionTestPreparationCatalog.All;
        Assert.That(catalog.Select(value => value.Scenario.Id).Distinct().Count(), Is.EqualTo(catalog.Count));
        foreach (var entry in catalog)
        {
            var scenario = entry.Scenario;
            player.AddFlag("unrelated_test_progress", true);
            player.SetCharacterAffinityAmount("test_friend", 23);
            scenario.ApplyConditions(player);
            Assert.That(player.CheckFlag("unrelated_test_progress"), Is.True, scenario.Id);
            Assert.That(player.ReadAffinity()["test_friend"], Is.EqualTo(23), scenario.Id);
            Assert.That(player.HasMoney(), Is.EqualTo(scenario.Money), scenario.Id);
            foreach (var pair in scenario.FlagOverrides) Assert.That(player.CheckFlag(pair.Key), Is.EqualTo(pair.Value), scenario.Id + "/" + pair.Key);
            scenario.Apply(player);
            Assert.That(player.CheckFlag("unrelated_test_progress"), Is.False, scenario.Id);
            Assert.That(player.ReadAffinity().ContainsKey("test_friend"), Is.False, scenario.Id);
        }
        Prepare("cat-feed-ready");
        WorldInteractionTestPreparationCatalog.Find(WorldInteractionTestPreparationCatalog.Prefix + "cat-no-tuna").Scenario.ApplyConditions(player);
        Assert.That(player.CheckFlag("shiba_bought_tuna_can"), Is.False, "Switching cases must clear stale ownership.");
        Assert.Throws<InvalidOperationException>(() => HomeInteractionTestSession.ApplyPreparation(catalog[0].Scenario.Id),
            "The editor must never modify a normal gameplay player.");
    }

    [TestCase("cat-feed-ready", OutsideStoryCommand.CatChoice, "feed,pet,cancel")]
    [TestCase("cat-no-tuna", OutsideStoryCommand.CatChoice, "pet,cancel")]
    [TestCase("outside-test-cat-feed", OutsideStoryCommand.CatChoice, "pet,cancel")]
    [TestCase("repair-gear", OutsideStoryCommand.RepairChoice, "gear,cancel")]
    [TestCase("repair-cash", OutsideStoryCommand.RepairChoice, "pay,cancel")]
    [TestCase("repair-short", OutsideStoryCommand.RepairChoice, "cancel")]
    [TestCase("outside-test-repair-paid", OutsideStoryCommand.RepairChoice, "cancel")]
    [TestCase("boss-cash", OutsideStoryCommand.BossRepeatChoice, "wait,pay,cancel")]
    [TestCase("boss-short", OutsideStoryCommand.BossRepeatChoice, "wait,cancel")]
    [TestCase("boss-repair", OutsideStoryCommand.BossRepeatChoice, "wait,repair,cancel")]
    [TestCase("boss-paid", OutsideStoryCommand.BossRepeatChoice, "wait,cancel")]
    [TestCase("boss-promised", OutsideStoryCommand.BossRepeatChoice, "wait,cancel")]
    [TestCase("boss-received", OutsideStoryCommand.BossRepeatChoice, "wait,cancel")]
    [TestCase("shop-budget", OutsideStoryCommand.ShopItems, "cancel,bitters,tuna_can")]
    [TestCase("shop-no-money", OutsideStoryCommand.ShopItems, "cancel")]
    [TestCase("shop-tuna-boundary", OutsideStoryCommand.ShopItems, "cancel,tuna_can")]
    [TestCase("shop-owned", OutsideStoryCommand.ShopItems, "cancel")]
    public void PreparedInventoryAndMoneyProduceTheExpectedRealMenuChoices(string id, OutsideStoryCommand command, string expected)
    {
        var conditions = Prepare(id);
        IOutsideStoryEventHandler handler = command == OutsideStoryCommand.ShopItems
            ? new ShopStoryEvents(player, GameStateManager.Instance, null) : new NeighborhoodStoryEvents(null);
        string[] enabled = null;
        var run = new OutsideStoryExecution(conditions, (options, _) =>
        {
            enabled = options.Where(option => conditions.CheckRequired(option.When)).Select(option => option.Id).ToArray();
            return UniTask.FromResult(options.Single(option => option.Id == "cancel"));
        }, null);
        handler.ExecuteAsync(command, null, run, CancellationToken.None).GetAwaiter().GetResult();
        CollectionAssert.AreEquivalent(expected.Split(','), enabled);
    }

    [TestCase("shop-allowance", "shiba-allowance")]
    [TestCase("shop-lesson", "shared-shop-quizIntro")]
    [TestCase("shop-lesson-done", null)]
    [TestCase("shop-no-money", null)]
    public void ShopPreparationAlsoResolvesThePrecedingAllowanceAndLessonBranches(string id, string expectedStory)
    {
        var conditions = Prepare(id);
        var content = new CsvStoryTestContent();
        var run = new OutsideStoryExecution(conditions, (options, _) => throw new InvalidOperationException("Opening the shop must not choose a product yet."), null);
        var result = new ShopStoryEvents(player, GameStateManager.Instance, content)
            .ExecuteAsync(OutsideStoryCommand.ShopOpen, null, run, CancellationToken.None).GetAwaiter().GetResult();
        if (expectedStory == null)
        {
            Assert.That(result.Length, Is.EqualTo(1));
            Assert.That(result[0].Command, Is.EqualTo(OutsideStoryCommand.ShopItems));
        }
        else CollectionAssert.AreEqual(content.GetSceneSteps(expectedStory), result.Take(content.GetSceneSteps(expectedStory).Length));
    }
}
