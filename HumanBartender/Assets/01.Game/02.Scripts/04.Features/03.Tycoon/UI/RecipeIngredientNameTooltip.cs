using TMPro;
using UnityEngine;

/// <summary>재료 이름을 커서 근처에 표시하는 말풍선이다.</summary>
public sealed class RecipeIngredientNameTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Bubble Size")]
    [Tooltip("말풍선의 최소 너비와 높이. 너비는 배경의 좌우 테두리 합보다 작지 않게 설정합니다.")]
    [SerializeField] private Vector2 minimumSize = new Vector2(16f, 24f);
    [Tooltip("이 너비를 넘는 이름은 줄바꿈합니다.")]
    [SerializeField, Min(1f)] private float maximumWidth = 160f;
    [Tooltip("커서에서 말풍선까지의 간격.")]
    [SerializeField, Min(0f)] private float cursorGap = 15f;

    public void Show(string name, Vector2 pointerPosition, RectTransform canvasRect)
    {
        if (bubbleRect == null || nameText == null)
        {
            Debug.LogError("[RecipeIngredientNameTooltip] Bubble Rect와 Name Text를 연결해 주세요.", this);
            return;
        }

        nameText.text = name ?? string.Empty;
        // Label stretches inside the bubble. Its authored offsets provide the outer padding.
        Vector2 padding = -nameText.rectTransform.sizeDelta;
        float preferredWidth = nameText.GetPreferredValues(nameText.text).x + padding.x;
        float width = Mathf.Clamp(Mathf.Ceil(preferredWidth), minimumSize.x, Mathf.Max(minimumSize.x, maximumWidth));
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float preferredHeight = nameText.GetPreferredValues(nameText.text, Mathf.Max(1f, width - padding.x), 0f).y + padding.y;
        float height = Mathf.Max(minimumSize.y, Mathf.Ceil(preferredHeight));
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        gameObject.SetActive(true);
        Move(pointerPosition, canvasRect);
    }

    public void Move(Vector2 pointerPosition, RectTransform canvasRect)
    {
        if (!gameObject.activeSelf || bubbleRect == null || canvasRect == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, null, out Vector2 localPoint))
            return;

        Rect canvasBounds = canvasRect.rect;
        Rect bubbleBounds = bubbleRect.rect;
        localPoint.x += bubbleBounds.width * 0.5f + cursorGap;
        localPoint.y -= bubbleBounds.height * 0.5f + cursorGap;

        float minX = canvasBounds.xMin + bubbleBounds.width * bubbleRect.pivot.x;
        float maxX = canvasBounds.xMax - bubbleBounds.width * (1f - bubbleRect.pivot.x);
        float minY = canvasBounds.yMin + bubbleBounds.height * bubbleRect.pivot.y;
        float maxY = canvasBounds.yMax - bubbleBounds.height * (1f - bubbleRect.pivot.y);
        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        Vector2 anchorPoint = new Vector2(
            Mathf.Lerp(canvasBounds.xMin, canvasBounds.xMax, bubbleRect.anchorMin.x),
            Mathf.Lerp(canvasBounds.yMin, canvasBounds.yMax, bubbleRect.anchorMin.y));
        bubbleRect.anchoredPosition = localPoint - anchorPoint;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
