using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class PourMouseRuntimeTests
{
    [UnityTest]
    public IEnumerator PlayerInputPicksUpFromTheWorkspaceAimsAtTheCupAndReturnsOnRelease()
    {
        yield return new EnterPlayMode();
        yield return ExerciseReferenceControls();
        yield return new ExitPlayMode();
    }

    // 플레이 모드 전환 전에 생성된 람다의 캡처 객체는 도메인 리로드로 사라진다.
    // 실제 입력 시나리오는 전환이 끝난 뒤 별도 IEnumerator에서 시작한다.
    static IEnumerator ExerciseReferenceControls()
    {
        var previousSettings = InputSystem.settings;
        var testSettings = Object.Instantiate(previousSettings);
        testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        InputSystem.settings = testSettings;
        var mouse = InputSystem.AddDevice<Mouse>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        mouse.MakeCurrent();
        var root = new GameObject("runtime pour test");
        root.SetActive(false);
        root.transform.position = new Vector3(700f, 600f, 0f);
        var actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/01.Game/05.Settings/01.Input/PlayerInput.inputactions"));
        var stage = root.AddComponent<CraftGimmickStage>();
        var cameraObject = new GameObject("stage camera", typeof(Camera));
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
        SetReference(stage, "stageCamera", camera);
        var instance = new GameObject("gimmick");
        instance.transform.SetParent(root.transform, false);
        var body = new GameObject("bottle");
        body.transform.SetParent(instance.transform, false);
        Vector3 rest = new Vector3(-0.65f, -0.96f, 0f);
        body.transform.localPosition = rest;
        body.transform.localScale = new Vector3(1.4f, 3.6f, 1f);
        var bottle = body.AddComponent<BottleTiltController>();
        var data = new SerializedObject(bottle);
        Assert.That(data.FindProperty("maxTiltAngle"), Is.Not.Null, "Runtime bottle fields must be available.");
        data.FindProperty("maxTiltAngle").floatValue = 110f;
        data.FindProperty("liftHeight").floatValue = 2.5f;
        data.ApplyModifiedPropertiesWithoutUndo();
        var handler = instance.AddComponent<PourInputHandler>();
        SetReference(handler, "bottle", bottle);
        var router = root.AddComponent<CraftGimmickInputRouter>();
        var playerInput = root.AddComponent<PlayerInput>();
        playerInput.actions = actions;
        playerInput.defaultActionMap = "Play";
        playerInput.notificationBehavior = PlayerNotifications.SendMessages;
        SetReference(router, "playerInput", playerInput);
        float previousMaximumDelta = Time.maximumDeltaTime;
        float previousTimeScale = Time.timeScale;
        Time.maximumDeltaTime = 0.02f;
        Time.timeScale = 1f;
        root.SetActive(true);
        Vector3 mouth = new Vector3(0f, 0.4f, 0f);
        bottle.ConfigurePourGeometry(mouth, root.transform.TransformPoint(new Vector3(0.804f, 0.145f, 0f)));
        try
        {
            yield return null;
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            router.Begin();
            router.Bind(instance);
            Assert.That(playerInput.currentActionMap, Is.Not.Null);
            Assert.That(playerInput.currentActionMap.FindAction("PointerPress", true), Is.Not.Null);
            int pressEvents = 0;
            playerInput.currentActionMap.FindAction("PointerPress", true).performed += _ => pressEvents++;
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 0f), true);
            Assert.That(pressEvents, Is.EqualTo(1));
            yield return WaitFor(() => Vector3.Distance(body.transform.TransformPoint(mouth), bottle.PourPivotWorldPosition) < 0.001f);
            Assert.That(bottle.CurrentAngle, Is.Zero.Within(0.05f));
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 95f), true);
            yield return WaitFor(() => Mathf.Abs(bottle.CurrentAngle - 95f) < 0.05f);
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 110f), true);
            yield return WaitFor(() => Mathf.Abs(bottle.CurrentAngle - 110f) < 0.05f);
            Assert.That(Vector3.Distance(body.transform.TransformPoint(mouth), bottle.PourPivotWorldPosition), Is.LessThan(0.001f));
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 0f), true);
            yield return WaitFor(() => bottle.CurrentAngle == 0f);
            Assert.That(Vector3.Distance(body.transform.TransformPoint(mouth), bottle.PourPivotWorldPosition), Is.LessThan(0.001f),
                "Standing the bottle up while holding must keep it at the pouring position.");
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 100f), true);
            yield return WaitFor(() => Mathf.Abs(bottle.CurrentAngle - 100f) < 0.05f);
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 100f), false);
            yield return null;
            Assert.That(bottle.CurrentAngle, Is.InRange(0.01f, 99.999f), "Release must begin a smooth return.");
            yield return WaitFor(() => bottle.CurrentAngle == 0f && Vector3.Distance(body.transform.localPosition, rest) < 0.001f);
            SetMouse(mouse, camera, Pointer(root.transform, bottle, 110f), true);
            yield return WaitFor(() => Mathf.Abs(bottle.CurrentAngle - 110f) < 0.05f);
            instance.SetActive(false);
            Assert.That(bottle.CurrentAngle, Is.Zero);
            Assert.That(body.transform.localPosition, Is.EqualTo(rest));
        }
        finally
        {
            router.End();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(actions);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
            InputSystem.settings = previousSettings;
            Object.DestroyImmediate(testSettings);
            Time.maximumDeltaTime = previousMaximumDelta;
            Time.timeScale = previousTimeScale;
        }
    }

    static IEnumerator WaitFor(System.Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The expected input response did not finish within three seconds.");
    }

    static Vector3 Pointer(Transform root, BottleTiltController bottle, float angle)
    {
        Vector3 pivot = root.InverseTransformPoint(bottle.PourPivotWorldPosition);
        return root.TransformPoint(pivot + Quaternion.Euler(0f, 0f, -angle) * Vector3.down * 2f);
    }

    static void SetMouse(Mouse mouse, Camera camera, Vector3 world, bool pressed)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = camera.WorldToScreenPoint(world) }
            .WithButton(MouseButton.Left, pressed));
        InputSystem.Update();
    }

    static void SetReference(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        Assert.That(property, Is.Not.Null, target.GetType().Name + "." + field);
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
