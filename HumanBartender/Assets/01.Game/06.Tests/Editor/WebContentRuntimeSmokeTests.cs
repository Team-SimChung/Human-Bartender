using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

public sealed class WebContentRuntimeSmokeTests
{
    SceneSetup[] setup;
    GameProgressionResult result;
    bool done;
    void Complete(GameProgressionResult value) { result = value; done = true; }
    static void NoOp() { }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (setup != null) EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    [UnityTest]
    public IEnumerator PinnedContentLoadsThroughExistingProgressionAndSceneScopes()
    {
        setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        float until = Time.realtimeSinceStartup + 30;
        while (!NewDataLoadManager.IsLoaded && NewDataLoadManager.LastLoadError == null && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(NewDataLoadManager.LastLoadError, Is.Null);
        Assert.That(NewDataLoadManager.IsLoaded, Is.True, "CSV load timed out");
        yield return null;
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None).FirstOrDefault(scope => scope.Container != null);
        Assert.That(root, Is.Not.Null);
        var progression = root.Container.Resolve<GameProgressionService>();
        Assert.That(progression, Is.Not.Null);
        result = default;
        done = false;
        progression.StartNewGameAsync(NoOp, NoOp).ContinueWith(Complete).Forget();
        until = Time.realtimeSinceStartup + 30;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done && result.Succeeded, Is.True, result.Message);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"));
        var home = Object.FindFirstObjectByType<WorldInteractionSceneContext>();
        Assert.That(home.IsReady, Is.True, home.InitializationError);
        var homeCat = WorldInteractionTarget.InScene(home.gameObject.scene).Single(target => target.StorySceneId == "neighborhood-homeCat");
        Assert.That(homeCat.GetComponent<WorldStoryActors>(), Is.Null);
        Assert.That(homeCat.Participant, Is.InstanceOf<NpcInteraction>());
        Assert.That(homeCat.GetComponentsInChildren<NpcActor>(true).Length, Is.EqualTo(2));
        Assert.That(homeCat.GetComponent<WorldDialogueSpeakers>().Resolve("boobi"), Is.Not.Null);
        Assert.That(homeCat.GetComponent<WorldDialogueSpeakers>().Resolve("chris"), Is.Not.Null);
        var content = root.Container.Resolve<IStoryContentSource>();
        Assert.That(content.GetCutsceneSteps("johnny").Length, Is.GreaterThan(1));
        done = false;
        progression.EnterAsync(GameProgressionDestination.OutsideFromHome).ContinueWith(Complete).Forget();
        until = Time.realtimeSinceStartup + 30;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done && result.Succeeded, Is.True, result.Message);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("OutSide"));
        foreach (var target in WorldInteractionTarget.InScene(SceneManager.GetActiveScene()))
            if (OutsideStorySceneRules.IsNativeEntry(target.StorySceneId))
            {
                Assert.That(target.Participant, Is.TypeOf<NpcInteraction>(), target.StorySceneId);
                Assert.That(target.GetComponent<WorldStoryActors>(), Is.Null, target.StorySceneId);
                Assert.That(target.GetComponentsInChildren<NpcActor>(true), Is.Not.Empty, target.StorySceneId);
                Assert.That(target.GetComponent<WorldDialogueSpeakers>(), Is.Not.Null, target.StorySceneId);
            }
        done = false;
        progression.EnterAsync(GameProgressionDestination.Bar).ContinueWith(Complete).Forget();
        until = Time.realtimeSinceStartup + 30;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done && result.Succeeded, Is.True, result.Message);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Play"));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HomeDoorAllowsRoundTripsInBothCommutePhases()
    {
        setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return VerifyHomeDoorRoundTrips();
    }

    IEnumerator VerifyHomeDoorRoundTrips()
    {
        yield return WaitFor(() => NewDataLoadManager.IsLoaded, "CSV did not load.");
        var root = Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
            .First(scope => scope.Container != null);
        var progression = root.Container.Resolve<GameProgressionService>();
        var conditions = root.Container.Resolve<IConditionUtil>();
        done = false;
        progression.RestoreHomeAsync(0, () => conditions.ApplyRequired("flag.opening_seen = true"), NoOp)
            .ContinueWith(Complete).Forget();
        yield return WaitFor(() => done, "Home did not load.");
        Assert.That(result.Succeeded, Is.True, result.Message);

        var state = GameStateManager.Instance;
        foreach (var flow in new[] { EGameFlow.CommuteOut, EGameFlow.CommuteIn })
        {
            state.GameFlow = flow;
            Object.FindFirstObjectByType<WorldInteractionSceneContext>().RefreshContents();
            yield return UseDoor("home_exit_door", "OutSide", flow);
            var context = Object.FindFirstObjectByType<WorldInteractionSceneContext>();
            var homeSpawn = GameObject.Find("HomeSpawn");
            Assert.That(homeSpawn, Is.Not.Null);
            Assert.That(context.Player.transform.position.x, Is.EqualTo(homeSpawn.transform.position.x).Within(.05f));
            Assert.That(Object.FindFirstObjectByType<OutsideElevator>().IsTop, Is.True);
            Assert.That(state.ArrivingAtOutsideHomeDoor, Is.False);
            yield return UseDoor("street_home_door", "Home", flow);
            Assert.That(progression.IsReturnedHome, Is.EqualTo(flow == EGameFlow.CommuteOut));
        }
    }

    static IEnumerator UseDoor(string sourceId, string destination, EGameFlow flow)
    {
        var scene = SceneManager.GetActiveScene();
        var controller = WorldInteractionController.ForScene(scene);
        var target = WorldInteractionTarget.InScene(scene).Single(item => item.Content?.SourceId == sourceId);
        var context = Object.FindFirstObjectByType<WorldInteractionSceneContext>();
        Assert.That(controller.TryResolve(target, out _), Is.True, sourceId + " / " + flow);
        context.Player.transform.position = target.transform.position;
        var storyResult = default(StoryExecutionResult);
        bool finished = false;
        controller.PlayAsync(target).ContinueWith(value => { storyResult = value; finished = true; }).Forget();
        yield return WaitFor(() => finished, "The door transition did not complete.");
        // 이동 후에는 출발 씬의 파괴 토큰도 취소되므로 도착 씬의 준비 상태를 확인한다.
        Assert.That(storyResult.Status, Is.Not.EqualTo(StoryExecutionStatus.Failed), storyResult.Error);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(destination));
        Assert.That(Object.FindFirstObjectByType<WorldInteractionSceneContext>().IsReady, Is.True);
        Assert.That(GameStateManager.Instance.CurrentDay, Is.Zero);
        Assert.That(GameStateManager.Instance.GameFlow, Is.EqualTo(flow));
    }

    static IEnumerator WaitFor(System.Func<bool> completed, string message)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!completed() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(completed(), Is.True, message);
    }
}
