using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CsvMemoryProgressionTests
{
    SceneSetup[] setup;
    bool done;
    StoryExecutionResult result;
    WorldInteractionCutscenePlayer player;
    readonly Step[] steps = { new() { Type = "say", Actor = "luna", Text = new Texts { Ko = "기억을 관찰한다." }, CutsceneAfter = "johnny-memory" } };

    sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() { }
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken ct) => UniTask.CompletedTask;
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> picked) => picked(options[0]);
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    async UniTask<bool> PlayMemory(string id, string key, CancellationToken token)
    {
        if (!CsvStoryCutsceneProjection.TryGetSource(id, key, out var source)) return false;
        using var projection = new CsvStoryCutsceneProjection(id, new CsvStoryTestContent().GetCutsceneSteps(source), ELanguage.Ko);
        await player.PlayAsync(projection.Sequence, token);
        return true;
    }
    async UniTask Observe(DialogueRunner runner, CancellationToken token)
    {
        done = false;
        result = await runner.PlayOutsideAsync(steps, token, PlayMemory);
        done = true;
    }
    static DialogueRunner CreateRunner(GameObject host, IConditionUtil conditions)
    {
        var runner = host.AddComponent<DialogueRunner>();
        // 이 테스트는 진행 효과와 실제 Studio 재생만 사용한다. 다른 서비스는 호출하지 않는다.
        typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, conditions);
        runner.Bind(new Presenter());
        return runner;
    }
    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (setup != null && setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator ObservationRequiresActualCsvMemoryCompletionAndCancellationCanRetry()
    {
        setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var host = new GameObject("CSV memory progression test");
        player = host.AddComponent<WorldInteractionCutscenePlayer>();
        var data = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, data, data);
        conditions.ApplyRequired("flag.gang_wild_dog_received = true; flag.shiba_bought_bitters = true");
        var runner = CreateRunner(host, conditions);
        Assert.That(CampaignStoryRules.CanPrepareJohnnyDrink(conditions), Is.False);
        using var cancellation = new CancellationTokenSource();
        Observe(runner, cancellation.Token).Forget();
        yield return null;
        Assert.That(player.IsPlaying, Is.True);
        cancellation.Cancel();
        float until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(result.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
        Assert.That(data.CheckFlag("flag.campaign_observation"), Is.False);
        Assert.That(player.IsPlaying, Is.False);

        runner.Bind(new Presenter());
        Observe(runner, CancellationToken.None).Forget();
        until = Time.realtimeSinceStartup + 120;
        int inputs = 0;
        while (!done && Time.realtimeSinceStartup < until)
        {
            Assert.That(data.CheckFlag("flag.campaign_observation"), Is.False);
            if (player.ActivePlayer != null && player.ActivePlayer.IsWaiting) { player.Continue(); inputs++; }
            yield return null;
        }
        Assert.That(done, Is.True, "CSV memory playback timed out");
        Assert.That(inputs, Is.GreaterThanOrEqualTo(39));
        Assert.That(result.Completed, Is.True, result.Error);
        Assert.That(CampaignStoryRules.CanPrepareJohnnyDrink(conditions), Is.True);
        Assert.That(player.IsPlaying, Is.False);
        Object.Destroy(data);
        Object.Destroy(host);
        yield return new ExitPlayMode();
    }
}
