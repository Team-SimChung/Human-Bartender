using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Diagnostics;
using Object = UnityEngine.Object;

/// <summary>홈 테스트 상황이 실제 씬·CSV 조건을 준비하고 이전 테스트 진행을 남기지 않는지 검사한다.</summary>
public sealed class HomeInteractionTestEnvironmentTests
{
    private const string HomePath = "Assets/01.Game/01.Scenes/Home.unity";
    private PlayerDataSO player;
    private int previousDay;
    private EGameFlow previousFlow;

    [SetUp]
    public void PreparePlayer()
    {
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        previousDay = GameStateManager.Instance.CurrentDay;
        previousFlow = GameStateManager.Instance.GameFlow;
    }

    [TearDown]
    public void RestoreState()
    {
        Object.DestroyImmediate(player);
        GameStateManager.Instance.CurrentDay = previousDay;
        GameStateManager.Instance.GameFlow = previousFlow;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TestServicesReplaceExistingSingletonsWithoutRegistrationConflicts(bool diagnosticsEnabled)
    {
        var original = ScriptableObject.CreateInstance<PlayerDataSO>();
        string collectorName = "HomeInteractionRegistrationTest-" + Guid.NewGuid().ToString("N");
        try
        {
            original.SetMoney(42);
            var builder = new ContainerBuilder();
            if (diagnosticsEnabled) builder.Diagnostics = DiagnositcsContext.GetCollector(collectorName);
            builder.RegisterInstance(original).AsSelf().As<IPlayerDataReader>().As<IPlayerDataWriter>();
            builder.Register<SaveManager>(Lifetime.Singleton);
            int originalCount = builder.Count;
            SaveManager isolatedSaves = null;

            HomeInteractionTestSession.ReplaceTestServices(builder, player, resolver =>
            {
                Assert.That(resolver.Resolve<PlayerDataSO>(), Is.SameAs(player));
                return isolatedSaves = new SaveManager(resolver.Resolve<PlayerDataSO>(), null,
                    GameStateManager.Instance, Path.Combine(Application.temporaryCachePath, "HomeInteractionRegistrationTest"));
            });

            Assert.That(builder.Count, Is.EqualTo(originalCount), "Overrides must replace registrations, not append duplicate singletons.");
            using var container = builder.Build();
            if (diagnosticsEnabled)
                Assert.That(builder.Diagnostics.GetDiagnosticsInfos().All(info => info.ResolveInfo != null), Is.True,
                    "Replaced registrations must not leave unbuilt diagnostic entries.");
            Assert.That(container.Resolve<PlayerDataSO>(), Is.SameAs(player));
            Assert.That(container.Resolve<IPlayerDataReader>(), Is.SameAs(player));
            Assert.That(container.Resolve<IPlayerDataWriter>(), Is.SameAs(player));
            Assert.That(container.Resolve<SaveManager>(), Is.SameAs(isolatedSaves));
            Assert.That(container.Resolve<SaveManager>(), Is.SameAs(isolatedSaves), "The isolated save service must remain a singleton.");
            player.SetMoney(1000);
            Assert.That(original.HasMoney(), Is.EqualTo(42), "Test progress must leave the original player untouched.");
        }
        finally
        {
            DiagnositcsContext.RemoveCollector(collectorName);
            Object.DestroyImmediate(original);
        }
    }

    [Test]
    public void EveryPresetReplacesPreviousProgressAndReplaysDeterministically()
    {
        foreach (var scenario in HomeInteractionTestScenarios.All)
        {
            player.AddFlag("home_lab_previous_run", true);
            player.SetCharacterAffinityAmount("home_lab_previous_actor", 321);
            player.SetMoney(-123);
            player.SetSkillAmount(456);
            scenario.Apply(player);
            int money = player.HasMoney();
            int skill = player.GetSkillValue();
            var affinity = player.ReadAffinity();
            var flags = player.ReadFlags();
            Assert.That(flags.ContainsKey("home_lab_previous_run"), Is.False, scenario.Id);
            Assert.That(affinity.ContainsKey("home_lab_previous_actor"), Is.False, scenario.Id);

            player.AddFlag("home_lab_previous_run", true);
            player.AddMoney(789);
            player.SetSkillAmount(-456);
            player.SetCharacterAffinityAmount("home_lab_previous_actor", 654);
            scenario.Apply(player);
            Assert.That(player.HasMoney(), Is.EqualTo(money), scenario.Id);
            Assert.That(player.GetSkillValue(), Is.EqualTo(skill), scenario.Id);
            CollectionAssert.AreEquivalent(affinity, player.ReadAffinity(), scenario.Id);
            CollectionAssert.AreEquivalent(flags, player.ReadFlags(), scenario.Id);
        }
    }

    [Test]
    public void PresetsCoverEveryAuthoredHomeTargetAndUseCurrentCsvDays()
    {
        var catalog = LoadCatalog();
        var days = catalog.Read<NewDayInfoData[]>("days").Select(day => day.Day).ToHashSet();
        var definitions = catalog.Read<NewInteractPointData[]>("interact_points_home");
        var scenarioIds = HomeInteractionTestScenarios.All.Select(scenario => scenario.Id).ToArray();
        Assert.That(scenarioIds.Distinct().Count(), Is.EqualTo(scenarioIds.Length));
        var home = SceneManager.GetSceneByPath(HomePath);
        bool opened = !home.IsValid() || !home.isLoaded;
        if (opened) home = EditorSceneManager.OpenScene(HomePath, OpenSceneMode.Additive);
        try
        {
            var targets = home.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Select(target => target.Content != null ? target.Content.SourceId : target.StorySceneId)
                .ToHashSet(StringComparer.Ordinal);
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scenario in HomeInteractionTestScenarios.All)
            {
                Assert.That(days.Contains(scenario.Day), Is.True, scenario.Id + " uses a missing CSV day.");
                Assert.That(scenario.Flow, Is.EqualTo(EGameFlow.CommuteIn).Or.EqualTo(EGameFlow.CommuteOut), scenario.Id);
                if (string.IsNullOrEmpty(scenario.FocusId)) continue;
                Assert.That(targets.Contains(scenario.FocusId), Is.True, scenario.Id + " points to an unauthored Home target.");
                scenario.Apply(player);
                var state = GameStateManager.Instance;
                state.CurrentDay = scenario.Day;
                state.GameFlow = scenario.Flow;
                var conditions = new ConditionUtil(state, player, player);
                if (definitions.Any(definition => definition.SourceId == scenario.FocusId))
                {
                    var definition = WorldInteractionContentRules.SelectDefinition(definitions, scenario.FocusId, scenario.Flow, conditions);
                    Assert.That(definition.HasValue, Is.True, scenario.Id + " cannot activate its focused content.");
                    Assert.That(conditions.CheckRequired(definition.Value.InteractWhen), Is.True, scenario.Id);
                }
                else
                    Assert.That(OutsideStorySceneRules.IsAvailable(scenario.FocusId, scenario.Day, scenario.Flow, conditions),
                        Is.True, scenario.Id + " cannot activate its focused story.");
                covered.Add(scenario.FocusId);
            }
            CollectionAssert.IsSubsetOf(targets, covered, "Every actual Home target needs a reachable test preset.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(home, true); }
    }

    [Test]
    public void ExpectedStoriesExistAndManualNightStoriesRemainEligible()
    {
        var stories = StoryCsvCompiler.Compile(LoadCatalog().Read<NewStreetData>("script/home")).Scenes
            .ToDictionary(story => story.Id, StringComparer.Ordinal);
        foreach (var scenario in HomeInteractionTestScenarios.All)
        {
            scenario.Apply(player);
            var state = GameStateManager.Instance;
            state.CurrentDay = scenario.Day;
            state.GameFlow = scenario.Flow;
            var conditions = new ConditionUtil(state, player, player);
            foreach (string id in scenario.ExpectedStories)
            {
                Assert.That(stories.ContainsKey(id), Is.True, scenario.Id + " names a missing Home story: " + id);
                var story = stories[id];
                Assert.That(story.Steps, Is.Not.Empty, scenario.Id + ": " + id);
                if (story.RuntimeScene == "Home" && story.StartMode == ENewSceneTrigger.Manual)
                    Assert.That(WorldInteractionSceneRules.IsEligible(story, "Home", scenario.Day, scenario.Flow, conditions),
                        Is.True, scenario.Id + " cannot reach " + id + " through the real night flow.");
            }
        }
    }

    [Test]
    public void DayTwoPresetsExerciseTheRealSleepPrerequisiteGate()
    {
        var state = GameStateManager.Instance;
        var conditions = new ConditionUtil(state, player, player);
        HomeInteractionTestScenarios.Find("night-day2-blocked").Apply(player);
        Assert.That(CampaignStoryRules.CanPrepareJohnnyDrink(conditions), Is.False);

        HomeInteractionTestScenarios.Find("night-day2-cleared").Apply(player);
        Assert.That(CampaignStoryRules.CanPrepareJohnnyDrink(conditions), Is.True);

        HomeInteractionTestScenarios.Find("night-day2").Apply(player);
        Assert.That(CampaignStoryRules.HasWildDog(conditions), Is.True);
        Assert.That(player.CheckFlag("shiba_bought_bitters"), Is.True);
        Assert.That(player.CheckFlag("campaign_observation"), Is.False,
            "The normal day-two night must acquire the observation through Johnny's memory.");
        Assert.That(player.CheckFlag("home_night2_seen"), Is.False);
    }

    private static CsvContentCatalog LoadCatalog() =>
        CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
}
