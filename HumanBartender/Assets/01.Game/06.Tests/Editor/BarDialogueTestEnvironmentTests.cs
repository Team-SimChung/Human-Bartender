using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public sealed class BarDialogueTestEnvironmentTests
{
    private PlayerDataSO player;
    private int previousDay;
    private EGameFlow previousFlow;

    [SetUp]
    public void Setup()
    {
        previousDay = GameStateManager.Instance.CurrentDay;
        previousFlow = GameStateManager.Instance.GameFlow;
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        player.SetMoney(1000);
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.Bar;
    }

    [TearDown]
    public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(player);
        GameStateManager.Instance.CurrentDay = previousDay;
        GameStateManager.Instance.GameFlow = previousFlow;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void LineCatalogIncludesEveryActualDialogueWithoutAmbiguousKeys(int day)
    {
        var script = BarDialogueTestCatalog.LoadScript(day);
        var lines = BarDialogueTestCatalog.LoadLines(day);
        Assert.That(lines, Is.Not.Empty);
        int count = script.Scenes.SelectMany(scene => scene.Steps).Count(step =>
            step.Type is ENewStepType.Say or ENewStepType.Order && !string.IsNullOrWhiteSpace(step.Text?.Ko));
        Assert.That(lines.Length, Is.EqualTo(count));
        Assert.That(lines.Select(line => line.Key).Distinct().Count(), Is.EqualTo(lines.Length));
        Assert.That(lines.All(line => line.Day == day && !string.IsNullOrWhiteSpace(line.Text)), Is.True);
        var scenario = HomeInteractionTestScenarios.Find(BarDialogueTestCatalog.Scenario(day).Id);
        Assert.That(scenario.RuntimeScene, Is.EqualTo("Play"));
        Assert.That(scenario.Flow, Is.EqualTo(EGameFlow.Bar));
        scenario.Apply(player);
        if (day == 3)
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
            Assert.That(CampaignStoryRules.CanPrepareJohnnyDrink(conditions), Is.True,
                "Day-three dialogue tests need the same ingredients and memory as a real arrival.");
        }
    }

    [Test]
    public void InGameQaCanvasStartsClosedAndHasAuthoredControls()
    {
        const string path = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
        var scope = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(scope, Is.Not.Null);
        var qa = scope.GetComponentInChildren<InGameQaPanel>(true);
        Assert.That(qa, Is.Not.Null);
        Assert.That(qa.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(qa.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(960, 540)));
        using var fields = new SerializedObject(qa);
        var root = (GameObject)fields.FindProperty("panelRoot").objectReferenceValue;
        Assert.That(root.activeSelf, Is.False);
        foreach (string name in new[] { "commuteIn", "commuteOut", "addMoney", "subtractMoney", "setFlag", "clearFlag", "forceChoices", "resetProgress" })
            Assert.That(fields.FindProperty(name).objectReferenceValue, Is.InstanceOf<Button>(), name);
        foreach (string name in new[] { "scriptSearch", "moneyAmount", "flagKey" })
        {
            var input = (TMP_InputField)fields.FindProperty(name).objectReferenceValue;
            Assert.That(input.textViewport, Is.Not.Null, name);
            Assert.That(input.textComponent, Is.Not.Null, name);
        }
        var buttons = fields.FindProperty("scriptButtons");
        var labels = fields.FindProperty("scriptLabels");
        Assert.That(buttons.arraySize, Is.GreaterThan(0).And.EqualTo(labels.arraySize));
        for (int i = 0; i < buttons.arraySize; i++)
        {
            var button = (Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
            var label = (TMP_Text)labels.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(label.transform.IsChildOf(button.transform), Is.True);
        }
    }

    [Test]
    public void StartingAtDialogueRestoresSeatsEffectsAndOpenOrderWithoutChangingSource()
    {
        var steps = new[]
        {
            Step(1, ENewStepType.Enter, "port", "L"),
            Step(2, ENewStepType.Say, "port", text: "건너뛸 대사"),
            Step(3, ENewStepType.Effect, effects: "money += 20; flag.test_prepared = true"),
            Step(4, ENewStepType.Enter, "shiba", "R"),
            Step(5, ENewStepType.Exit, "port"),
            Step(6, ENewStepType.Order, "shiba", "exact:gin_tonic", "건너뛸 주문 문구"),
            Step(7, ENewStepType.Craft, "shiba", "order"),
            Step(8, ENewStepType.Say, "shiba", text: "여기서 시작", when: "flag.test_choice"),
            Step(9, ENewStepType.Say, "luna", text: "다음 대사")
        };
        var source = Script(steps);
        var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        var plan = BarDialogueTestStartPlan.Build(source, "test_scene", 8, conditions, ENewGrade.Good, true);
        Assert.That(plan.Seats.Single().Actor, Is.EqualTo("shiba"));
        Assert.That(plan.Seats.Single().Arg, Is.EqualTo("R"));
        Assert.That(plan.OpenOrder?.Actor, Is.EqualTo("shiba"));
        Assert.That(plan.CraftWasOpened, Is.True);
        Assert.That(plan.ResumeScene.Steps.Select(step => step.Seq), Is.EqualTo(new[] { 8, 9 }));
        Assert.That(plan.ResumeScene.Steps[0].When, Is.Null);
        Assert.That(source.Scenes[0].Steps[7].When, Is.EqualTo("flag.test_choice"));
        Assert.That(source.Scenes[0].Steps.Length, Is.EqualTo(9));
        Assert.That(player.HasMoney(), Is.EqualTo(1020));
        Assert.That(player.CheckFlag("test_prepared"), Is.True);
        Assert.That(player.CheckFlag("test_choice"), Is.False, "Skipped choice outcomes must not be invented.");
    }

    [Test]
    public void SkippedServeUsesSelectedResultAndLeavesNoLiveOrder()
    {
        var source = Script(new[]
        {
            Step(1, ENewStepType.Enter, "port", "L"),
            Step(2, ENewStepType.Order, "port", "exact:bottle_beer", "주문"),
            Step(3, ENewStepType.Serve, "port"),
            Step(4, ENewStepType.Effect, when: "grade < good", effects: "flag.bad_drink = true"),
            Step(5, ENewStepType.Say, "port", text: "결과 대사")
        });
        var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        var plan = BarDialogueTestStartPlan.Build(source, "test_scene", 5, conditions, ENewGrade.Poor, false);
        Assert.That(plan.OpenOrder, Is.Null);
        Assert.That(plan.PreviousResult.FinalGrade, Is.EqualTo(ENewGrade.Poor));
        Assert.That(plan.PreviousResult.OrderMatch, Is.False);
        Assert.That(player.CheckFlag("bad_drink"), Is.True);
    }

    private static NewDialogueStepData Step(int seq, ENewStepType type, string actor = null, string arg = null,
        string text = null, string when = null, string effects = null) => new()
        { Seq = seq, Type = type, Actor = actor, Arg = arg, Text = text == null ? null : new LocalizedText { Ko = text }, When = when, Effects = effects };

    private static NewDayScriptBase Script(NewDialogueStepData[] steps) => new()
    {
        Scenes = new[] { new NewScriptSceneData { Id = "test_scene", Day = 1, Seq = 1, Phase = ENewScenePhase.Bar,
            Trigger = ENewSceneTrigger.Auto, Steps = steps } }
    };
}

