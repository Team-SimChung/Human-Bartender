using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>남은 NPC들의 기존 CSV 조건·그룹 연출·중단 복구·취침 연결을 검사한다. 저장 슬롯은 쓰지 않는다.</summary>
public sealed class RemainingNpcMigrationTests
{
    private const string HomePath = "Assets/01.Game/01.Scenes/Home.unity";
    private const string OutsidePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private static readonly string[] OutsideIds =
    {
        "day2-corridor-thug", "day2-thug-information", "commute2", "commute3",
        "outside_day1_samho_smuggler", "day3-samho-contract", "shiba-shop", "shiba-shop-revisit",
        "neighborhood-reveal", "neighborhood-bossIntro"
    };
    private SceneSetup[] sceneSetup;
    private PlayerDataSO isolatedPlayer;
    private PlayerDataSO sharedPlayer;
    private int originalMoney;
    private int originalSkill;
    private Dictionary<string, int> originalAffinity;
    private Dictionary<string, bool> originalFlags;
    private bool finished;
    private Exception failure;
    private bool progressionFinished;
    private GameProgressionResult progressionResult;

    private sealed class RecordingFade : ISceneFadeService
    {
        public readonly List<float> Durations = new();
        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Durations.Add(duration); return UniTask.CompletedTask; }
        public UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Durations.Add(duration); return UniTask.CompletedTask; }
    }

    private static WorldInteractionTarget FindTarget(Scene scene, string id) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
        .Single(target => target.StorySceneId == id);

    private static NpcActor Actor(WorldInteractionTarget target, string role) => target
        .GetComponentsInChildren<NpcActor>(true).Single(value => value.name.EndsWith("· " + role, StringComparison.Ordinal));

    private static NewSceneData Story(string id, Scene destination)
    {
        bool home = id == "neighborhood-homeCat";
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var value = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>(home ? "script/home" : "script/street"))
            .Scenes.Single(story => story.Id == id);
        if (OutsideStorySceneRules.TryBind(value, home ? "Home" : "OutSide", out var native)) value = native;
        value.RuntimeScene = destination.name;
        return value;
    }

    private void PrepareClones(params string[] ids)
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string path in new[] { HomePath, OutsidePath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (string id in ids.Where(id => (id == "neighborhood-homeCat") == (path == HomePath)))
                {
                    var clone = Object.Instantiate(FindTarget(scene, id).gameObject);
                    clone.name = id;
                    SceneManager.MoveGameObjectToScene(clone, isolated);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        SceneManager.SetActiveScene(isolated);
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason, float seconds = 10)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private static IEnumerator Frames(int count = 2)
    {
        int frame = Time.frameCount;
        yield return WaitUntil(() => Time.frameCount >= frame + count, "Game frames did not advance.");
    }

    private async UniTask Observe(UniTask task)
    {
        finished = false;
        failure = null;
        try { await task; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private IEnumerator CompleteAction(UniTask task)
    {
        Observe(task).Forget();
        yield return WaitUntil(() => finished, "NPC action did not complete.");
        Assert.That(failure, Is.Null);
    }

    private void RestorePlayerSnapshot()
    {
        if (sharedPlayer != null && originalFlags != null)
            sharedPlayer.ReplaceProgress(originalMoney, originalSkill, originalAffinity, originalFlags);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying)
        {
            var controller = WorldInteractionController.ForScene(SceneManager.GetActiveScene());
            if (controller != null)
            {
                controller.Cancel();
                float deadline = Time.realtimeSinceStartup + 5;
                while (controller != null && controller.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            }
            RestorePlayerSnapshot();
            yield return new ExitPlayMode();
        }
        if (isolatedPlayer != null) Object.DestroyImmediate(isolatedPlayer);
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void BothScenesUseCommonParticipantsForEveryRemainingStoryAndContentNpc()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        foreach (string path in new[] { HomePath, OutsidePath })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<WorldStoryActors>(true)), Is.Empty,
                    "The authored scenes must not retain a second actor execution path.");
                var targets = (path == HomePath ? new[] { "neighborhood-homeCat" } : OutsideIds)
                    .Select(id => FindTarget(scene, id)).ToList();
                if (path == OutsidePath)
                    targets.AddRange(roots.SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                        .Where(target => target.Content != null && target.Content.SourceId is "bubi" or "shiba"));
                Assert.That(targets.Count, Is.EqualTo(path == HomePath ? 1 : 12));
                foreach (var target in targets)
                {
                    Assert.That(target.Participant, Is.InstanceOf<NpcInteraction>(), target.name);
                    var actors = target.GetComponentsInChildren<NpcActor>(true);
                    Assert.That(actors, Is.Not.Empty, target.name);
                    foreach (var actor in actors)
                    {
                        Assert.That(actor.GetComponent<NpcMovement2D>(), Is.Not.Null, actor.name);
                        var animator = actor.GetComponent<Animator>();
                        Assert.That(animator, Is.Not.Null, actor.name);
                        Assert.That(animator.runtimeAnimatorController, Is.Not.Null, actor.name);
                        Assert.That(animator.applyRootMotion, Is.False, actor.name);
                    }
                }
                if (path == OutsidePath)
                {
                    Assert.That(FindTarget(scene, "day2-corridor-thug").StartMode, Is.EqualTo(WorldInteractionStartMode.Automatic));
                    Assert.That(FindTarget(scene, "day2-thug-information").StartMode, Is.EqualTo(WorldInteractionStartMode.Interact));
                    Assert.That(FindTarget(scene, "shiba-shop").GetComponent<WorldDialogueSpeakers>()
                        .TryResolve("shiba", out _, out _, out _), Is.True);
                    foreach (string id in new[] { "outside_day1_samho_smuggler", "day3-samho-contract" })
                    {
                        var speakers = FindTarget(scene, id).GetComponent<WorldDialogueSpeakers>();
                        string prefix = id == "day3-samho-contract" ? "contract-" : "trade-";
                        Assert.That(speakers.TryResolve(prefix + "samho", out _, out _, out _), Is.True);
                        Assert.That(speakers.TryResolve(prefix + "smuggler", out _, out _, out _), Is.True);
                    }
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    [UnityTest]
    public IEnumerator HiddenCatCanBeRevealedAndAdoptedWhileSupportingActorsRestoreAfterInteraction()
    {
        PrepareClones("neighborhood-reveal", "neighborhood-homeCat");
        yield return new EnterPlayMode();
        yield return VerifyCatAndSupportingActors();
    }

    private IEnumerator VerifyCatAndSupportingActors()
    {
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 2;
        state.GameFlow = EGameFlow.CommuteIn;
        isolatedPlayer = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(state, isolatedPlayer, isolatedPlayer);
        var content = new CsvStoryTestContent();
        var events = new NeighborhoodStoryEvents(content);
        var scene = SceneManager.GetActiveScene();
        var target = FindTarget(scene, "neighborhood-reveal");
        var interaction = target.GetComponent<NpcInteraction>();
        var cat = Actor(target, "boobi");
        var neighbor = Actor(target, "neighbor");
        var catHome = cat.transform.position;
        var neighborHome = neighbor.transform.position;
        var luna = new GameObject("Cat sequence Luna").transform;
        luna.position = catHome + Vector3.left * 2;
        NpcSequenceTestSettings.CreateStagingCamera(luna);
        var fades = new RecordingFade();
        interaction.Initialize(conditions, luna, Story(target.StorySceneId, scene), fades, null);
        Assert.That(cat.IsVisible, Is.False);
        Assert.That(neighbor.IsVisible, Is.False);
        Assert.That(interaction.CanInteract, Is.True, "The hidden cat's discovery trigger must remain usable.");
        interaction.BeginInteraction();
        var run = new OutsideStoryExecution(conditions,
            (_, _) => throw new InvalidOperationException("This action-only regression must not request a choice."),
            interaction.PlayWorldActionAsync);
        yield return CompleteAction(events.ExecuteAsync(OutsideStoryCommand.RevealCat, null, run, CancellationToken.None).AsUniTask());
        Assert.That(isolatedPlayer.CheckFlag("boobi_revealed"), Is.True);
        Assert.That(cat.IsVisible, Is.True, "The reveal command must show the cat.");
        Assert.That(cat.transform.position, Is.EqualTo(catHome));
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.FeedCat, CancellationToken.None));
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.NeighborEnter, CancellationToken.None));
        Assert.That(neighbor.IsVisible, Is.True);
        Assert.That(neighbor.transform.position.x, Is.EqualTo(cat.transform.position.x + .8f).Within(.0001f));
        Assert.That(neighbor.transform.position.y, Is.EqualTo(neighborHome.y), "The neighbor stays on the corridor floor while approaching the cat.");
        Assert.That(neighbor.transform.position.z, Is.EqualTo(neighborHome.z));
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.NeighborExit, CancellationToken.None));
        Assert.That(neighbor.IsVisible, Is.False);
        Assert.That(neighbor.transform.position, Is.EqualTo(neighborHome));
        yield return CompleteAction(events.ExecuteAsync(OutsideStoryCommand.AdoptCat, null, run, CancellationToken.None).AsUniTask());
        Assert.That(isolatedPlayer.CheckFlag("boobi_adopted"), Is.True);
        Assert.That(run.Destination, Is.EqualTo(GameProgressionDestination.HomeVisit));
        Assert.That(cat.IsVisible, Is.True, "The actor stays visible until the scene-transition boundary takes over.");
        interaction.EndInteraction();
        Assert.That(interaction.CanInteract, Is.False);
        Assert.That(cat.IsVisible, Is.False, "Adoption must keep the street cat hidden after pose restoration.");

        var restored = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            restored.ReplaceProgress(isolatedPlayer.HasMoney(), isolatedPlayer.GetSkillValue(),
                isolatedPlayer.ReadAffinity(), isolatedPlayer.ReadFlags());
            var restoredConditions = new ConditionUtil(state, restored, restored);
            interaction.Initialize(restoredConditions, luna, Story(target.StorySceneId, scene), fades, null);
            Assert.That(interaction.CanInteract, Is.False);
            var homeTarget = FindTarget(scene, "neighborhood-homeCat");
            var home = homeTarget.GetComponent<NpcInteraction>();
            home.Initialize(restoredConditions, luna, Story(homeTarget.StorySceneId, scene), fades, null);
            var homeCat = Actor(homeTarget, "boobi");
            var chris = Actor(homeTarget, "chris");
            var chrisHome = chris.transform.position;
            Assert.That(homeCat.IsVisible, Is.True);
            Assert.That(chris.IsVisible, Is.False);
            home.BeginInteraction();
            yield return CompleteAction(home.PlayWorldActionAsync(OutsideStoryWorldAction.ChrisEnter, CancellationToken.None));
            Assert.That(chris.IsVisible, Is.True);
            Assert.That(chris.transform.position, Is.EqualTo(chrisHome));
            Assert.That(luna.position, Is.EqualTo(new Vector3(2.2f, -.7f, 0)));
            Assert.That(fades.Durations, Is.EqualTo(new[] { .45f, .6f }));
            Assert.That(home.GetComponent<NpcSceneStaging>().HasCameraOverride, Is.True);
            yield return CompleteAction(home.PlayWorldActionAsync(OutsideStoryWorldAction.CatMeow, CancellationToken.None));
            yield return CompleteAction(home.PlayWorldActionAsync(OutsideStoryWorldAction.PetCat, CancellationToken.None));
            yield return CompleteAction(home.CompleteInteractionAsync(CancellationToken.None));
            Assert.That(home.GetComponent<NpcSceneStaging>().HasCameraOverride, Is.False);
            home.EndInteraction();
            Assert.That(homeCat.IsVisible, Is.True);
            Assert.That(chris.IsVisible, Is.False, "Chris must not remain standing beside the cat after the rest scene.");
            Assert.That(chris.transform.position, Is.EqualTo(chrisHome));
        }
        finally
        {
            interaction.Initialize(conditions, luna, Story(target.StorySceneId, scene), fades, null);
            var homeTarget = FindTarget(scene, "neighborhood-homeCat");
            homeTarget.GetComponent<NpcInteraction>().Initialize(conditions, luna, Story(homeTarget.StorySceneId, scene), fades, null);
            Object.Destroy(restored);
        }
    }

    [UnityTest]
    public IEnumerator SamhoAndDealerUseAuthoredMovesAndCancellationRestoresTheWholeGroup()
    {
        PrepareClones("commute2", "day3-samho-contract");
        yield return new EnterPlayMode();
        yield return VerifySamhoAndDealer();
    }

    private IEnumerator VerifySamhoAndDealer()
    {
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 2;
        state.GameFlow = EGameFlow.CommuteIn;
        isolatedPlayer = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(state, isolatedPlayer, isolatedPlayer);
        var scene = SceneManager.GetActiveScene();
        var target = FindTarget(scene, "commute2");
        var interaction = target.GetComponent<NpcInteraction>();
        var samho = Actor(target, "samho");
        var home = samho.transform.position;
        var luna = new GameObject("Samho sequence Luna").transform;
        luna.position = home + Vector3.right * 3;
        NpcSequenceTestSettings.CreateStagingCamera(luna);
        interaction.Initialize(conditions, luna, Story(target.StorySceneId, scene), null, null);
        interaction.BeginInteraction();
        float started = Time.time;
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.SamhoDepart, CancellationToken.None));
        Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(1.28f), "The web departure includes one second walking and .3 seconds moving fade.");
        Assert.That(samho.transform.position.x, Is.EqualTo(home.x - .845f).Within(.0001f), "Samho departs away from Luna.");
        Assert.That(samho.transform.position.y, Is.EqualTo(home.y));
        Assert.That(samho.IsVisible, Is.False);
        isolatedPlayer.AddFlag("day2_samho_done", true);
        interaction.EndInteraction();
        Assert.That(samho.transform.position, Is.EqualTo(home));
        Assert.That(samho.IsVisible, Is.False);
        Assert.That(interaction.CanInteract, Is.False);

        state.CurrentDay = 3;
        state.GameFlow = EGameFlow.CommuteOut;
        target = FindTarget(scene, "day3-samho-contract");
        interaction = target.GetComponent<NpcInteraction>();
        samho = Actor(target, "samho");
        var dealer = Actor(target, "dealer");
        var samhoHome = samho.transform.position;
        var dealerHome = dealer.transform.position;
        luna.position = samhoHome + new Vector3(3, 4, 5);
        interaction.Initialize(conditions, luna, Story(target.StorySceneId, scene), null, null);
        interaction.BeginInteraction();
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.NotionDealerExit, CancellationToken.None));
        Assert.That(dealer.IsVisible, Is.False);
        Assert.That(dealer.transform.position.x, Is.EqualTo(dealerHome.x - .845f).Within(.0001f));
        yield return CompleteAction(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.NotionApproach, CancellationToken.None));
        Assert.That(dealer.IsVisible, Is.False, "The departing dealer must not reappear when Samho approaches Luna.");
        Assert.That(samho.transform.position.x, Is.EqualTo(luna.position.x - .75f).Within(.0001f));
        Assert.That(samho.transform.position.y, Is.EqualTo(samhoHome.y));
        Assert.That(samho.transform.position.z, Is.EqualTo(samhoHome.z));
        using (var cancellation = new CancellationTokenSource())
        {
            Observe(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.NotionSamhoExit, cancellation.Token)).Forget();
            yield return Frames(3);
            cancellation.Cancel();
            interaction.EndInteraction();
            yield return WaitUntil(() => finished && !samho.IsBusy, "Cancelled group movement did not release actors.");
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        }
        Assert.That(samho.transform.position, Is.EqualTo(samhoHome));
        Assert.That(dealer.transform.position, Is.EqualTo(dealerHome));
        Assert.That(samho.IsVisible, Is.True);
        Assert.That(dealer.IsVisible, Is.True);
        Assert.That(isolatedPlayer.CheckFlag("day3_contract_done"), Is.False);
        Assert.That(interaction.CanInteract, Is.True);
    }

    private void CompleteProgression(GameProgressionResult result)
    {
        progressionResult = result;
        progressionFinished = true;
    }

    [UnityTest]
    public IEnumerator DayTwoThugAndShopKeepCsvPresenceAndRestoredProgress()
    {
        PrepareClones("day2-corridor-thug", "day2-thug-information", "shiba-shop", "shiba-shop-revisit");
        yield return new EnterPlayMode();
        yield return VerifyDayAndShopConditions();
    }

    private IEnumerator VerifyDayAndShopConditions()
    {
        Time.timeScale = 1;
        var state = GameStateManager.Instance;
        state.CurrentDay = 1;
        state.GameFlow = EGameFlow.CommuteIn;
        isolatedPlayer = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(state, isolatedPlayer, isolatedPlayer);
        var scene = SceneManager.GetActiveScene();
        var thug = FindTarget(scene, "day2-corridor-thug").GetComponent<NpcInteraction>();
        var information = FindTarget(scene, "day2-thug-information").GetComponent<NpcInteraction>();
        var shop = FindTarget(scene, "shiba-shop").GetComponent<NpcInteraction>();
        var revisit = FindTarget(scene, "shiba-shop-revisit").GetComponent<NpcInteraction>();
        var interactions = new[] { thug, information, shop, revisit };
        var luna = new GameObject("Presence test Luna").transform;
        foreach (var interaction in interactions)
            interaction.Initialize(conditions, luna, Story(interaction.GetComponent<WorldInteractionTarget>().StorySceneId, scene), null, null);
        Assert.That(interactions.All(value => !value.CanInteract), Is.True,
            "Day one's first commute must not expose day two's thug or the not-yet-open shop.");
        isolatedPlayer.AddFlag("day1_bar_completed", true);
        state.GameFlow = EGameFlow.CommuteOut;
        yield return Frames();
        Assert.That(shop.CanInteract, Is.False, "The web shop first opens on day two, even after day one's bar is complete.");
        Assert.That(revisit.CanInteract, Is.False);
        state.CurrentDay = 2;
        state.GameFlow = EGameFlow.CommuteIn;
        yield return Frames();
        Assert.That(thug.CanInteract, Is.True);
        Assert.That(information.CanInteract, Is.False);
        Assert.That(shop.CanInteract, Is.True);
        isolatedPlayer.AddFlag("day2_thug_done", true);
        isolatedPlayer.AddFlag("shiba_shop_met", true);
        yield return Frames();
        Assert.That(thug.CanInteract, Is.False);
        Assert.That(information.CanInteract, Is.True);
        Assert.That(shop.CanInteract, Is.False);
        Assert.That(revisit.CanInteract, Is.True);
        var restored = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            restored.ReplaceProgress(isolatedPlayer.HasMoney(), isolatedPlayer.GetSkillValue(),
                isolatedPlayer.ReadAffinity(), isolatedPlayer.ReadFlags());
            var restoredConditions = new ConditionUtil(state, restored, restored);
            foreach (var interaction in interactions)
                interaction.Initialize(restoredConditions, luna, Story(interaction.GetComponent<WorldInteractionTarget>().StorySceneId, scene), null, null);
            Assert.That(thug.CanInteract, Is.False);
            Assert.That(information.CanInteract, Is.True);
            Assert.That(shop.CanInteract, Is.False);
            Assert.That(revisit.CanInteract, Is.True);
            state.GameFlow = EGameFlow.CommuteOut;
            yield return Frames();
            Assert.That(information.CanInteract, Is.False, "The information thug still belongs to day two's outbound commute.");
            Assert.That(revisit.CanInteract, Is.True);
            state.CurrentDay = 3;
            state.GameFlow = EGameFlow.CommuteIn;
            yield return Frames();
            Assert.That(thug.CanInteract, Is.False);
            Assert.That(information.CanInteract, Is.False);
            Assert.That(revisit.CanInteract, Is.True);
        }
        finally
        {
            foreach (var interaction in interactions)
                interaction.Initialize(conditions, luna, Story(interaction.GetComponent<WorldInteractionTarget>().StorySceneId, scene), null, null);
            Object.Destroy(restored);
        }
    }

    [UnityTest]
    public IEnumerator RealSofaRestUsesTheHomeCatParticipantAndReleasesItBeforeMorning()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyRealNestedRest();
    }

    private IEnumerator VerifyRealNestedRest()
    {
        Time.timeScale = 1;
        yield return WaitUntil(() => NewDataLoadManager.IsLoaded || NewDataLoadManager.LastLoadError != null,
            "Real CSV load timed out.", 30);
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        yield return null;
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope.Container != null);
        Assert.That(root, Is.Not.Null);
        var progression = root.Container.Resolve<GameProgressionService>();
        var conditions = root.Container.Resolve<IConditionUtil>();
        sharedPlayer = root.Container.Resolve<PlayerDataSO>();
        originalMoney = sharedPlayer.HasMoney();
        originalSkill = sharedPlayer.GetSkillValue();
        originalAffinity = sharedPlayer.ReadAffinity();
        originalFlags = sharedPlayer.ReadFlags();
        progressionFinished = false;
        progression.StartNewGameAsync(() => conditions.ApplyRequired("flag.opening_seen = true"), RestorePlayerSnapshot)
            .ContinueWith(CompleteProgression).Forget();
        yield return WaitUntil(() => progressionFinished, "New game to Home timed out.", 30);
        Assert.That(progressionResult.Succeeded, Is.True, progressionResult.Message);
        var scene = SceneManager.GetActiveScene();
        var controller = WorldInteractionController.ForScene(scene);
        yield return WaitUntil(() => controller != null && controller.IsReady, "Home interaction initialization timed out.", 30);
        var context = Object.FindObjectsByType<WorldInteractionSceneContext>(FindObjectsSortMode.None)
            .Single(value => value.gameObject.scene == scene);
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        // Isolate the nested cat rest from the independently tested terrace story; use the real sofa and sleep flow.
        conditions.ApplyRequired("flag.boobi_adopted = true; flag.boobi_rest_seen = false; flag.home_night1_seen = true");
        context.RefreshContents();
        yield return Frames();
        var catTarget = FindTarget(scene, "neighborhood-homeCat");
        var cat = Actor(catTarget, "boobi");
        var chris = Actor(catTarget, "chris");
        Assert.That(cat.IsVisible, Is.True, "The adopted HomeCat must become visible without manually enabling its target.");
        Assert.That(chris.IsVisible, Is.False);
        var sofa = WorldInteractionTarget.InScene(scene).Single(target => target.Content != null && target.Content.SourceId == "home_sofa");
        yield return WaitUntil(() => context.Player.State == EInteractorState.None && !context.Runner.IsRunning && !controller.IsRunning,
            "Home startup did not release the player and dialogue runner.");
        context.Player.transform.position = sofa.transform.position;
        StoryExecutionResult restResult = default;
        Observe(controller.PlayAsync(sofa).ContinueWith(value => { restResult = value; })).Forget();
        bool sawChris = false;
        float deadline = Time.realtimeSinceStartup + 35;
        while (!finished && Time.realtimeSinceStartup < deadline)
        {
            sawChris |= chris.IsVisible;
            controller.TryHandleInput(); // First sofa option is Sleep, then progress the authored rest dialogue.
            yield return null;
        }
        Assert.That(finished, Is.True, "The real sofa's nested rest did not complete.");
        Assert.That(failure, Is.Null);
        Assert.That(restResult.Completed, Is.True, restResult.Status + ": " + restResult.Error);
        Assert.That(controller.LastResult.Completed, Is.True, controller.LastResult.Error);
        Assert.That(sawChris, Is.True, "ChrisEnter was not dispatched to the nested HomeCat participant.");
        Assert.That(conditions.CheckRequired("flag.boobi_rest_seen"), Is.True);
        Assert.That(GameStateManager.Instance.CurrentDay, Is.EqualTo(2));
        Assert.That(GameStateManager.Instance.GameFlow, Is.EqualTo(EGameFlow.CommuteIn));
        Assert.That(chris.IsVisible, Is.False);
        Assert.That(cat.IsVisible, Is.True);
        Assert.That(catTarget.GetComponent<NpcInteraction>().CanInteract, Is.True);
        Assert.That(context.Player.State, Is.EqualTo(EInteractorState.None));
        Assert.That(context.IsSafeForSave, Is.True);
        var restored = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            restored.ReplaceProgress(sharedPlayer.HasMoney(), sharedPlayer.GetSkillValue(), sharedPlayer.ReadAffinity(), sharedPlayer.ReadFlags());
            Assert.That(restored.CheckFlag("boobi_adopted"), Is.True);
            Assert.That(restored.CheckFlag("boobi_rest_seen"), Is.True);
        }
        finally { Object.Destroy(restored); }
    }
}
