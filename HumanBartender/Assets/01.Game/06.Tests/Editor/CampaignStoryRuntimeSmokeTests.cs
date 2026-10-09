using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

public sealed class CampaignStoryRuntimeSmokeTests
{
    [UnityTest]
    [Explicit("Run in isolation: enters the real Main/Home/OutSide/Play scenes.")]
    public IEnumerator NewGameStartsAtHomeAndReachesTheClickableBarTutorial()
    {
        DOTween.Clear(true);
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        yield return WaitFor(() => NewDataLoadManager.IsLoaded && SceneTransitionManager.Instance != null &&
            !SceneTransitionManager.Instance.IsBusy, "data loading and title fade");
        UnityEngine.Object.FindFirstObjectByType<MainManager>().TempStart();
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "Home" &&
            !SceneTransitionManager.Instance.IsBusy &&
            UnityEngine.Object.FindFirstObjectByType<OutsideStoryFlow>() is { IsReady: true, IsRunning: false }, "new game Home");
        Assert.AreEqual(0, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.CommuteIn, GameStateManager.Instance.GameFlow);
        var scope = LifetimeScope.Find<ProjectLifetimeScope>();
        var progression = scope.Container.Resolve<GameProgressionService>();
        var outside = progression.EnterAsync(GameProgressionDestination.OutsideFromHome);
        yield return AssertArrival(outside).ToCoroutine();
        yield return WaitFor(() => UnityEngine.Object.FindFirstObjectByType<OutsideStoryFlow>() is { IsReady: true }, "street story UI");
        Assert.AreEqual("OutSide", SceneManager.GetActiveScene().name);
        var bar = progression.EnterAsync(GameProgressionDestination.Bar);
        yield return AssertArrival(bar).ToCoroutine();
        yield return WaitFor(() => UnityEngine.Object.FindFirstObjectByType<StoryScriptRunner>() is { IsWaitingForAdvance: true }, "first clickable bar line");
        var flow = UnityEngine.Object.FindFirstObjectByType<StoryFlow>(FindObjectsInactive.Include);
        var runner = UnityEngine.Object.FindFirstObjectByType<StoryScriptRunner>();
        Assert.IsNotNull(flow, "Play scene StoryFlow");
        Assert.IsNotNull(runner, "Play scene StoryScriptRunner");
        Assert.AreEqual("dlg_day0_notion_bar_3", runner.CurrentDialogueId);
        Assert.IsTrue(flow.TryAdvance());
        yield return WaitFor(() => UnityEngine.Object.FindFirstObjectByType<StoryScriptRunner>() is
            { IsWaitingForAdvance: true, CurrentDialogueId: "dlg_day0_notion_bar_4" }, "next clickable bar line");
        Assert.AreEqual("Play", SceneManager.GetActiveScene().name);
        yield return new ExitPlayMode();
    }

    static IEnumerator WaitFor(Func<bool> ready, string stage)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), stage + " timed out");
    }

    static async UniTask AssertArrival(UniTask<GameProgressionResult> operation)
    {
        var result = await operation;
        Assert.IsTrue(result.Succeeded, result.Message);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        DOTween.Clear(true);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