public sealed class InGameQaPanelRuntimeTests
{
    [UnityTest]
    public IEnumerator ShortcutHandlerAndMoneyButtonsUseTheLivePlayer()
    {
        yield return new EnterPlayMode();
        var panel = UnityEngine.Object.FindFirstObjectByType<InGameQaPanel>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.isActiveAndEnabled, Is.True);
        var container = LifetimeScope.Find<ProjectLifetimeScope>().Container;
        var transitions = container.Resolve<ISceneTransitionService>();
        float deadline = Time.realtimeSinceStartup + 8f;
        while (transitions.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(transitions.IsBusy, Is.False, "Wait for the initial scene fade before sending QA input.");
        SceneManager.SetActiveScene(SceneManager.CreateScene("OutSide"));
        Assert.That(UIDisplayOptions.IsOptionOpen, Is.False);
        var player = container.Resolve<PlayerDataSO>();
        const BindingFlags privateFields = BindingFlags.Instance | BindingFlags.NonPublic;
        var injected = typeof(InGameQaPanel).GetField("player", privateFields);
        Assert.That(injected.GetValue(panel), Is.SameAs(player), "The QA panel must receive the same player as the project scope.");
        var inputField = typeof(InGameQaPanel).GetField("toggleInput", privateFields);
        var input = (InputAction)inputField.GetValue(panel);
        Assert.That(input.enabled, Is.True);
        Assert.That(input.bindings.Single().path, Is.EqualTo("<Keyboard>/p"));
        var toggle = typeof(InGameQaPanel).GetMethod("ToggleFromKeyboard", privateFields);
        var callback = new object[] { default(InputAction.CallbackContext) };
        int initialMoney = player.HasMoney();
        try
        {
            using var fields = new SerializedObject(panel);
            var root = (GameObject)fields.FindProperty("panelRoot").objectReferenceValue;
            Assert.That(root.activeSelf, Is.False);
            toggle.Invoke(panel, callback);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(GamePause.IsPaused, Is.True);

            var amount = (TMP_InputField)fields.FindProperty("moneyAmount").objectReferenceValue;
            amount.text = "25";
            ((Button)fields.FindProperty("addMoney").objectReferenceValue).onClick.Invoke();
            Assert.That(player.HasMoney(), Is.EqualTo(initialMoney + 25));
            ((Button)fields.FindProperty("subtractMoney").objectReferenceValue).onClick.Invoke();
            Assert.That(player.HasMoney(), Is.EqualTo(initialMoney));

            toggle.Invoke(panel, callback);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(GamePause.IsPaused, Is.False);
        }
        finally
        {
            if (player.HasMoney() != initialMoney) player.SetMoney(initialMoney);
        }
        yield return new ExitPlayMode();
    }
}
