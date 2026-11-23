using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>마우스를 따라다니며 카메라 화면 안에 머무는 공용 물건 설명창.</summary>
public sealed class CraftPrepItemTooltip : MonoBehaviour
{
    [SerializeField] private SpriteRenderer frame;
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro descriptionText;
    [Tooltip("540p 기준 마우스와 설명창 사이의 간격. Y가 음수이면 아래에 표시합니다.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(14f, -14f);
    [SerializeField, Min(0f)] private float screenPadding = 9f;
    [Header("Content Size (540p pixels)")]
    [SerializeField] private Vector2 contentPadding = new Vector2(11f, 8f);
    [SerializeField, Min(0f)] private float textSpacing = 4f;
    [SerializeField, Min(80f)] private float maxWidth = 218f;

    private const float ReferenceHeight = 540f;
    private Camera followCamera;
    private float cameraDepth;
    private Vector2Int viewportSize;
    private float UiScale => followCamera.pixelHeight / ReferenceHeight;

    public void Show(NewShelfItemData item, Camera camera, Vector2 pointerPosition)
    {
        string displayName = string.IsNullOrWhiteSpace(item.Name.Ko) ? item.Id : item.Name.Ko;
        Show(displayName, item.Desc.Ko, camera, pointerPosition);
    }

    public void Show(string displayName, string description, Camera camera, Vector2 pointerPosition)
    {
        if (camera == null) { Hide(); return; }
        followCamera = camera;
        cameraDepth = Mathf.Max(camera.nearClipPlane + 0.01f, camera.WorldToScreenPoint(transform.position).z);
        if (nameText != null) nameText.text = displayName;
        if (descriptionText != null) descriptionText.text = description;
        gameObject.SetActive(true);
        ResizeToContent();
        UpdatePointerPosition(pointerPosition);
    }

    private void ResizeToContent()
    {
        viewportSize = new Vector2Int(followCamera.pixelWidth, followCamera.pixelHeight);
        if (frame == null || frame.sprite == null || viewportSize.y == 0) return;
        float uiScale = UiScale;
        Vector2 padding = Vector2.Max(contentPadding, Vector2.zero) * uiScale;
        Vector2 frameScale = ScreenScale(frame.transform);
        Vector4 border = frame.sprite.border / frame.sprite.pixelsPerUnit;
        float minimumWidth = (border.x + border.z) * frameScale.x;
        float minimumHeight = (border.y + border.w) * frameScale.y;
        PrepareText(nameText);
        PrepareText(descriptionText);
        float preferredWidth = Mathf.Max(PreferredScreenWidth(nameText), PreferredScreenWidth(descriptionText));
        float availableWidth = Mathf.Max(1f, viewportSize.x - screenPadding * uiScale * 2f);
        float widthLimit = Mathf.Min(availableWidth, maxWidth * uiScale);
        float width = Mathf.Max(minimumWidth, Mathf.Min(widthLimit, preferredWidth + padding.x * 2f));
        float contentWidth = Mathf.Max(1f, width - padding.x * 2f);
        float nameHeight = ResizeText(nameText, contentWidth);
        float descriptionHeight = ResizeText(descriptionText, contentWidth);
        float gap = nameHeight > 0f && descriptionHeight > 0f ? textSpacing * uiScale : 0f;
        float height = Mathf.Max(minimumHeight, nameHeight + gap + descriptionHeight + padding.y * 2f);

        frame.size = new Vector2(width / frameScale.x, height / frameScale.y);
        Rect bounds = GetRelativeScreenBounds();
        Vector2 origin = followCamera.WorldToScreenPoint(transform.position);
        Vector2 top = origin + new Vector2(bounds.xMin + padding.x, bounds.yMax - padding.y);
        PositionText(nameText, top);
        top.y -= nameHeight + gap;
        PositionText(descriptionText, top);
    }

    private static void PrepareText(TextMeshPro text)
    {
        if (text == null) return;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
    }

    private Vector2 ScreenScale(Transform textTransform)
    {
        Vector2 origin = followCamera.WorldToScreenPoint(textTransform.position);
        float x = Vector2.Distance(origin, followCamera.WorldToScreenPoint(textTransform.TransformPoint(Vector3.right)));
        float y = Vector2.Distance(origin, followCamera.WorldToScreenPoint(textTransform.TransformPoint(Vector3.up)));
        return new Vector2(Mathf.Max(x, 0.0001f), Mathf.Max(y, 0.0001f));
    }

    private float PreferredScreenWidth(TextMeshPro text) => text == null || string.IsNullOrEmpty(text.text) ? 0f
        : text.GetPreferredValues(text.text, Mathf.Infinity, Mathf.Infinity).x * ScreenScale(text.transform).x;

    private float ResizeText(TextMeshPro text, float screenWidth)
    {
        if (text == null) return 0f;
        Vector2 scale = ScreenScale(text.transform);
        float width = screenWidth / scale.x;
        float height = string.IsNullOrEmpty(text.text) ? 0f : text.GetPreferredValues(text.text, width, Mathf.Infinity).y;
        RectTransform rect = text.rectTransform;
        rect.pivot = new Vector2(0f, 1f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 0.01f));
        return height * scale.y;
    }

    private void PositionText(TextMeshPro text, Vector2 screenTop)
    {
        if (text == null) return;
        float depth = followCamera.WorldToScreenPoint(text.transform.position).z;
        text.rectTransform.position = followCamera.ScreenToWorldPoint(new Vector3(screenTop.x, screenTop.y, depth));
        text.ForceMeshUpdate();
    }

    private void LateUpdate()
    {
        if (followCamera == null) { Hide(); return; }
        if (viewportSize.x != followCamera.pixelWidth || viewportSize.y != followCamera.pixelHeight)
            ResizeToContent();
        if (Mouse.current != null) UpdatePointerPosition(Mouse.current.position.ReadValue());
    }

    public void UpdatePointerPosition(Vector2 pointerPosition)
    {
        if (followCamera == null) return;
        Rect relativeBounds = GetRelativeScreenBounds();
        Rect viewport = followCamera.pixelRect;
        float padding = screenPadding * UiScale;
        Vector2 offset = cursorOffset * UiScale;
        float left = viewport.xMin + padding;
        float right = viewport.xMax - padding;
        float bottom = viewport.yMin + padding;
        float top = viewport.yMax - padding;
        Vector2 position = pointerPosition + offset - new Vector2(relativeBounds.xMin, relativeBounds.yMax);

        if (position.x + relativeBounds.xMax > right)
            position.x = pointerPosition.x - Mathf.Abs(offset.x) - relativeBounds.xMax;
        if (position.y + relativeBounds.yMin < bottom)
            position.y = pointerPosition.y + Mathf.Abs(offset.y) - relativeBounds.yMin;
        position.x = ClampPosition(position.x, left - relativeBounds.xMin, right - relativeBounds.xMax);
        position.y = ClampPosition(position.y, bottom - relativeBounds.yMin, top - relativeBounds.yMax);
        transform.position = followCamera.ScreenToWorldPoint(new Vector3(position.x, position.y, cameraDepth));
    }

    private Rect GetRelativeScreenBounds()
    {
        if (frame == null) return new Rect();
        Bounds bounds = frame.bounds;
        Vector2 origin = followCamera.WorldToScreenPoint(transform.position);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 world = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
            Vector2 screen = (Vector2)followCamera.WorldToScreenPoint(world) - origin;
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static float ClampPosition(float value, float min, float max) => min <= max
        ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;

    public void Hide()
    {
        followCamera = null;
        gameObject.SetActive(false);
    }
}
