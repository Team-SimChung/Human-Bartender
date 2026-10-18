using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed class PourMouseControlTests
{
    static readonly Vector3 Rest = new Vector3(-0.65f, -0.96f, 0f);
    static readonly Vector3 Mouth = new Vector3(0f, 0.4f, 0f);
    static readonly Vector3 CupRim = new Vector3(0.804f, 0.145f, 0f);
    GameObject root;
    BottleTiltController bottle;
    PourInputHandler input;
    CraftGimmickInputRouter router;
    Camera camera;
    Mouse mouse;
    InputSettings previousSettings;
    InputSettings testSettings;

    [SetUp]
    public void SetUp()
    {
        previousSettings = InputSystem.settings;
        testSettings = Object.Instantiate(previousSettings);
        testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        InputSystem.settings = testSettings;
        mouse = InputSystem.AddDevice<Mouse>();
        mouse.MakeCurrent();
        root = new GameObject("pour mouse test");
        root.transform.position = new Vector3(700f, 600f, 0f);
        var stage = root.AddComponent<CraftGimmickStage>();
        var cameraObject = new GameObject("stage camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
        SetField(stage, "stageCamera", camera);
        var body = new GameObject("bottle");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Rest;
        body.transform.localScale = new Vector3(1.4f, 3.6f, 1f);
        bottle = body.AddComponent<BottleTiltController>();
        var data = new SerializedObject(bottle);
        data.FindProperty("maxTiltAngle").floatValue = 110f;
        data.FindProperty("liftHeight").floatValue = 2.5f;
        data.ApplyModifiedPropertiesWithoutUndo();
        bottle.ConfigurePourGeometry(Mouth, root.transform.TransformPoint(CupRim));
        input = root.AddComponent<PourInputHandler>();
        SetField(input, "bottle", bottle);
        Invoke(input, "OnEnable");
        router = root.AddComponent<CraftGimmickInputRouter>();
    }

    [TearDown]
    public void TearDown()
    {
        Invoke(input, "OnDisable");
        Object.DestroyImmediate(root);
        if (mouse.added) InputSystem.RemoveDevice(mouse);
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
        InputSystem.settings = previousSettings;
        Object.DestroyImmediate(testSettings);
    }

    [Test]
    public void HoldingAStationaryPointerAutomaticallyPicksUpTheBottleBeforeTilting()
    {
        Vector3 restingMouth = bottle.BottleVisual.TransformPoint(Mouth);
        bottle.BeginDrag(Pointer(110f));
        Advance(0.1f);
        AssertAngle(0f);
        Assert.That(Vector3.Distance(restingMouth, bottle.BottleVisual.TransformPoint(Mouth)), Is.GreaterThan(0.01f));
        Advance(0.4f);
        AssertAngle(110f);
        AssertMouthAtPivot();
    }

    [Test]
    public void AbsolutePointerAnglesMatchTheReferenceAndTheMouthStaysAtTheCup()
    {
        bottle.BeginDrag(Pointer(0f));
        Advance(0.5f);
        foreach (float angle in new[] { 45f, 95f, 100f, 110f, 80f, 0f })
        {
            Vector3 pointer = Pointer(angle);
            bottle.DragTo(pointer);
            Advance(0.5f);
            AssertAngle(angle);
            AssertMouthAtPivot();
            Assert.That(Vector2.Angle(bottle.BottleVisual.TransformVector(Vector3.down),
                pointer - bottle.PourPivotWorldPosition), Is.LessThan(0.05f));
        }
    }

    [Test]
    public void RightSideQuadrantsUseTheReferenceClampWithoutFlipping()
    {
        bottle.BeginDrag(Pointer(-45f));
        Advance(0.5f);
        AssertAngle(0f);
        bottle.DragTo(Pointer(-135f));
        Advance(0.5f);
        AssertAngle(110f);
        bottle.DragTo(Pointer(0f));
        Advance(0.5f);
        AssertAngle(0f);
    }

    [Test]
    public void AngularMovementIsSmoothAndTheCentreDeadZoneDoesNotFlipTheBottle()
    {
        bottle.BeginDrag(Pointer(0f));
        Advance(0.5f);
        bottle.DragTo(Pointer(110f));
        Invoke(bottle, "Tick", 0.05f);
        Assert.That(bottle.CurrentAngle, Is.InRange(0.1f, 109.9f));
        Advance(0.4f);
        AssertAngle(110f);
        bottle.DragTo(bottle.PourPivotWorldPosition);
        Advance(0.2f);
        AssertAngle(110f);
        AssertMouthAtPivot();
    }

    [Test]
    public void ReleaseQuicklyStandsTheBottleUpAndReturnsItHome()
    {
        bottle.BeginDrag(Pointer(110f));
        Advance(0.5f);
        bottle.EndDrag();
        Advance(0.1f);
        Assert.That(bottle.CurrentAngle, Is.InRange(0.1f, 109.9f));
        Advance(0.3f);
        AssertAngle(0f);
        Assert.That(Vector3.Distance(bottle.BottleVisual.localPosition, Rest), Is.LessThan(0.001f));
    }

    [Test]
    public void RepressingDuringReturnContinuesSmoothlyFromTheCurrentPose()
    {
        bottle.BeginDrag(Pointer(110f));
        Advance(0.5f);
        bottle.EndDrag();
        Advance(0.1f);
        float angle = bottle.CurrentAngle;
        Vector3 position = bottle.BottleVisual.position;
        bottle.BeginDrag(Pointer(45f));
        AssertAngle(angle);
        Assert.That(bottle.BottleVisual.position, Is.EqualTo(position));
        Advance(0.5f);
        AssertAngle(45f);
        AssertMouthAtPivot();
    }

    [Test]
    public void MovingAndScalingTheStageDoesNotChangeThePointerAngle()
    {
        root.transform.localScale = new Vector3(1.3f, 1.7f, 1f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        bottle.ConfigurePourGeometry(Mouth, root.transform.TransformPoint(CupRim + Vector3.right * 0.3f));
        bottle.BeginDrag(Pointer(100f));
        Advance(0.5f);
        AssertAngle(100f);
        AssertMouthAtPivot();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StandaloneAndRoutedInputCanBeginAnywhereInTheWorkspace(bool routed)
    {
        if (routed) router.Bind(root);
        // 테스트 병에는 Renderer/Collider도 없다. 작업 영역을 누르는 것으로 시작해야 한다.
        SetMouse(Pointer(110f), true);
        if (routed) input.OnPointerInput(true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(110f);
        AssertMouthAtPivot();
        SetMouse(Pointer(45f), true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(45f);
        SetMouse(Pointer(45f), false);
        if (routed) input.OnPointerInput(false);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(0f);
        Assert.That(Vector3.Distance(bottle.BottleVisual.localPosition, Rest), Is.LessThan(0.001f));
    }

    [Test]
    public void PressingOutsideTheViewportCannotBecomeAHeldGrabInsideIt()
    {
        SetMouse(root.transform.TransformPoint(new Vector3(20f, 20f, 0f)), true);
        Invoke(input, "Update");
        SetMouse(Pointer(110f), true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(0f);
        Assert.That(Vector3.Distance(bottle.BottleVisual.localPosition, Rest), Is.LessThan(0.001f));
        SetMouse(Pointer(110f), false);
        Invoke(input, "Update");
        SetMouse(Pointer(110f), true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(110f);
    }

    [Test]
    public void UiRaycastsBlockButtonsButAllowDecorativeGraphics()
    {
        EventSystem previous = EventSystem.current;
        var events = root.AddComponent<EventSystem>();
        Invoke(events, "OnEnable");
        EventSystem.current = events;
        var raycaster = root.AddComponent<PourControlTestRaycaster>();
        Invoke(raycaster, "OnEnable");
        var control = new GameObject("finish button", typeof(RectTransform), typeof(Image), typeof(Button));
        control.transform.SetParent(root.transform, false);
        raycaster.hit = control;
        Vector3 point = root.transform.TransformPoint(Vector3.zero);
        try
        {
            SetMouse(point, true);
            Invoke(input, "Update");
            Advance(0.5f);
            Assert.That(Vector3.Distance(bottle.BottleVisual.localPosition, Rest), Is.LessThan(0.001f),
                "Pressing a UI button must not start pickup.");
            SetMouse(point, false);
            Invoke(input, "Update");
            Object.DestroyImmediate(control.GetComponent<Button>());
            SetMouse(point, true);
            Invoke(input, "Update");
            Advance(0.5f);
            AssertMouthAtPivot();
        }
        finally
        {
            Invoke(raycaster, "OnDisable");
            Object.DestroyImmediate(raycaster);
            Invoke(events, "OnDisable");
            Object.DestroyImmediate(events);
            if (previous != null) EventSystem.current = previous;
        }
    }

    [Test]
    public void UnbindingWhileHeldRequiresAFreshPress()
    {
        router.Bind(root);
        SetMouse(Pointer(110f), true);
        input.OnPointerInput(true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(110f);
        router.Unbind();
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(0f);
        Assert.That(Vector3.Distance(bottle.BottleVisual.localPosition, Rest), Is.LessThan(0.001f));
    }

    [Test]
    public void FocusLossAndRemovingTheDeviceCancelPickupAndDisableResetsThePose()
    {
        SetMouse(Pointer(110f), true);
        Invoke(input, "Update");
        Advance(0.5f);
        Invoke(input, "OnApplicationFocus", false);
        Advance(0.5f);
        AssertAngle(0f);
        SetMouse(Pointer(110f), false);
        Invoke(input, "Update");
        SetMouse(Pointer(110f), true);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(110f);
        InputSystem.RemoveDevice(mouse);
        Invoke(input, "Update");
        Advance(0.5f);
        AssertAngle(0f);
        bottle.BeginDrag(Pointer(110f));
        Advance(0.5f);
        Invoke(bottle, "OnDisable");
        AssertAngle(0f);
        Assert.That(bottle.BottleVisual.localPosition, Is.EqualTo(Rest));
    }

    Vector3 Pointer(float angle)
    {
        Vector3 pivot = root.transform.InverseTransformPoint(bottle.PourPivotWorldPosition);
        return root.transform.TransformPoint(pivot + Quaternion.Euler(0f, 0f, -angle) * Vector3.down * 2f);
    }

    void Advance(float seconds)
    {
        while (seconds > 0f)
        {
            float step = Mathf.Min(0.01f, seconds);
            Invoke(bottle, "Tick", step);
            seconds -= step;
        }
    }

    void AssertMouthAtPivot() => Assert.That(
        Vector3.Distance(bottle.BottleVisual.TransformPoint(Mouth), bottle.PourPivotWorldPosition), Is.LessThan(0.001f));

    void SetMouse(Vector3 world, bool pressed)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(world) }
            .WithButton(MouseButton.Left, pressed));
        InputSystem.Update();
    }

    void AssertAngle(float expected) => Assert.That(bottle.CurrentAngle, Is.EqualTo(expected).Within(0.05f));

    static void SetField(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Invoke(Object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}

// EditMode/헤드리스에서는 UI가 그려지지 않아 Graphic.depth가 -1이다.
// 실제 EventSystem의 raycast 결과 계약을 사용해 입력 차단을 검증한다.
public sealed class PourControlTestRaycaster : BaseRaycaster
{
    public GameObject hit;
    public override Camera eventCamera => null;

    public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
    {
        if (hit != null)
            resultAppendList.Add(new RaycastResult { gameObject = hit, module = this, distance = 0f });
    }
}
