using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>선택지 확인 입력을 일정 시간 유지했을 때만 onClick을 한 번 호출한다.</summary>
[AddComponentMenu("UI/Choice Hold Button")]
public sealed class ChoiceHoldButton : Button
{
    [SerializeField, Min(0.1f)] private float holdDuration = 1f;
    [SerializeField, Min(0.01f)] private float resetDuration = 0.2f;
    [SerializeField] private GameObject holdProgressRoot;
    [SerializeField] private Image holdProgressFill;

    private bool holding;
    private int heldPointerId;
    private double holdStartedAt;
    private float holdStartProgress;
    private double lastProgressUpdateTime;
    private float displayedProgress;
    private bool pointerInside;
    private PointerEventData hoverPointer;
    private ButtonControl submitControl;

    protected override void OnEnable()
    {
        base.OnEnable();
        // Prefab Mode에서는 저장된 기본 외형을 유지하고, 게임 인스턴스만 초기화한다.
        if (Application.IsPlaying(gameObject)) CancelHold(true);
        RestorePointerHover();
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        if (Application.IsPlaying(gameObject) || holding || displayedProgress > 0f) CancelHold(true);
        base.OnDisable();
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && CanHold()) pointerInside = true;
        base.OnPointerDown(eventData);
        if (eventData.button != PointerEventData.InputButton.Left || holding || !CanHold()) return;
        heldPointerId = eventData.pointerId;
        BeginHold();
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (eventData.button == PointerEventData.InputButton.Left &&
            eventData.pointerId == heldPointerId && submitControl == null)
            CancelHold();
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        hoverPointer = eventData;
        pointerInside = true;
        base.OnPointerEnter(eventData);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        if (hoverPointer == eventData) hoverPointer = null;
        pointerInside = false;
        base.OnPointerExit(eventData);
        if (eventData.pointerId == heldPointerId && submitControl == null)
            CancelHold();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // 마우스로 선택된 상태에서도 커서가 안에 있으면 호버 외형을 유지한다.
        // 논리적인 선택은 유지하여 키보드·게임패드 이동을 방해하지 않는다.
        if (state == SelectionState.Selected && pointerInside) state = SelectionState.Highlighted;
        base.DoStateTransition(state, instant);
    }

    // 확정은 Update에서만 한다. 손을 뗄 때 발생하는 클릭은 다음 선택지에도 전달하지 않는다.
    public override void OnPointerClick(PointerEventData eventData) { }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (holding || !CanHold()) return;
        var module = eventData.currentInputModule as InputSystemUIInputModule;
        var control = module?.submit?.action?.activeControl as ButtonControl;
        if (control == null || !control.isPressed) return;
        submitControl = control;
        BeginHold();
    }

    public override void OnDeselect(BaseEventData eventData)
    {
        CancelHold();
        base.OnDeselect(eventData);
    }

    private bool CanHold() => IsActive() && IsInteractable();

    private void BeginHold()
    {
        holding = true;
        holdStartProgress = displayedProgress;
        holdStartedAt = Time.unscaledTimeAsDouble;
        lastProgressUpdateTime = holdStartedAt;
        if (holdProgressRoot != null) holdProgressRoot.SetActive(true);
    }

    private void Update()
    {
        if (!holding && displayedProgress <= 0f) return;
        double now = Time.unscaledTimeAsDouble;
        float delta = Mathf.Max(0f, (float)(now - lastProgressUpdateTime));
        lastProgressUpdateTime = now;
        float resetSpeed = 1f / Mathf.Max(0.01f, resetDuration);
        if (!holding)
        {
            SetProgress(Mathf.MoveTowards(displayedProgress, 0f, resetSpeed * delta));
            if (displayedProgress <= 0f && holdProgressRoot != null) holdProgressRoot.SetActive(false);
            return;
        }
        if (!CanHold())
        {
            CancelHold(true);
            return;
        }
        if (submitControl != null && !submitControl.isPressed)
        {
            CancelHold();
            return;
        }

        float progress = Mathf.Clamp01(holdStartProgress + (float)((now - holdStartedAt) /
            Mathf.Max(0.1f, holdDuration)));
        SetProgress(progress);
        if (progress < 1f) return;

        // 콜백이 페이지를 바꾸거나 같은 버튼을 다시 켜도 입력은 여기서 끝낸다.
        CancelHold(true);
        onClick.Invoke();
    }

    private void CancelHold(bool immediately = false)
    {
        holding = false;
        submitControl = null;
        lastProgressUpdateTime = Time.unscaledTimeAsDouble;
        if (immediately || displayedProgress <= 0f)
        {
            SetProgress(0f);
            if (holdProgressRoot != null) holdProgressRoot.SetActive(false);
        }
    }

    private void SetProgress(float progress)
    {
        displayedProgress = progress;
        if (holdProgressFill == null) return;
        var rect = holdProgressFill.rectTransform;
        rect.anchorMax = new Vector2(progress, 1f);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ClearInputState();
        else RestorePointerHover();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ClearInputState();
        else RestorePointerHover();
    }

    private void RestorePointerHover()
    {
        // 같은 슬롯을 즉시 다시 켜면 입력 모듈은 PointerEnter를 재전송하지 않는다.
        // 모듈이 유지하는 실제 hovered 목록으로 기본 Button의 상태도 복원한다.
        if (!IsActive() || hoverPointer == null || !hoverPointer.hovered.Contains(gameObject)) return;
        pointerInside = true;
        base.OnPointerEnter(hoverPointer);
    }

    private void ClearInputState()
    {
        if (Application.IsPlaying(gameObject) || holding || displayedProgress > 0f) CancelHold(true);
        pointerInside = false;
        InstantClearState();
    }
}
