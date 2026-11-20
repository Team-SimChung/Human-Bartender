using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>외부 상호작용 전용 선택지 항목. 외형은 프리팹에서 편집함.</summary>
public sealed class WorldInteractionChoiceItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private VerticalLayoutGroup contentLayout;
    [SerializeField] private CanvasGroup visibility;
    [Header("선택지 표시")]
    [SerializeField] private Image selectedBackground;
    [SerializeField] private Image inactiveLine;
    [SerializeField] private Sprite rightBackground;
    [SerializeField] private Sprite leftBackground;
    [SerializeField] private Color normalTextColor = new(.8f, .94f, .95f, 1);
    [SerializeField] private Color selectedTextColor = new(.025f, .12f, .15f, 1);
    [SerializeField, InspectorName("선택 배경색")] private Color selectedBackgroundColor = Color.white;
    [SerializeField, InspectorName("거부 배경색")] private Color rejectedBackgroundColor = new(1, .15f, .15f, 1);
    [SerializeField, InspectorName("거부 글자색")] private Color rejectedTextColor = new(1, .92f, .94f, 1);
    [SerializeField, InspectorName("거부 글리치 설정")] private UIChoiceGlitchSettings rejectionGlitchSettings;
    private Action selected;
    private Action hovered;
    private readonly ChoiceTextGlitch rejectionGlitch = new();
    private string originalText;
    private string rejectionReason;
    private bool executable;
    private float originalFontSize;

    internal Button Button => button;
    internal TMP_Text Label => label;
    internal Image Background => selectedBackground;
    public bool CanSelect => isActiveAndEnabled && button != null && button.interactable;
    public bool CanExecute => CanSelect && executable;
    public bool IsRejected { get; private set; }
    public void Confirm()
    {
        if (CanSelect) button.onClick.Invoke();
    }
    public bool IsFocused { get; private set; }
    public RectTransform Rect => (RectTransform)transform;
    public bool IsConfigured => ConfigurationError == null;
    public string ConfigurationError
    {
        get
        {
            if (button == null) return $"{name}의 Button을 연결하세요.";
            if (label == null) return $"{name}의 Label에 TextMeshPro 텍스트를 연결하세요.";
            if (contentLayout == null || GetComponent<ContentSizeFitter>() == null)
                return $"{name}의 Content Layout과 Content Size Fitter를 연결하세요.";
            if (visibility == null) return $"{name}의 Visibility에 Canvas Group을 연결하세요.";
            if (selectedBackground == null || inactiveLine == null)
                return $"{name}의 Selected Background와 Inactive Line을 연결하세요.";
            return rightBackground == null || leftBackground == null ? $"{name}의 좌우 선택 배경 Sprite를 연결하세요." : null;
        }
    }

    private void Awake()
    {
        originalFontSize = label.fontSize;
        button.onClick.AddListener(Select);
    }
    private void Select()
    {
        if (CanSelect) selected?.Invoke();
    }

    public static string SingleLine(string text) => Regex.Replace(text ?? "",
        @"(?:\r\n|[\r\n\u0085\u2028\u2029]|<br\s*/?>)+", " ", RegexOptions.IgnoreCase).Trim();

    public float MeasureWidth(string text, string lockReason = null)
    {
        float titleWidth = label.GetPreferredValues(SingleLine(text), Mathf.Infinity, Mathf.Infinity).x;
        float reasonWidth = string.IsNullOrEmpty(lockReason) ? 0
            : label.GetPreferredValues(SingleLine(lockReason), Mathf.Infinity, Mathf.Infinity).x;
        return Mathf.Max(titleWidth, reasonWidth) + contentLayout.padding.horizontal;
    }

    public void SetFocused(bool focused)
    {
        IsFocused = focused && button.interactable;
        if (!IsFocused) ResetRejection();
        RefreshAppearance();
    }

    public void Reject()
    {
        if (!CanSelect || executable) return;
        IsRejected = true;
        label.text = rejectionReason;
        RefreshAppearance();
        rejectionGlitch.Play(label, rejectionGlitchSettings);
    }

    private void ResetRejection()
    {
        rejectionGlitch.Stop();
        IsRejected = false;
        if (label != null && originalText != null) label.text = originalText;
    }

    private void RefreshAppearance()
    {
        selectedBackground.gameObject.SetActive(IsFocused);
        inactiveLine.gameObject.SetActive(!IsFocused);
        selectedBackground.color = IsRejected ? rejectedBackgroundColor : selectedBackgroundColor;
        label.color = IsRejected ? rejectedTextColor : IsFocused ? selectedTextColor : normalTextColor;
    }

    private void LateUpdate() => rejectionGlitch.Tick();

    public void SetSide(bool right)
    {
        selectedBackground.sprite = right ? rightBackground : leftBackground;
        var line = inactiveLine.rectTransform;
        line.anchorMin = new Vector2(right ? 0 : 1, 0);
        line.anchorMax = new Vector2(right ? 0 : 1, 1);
        line.anchoredPosition = Vector2.zero;
        label.horizontalAlignment = right
            ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
    }

    public void Bind(string text, bool enabled, Action onSelected, Action onHovered, string lockReason = null)
    {
        ResetRejection();
        originalText = SingleLine(text);
        rejectionReason = SingleLine(lockReason);
        executable = enabled;
        if (originalFontSize <= 0) originalFontSize = label.fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        // 원문과 사유는 같은 글자 칸을 쓰며, 긴 번역은 기존 항목 폭 안에서 줄인다.
        label.enableAutoSizing = true;
        label.fontSizeMax = originalFontSize;
        label.fontSizeMin = originalFontSize * .5f;
        label.text = originalText;
        // 조건이 부족해도 탐색과 거부 입력은 받는다. 실제 결과 실행은 뷰에서 차단한다.
        button.interactable = true;
        visibility.alpha = 1;
        selected = onSelected;
        hovered = onHovered;
        SetFocused(false);
        gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable) hovered?.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (CanSelect) hovered?.Invoke();
    }

    public void Clear()
    {
        selected = null;
        hovered = null;
        SetFocused(false);
        gameObject.SetActive(false);
    }

    private void OnDisable() => ResetRejection();

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Select);
        rejectionGlitch.Dispose();
    }
}
