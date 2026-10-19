using System;
using System.Collections;
using UnityEngine.TestTools;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldInteractionMigrationTests
{
    [TestCase("OutSide"), TestCase("Home")]
    public void SceneHasOneInputAndPresentationPathAndKeepsCsvEntrypoints(string name)
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/" + name + ".unity", OpenSceneMode.Additive);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Assert.IsEmpty(components.OfType<InteractiveEntity>());
            Assert.IsEmpty(components.OfType<InteractiveEntityManager>());
            Assert.IsEmpty(components.OfType<InteractionDetector>());
            Assert.IsEmpty(components.OfType<OutsideStoryFlow>());
            Assert.IsEmpty(components.OfType<OutsideDialoguePresenter>());
            Assert.IsEmpty(components.OfType<UIInteractableButton>());
            Assert.IsEmpty(components.OfType<OustideTimelineManager>());
            Assert.AreEqual(1, components.OfType<WorldInteractionController>().Count());
            Assert.AreEqual(1, components.OfType<WorldInteractionView>().Count());
            var context = components.OfType<WorldInteractionSceneContext>().Single();
            Assert.IsNotNull(context.Player);
            Assert.IsNotNull(context.Runner);
            var targets = components.OfType<WorldInteractionTarget>().ToArray();
            Assert.AreEqual(targets.Length, targets.Select(target => target.RegistryKey).Distinct().Count());
            var data = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
                .Read<NewStreetData>(name == "Home" ? "script/home" : "script/street");
            foreach (var story in data.Scenes)
                if (story.RuntimeScene == name && story.Radius > 0 &&
                    (story.StartMode == ENewSceneTrigger.Interact || story.StartMode == ENewSceneTrigger.Auto))
                    Assert.AreEqual(1, targets.Count(target => target.StorySceneId == story.Id), story.Id);
            var sourceIds = components.OfType<WorldInteractionContent>().Select(content => content.SourceId).ToArray();
            Assert.AreEqual(sourceIds.Length, sourceIds.Distinct().Count());
            foreach (var door in components.OfType<WorldEntranceAction>())
                Assert.AreSame(door, door.GetComponent<WorldInteractionTarget>().Action);
            if (name == "Home")
            {
                CollectionAssert.IsSubsetOf(new[] { "home_sofa", "home_exit_door" }, sourceIds);
                var sofa = components.OfType<HomeSofaAction>().Single();
                Assert.AreSame(sofa, sofa.GetComponent<WorldInteractionTarget>().Action);
            }
            else CollectionAssert.IsSubsetOf(new[] { "street_home_door", "street_bar_door", "elevator_radio" }, sourceIds);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest, Explicit("외부에서 Home까지 실제 이동하며 취침·취소·자동 이벤트를 확인함.")]
    public IEnumerator MigratedHomeAndStreetRuntime()
    {
        EditorSceneManager.OpenScene(WorldInteractionCutsceneTests.OutsidePath);
        yield return new EnterPlayMode();
        yield return WorldInteractionCutsceneTests.ExerciseAsync().ToCoroutine();
        yield return WorldInteractionMigrationRuntime.ExerciseAsync().ToCoroutine();
        yield return new ExitPlayMode();
    }

    private sealed class Progression : IGameProgressionService
    {
        public GameProgressionDestination Destination;
        public GameProgressionOutcome Outcome = GameProgressionOutcome.Succeeded;
        public UniTask<GameProgressionResult> EnterAsync(GameProgressionDestination destination, CancellationToken cancellationToken = default)
        { Destination = destination; return UniTask.FromResult(new GameProgressionResult(Outcome, "test")); }
        public UniTask<GameProgressionResult> SleepAsync(Action refreshConditions, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public UniTask<GameProgressionResult> CompleteBarAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public UniTask<GameProgressionResult> WaitForBarEntryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [TestCase("enter_bar", GameProgressionDestination.Bar)]
    [TestCase("enter_home", GameProgressionDestination.Home)]
    [TestCase("exit_home", GameProgressionDestination.OutsideFromHome)]
    public void EntranceDispatchesCsvDestinationThroughProgression(string actionRef, GameProgressionDestination expected)
    {
        var host = new GameObject("entrance test");
        var definitions = ScriptableObject.CreateInstance<NewInteractPointDataSO>();
        var scripts = ScriptableObject.CreateInstance<NewStreetDataSO>();
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var context = host.AddComponent<WorldInteractionSceneContext>();
            var action = host.AddComponent<WorldEntranceAction>();
            var content = host.GetComponent<WorldInteractionContent>();
            definitions.interactPointData = new[] { new NewInteractPointData
            { Id = "door", SourceId = "door", Phase = EGameFlow.Both, ActionType = EActionType.Transition,
                ActionRef = actionRef, ActivationMode = EActivationMode.Interact } };
            Set(context, "interactPointData", definitions);
            Set(context, "streetData", scripts);
            Set(context, "conditions", new ConditionUtil(GameStateManager.Instance, wallet, wallet));
            Set(content, "sourceId", "door");
            content.Initialize(context);
            content.Refresh();
            var progression = new Progression();
            Set(action, "content", content);
            Set(action, "progression", progression);
            Assert.IsTrue(action.CanExecute);
            action.ExecuteAsync(default, default).GetAwaiter().GetResult();
            Assert.AreEqual(expected, progression.Destination);
            progression.Outcome = GameProgressionOutcome.Rejected;
            Assert.Throws<InvalidOperationException>(() => action.ExecuteAsync(default, default).GetAwaiter().GetResult());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(definitions);
            UnityEngine.Object.DestroyImmediate(scripts);
            UnityEngine.Object.DestroyImmediate(wallet);
        }
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
