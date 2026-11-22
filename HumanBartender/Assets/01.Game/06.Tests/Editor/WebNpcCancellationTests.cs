using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WebNpcCancellationTests
{
    // A canceled global fade still owns its coordinator until its finally has completed.
    private sealed class DeferredFade : ISceneFadeService
    {
        private readonly UniTaskCompletionSource releaseCleanup = new();
        private bool heldFadeStarted;
        public bool Busy, CleanupWaiting;
        public float Alpha;
        public int Overlaps, Recoveries;

        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.That(Busy, Is.False);
            Alpha = 1;
            return UniTask.CompletedTask;
        }

        public async UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            if (Busy) { Overlaps++; throw new InvalidOperationException("The previous fade still owns the screen."); }
            Busy = true;
            if (!cancellationToken.CanBeCanceled) Recoveries++;
            bool hold = cancellationToken.CanBeCanceled && !heldFadeStarted;
            if (hold) heldFadeStarted = true;
            try
            {
                if (hold)
                {
                    while (true) await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
                Alpha = 0;
            }
            catch (OperationCanceledException) { Alpha = 1; throw; }
            finally
            {
                if (hold)
                {
                    CleanupWaiting = true;
                    await releaseCleanup.Task;
                    CleanupWaiting = false;
                }
                Busy = false;
            }
        }
        public void ReleaseCleanup() => releaseCleanup.TrySetResult();
    }

    private SceneSetup[] originalScenes;
    private PlayerDataSO playerData;
    private bool finished;
    private Exception failure;

    private void CloneHomeCat()
    {
        originalScenes = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var home = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Home.unity", OpenSceneMode.Additive);
        try
        {
            var source = home.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(value => value.StorySceneId == "neighborhood-homeCat");
            var clone = Object.Instantiate(source.gameObject);
            clone.name = "Chris cancellation scene";
            clone.SetActive(true);
            SceneManager.MoveGameObjectToScene(clone, isolated);
            using var settings = new SerializedObject(clone.GetComponent<NpcInteraction>());
            settings.FindProperty("useStoryPresence").boolValue = false;
            settings.FindProperty("minimumDay").intValue = 0;
            settings.FindProperty("presenceCondition").stringValue = "";
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { EditorSceneManager.CloseScene(home, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private async UniTask Observe(UniTask operation)
    {
        finished = false; failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private static IEnumerator Until(Func<bool> predicate, string message)
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, message);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (playerData != null) Object.DestroyImmediate(playerData);
        if (originalScenes != null && originalScenes.Any(value => value.isLoaded && value.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator CancelingChrisParallelPanWaitsForFadeCleanupBeforeRecoveryAndAllowsRetry()
    {
        CloneHomeCat();
        yield return new EnterPlayMode();
        yield return VerifyChrisCancellation();
    }

    private IEnumerator VerifyChrisCancellation()
    {
        Time.timeScale = 1;
        var interaction = GameObject.Find("Chris cancellation scene").GetComponent<NpcInteraction>();
        using (var settings = new SerializedObject(interaction))
        {
            var steps = NpcSequenceTestSettings.WorldActions(settings, OutsideStoryWorldAction.ChrisEnter);
            Assert.That(NpcSequenceTestSettings.Kinds(steps), Does.Contain(NpcActionKind.CameraPan),
                "This regression must exercise the authored Chris camera sequence.");
            Assert.That(NpcSequenceTestSettings.Action(settings, NpcActionKind.CameraPan)
                .FindPropertyRelative("fadeInTogether").floatValue, Is.GreaterThan(0));
        }
        var player = new GameObject("Chris cancellation Luna").transform;
        player.position = new Vector3(5, -.7f, 0);
        var origin = player.position;
        var rig = NpcSequenceTestSettings.CreateStagingCamera(player);
        var staging = interaction.GetComponent<NpcSceneStaging>();
        Assert.That(staging, Is.Not.Null);
        playerData = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, playerData, playerData);
        conditions.ApplyRequired("flag.boobi_adopted = true; flag.boobi_rest_seen = false");
        var fades = new DeferredFade();
        interaction.Initialize(conditions, player, new NewSceneData { RuntimeScene = interaction.gameObject.scene.name, Route = "both" }, fades, null);
        using var cancel = new CancellationTokenSource();
        interaction.BeginInteraction();
        try
        {
            Observe(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.ChrisEnter, cancel.Token)).Forget();
            yield return Until(() => fades.Busy && staging.HasCameraOverride, "Chris never reached the parallel pan/fade.");
            cancel.Cancel();
            yield return Until(() => fades.CleanupWaiting, "The canceled fade did not enter cleanup.");
            Assert.That(finished, Is.False, "The NPC sequence must drain the canceled fade before it returns.");
            Assert.That(fades.Recoveries, Is.Zero, "Recovery cannot acquire the fade coordinator before cleanup releases it.");
            fades.ReleaseCleanup();
            yield return Until(() => finished, "The canceled Chris sequence did not finish.");
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(fades.Overlaps, Is.Zero);
            Assert.That(fades.Recoveries, Is.EqualTo(1));
            Assert.That(fades.Alpha, Is.Zero, "Cancellation must leave a visible screen.");
            Assert.That(fades.Busy, Is.False);
            Assert.That(conditions.CheckRequired("flag.boobi_rest_seen"), Is.False);
        }
        finally
        {
            fades.ReleaseCleanup();
            interaction.EndInteraction();
        }
        Assert.That(player.position, Is.EqualTo(origin));
        Assert.That(staging.HasCameraOverride, Is.False);
        Assert.That(interaction.CanInteract, Is.True);
        using (var frame = rig.BeginFrameOverride()) Assert.That(frame.IsValid, Is.True);

        interaction.BeginInteraction();
        try
        {
            Observe(interaction.PlayWorldActionAsync(OutsideStoryWorldAction.ChrisEnter, CancellationToken.None)).Forget();
            yield return Until(() => finished, "Chris could not be retried after cancellation.");
            Assert.That(failure, Is.Null);
            Observe(interaction.CompleteInteractionAsync(CancellationToken.None)).Forget();
            yield return Until(() => finished, "The successful Chris camera did not restore tracking.");
            Assert.That(failure, Is.Null);
        }
        finally { interaction.EndInteraction(); }
        Assert.That(player.position.x, Is.EqualTo(2.2f).Within(.001f));
        Assert.That(fades.Alpha, Is.Zero);
        Assert.That(fades.Overlaps, Is.Zero);
        Assert.That(staging.HasCameraOverride, Is.False);
        Assert.That(interaction.CanInteract, Is.True);
    }
}
