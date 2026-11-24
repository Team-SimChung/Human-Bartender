using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레시피 태그 하나의 글자·너비·색을 표시한다. 도구명 태그(SHAKE·BUILD·STIR)만 강조색이고 맛·분위기·도수
/// 태그는 모두 기본색이다. 외곽선 이미지와 글자를 같은 색으로 칠한다(웹 preparation-resources.css .recipe-tags).
/// 제조 준비 화면과 바 레시피 브라우저가 이 프리팹 하나를 같이 쓰므로 색은 여기에만 둔다.
/// </summary>
public sealed class RecipeTagChip : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("흰 외곽선 이미지. 글자와 같은 색으로 칠한다.")]
    [SerializeField] private Image background;
    [Tooltip("태그가 차지하는 최소 너비와 높이입니다. 글자 여백은 Label의 RectTransform에서 조절합니다.")]
    [SerializeField] private Vector2 minimumSize = new Vector2(44f, 18f);

    [Header("Colors")]
    [Tooltip("도구명 태그(SHAKE·BUILD·STIR 등)의 글자·외곽선 색. 웹 #dcb076.")]
    [SerializeField] private Color toolColor = new Color32(0xDC, 0xB0, 0x76, 0xFF);
    [Tooltip("맛·분위기·도수 등 나머지 태그의 글자·외곽선 색. 웹 #6281a0.")]
    [SerializeField] private Color defaultColor = new Color32(0x62, 0x81, 0xA0, 0xFF);

    public Color ToolColor => toolColor;
    public Color DefaultColor => defaultColor;

    public void Bind(string value, bool isToolTag, float maximumWidth = float.PositiveInfinity)
    {
        if (root == null || label == null)
        {
            Debug.LogError("[RecipeTagChip] Root와 Label을 프리팹 인스펙터에 연결해 주세요.", this);
            return;
        }

        label.text = value;
        Color color = isToolTag ? toolColor : defaultColor;
        label.color = color;
        if (background != null) background.color = color;
        Vector2 padding = -label.rectTransform.sizeDelta;
        float width = Mathf.Min(maximumWidth, Mathf.Max(minimumSize.x,
            Mathf.Ceil(label.GetPreferredValues(value).x + padding.x)));
        float textHeight = label.GetPreferredValues(value, Mathf.Max(1f, width - padding.x), float.PositiveInfinity).y;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(minimumSize.y, Mathf.Ceil(textHeight + padding.y)));
    }
}
