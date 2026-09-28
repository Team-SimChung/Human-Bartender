using TMPro;
using UnityEngine;

/// <summary>재료 이름을 커서 근처에 표시하는 말풍선이다.</summary>
public sealed class RecipeIngredientNameTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private TextMeshProUGUI nameText;

    private const float MinimumWidth = 60f;
    private const float MaximumWidth = 160f;
    private const float HorizontalPadding = 18f;
    private const float VerticalPadding = 8f;
    private const float MinimumHeight = 20f;
    private const float CursorGap = 15f;

    public void Show(string name, Vector2 pointerPosition, RectTransform canvasRect)
    {
        if (bubbleRect == null || nameText == null)
        {
            Debug.LogError("[RecipeIngredientNameTooltip] Bubble Rect와 Name Text를 연결해 주세요.", this);
            return;
        }

        nameText.text = name ?? string.Empty;
        float width = Mathf.Clamp(nameText.GetPreferredValues(nameText.text).x + HorizontalPadding, MinimumWidth, MaximumWidth);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float height = Mathf.Max(MinimumHeight,
            nameText.GetPreferredValues(nameText.text, width - HorizontalPadding, 0f).y + VerticalPadding);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - VerticalPadding);

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
        localPoint.x += bubbleBounds.width * 0.5f + CursorGap;
        localPoint.y -= bubbleBounds.height * 0.5f + CursorGap;

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
