using DG.Tweening;
using UnityEngine;

/// <summary>편집 시 panel 위치가 표시 위치다. Hide 신호 또는 선택적 타이머로 닫는다.</summary>
public class UIPopupView : MonoBehaviour
{
    [SerializeField] RectTransform panel;
    [Tooltip("켜면 오른쪽에서 왼쪽으로 진입, 끄면 왼쪽에서 오른쪽으로 진입한다.")]
    [SerializeField] bool enterFromRight = true;
    [SerializeField, Min(0)] float duration = 0.3f;
    [SerializeField, Min(0)] float outsideMargin = 20f;
    [Tooltip("0이면 Hide() 신호가 올 때까지 표시한다.")]
    [SerializeField, Min(0)] float autoHideSeconds;
    [SerializeField] Ease enterEase = Ease.OutCubic;
    [SerializeField] Ease exitEase = Ease.InCubic;

    Vector2 shownPosition;
    Vector2 hiddenPosition;
    bool initialized;
    bool showing;
    Tween movement;
    Tween hideTimer;
    readonly Vector3[] corners = new Vector3[4];
    public event System.Action Shown;
    bool notified;

    protected virtual void Awake() => Initialize();

    bool Initialize()
    {
        if (initialized) return true;
        if (panel == null || panel.parent is not RectTransform || panel.GetComponentInParent<Canvas>() == null)
            return false;
        shownPosition = panel.anchoredPosition;
        initialized = true;
        UpdateHiddenPosition();
        panel.anchoredPosition = hiddenPosition;
        return true;
    }

    void UpdateHiddenPosition()
    {
        Canvas.ForceUpdateCanvases();
        var parent = (RectTransform)panel.parent;
        var canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
        panel.anchoredPosition = shownPosition;
        panel.GetWorldCorners(corners);
        float panelMin = float.PositiveInfinity, panelMax = float.NegativeInfinity;
        foreach (var corner in corners)
        {
            float x = parent.InverseTransformPoint(corner).x;
            panelMin = Mathf.Min(panelMin, x);
            panelMax = Mathf.Max(panelMax, x);
        }
        ((RectTransform)canvas.transform).GetWorldCorners(corners);
        float canvasMin = float.PositiveInfinity, canvasMax = float.NegativeInfinity;
        foreach (var corner in corners)
        {
            float x = parent.InverseTransformPoint(corner).x;
            canvasMin = Mathf.Min(canvasMin, x);
            canvasMax = Mathf.Max(canvasMax, x);
        }
        float offset = enterFromRight
            ? Mathf.Max(0, canvasMax - panelMin + outsideMargin)
            : Mathf.Min(0, canvasMin - panelMax - outsideMargin);
        hiddenPosition = shownPosition + Vector2.right * offset;
    }

    /// <summary>표시 시작 성공 여부를 즉시 반환한다. 애니메이션 완료는 기다리지 않는다.</summary>
    public bool Popup(string data)
    {
        if (!isActiveAndEnabled || !Initialize()) return false;
        if (!Prepare(data)) return false;
        movement?.Kill();
        hideTimer?.Kill();
        panel.gameObject.SetActive(true);
        UpdateHiddenPosition();
        panel.anchoredPosition = hiddenPosition;
        showing = true;
        notified = false;
        movement = panel.DOAnchorPos(shownPosition, duration).SetEase(enterEase).SetUpdate(true)
            .OnUpdate(NotifyVisible)
            .OnComplete(() =>
            {
                NotifyShown();
                if (autoHideSeconds > 0)
                    hideTimer = DOVirtual.DelayedCall(autoHideSeconds, Hide).SetUpdate(true);
            });
        return true;
    }

    void NotifyVisible()
    {
        if (notified || !showing) return;
        panel.GetWorldCorners(corners);
        var canvas = (RectTransform)panel.GetComponentInParent<Canvas>().rootCanvas.transform;
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        foreach (var corner in corners)
        {
            float x = canvas.InverseTransformPoint(corner).x;
            minimum = Mathf.Min(minimum, x); maximum = Mathf.Max(maximum, x);
        }
        if (maximum > canvas.rect.xMin && minimum < canvas.rect.xMax) NotifyShown();
    }

    void NotifyShown()
    {
        if (notified || !showing) return;
        notified = true;
        Shown?.Invoke();
    }

    protected virtual bool Prepare(string data) => true;

    public void Hide()
    {
        if (!initialized || !showing) return;
        showing = false;
        hideTimer?.Kill();
        movement?.Kill();
        movement = panel.DOAnchorPos(hiddenPosition, duration).SetEase(exitEase).SetUpdate(true);
    }

    protected virtual void OnDisable()
    {
        movement?.Kill();
        hideTimer?.Kill();
        showing = false;
        if (initialized && panel != null) panel.anchoredPosition = hiddenPosition;
    }
}
