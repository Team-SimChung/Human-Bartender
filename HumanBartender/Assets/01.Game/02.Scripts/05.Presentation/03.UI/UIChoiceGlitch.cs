using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기존 선택지의 포커스·홀드 입력을 공통 글자 글리치에 연결한다.</summary>
[DisallowMultipleComponent]
public sealed class UIChoiceGlitch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private ChoiceHoldButton holdButton;
    [SerializeField] private GameObject focusDecoration;
    [SerializeField] private Color restingColor = new Color32(77, 145, 165, 255);
    [SerializeField] private Color focusedColor = new Color32(237, 252, 255, 255);
    [SerializeField] private TMP_FontAsset restingFont;
    [SerializeField] private TMP_FontAsset focusedFont;

    [Header("글리치")]
    [Tooltip("글리치 연출 값 에셋(04.Data/01.Definitions/UI/ChoiceGlitchSettings). 비우면 기본값으로 재생한다.")]
    [SerializeField] private UIChoiceGlitchSettings settings;

    private float Duration => ChoiceTextGlitch.GetDuration(settings);
    private float FrameRate => settings != null ? settings.FrameRate : UIChoiceGlitchSettings.DefaultFrameRate;
    private int ActiveFrames => settings != null ? settings.ActiveFrames : UIChoiceGlitchSettings.DefaultActiveFrames;
    private int RestEvery => settings != null ? settings.RestEvery : UIChoiceGlitchSettings.DefaultRestEvery;
    private float Jitter => settings != null ? settings.Jitter : UIChoiceGlitchSettings.DefaultJitter;
    private Color32 TintA => settings != null ? settings.TintA : UIChoiceGlitchSettings.DefaultTintA;
    private Color32 TintB => settings != null ? settings.TintB : UIChoiceGlitchSettings.DefaultTintB;

    private readonly ChoiceTextGlitch glitch = new ChoiceTextGlitch();
    private Selectable selectable;
    private bool hovered;
    private bool selected;
    private bool animating;
    // 마우스 선택은 호버가 강조를 맡는다. 다시 열린 선택지에 이전 클릭의 강조가 남지 않게 구분한다.
    private bool selectedByPointer;
    private bool lastFocus;
    private double startedAt;

    public bool HasExternalFeedback { get; private set; }

    /// <summary>거부 표시가 글자 외형을 소유하는 동안에도 포커스 추적과 화살표는 유지한다.</summary>
    public void SetExternalFeedback(bool active)
    {
        if (HasExternalFeedback == active) return;
        animating = false;
        glitch.Stop();
        HasExternalFeedback = active;
        RefreshFocus();
    }

    private void OnEnable()
    {
        selectable = GetComponent<Selectable>();
        if (holdButton != null)
        {
            holdButton.HoldingChanged -= OnHoldingChanged;
            holdButton.HoldingChanged += OnHoldingChanged;
        }
        selected = !selectedByPointer && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        RefreshFocus();
    }

    private void OnDisable()
    {
        if (holdButton != null) holdButton.HoldingChanged -= OnHoldingChanged;
        hovered = selected = animating = false;
        if (focusDecoration != null) focusDecoration.SetActive(false);
        // 외부 효과의 재질·폰트 복원은 그 효과를 시작한 선택지 항목이 담당한다.
        if (!HasExternalFeedback && label != null && restingFont != null) label.font = restingFont;
        Restore();
    }

    private void OnHoldingChanged(bool holding)
    {
        if (HasExternalFeedback) return;
        animating = holding;
        startedAt = Time.unscaledTimeAsDouble;
        if (!holding) Restore();
    }

    public void OnPointerEnter(PointerEventData data)
    {
        hovered = true;
        RefreshFocus(true);
    }

    public void OnPointerExit(PointerEventData data)
    {
        hovered = false;
        RefreshFocus();
    }

    public void OnSelect(BaseEventData data)
    {
        selectedByPointer = data is PointerEventData;
        selected = !selectedByPointer;
        RefreshFocus(true);
    }

    public void OnDeselect(BaseEventData data)
    {
        selected = selectedByPointer = false;
        RefreshFocus();
    }

    private void RefreshFocus(bool entered = false)
    {
        bool focus = (hovered || selected) && selectable != null && selectable.IsInteractable();
        lastFocus = focus;
        if (focusDecoration != null) focusDecoration.SetActive(focus);
        if (HasExternalFeedback) return;
        if (label != null)
        {
            label.color = focus ? focusedColor : restingColor;
            TMP_FontAsset font = focus ? focusedFont : restingFont;
            if (font != null && label.font != font) label.font = font;
        }
        // 홀드 선택지는 포커스 색만 적용하고, 글리치는 누르고 있는 동안 재생한다.
        if (holdButton != null) return;
        if (focus && entered)
        {
            animating = true;
            startedAt = Time.unscaledTimeAsDouble;
        }
        else if (!focus)
        {
            animating = false;
            Restore();
        }
    }

    private void LateUpdate()
    {
        bool focus = (hovered || selected) && selectable != null && selectable.IsInteractable();
        if (focus != lastFocus) RefreshFocus();
        if (HasExternalFeedback) return;
        if (!animating || label == null) return;
        double elapsed = Time.unscaledTimeAsDouble - startedAt;
        if (selectable == null || !selectable.IsInteractable() || (holdButton == null && elapsed >= Duration))
        {
            animating = false;
            Restore();
            return;
        }
        glitch.Sample(label, settings, elapsed);
    }

    private void Restore()
    {
        glitch.Stop();
        if (!HasExternalFeedback && label != null) label.ForceMeshUpdate(true);
    }

    private void OnDestroy()
    {
        glitch.Dispose();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        animating = false;
        hovered = false;
        Restore();
    }
}
