using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>공용 알림 목록의 한 줄. 화면 위치는 부모 Layout Group이 정한다.</summary>
public class UIPopupView : MonoBehaviour
{
    [SerializeField] RectTransform panel;
    [SerializeField] CanvasGroup visibility;
    [Tooltip("요청 문구를 표시할 본문입니다. 비어 있는 요청에는 Default Message를 씁니다.")]
    [SerializeField] TMP_Text message;
    [SerializeField] string defaultMessage;

    [Header("Animation")]
    [Tooltip("알림이 나타나거나 사라지는 시간(초).")]
    [SerializeField, Min(0)] float duration = 0.3f;
    [Tooltip("0이면 Hide() 신호가 올 때까지 표시합니다.")]
    [SerializeField, Min(0)] float autoHideSeconds;
    [SerializeField] Ease enterEase = Ease.OutCubic;
    [SerializeField] Ease exitEase = Ease.InCubic;

    Tween fade;
    Tween hideTimer;
    Action onClosed;
    bool closing;

    internal float Height => panel.rect.height;

    internal bool TryPrepare(string data) => panel != null && visibility != null && Prepare(data);

    internal void Show(Action onShown, Action onClosed)
    {
        this.onClosed = onClosed;
        visibility.alpha = 0;
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        fade = visibility.DOFade(1, duration).SetEase(enterEase).SetUpdate(true)
            .OnStart(() => onShown?.Invoke())
            .OnComplete(() =>
            {
                if (autoHideSeconds > 0)
                    hideTimer = DOVirtual.DelayedCall(autoHideSeconds, Hide).SetUpdate(true);
            });
    }

    protected virtual bool Prepare(string data)
    {
        if (message != null) message.text = string.IsNullOrWhiteSpace(data) ? defaultMessage : data;
        return true;
    }

    public void Hide()
    {
        if (onClosed == null || closing) return;
        closing = true;
        hideTimer?.Kill();
        fade?.Kill();
        fade = visibility.DOFade(0, duration).SetEase(exitEase).SetUpdate(true)
            .OnComplete(() =>
            {
                fade = null;
                onClosed?.Invoke();
            });
    }

    internal void ResetView()
    {
        ClearAnimation();
        gameObject.SetActive(false);
    }

    protected virtual void OnDisable() => ClearAnimation();

    void ClearAnimation()
    {
        fade?.Kill();
        hideTimer?.Kill();
        fade = null;
        hideTimer = null;
        onClosed = null;
        closing = false;
    }
}
