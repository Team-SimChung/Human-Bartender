using TMPro;
using UnityEngine;

/// <summary>레시피 미리보기에서 태그 하나의 텍스트와 너비를 표시한다.</summary>
public sealed class RecipeTagChip : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float minimumWidth = 45f;
    [SerializeField] private float horizontalPadding = 16f;

    public void Bind(string value)
    {
        if (root == null || label == null)
        {
            Debug.LogError("[RecipeTagChip] Root와 Label을 프리팹 인스펙터에 연결해 주세요.", this);
            return;
        }

        label.text = value;
        float width = Mathf.Max(minimumWidth,
            label.GetPreferredValues(value, float.PositiveInfinity, 20f).x + horizontalPadding);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }
}
