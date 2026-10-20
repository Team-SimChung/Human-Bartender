using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class ChoiceHoldButtonTests
{
    private GameObject root;
    private EventSystem events;
    private EventSystem previousEvents;
    private ChoiceHoldButton button;
    private GameObject progressRoot;
    private Image fill;
    private int confirmed;

    [SetUp]
    public void SetUp()
    {
        confirmed = 0;
        previousEvents = EventSystem.current;
        root = new GameObject("choice hold test");
        events = root.AddComponent<EventSystem>();
        // EditModeでは通常のMonoBehaviourのOnEnableが走らないため明示的に登録する。
        typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(events, null);
        EventSystem.current = events;
        var item = new GameObject("choice", typeof(RectTransform), typeof(Image), typeof(ChoiceHoldButton));
        item.transform.SetParent(root.transform);
        button = item.GetComponent<ChoiceHoldButton>();
        progressRoot = new GameObject("progress", typeof(RectTransform));
        progressRoot.transform.SetParent(item.transform);
        var fillObject = new GameObject("fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(progressRoot.transform);
        fill = fillObject.GetComponent<Image>();
        SetField("holdProgressRoot", progressRoot);
        SetField("holdProgressFill", fill);
        button.onClick.AddListener(() => confirmed++);
        item.SetActive(false);
        item.SetActive(true);
        ResetRuntimeState();
    }

    [TearDown]
    public void TearDown()
    {
        if (events != null)
            typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(events, null);
        Object.DestroyImmediate(root);
        if (previousEvents != null) EventSystem.current = previousEvents;
    }

    [Test]
    public void ShortClickDoesNotConfirmAndClearsProgress()
    {
        var pointer = Pointer();
        button.OnPointerDown(pointer);
        AdvanceHold(0.4);
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(progressRoot.activeSelf, Is.True);
        Assert.That(confirmed, Is.Zero);
        button.OnPointerUp(pointer);
        button.OnPointerClick(pointer);
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        Assert.That(fill.rectTransform.anchorMax.x, Is.Zero);
        Assert.That(progressRoot.activeSelf, Is.False);
    }

    [Test]
    public void OneSecondConfirmsExactlyOnceAndReleaseDoesNotConfirmAgain()
    {
        var pointer = Pointer();
        button.OnPointerDown(pointer);
        AdvanceHold(0.99);
        Assert.That(confirmed, Is.Zero);
        AdvanceHold(1.01);
        Assert.That(confirmed, Is.EqualTo(1));
        AdvanceHold(2);
        button.OnPointerUp(pointer);
        button.OnPointerClick(pointer);
        Assert.That(confirmed, Is.EqualTo(1));
        Assert.That(progressRoot.activeSelf, Is.False);
    }

    [Test]
    public void ReleaseDrainsTheBarFiveTimesFasterThanItFills()
    {
        button.OnPointerDown(Pointer());
        AdvanceHold(0.8);
        button.OnPointerUp(Pointer());
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(progressRoot.activeSelf, Is.True);
        AdvanceReset(0.08);
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(progressRoot.activeSelf, Is.True);
        AdvanceReset(0.081);
        Assert.That(fill.rectTransform.anchorMax.x, Is.Zero.Within(0.001f));
        Assert.That(progressRoot.activeSelf, Is.False);
        Assert.That(confirmed, Is.Zero);
    }

    [Test]
    public void PressingDuringResetImmediatelyFillsFromTheRemainingProgress()
    {
        var pointer = Pointer();
        button.OnPointerDown(pointer);
        AdvanceHold(0.8);
        button.OnPointerUp(pointer);
        AdvanceReset(0.04);
        button.OnPointerDown(pointer);
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.6f).Within(0.001f));
        AdvanceHold(0.04);
        Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.64f).Within(0.001f));
        AdvanceHold(0.39);
        Assert.That(confirmed, Is.Zero);
        AdvanceHold(0.41);
        Assert.That(confirmed, Is.EqualTo(1));
    }

    [TestCase("ChoicePanel.prefab")]
    public void ReleasingOverASelectedChoiceKeepsHoverAndLeavingRestoresItsSelectedAppearance(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/" + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            var hold = instance.GetComponent<ChoiceHoldButton>();
            var image = (Image)hold.targetGraphic;
            var hover = hold.spriteState.highlightedSprite;
            Assert.That(hover, Is.Not.Null);
            var pointer = Pointer();
            hold.OnPointerEnter(pointer);
            Assert.That(image.overrideSprite, Is.EqualTo(hover));
            hold.OnPointerDown(pointer);
            hold.OnPointerUp(pointer);
            hold.OnPointerClick(pointer);
            Assert.That(events.currentSelectedGameObject, Is.EqualTo(instance));
            Assert.That(image.overrideSprite, Is.EqualTo(hover), "Releasing the press must keep the hover visual.");
            hold.OnPointerExit(pointer);
            Assert.That(image.overrideSprite, Is.EqualTo(hold.spriteState.selectedSprite != null ?
                hold.spriteState.selectedSprite : image.sprite));
            hold.OnPointerEnter(pointer);
            Assert.That(image.overrideSprite, Is.EqualTo(hover));
            hold.interactable = false;
            Assert.That(image.overrideSprite, Is.Not.EqualTo(hover), "Locked choices must keep their disabled state.");
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [Test]
    public void LeavingChoiceCancelsAndReenteringRequiresANewPress()
    {
        var pointer = Pointer();
        button.OnPointerDown(pointer);
        AdvanceHold(0.6);
        button.OnPointerExit(pointer);
        button.OnPointerEnter(pointer);
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        Assert.That(progressRoot.activeSelf, Is.False);
        button.OnPointerDown(pointer);
        AdvanceHold(0.5);
        Assert.That(confirmed, Is.Zero);
        AdvanceHold(1.01);
        Assert.That(confirmed, Is.EqualTo(1));
    }

    [Test]
    public void LockedChoiceCannotStartOrFinishAHold()
    {
        button.interactable = false;
        button.OnPointerDown(Pointer());
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        Assert.That(progressRoot.activeSelf, Is.False);
        button.interactable = true;
        button.OnPointerDown(Pointer());
        AdvanceHold(0.5);
        button.interactable = false;
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        Assert.That(progressRoot.activeSelf, Is.False);
    }

    [Test]
    public void HiddenOrDeselectedChoiceDoesNotResumeAnOldHold()
    {
        button.OnPointerDown(Pointer());
        AdvanceHold(0.5);
        button.gameObject.SetActive(false);
        button.gameObject.SetActive(true);
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        button.OnPointerDown(Pointer());
        button.OnDeselect(new BaseEventData(events));
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        Assert.That(progressRoot.activeSelf, Is.False);
    }

    [Test]
    public void OtherPointerAndRightClickCannotCompleteOrCancelTheHeldPointer()
    {
        var right = Pointer();
        right.button = PointerEventData.InputButton.Right;
        button.OnPointerDown(right);
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
        button.OnPointerDown(Pointer());
        var other = Pointer();
        other.pointerId = 42;
        button.OnPointerUp(other);
        AdvanceHold(1.01);
        Assert.That(confirmed, Is.EqualTo(1));
    }

    [Test]
    public void ReusingAButtonForTheNextPageDoesNotCarryOverThePress()
    {
        button.onClick.AddListener(() =>
        {
            button.gameObject.SetActive(false);
            button.gameObject.SetActive(true);
        });
        var pointer = Pointer();
        button.OnPointerDown(pointer);
        AdvanceHold(1.01);
        button.OnPointerUp(pointer);
        button.OnPointerClick(pointer);
        AdvanceHold(2);
        Assert.That(confirmed, Is.EqualTo(1));
        button.OnPointerDown(pointer);
        AdvanceHold(1.01);
        Assert.That(confirmed, Is.EqualTo(2));
    }

    [TestCase("ChoicePanel.prefab")]
    public void ReopeningTheSameChoiceUnderAStationaryPointerKeepsHover(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/" + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            var hold = instance.GetComponent<ChoiceHoldButton>();
            var image = (Image)hold.targetGraphic;
            var module = root.AddComponent<InputSystemUIInputModule>();
            module.enabled = false;
            var pointer = new ExtendedPointerEventData(events)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = 7
            };
            var move = typeof(InputSystemUIInputModule).GetMethod("ProcessPointerMovement",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(ExtendedPointerEventData), typeof(GameObject) }, null);
            move.Invoke(module, new object[] { pointer, instance });
            Assert.That(image.overrideSprite, Is.EqualTo(hold.spriteState.highlightedSprite));
            hold.onClick.AddListener(() => { instance.SetActive(false); instance.SetActive(true); });
            hold.OnPointerDown(pointer);
            typeof(ChoiceHoldButton).GetField("holdStartedAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hold, Time.unscaledTimeAsDouble - 1.01);
            typeof(ChoiceHoldButton).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hold, null);
            hold.OnPointerUp(pointer);
            move.Invoke(module, new object[] { pointer, instance });
            Assert.That(pointer.pointerEnter, Is.EqualTo(instance));
            Assert.That(image.overrideSprite, Is.EqualTo(hold.spriteState.highlightedSprite));

            instance.SetActive(false);
            move.Invoke(module, new object[] { pointer, null });
            instance.SetActive(true);
            Assert.That(image.overrideSprite, Is.EqualTo(image.sprite), "An expired hover must not be restored.");
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase("OnApplicationFocus", false)]
    [TestCase("OnApplicationPause", true)]
    public void FocusLossOrPauseClearsBothTheHoldAndThePressedAppearance(string message, bool value)
    {
        button.OnPointerDown(Pointer());
        AdvanceHold(0.5);
        button.SendMessage(message, value);
        var state = typeof(Selectable).GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(state.GetValue(button).ToString(), Is.Not.EqualTo("Pressed"));
        Assert.That(progressRoot.activeSelf, Is.False);
        AdvanceHold(2);
        Assert.That(confirmed, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SubmitRequiresHoldingTheBoundKeyboardOrGamepadControl(bool useGamepad)
    {
        root.SetActive(false);
        var module = root.AddComponent<InputSystemUIInputModule>();
        module.enabled = false;
        root.SetActive(true);
        typeof(EventSystem).GetField("m_CurrentInputModule", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(events, module);

        InputDevice device = useGamepad ? (InputDevice)InputSystem.AddDevice<Gamepad>() : InputSystem.AddDevice<Keyboard>();
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var action = asset.AddActionMap("Choice").AddAction("Submit", InputActionType.Button,
            useGamepad ? "<Gamepad>/buttonSouth" : "<Keyboard>/enter");
        var reference = InputActionReference.Create(action);
        module.submit = reference;
        var previousSettings = InputSystem.settings;
        var testSettings = Object.Instantiate(previousSettings);
        testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        InputSystem.settings = testSettings;
        // EditMode에서도 게임 입력 액션을 처리하며 원래 설정은 finally에서 복원한다.
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        action.Enable();
        try
        {
            SetSubmitHeld(device, true);
            Assert.That(action.activeControl, Is.Not.Null, "Submit action must receive the simulated press.");
            Assert.That(events.currentInputModule, Is.SameAs(module));
            button.OnSubmit(new BaseEventData(events));
            AdvanceHold(0.5);
            Assert.That(progressRoot.activeSelf, Is.True);
            Assert.That(confirmed, Is.Zero);
            SetSubmitHeld(device, false);
            AdvanceHold(1.01);
            Assert.That(confirmed, Is.Zero);
            Assert.That(progressRoot.activeSelf, Is.True);
            AdvanceReset(0.04);
            Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(0.3f).Within(0.001f));

            SetSubmitHeld(device, true);
            button.OnSubmit(new BaseEventData(events));
            AdvanceHold(0.69);
            Assert.That(confirmed, Is.Zero);
            AdvanceHold(0.71);
            Assert.That(confirmed, Is.EqualTo(1));
        }
        finally
        {
            action.Disable();
            InputSystem.RemoveDevice(device);
            Object.DestroyImmediate(reference);
            Object.DestroyImmediate(asset);
            testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
            InputSystem.settings = previousSettings;
            Object.DestroyImmediate(testSettings);
        }
    }

    [TestCase("ChoicePanel.prefab")]
    public void BothPrefabsUseAOneSecondHoldAndABlueNonBlockingBottomBar(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/" + name);
        var hold = prefab.GetComponent<ChoiceHoldButton>();
        var scriptPath = AssetDatabase.GUIDToAssetPath("4913d2db59724ceaa581a5da2750576b");
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        Assert.That(script.GetClass(), Is.EqualTo(typeof(ChoiceHoldButton)), scriptPath + "; instance script: " +
            AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(button)) + "; assembly: " +
            typeof(ChoiceHoldButton).Assembly.FullName);
        Assert.That(hold, Is.Not.Null, "Prefab must keep its button component linked to ChoiceHoldButton: " +
            string.Join(", ", System.Array.ConvertAll(prefab.GetComponents<Component>(),
                component => component == null ? "Missing script" : component.GetType().FullName)));
        var data = new SerializedObject(hold);
        Assert.That(data.FindProperty("holdDuration").floatValue, Is.EqualTo(1f));
        Assert.That(data.FindProperty("resetDuration").floatValue, Is.EqualTo(0.2f));
        var track = (GameObject)data.FindProperty("holdProgressRoot").objectReferenceValue;
        var progress = (Image)data.FindProperty("holdProgressFill").objectReferenceValue;
        Assert.That(prefab.activeSelf, Is.True);
        Assert.That(track.activeSelf, Is.True, "Prefab Mode must show the bar without manual activation.");
        Assert.That(progress, Is.Not.Null);
        Assert.That(progress.raycastTarget, Is.False);
        Assert.That(track.GetComponent<Image>().raycastTarget, Is.False);
        Assert.That(progress.color.a, Is.GreaterThan(0f));
        Assert.That(progress.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(prefab.GetComponent<RectTransform>().rect.width, Is.GreaterThan(0));
        var rect = track.GetComponent<RectTransform>();
        Assert.That(rect.anchorMin.y, Is.Zero);
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.right));
        Assert.That(rect.sizeDelta.y, Is.GreaterThan(0));
    }

    [TestCase("ChoicePanel.prefab")]
    public void EditorInstancesKeepTheirDefaultShapeAndRuntimeResetHidesOnlyProgress(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/" + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Vector2 defaultSize = prefab.GetComponent<RectTransform>().rect.size;
        try
        {
            var hold = instance.GetComponent<ChoiceHoldButton>();
            var data = new SerializedObject(hold);
            var track = (GameObject)data.FindProperty("holdProgressRoot").objectReferenceValue;
            var progress = (Image)data.FindProperty("holdProgressFill").objectReferenceValue;
            var rect = instance.GetComponent<RectTransform>();
            Color defaultColor = progress.color;
            Assert.That(defaultSize.x, Is.GreaterThan(0f));
            Assert.That(defaultSize.y, Is.GreaterThan(0f));
            Assert.That(rect.rect.size, Is.EqualTo(defaultSize));
            instance.SetActive(false);
            instance.SetActive(true);
            hold.SendMessage("Update");
            Assert.That(track.activeSelf, Is.True);
            Assert.That(progress.rectTransform.anchorMax.x, Is.EqualTo(1f));
            var labels = instance.GetComponentsInChildren<Text>();
            Assert.That(labels.Length, Is.EqualTo(1));
            Assert.That(labels[0].text, Is.Not.Empty);
            Assert.That(instance.GetComponentsInChildren<TMPro.TMP_Text>(), Is.Empty,
                "The unused TMP label must not overlap the live Legacy label.");

            typeof(ChoiceHoldButton).GetMethod("CancelHold", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hold, new object[] { true });
            Assert.That(track.activeSelf, Is.False);
            Assert.That(progress.rectTransform.anchorMax.x, Is.Zero);
            Assert.That(rect.rect.size, Is.EqualTo(defaultSize));
            Assert.That(progress.color, Is.EqualTo(defaultColor));
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private PointerEventData Pointer() => new(events)
    {
        button = PointerEventData.InputButton.Left,
        pointerId = 7
    };

    private static void SetSubmitHeld(InputDevice device, bool held)
    {
        if (device is Keyboard keyboard)
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held ? new[] { Key.Enter } : new Key[0]));
        else
            InputSystem.QueueStateEvent((Gamepad)device, held ?
                new GamepadState().WithButton(GamepadButton.South) : new GamepadState());
        InputSystem.Update();
    }

    // 경계 시간만 제어하고 실제 Update와 UI 이벤트 경로를 검증한다.
    private void AdvanceHold(double seconds)
    {
        SetField("holdStartedAt", Time.unscaledTimeAsDouble - seconds);
        AdvanceReset(seconds);
    }

    private void AdvanceReset(double seconds)
    {
        SetField("lastProgressUpdateTime", Time.unscaledTimeAsDouble - seconds);
        typeof(ChoiceHoldButton).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, null);
    }

    private void SetField(string name, object value) =>
        typeof(ChoiceHoldButton).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(button, value);

    private void ResetRuntimeState() =>
        typeof(ChoiceHoldButton).GetMethod("CancelHold", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(button, new object[] { true });
}
