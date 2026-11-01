using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>외부 상호작용 전용 선택지 항목. 외형은 프리팹에서 편집함.</summary>
public sealed class WorldInteractionChoiceItem : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text lockReasonLabel;
    [SerializeField] private VerticalLayoutGroup contentLayout;
    [SerializeField] private CanvasGroup visibility;
    [Header("Selection appearance")]
    [SerializeField] private Image selectedBackground;
    [SerializeField] private Image inactiveLine;
    [SerializeField] private Sprite rightBackground;
    [SerializeField] private Sprite leftBackground;
    [SerializeField] private Color normalTextColor = new(.8f, .94f, .95f, 1);
    [SerializeField] private Color selectedTextColor = new(.025f, .12f, .15f, 1);
    [SerializeField] private Color disabledTextColor = new(.55f, .67f, .69f, 1);
    [SerializeField, Range(0, 1)] private float disabledAlpha = .7f;
    private Action selected;
    private Action hovered;

    internal Button Button => button;
    internal TMP_Text Label => label;
    internal TMP_Text LockReasonLabel => lockReasonLabel;
    internal Image Background => selectedBackground;
    public bool CanSelect => isActiveAndEnabled && button != null && button.interactable;
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
            if (lockReasonLabel == null) return $"{name}의 Lock Reason Label에 TextMeshPro 텍스트를 연결하세요.";
            if (contentLayout == null || GetComponent<ContentSizeFitter>() == null)
                return $"{name}의 Content Layout과 Content Size Fitter를 연결하세요.";
            if (visibility == null) return $"{name}의 Visibility에 Canvas Group을 연결하세요.";
            if (selectedBackground == null || inactiveLine == null)
                return $"{name}의 Selected Background와 Inactive Line을 연결하세요.";
            return rightBackground == null || leftBackground == null ? $"{name}의 좌우 선택 배경 Sprite를 연결하세요." : null;
        }
    }

    private void Awake() => button.onClick.AddListener(Select);
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
            : lockReasonLabel.GetPreferredValues(SingleLine(lockReason), Mathf.Infinity, Mathf.Infinity).x;
        return Mathf.Max(titleWidth, reasonWidth) + contentLayout.padding.horizontal;
    }

    public void SetFocused(bool focused)
    {
        IsFocused = focused && button.interactable;
        selectedBackground.gameObject.SetActive(IsFocused);
        inactiveLine.gameObject.SetActive(!IsFocused);
        label.color = !button.interactable ? disabledTextColor : IsFocused ? selectedTextColor : normalTextColor;
    }

    public void SetSide(bool right)
    {
        selectedBackground.sprite = right ? rightBackground : leftBackground;
        var line = inactiveLine.rectTransform;
        line.anchorMin = new Vector2(right ? 0 : 1, 0);
        line.anchorMax = new Vector2(right ? 0 : 1, 1);
        line.anchoredPosition = Vector2.zero;
        label.horizontalAlignment = lockReasonLabel.horizontalAlignment = right
            ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right;
    }

    public void Bind(string text, bool enabled, Action onSelected, Action onHovered, string lockReason = null)
    {
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = SingleLine(text);
        lockReasonLabel.textWrappingMode = TextWrappingModes.NoWrap;
        lockReasonLabel.overflowMode = TextOverflowModes.Ellipsis;
        lockReasonLabel.text = enabled ? "" : SingleLine(lockReason);
        lockReasonLabel.gameObject.SetActive(!enabled && !string.IsNullOrEmpty(lockReasonLabel.text));
        button.interactable = enabled;
        visibility.alpha = enabled ? 1 : disabledAlpha;
        selected = onSelected;
        hovered = onHovered;
        SetFocused(false);
        gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable) hovered?.Invoke();
    }

    public void Clear()
    {
        selected = null;
        hovered = null;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Select);
    }
}
