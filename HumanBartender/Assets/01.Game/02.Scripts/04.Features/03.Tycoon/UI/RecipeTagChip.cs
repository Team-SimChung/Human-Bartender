using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>레시피 태그의 텍스트·너비와 카테고리 배경색을 표시한다.</summary>
public sealed class RecipeTagChip : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;
    [SerializeField] private float minimumWidth = 45f;
    [SerializeField] private float horizontalPadding = 16f;

    private bool defaultStyleCaptured;
    private Color defaultBackgroundColor, defaultTextColor;

    public void Bind(string value, Color? categoryColor = null)
    {
        if (root == null || label == null)
        {
            Debug.LogError("[RecipeTagChip] Root와 Label을 프리팹 인스펙터에 연결해 주세요.", this);
            return;
        }

        label.text = value;
        ApplyColor(categoryColor);
        float width = Mathf.Max(minimumWidth,
            label.GetPreferredValues(value, float.PositiveInfinity, 20f).x + horizontalPadding);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void ApplyColor(Color? categoryColor)
    {
        if (background == null) return;
        if (!defaultStyleCaptured)
        {
            defaultBackgroundColor = background.color;
            defaultTextColor = label.color;
            defaultStyleCaptured = true;
        }

        if (!categoryColor.HasValue)
        {
            background.color = defaultBackgroundColor;
            label.color = defaultTextColor;
            return;
        }

        // 프리팹의 테두리 이미지와 투명 영역은 유지하고 색만 적용한다.
        Color color = categoryColor.Value;
        background.color = color;
        label.color = color;
    }
}
