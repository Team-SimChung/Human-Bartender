using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public sealed class OutsideStoryCutsceneLifetimeTests
{
    SceneSetup[] setup;
    bool done;
    Exception failure;

    async UniTask Observe(WorldInteractionCutscenePlayer player, StudioSequence sequence, CancellationToken token)
    {
        done = false;
        failure = null;
        try { await player.PlayAsync(sequence, token); }
        catch (Exception error) { failure = error; }
        finally { done = true; }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (setup != null && setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    // EnterPlayMode 도메인 재로드를 건너는 iterator에는 캡처된 로컬을 두지 않는다.
    static void AssertConcurrentPlaybackRejected(WorldInteractionCutscenePlayer player, StudioSequence sequence)
        => Assert.Throws<InvalidOperationException>(() => player.PlayAsync(sequence, CancellationToken.None).GetAwaiter().GetResult());

    [UnityTest]
    public IEnumerator ActualStudioPlaybackRestoresGameplayOnCancelDisableAndReplay()
    {
        setup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var sequence = ScriptableObject.CreateInstance<StudioSequence>();
        sequence.Id = "outside-lifetime-test";
        sequence.Timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        sequence.Timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        sequence.Timeline.fixedDuration = 2;
        var track = sequence.Timeline.CreateTrack<StudioTrack>();
        var clip = track.CreateClip<StudioClip>();
        clip.start = 0; clip.duration = 2;
        ((StudioClip)clip.asset).Kind = StudioKind.Effect;
        ((StudioClip)clip.asset).Effect = StudioEffect.Flash;
        var host = new GameObject("Native outside cutscene lifetime test");
        var player = host.AddComponent<WorldInteractionCutscenePlayer>();
        var state = GameStateManager.Instance;
        var oldState = state.CurrentGameState;
        float oldScale = Time.timeScale;
        bool oldPause = AudioListener.pause;
        using var cancellation = new CancellationTokenSource();
        Observe(player, sequence, cancellation.Token).Forget();
        Assert.That(player.IsPlaying, Is.True);
        Assert.That(player.ActivePlayer.HandleKeyboardInput, Is.False, "The interaction session owns input.");
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(state.CurrentGameState, Is.EqualTo(GameState.Effect));
        Assert.That(AudioListener.pause, Is.True);
        AssertConcurrentPlaybackRejected(player, sequence);
        cancellation.Cancel();
        float until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(player.IsPlaying, Is.False);
        Assert.That(player.ActivePlayer, Is.Null);
        Assert.That(Time.timeScale, Is.EqualTo(oldScale));
        Assert.That(AudioListener.pause, Is.EqualTo(oldPause));
        Assert.That(state.CurrentGameState, Is.EqualTo(oldState));

        Observe(player, sequence, CancellationToken.None).Forget();
        host.SetActive(false);
        until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(player.IsPlaying, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(oldScale));
        Assert.That(AudioListener.pause, Is.EqualTo(oldPause));

        host.SetActive(true);
        sequence.Timeline.fixedDuration = .1;
        clip.duration = .1;
        Observe(player, sequence, CancellationToken.None).Forget();
        until = Time.realtimeSinceStartup + 5;
        while (!done && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(done, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(player.IsPlaying, Is.False);
        Assert.That(state.CurrentGameState, Is.EqualTo(oldState));
        Assert.That(Time.timeScale, Is.EqualTo(oldScale));
        Assert.That(AudioListener.pause, Is.EqualTo(oldPause));
        Object.Destroy(host);
        Object.Destroy(sequence.Timeline);
        Object.Destroy(sequence);
        yield return new ExitPlayMode();
    }
}
