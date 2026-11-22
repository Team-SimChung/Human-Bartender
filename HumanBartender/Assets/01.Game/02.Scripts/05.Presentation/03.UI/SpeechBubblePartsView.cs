using UnityEngine;

/// <summary>확정된 Bubble Rect 안에 파츠를 배치한다. 텍스트 측정이나 화자 추적 상태를 소유하지 않는다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
public sealed class SpeechBubblePartsView : MonoBehaviour
{
    [Header("Body Parts")]
    [SerializeField] SpeechBubblePartGraphic topLeft;
    [SerializeField] SpeechBubblePartGraphic top;
    [SerializeField] SpeechBubblePartGraphic topRight;
    [SerializeField] SpeechBubblePartGraphic left;
    [SerializeField] SpeechBubblePartGraphic center;
    [SerializeField] SpeechBubblePartGraphic right;
    [SerializeField] SpeechBubblePartGraphic bottomLeft;
    [SerializeField] SpeechBubblePartGraphic bottom;
    [SerializeField] SpeechBubblePartGraphic bottomRight;

    [Header("Geometry (UI units)")]
    [SerializeField] Vector2 cornerSize = new Vector2(8, 8);

    public Vector2 EffectiveCornerSize { get; private set; }

    public bool IsConfigured => HasSprite(topLeft) && HasSprite(top) && HasSprite(topRight) &&
        HasSprite(left) && HasSprite(center) && HasSprite(right) && HasSprite(bottomLeft) &&
        HasSprite(bottom) && HasSprite(bottomRight);

    static bool HasSprite(SpeechBubblePartGraphic part) => part != null && part.Sprite != null;

    public void Refresh()
    {
        if (!IsConfigured) return;
        var root = (RectTransform)transform;
        Rect area = root.rect;
        // 정상 범위에서는 모서리 크기를 고정한다. 수동으로 너무 작게 만든 Rect도 뒤집히지 않게 제한한다.
        float w = Mathf.Max(0, area.width), h = Mathf.Max(0, area.height);
        float scale = Mathf.Min(1, Mathf.Min(w / Mathf.Max(.001f, cornerSize.x * 2), h / Mathf.Max(.001f, cornerSize.y * 2)));
        float cx = Mathf.Max(0, cornerSize.x * scale), cy = Mathf.Max(0, cornerSize.y * scale);
        EffectiveCornerSize = new Vector2(cx, cy);
        float innerW = Mathf.Max(0, w - 2 * cx), innerH = Mathf.Max(0, h - 2 * cy);

        Place(topLeft, 0, h - cy, cx, cy);
        Place(top, cx, h - cy, innerW, cy);
        Place(topRight, w - cx, h - cy, cx, cy);
        Place(left, 0, cy, cx, innerH);
        Place(center, cx, cy, innerW, innerH);
        Place(right, w - cx, cy, cx, innerH);
        Place(bottomLeft, 0, 0, cx, cy);
        Place(bottom, cx, 0, innerW, cy);
        Place(bottomRight, w - cx, 0, cx, cy);
    }

    public void SetTailConnection(int index, SpeechBubbleTail.Connection connection)
    {
        // 꼬리의 최종 연결 영역만 소비한다. 위치/반전 규칙은 여기서 다시 계산하지 않는다.
        Rect topCutout = default, bottomCutout = default;
        Rect gap = connection.Area;
        if (gap.width > 0 && gap.height > 0)
        {
            gap.x -= EffectiveCornerSize.x;
            if (connection.Upward)
            {
                gap.y -= ((RectTransform)transform).rect.height - EffectiveCornerSize.y;
                topCutout = gap;
            }
            else bottomCutout = gap;
        }
        top.SetCutout(index, topCutout);
        bottom.SetCutout(index, bottomCutout);
    }

    static void Place(SpeechBubblePartGraphic part, float x, float y, float width, float height)
        => SetRect(part.rectTransform, new Vector2(x, y), new Vector2(width, height), Vector2.zero);

    static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
    {
        if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
        if (rect.anchorMax != Vector2.zero) rect.anchorMax = Vector2.zero;
        if (rect.pivot != pivot) rect.pivot = pivot;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
    }
}
