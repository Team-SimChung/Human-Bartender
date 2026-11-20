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

public sealed class NpcInteractionSequenceTests
{
    private const string ActorName = "Common sequence data test";
    private SceneSetup[] sceneSetup;
    private PlayerDataSO playerData;
    private bool finished;
    private Exception failure;

    private sealed class RecoveryFade : ISceneFadeService
    {
        private readonly UniTaskCompletionSource release = new();
        public bool Dark { get; private set; }
        public bool Recovering { get; private set; }
        public bool RecoveryCanBeCancelled { get; private set; }

        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dark = true;
            return UniTask.CompletedTask;
        }

        public async UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            Recovering = true;
            RecoveryCanBeCancelled = cancellationToken.CanBeCanceled;
            await release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            Dark = false;
            Recovering = false;
        }

        public void Release() => release.TrySetResult();
    }

    private void PrepareActor()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outside = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/OutSide.unity", OpenSceneMode.Additive);
        try
        {
            var source = outside.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(target => target.StorySceneId == "day1-corridor-thug");
            Assert.That(source.GetComponent<NpcInteraction>(), Is.Not.Null);
            var clone = Object.Instantiate(source.gameObject);
            clone.name = ActorName;
            SceneManager.MoveGameObjectToScene(clone, isolated);
            using var data = new SerializedObject(clone.GetComponent<NpcInteraction>());
            data.FindProperty("useStoryPresence").boolValue = false;
            data.FindProperty("minimumDay").intValue = 0;
            data.FindProperty("presenceCondition").stringValue = "";
            data.FindProperty("committedCondition").stringValue = "";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { EditorSceneManager.CloseScene(outside, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private static void WriteGreeting(NpcInteraction interaction, bool waitFirst, Vector2 offset)
    {
        using var data = new SerializedObject(interaction);
        var sequences = data.FindProperty("sequences");
        sequences.arraySize = 1;
        var sequence = sequences.GetArrayElementAtIndex(0);
        sequence.FindPropertyRelative("id").stringValue = "greet";
        sequence.FindPropertyRelative("trigger").enumValueIndex = (int)NpcSequenceTrigger.Named;
        sequence.FindPropertyRelative("requiredCondition").stringValue = "";
        var actions = sequence.FindPropertyRelative("actions");
        actions.arraySize = 4;
        var kinds = waitFirst
            ? new[] { NpcActionKind.Wait, NpcActionKind.MoveBy, NpcActionKind.Face, NpcActionKind.Animation }
            : new[] { NpcActionKind.MoveBy, NpcActionKind.Wait, NpcActionKind.Face, NpcActionKind.Animation };
        for (int i = 0; i < kinds.Length; i++)
        {
            var step = actions.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("kind").enumValueIndex = (int)kinds[i];
            step.FindPropertyRelative("offset").vector2Value = offset;
            step.FindPropertyRelative("distance").floatValue = .65f;
            step.FindPropertyRelative("duration").floatValue = kinds[i] == NpcActionKind.Wait ? .4f : .2f;
            step.FindPropertyRelative("target").objectReferenceValue = null;
            step.FindPropertyRelative("stateName").stringValue = "Punch";
            step.FindPropertyRelative("returnState").stringValue = "Idle";
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteCompletionFade(NpcInteraction interaction)
    {
        using var data = new SerializedObject(interaction);
        var sequences = data.FindProperty("sequences");
        sequences.arraySize = 1;
        var sequence = sequences.GetArrayElementAtIndex(0);
        sequence.FindPropertyRelative("id").stringValue = "complete-with-fade";
        sequence.FindPropertyRelative("trigger").enumValueIndex = (int)NpcSequenceTrigger.DialogueCompleted;
        sequence.FindPropertyRelative("requiredCondition").stringValue = "";
        var actions = sequence.FindPropertyRelative("actions");
        actions.arraySize = 1;
        var step = actions.GetArrayElementAtIndex(0);
        step.FindPropertyRelative("kind").enumValueIndex = (int)NpcActionKind.ScreenFadeOut;
        step.FindPropertyRelative("duration").floatValue = .2f;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private async UniTask Observe(UniTask operation)
    {
        finished = false;
        failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason)
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private static IEnumerator WaitGameSeconds(float seconds)
    {
        float deadline = Time.time + seconds;
        yield return WaitUntil(() => Time.time >= deadline, "Game time did not advance.");
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (playerData != null) Object.DestroyImmediate(playerData);
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator NamedSequenceUsesAuthoredOrderArgumentsAndAnimatorWithoutAnEventSpecificClass()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyNamedSequenceData();
    }

    [UnityTest]
    public IEnumerator CancellationDuringCompletionFadeRecoveryCannotCommitCompletion()
    {
        PrepareActor();
        yield return new EnterPlayMode();
        yield return VerifyCompletionRecoveryCancellation();
    }

    private IEnumerator VerifyCompletionRecoveryCancellation()
    {
        Time.timeScale = 1;
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var interaction = GameObject.Find(ActorName).GetComponent<NpcInteraction>();
        var luna = new GameObject("Completion recovery target").transform;
        playerData = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, playerData, playerData);
        WriteCompletionFade(interaction);

        // Both controller cancellation and disabling/ending the participant can arrive during cleanup.
        foreach (bool endSession in new[] { false, true })
        {
            var fades = new RecoveryFade();
            interaction.Initialize(conditions, luna, new NewSceneData
            {
                Id = "completion-recovery", RuntimeScene = interaction.gameObject.scene.name,
                Route = "both", CompletionFlag = "completion_recovery_done"
            }, fades, null);
            using var cancellation = new CancellationTokenSource();
            async UniTask CompleteAndCommit()
            {
                await interaction.CompleteInteractionAsync(cancellation.Token);
                conditions.ApplyRequired("flag.completion_recovery_done = true");
            }

            interaction.BeginInteraction();
            try
            {
                Observe(CompleteAndCommit()).Forget();
                yield return WaitUntil(() => fades.Recovering, "The final fade-out did not start its recovery.");
                Assert.That(fades.Dark, Is.True);
                Assert.That(fades.RecoveryCanBeCancelled, Is.False, "Cleanup must restore the screen despite cancellation.");
                Assert.That(finished, Is.False);
                if (endSession) interaction.EndInteraction();
                else cancellation.Cancel();
                Assert.That(finished, Is.False, "Cancellation must still wait for the screen to recover.");

                fades.Release();
                yield return WaitUntil(() => finished, "Completion did not finish after screen recovery.");
                Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
                Assert.That(fades.Dark, Is.False);
                Assert.That(conditions.CheckRequired("flag.completion_recovery_done"), Is.False,
                    "Cancellation during recovery must not return success to the completion-flag writer.");
            }
            finally
            {
                fades.Release();
                interaction.EndInteraction();
            }
            Assert.That(interaction.CanInteract, Is.True, "The cancelled session must release the NPC for a retry.");
        }
    }

    private IEnumerator VerifyNamedSequenceData()
    {
        Time.timeScale = 1;
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var interaction = GameObject.Find(ActorName).GetComponent<NpcInteraction>();
        var actor = interaction.GetComponent<NpcActor>();
        var animator = actor.GetComponent<Animator>();
        var movement = actor.GetComponent<NpcMovement2D>();
        movement.Configure(20);
        var origin = new Vector3(2, 3, 4);
        actor.transform.position = origin;
        var luna = new GameObject("Greeting target").transform;
        luna.position = new Vector3(-10, 3, 0);
        playerData = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, playerData, playerData);
        WriteGreeting(interaction, false, new Vector2(2, 0));
        interaction.Initialize(conditions, luna,
            new NewSceneData { Id = "custom-greeting", RuntimeScene = interaction.gameObject.scene.name, Route = "both" }, null, null);

        interaction.BeginInteraction();
        Observe(interaction.PlaySequenceAsync("greet", CancellationToken.None)).Forget();
        yield return WaitUntil(() => actor.transform.position.x >= 4 && !actor.IsBusy,
            "Move-first data did not reach its destination before waiting.");
        Assert.That(actor.transform.position, Is.EqualTo(origin + Vector3.right * 2));
        Assert.That(finished, Is.False);
        yield return WaitGameSeconds(.1f);
        Assert.That(actor.transform.position, Is.EqualTo(origin + Vector3.right * 2));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Punch"), Is.False,
            "The authored wait must finish before the animation starts.");
        yield return WaitUntil(() => animator.GetCurrentAnimatorStateInfo(0).IsName("Punch"), "Named sequence did not play its Animator state.");
        Assert.That(movement.FacingRight, Is.False, "Face uses the current player target.");
        yield return WaitUntil(() => finished, "Named greeting did not finish.");
        Assert.That(failure, Is.Null);
        interaction.EndInteraction();
        Assert.That(actor.transform.position, Is.EqualTo(origin));

        // Editing only the list order and arguments changes both when and where the same NPC moves.
        WriteGreeting(interaction, true, new Vector2(-1, .5f));
        interaction.BeginInteraction();
        Observe(interaction.PlaySequenceAsync("greet", CancellationToken.None)).Forget();
        yield return WaitGameSeconds(.1f);
        Assert.That(actor.transform.position, Is.EqualTo(origin), "Wait-first data must delay movement.");
        Assert.That(finished, Is.False);
        yield return WaitUntil(() => finished, "Reordered greeting did not finish.");
        Assert.That(failure, Is.Null);
        Assert.That(actor.transform.position, Is.EqualTo(origin + new Vector3(-1, .5f, 0)));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        interaction.EndInteraction();
        Assert.That(actor.transform.position, Is.EqualTo(origin));

        interaction.BeginInteraction();
        Assert.Throws<InvalidOperationException>(() => interaction.PlaySequenceAsync("missing-greeting", CancellationToken.None).GetAwaiter().GetResult());
        Assert.That(actor.transform.position, Is.EqualTo(origin));
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(actor.IsVisible, Is.True);
        interaction.EndInteraction();
    }
}
