using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayPhaseTestOverrideTests
{
    sealed class Entry : IGameProgressionService
    {
        public GameProgressionOutcome Outcome;
        public UniTask<GameProgressionResult> WaitForBarEntryAsync(CancellationToken token = default)
            => UniTask.FromResult(new GameProgressionResult(Outcome));
        public UniTask<GameProgressionResult> CompleteBarAsync(CancellationToken token = default) => throw new NotSupportedException();
        public UniTask<GameProgressionResult> EnterAsync(GameProgressionDestination destination, CancellationToken token = default) => throw new NotSupportedException();
        public UniTask<GameProgressionResult> SleepAsync(Action refresh, CancellationToken token = default) => throw new NotSupportedException();
    }

    [TestCase(GameProgressionOutcome.Succeeded, 1)]
    [TestCase(GameProgressionOutcome.Rejected, 0)]
    [TestCase(GameProgressionOutcome.Failed, 1)]
    public void OnlyExplicitDirectSceneTestMayOverrideTheDay(GameProgressionOutcome entry, int expectedDay)
    {
        int previousDay = GameStateManager.Instance.CurrentDay;
        var completionField = typeof(NewDataLoadManager).GetField("loadCompletion", BindingFlags.NonPublic | BindingFlags.Static);
        object previousCompletion = completionField.GetValue(null);
        var canceledLoad = new UniTaskCompletionSource();
        canceledLoad.TrySetCanceled(); // Stop after entry, before either phase needs real scene/data dependencies.
        var actor = new GameObject("day regression");
        actor.SetActive(false);
        try
        {
            GameStateManager.Instance.CurrentDay = 1;
            completionField.SetValue(null, canceledLoad);
            var controller = actor.AddComponent<PlayPhaseController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("testDay").intValue = 0;
            serialized.FindProperty("tycoonFlow").objectReferenceValue = actor.AddComponent<TycoonFlow>();
            serialized.FindProperty("storyFlow").objectReferenceValue = actor.AddComponent<StoryFlow>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(PlayPhaseController).GetField("progression", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new Entry { Outcome = entry });

            // Explicitly exercise scene initialization even in EditMode; this caught the old unconditional reset.
            controller.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            Assert.That(GameStateManager.Instance.CurrentDay, Is.EqualTo(1), "scene initialization must preserve saved progress");
            var request = controller.RequestRunDay();
            Assert.That(request.Accepted, Is.True);
            request.Completion.GetAwaiter().GetResult();
            Assert.That(GameStateManager.Instance.CurrentDay, Is.EqualTo(expectedDay));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            completionField.SetValue(null, previousCompletion);
            GameStateManager.Instance.CurrentDay = previousDay;
        }
    }
}
