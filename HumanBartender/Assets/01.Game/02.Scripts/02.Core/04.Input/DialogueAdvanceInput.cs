using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>대사·선택지의 공통 키와 한 프레임의 진행 입력 소유권. UI Submit 바인딩은 바꾸지 않는다.</summary>
internal static class DialogueAdvanceInput
{
    static int consumedFrame = -1;
    static int uiSubmitFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        consumedFrame = uiSubmitFrame = -1;
        InputSystem.onAfterUpdate -= CaptureUISubmit;
        InputSystem.onAfterUpdate += CaptureUISubmit;
    }

    internal static bool Matches(Key key, bool includeInteraction) =>
        key == GameKeyBindings.GetKey(GameControl.Advance) || key == Key.E || key == Key.Enter ||
        includeInteraction && key == GameKeyBindings.GetKey(GameControl.Interact);

    internal static KeyControl PressedKey(bool includeInteraction = false)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return null;
        // Enter가 다른 키와 동시에 들어와도 UI Submit에 먼저 맡긴다.
        if (keyboard.enterKey.wasPressedThisFrame) return keyboard.enterKey;
        foreach (var key in keyboard.allKeys)
            if (key.wasPressedThisFrame && Matches(key.keyCode, includeInteraction)) return key;
        return null;
    }

    internal static bool ConsumedThisFrame => consumedFrame == Time.frameCount;
    internal static bool UISubmitOwnsEnter => uiSubmitFrame == Time.frameCount;
    internal static bool TryConsume()
    {
        if (ConsumedThisFrame) return false;
        consumedFrame = Time.frameCount;
        return true;
    }

    // UI의 Update보다 먼저 선택 상태를 기록한다. Submit이 창을 닫아도 같은 Enter는 재사용하지 않는다.
    static void CaptureUISubmit()
    {
        var key = Keyboard.current?.enterKey;
        if (key?.wasPressedThisFrame != true) return;
        var events = EventSystem.current;
        if (events == null || !events.isActiveAndEnabled || !events.sendNavigationEvents ||
            events.currentInputModule is not InputSystemUIInputModule module ||
            !module.isActiveAndEnabled || module.submit?.action?.enabled != true) return;
        bool bound = false;
        foreach (var control in module.submit.action.controls)
            if (control == key) { bound = true; break; }
        if (bound) ReserveUISubmit(events.currentSelectedGameObject);
    }

    internal static void ReserveUISubmit(GameObject target)
    {
        if (target == null || !target.activeInHierarchy || ExecuteEvents.GetEventHandler<ISubmitHandler>(target) == null) return;
        if (target.TryGetComponent<Selectable>(out var selectable) && (!selectable.IsActive() || !selectable.IsInteractable())) return;
        uiSubmitFrame = Time.frameCount;
    }
}
