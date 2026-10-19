using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldInteractionRefactorTests
{
    [Test]
    public void DisabledTargetKeepsOwnershipUntilItIsDestroyed()
    {
        var host = new GameObject("ownership test");
        var child = new GameObject("target");
        child.transform.SetParent(host.transform);
        try
        {
            var controller = host.AddComponent<WorldInteractionController>();
            var target = child.AddComponent<WorldInteractionTarget>();
            var settings = new SerializedObject(target);
            settings.FindProperty("storySceneId").stringValue = "refactor_test";
            settings.ApplyModifiedPropertiesWithoutUndo();
            WorldInteractionTarget.RegisterScene(host.scene);
            Assert.IsTrue(controller.OwnsStory("refactor_test"));
            child.SetActive(false);
            Assert.IsTrue(controller.OwnsStory("refactor_test"));
            Object.DestroyImmediate(child);
            Assert.IsFalse(controller.OwnsStory("refactor_test"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(0, EGameFlow.CommuteOut, 500, false, true)]
    [TestCase(1, EGameFlow.CommuteOut, 500, false, false)]
    [TestCase(0, EGameFlow.CommuteIn, 500, false, false)]
    [TestCase(0, EGameFlow.CommuteOut, 499, false, false)]
    [TestCase(0, EGameFlow.CommuteOut, 500, true, false)]
    public void SceneRulesCombineDayRouteConditionAndCompletion(int day, EGameFlow flow, int money, bool completed, bool expected)
    {
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            wallet.SetMoney(money);
            wallet.AddFlag("refactor_finished", completed);
            var conditions = new ConditionUtil(GameStateManager.Instance, wallet, wallet);
            var scene = new NewSceneData
            {
                RuntimeScene = "OutSide", Day = 0, Route = "out", When = "money >= 500", CompletionFlag = "refactor_finished"
            };
            Assert.AreEqual(expected, WorldInteractionSceneRules.IsEligible(scene, "OutSide", day, flow, conditions));
            Assert.IsFalse(WorldInteractionSceneRules.IsEligible(scene, "Home", day, flow, conditions));
        }
        finally { Object.DestroyImmediate(wallet); }
    }

    [TestCase(0, true), TestCase(1, false)]
    public void ActualRadioCsvCanSpawnOnlyOnItsConfiguredDay(int day, bool expected)
    {
        int previousDay = GameStateManager.Instance.CurrentDay;
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            GameStateManager.Instance.CurrentDay = day;
            var conditions = new ConditionUtil(GameStateManager.Instance, wallet, wallet);
            var rows = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
                .Read<NewInteractPointData[]>("interact_points_outside");
            Assert.IsTrue(rows.Any(row => row.SourceId == "elevator_radio"));
            var selected = WorldInteractionContentRules.SelectDefinition(rows, "elevator_radio", EGameFlow.CommuteOut, conditions);
            Assert.AreEqual(expected, selected.HasValue);
        }
        finally
        {
            GameStateManager.Instance.CurrentDay = previousDay;
            Object.DestroyImmediate(wallet);
        }
    }

    [TestCase(StoryExecutionStatus.Completed, "second")]
    [TestCase(StoryExecutionStatus.Cancelled, "first")]
    [TestCase(StoryExecutionStatus.Failed, "first")]
    public void RadioCompletionRebindsOnceDialogueWithoutConsumingCancelledPlayback(StoryExecutionStatus status, string expected)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("radio refresh test");
        SceneManager.MoveGameObjectToScene(host, scene);
        var definitions = ScriptableObject.CreateInstance<NewInteractPointDataSO>();
        var scripts = ScriptableObject.CreateInstance<NewStreetDataSO>();
        
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var manager = host.AddComponent<WorldInteractionSceneContext>();
            var radioObject = new GameObject("radio");
            radioObject.transform.SetParent(host.transform);
            var radio = radioObject.AddComponent<OutsideElevatorRadio>();
            var content = radioObject.AddComponent<WorldInteractionContent>();
            definitions.interactPointData = new[]
            {
                new NewInteractPointData
                {
                    Id = "test", SourceId = "radio", SpotId = "spot", Phase = EGameFlow.Both, ActionType = EActionType.Dialogue,
                    DialogueFlows = new[]
                    {
                        new NewInteractDialogueFlowData { SceneId = "first", FlowSeq = 0, PlayType = EPlayType.Once },
                        new NewInteractDialogueFlowData { SceneId = "second", FlowSeq = 1, PlayType = EPlayType.Repeat }
                    }
                }
            };
            scripts.newStreetData = new NewStreetData { Scenes = new[]
            {
                new NewSceneData { Id = "first", Steps = new[] { new Step { Type = "say", Text = new Texts { Ko = "첫 방송" } } } },
                new NewSceneData { Id = "second", Steps = new[] { new Step { Type = "say", Text = new Texts { Ko = "다음 방송" } } } }
            } };
            Set(manager, "interactPointData", definitions);
            Set(manager, "streetData", scripts);
            Set(manager, "conditions", new ConditionUtil(GameStateManager.Instance, wallet, wallet));
            Set(content, "sourceId", "radio");
            Set(radio, "content", content);
            content.Initialize(manager);
            content.Refresh();
            Assert.AreEqual("first", radio.DialogueSceneId);
            radio.CompletePlayback("first", new StoryExecutionResult(status));
            Assert.AreEqual(expected, radio.DialogueSceneId);
            Assert.IsTrue(radio.IsAvailable);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(definitions);
            Object.DestroyImmediate(scripts);
            Object.DestroyImmediate(wallet);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
}
