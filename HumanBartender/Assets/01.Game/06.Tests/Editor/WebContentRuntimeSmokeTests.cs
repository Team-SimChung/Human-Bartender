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
}
