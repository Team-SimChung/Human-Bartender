using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 따르기 작업 영역에서 누른 포인터로 병을 집어 들고 마우스 위치를 계속 전달한다.
/// 공통 Craft 입력은 누름/해제만 전달하므로, 라우팅 중에도 포인터 위치는 계속 읽는다.
/// </summary>
public class PourInputHandler : MonoBehaviour, ICraftPointerInput
{
    [SerializeField] BottleTiltController bottle;
    bool routedInput;
    bool pressHeld;
    Pointer dragPointer;
    Camera pointerCamera;
    PourManager manager;
    EventSystem pointerEvents;
    PointerEventData pointerEventData;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>(32);
    static readonly ProfilerMarker PressMarker = new ProfilerMarker("Pour.PointerPress");
    static readonly ProfilerMarker DragMarker = new ProfilerMarker("Pour.PointerDrag");

    void Awake() => manager = GetComponentInParent<PourManager>();

    void OnEnable()
    {
        pointerCamera = null;
        SynchronizePress();
    }

    public void SetRoutedInput(bool routed)
    {
        EndDrag();
        routedInput = routed;
        SynchronizePress();
    }

    public void OnPointerInput(bool pressed)
    {
        if (!isActiveAndEnabled || pressHeld == pressed) return;
        using var marker = PressMarker.Auto();
        pressHeld = pressed;

        if (!pressed)
        {
            EndDrag();
            return;
        }

        Pointer pointer = Pointer.current;
        if (bottle == null || !bottle.isActiveAndEnabled || pointer == null ||
            (manager != null && !manager.IsManualInputActive) ||
            IsOverControl(pointer) || !TryGetPointerPosition(pointer, out Vector3 worldPosition, true)) return;
        dragPointer = pointer;
        bottle.BeginDrag(worldPosition);
    }

    void Update()
    {
        if (UIDisplayOptions.IsOptionOpen) { EndDrag(); SynchronizePress(); return; }
        if (!routedInput)
        {
            Pointer pointer = Pointer.current;
            OnPointerInput(pointer != null && pointer.press.isPressed);
        }

        if (dragPointer == null) return;
        using var marker = DragMarker.Auto();
        if (!dragPointer.added || !dragPointer.press.isPressed || bottle == null || !bottle.isActiveAndEnabled)
        {
            EndDrag();
            SynchronizePress();
            return;
        }

        if (TryGetPointerPosition(dragPointer, out Vector3 position))
            bottle.DragTo(position);
    }

    bool IsOverControl(Pointer pointer)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return false;
        if (pointerEventData == null || pointerEvents != events)
        {
            pointerEvents = events;
            pointerEventData = new PointerEventData(events);
        }
        pointerEventData.Reset();
        pointerEventData.position = pointer.position.ReadValue();
        uiHits.Clear();
        events.RaycastAll(pointerEventData, uiHits);
        foreach (RaycastResult hit in uiHits)
        {
            if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
        }
        return false;
    }

    bool TryGetPointerPosition(Pointer pointer, out Vector3 position, bool requireInsideViewport = false)
    {
        position = default;
        if (pointerCamera == null) pointerCamera = CraftGimmickStage.Find(this);
        Camera camera = pointerCamera;
        if (camera == null) return false;
        Vector2 screen = pointer.position.ReadValue();
        if (requireInsideViewport && !camera.pixelRect.Contains(screen)) return false;
        Ray ray = camera.ScreenPointToRay(screen);
        if (!bottle.DragPlane.Raycast(ray, out float distance)) return false;
        position = ray.GetPoint(distance);
        return true;
    }

    void OnTransformParentChanged() => pointerCamera = null;

    void EndDrag()
    {
        dragPointer = null;
        if (bottle != null) bottle.EndDrag();
    }

    void SynchronizePress()
    {
        // 화면 전환/포커스 복귀 때 이미 누른 버튼으로 병을 새로 잡지 않는다.
        pressHeld = Pointer.current != null && Pointer.current.press.isPressed;
    }

    void OnDisable()
    {
        EndDrag();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        EndDrag();
        SynchronizePress();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) OnApplicationFocus(false);
    }
}
