using UnityEngine;
using UnityEngine.UI;

/// <summary>두 외형 모드가 공유하는 꼬리. 추적 요청과 설정으로 최종 위치/반전/크기를 한 번만 결정한다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class SpeechBubbleTail : MonoBehaviour
{
    [Header("Attachment")]
    [SerializeField, Range(0, 1)] float horizontalAnchor = .5f;
    [SerializeField] float horizontalOffset;
    [SerializeField] bool pointUp;
    [Tooltip("Point Up과 별도로 꼬리 이미지만 좌우 반전합니다. Scale X의 부호는 이 설정으로 결정됩니다.")]
    [SerializeField] bool flipX;
    [SerializeField, Min(0)] float trackingEdgeMargin = 12;

    [Header("Single Image")]
    [SerializeField] Sprite singleImageSprite;
    [SerializeField] Vector2 singleImageSize = new Vector2(8.5f, 8.5f);
    [SerializeField] float singleImageJoinDepth;

    [Header("Parts")]
    [SerializeField] Sprite partsSprite;
    [SerializeField, Min(0)] float partsHeight = 8;
    [Tooltip("본체 테두리 안으로 들어가는 깊이. 본체는 이 연결 구간만 비웁니다.")]
    [SerializeField, Min(0)] float partsJoinDepth = .5f;

    Image tailImage;
    bool hasTarget;
    Vector3 targetPosition;
    bool targetUpward;

    public bool IsConfigured => singleImageSprite != null && partsSprite != null;

    /// <param name="canvasWorldPosition">Canvas 좌표를 TransformPoint로 변환한 화자의 위치.</param>
    public void SetTarget(Vector3 canvasWorldPosition, bool upward)
    {
        hasTarget = true;
        targetPosition = canvasWorldPosition;
        targetUpward = upward;
    }

    public void ClearTarget() => hasTarget = false;
    void OnDisable() => ClearTarget();

    /// <returns>본체의 왼쪽 아래를 원점으로 하는 최종 연결 영역.</returns>
    public Connection Refresh(RectTransform body, bool useParts, Vector2 cornerSize)
    {
        // 편집 중 아직 설정되지 않은 컴포넌트가 기존 Image의 원본을 지우지 않게 한다.
        if (singleImageSprite == null) return default;
        if (tailImage == null) tailImage = GetComponent<Image>();
        Sprite sprite = useParts ? partsSprite : singleImageSprite;
        if (tailImage.sprite != sprite) tailImage.sprite = sprite;
        Rect area = body.rect;
        Vector2 size = singleImageSize;
        float join = singleImageJoinDepth;
        if (useParts)
        {
            float height = Mathf.Max(0, partsHeight);
            float width = sprite != null ? height * sprite.rect.width / sprite.rect.height : 0;
            float available = Mathf.Max(0, area.width - 2 * cornerSize.x);
            if (width > available) { height *= available / width; width = available; }
            size = new Vector2(width, height);
            join = Mathf.Min(partsJoinDepth, Mathf.Min(cornerSize.y, height));
        }

        bool upward = hasTarget ? targetUpward : pointUp;
        float x = hasTarget ? body.InverseTransformPoint(targetPosition).x - area.xMin :
            area.width * horizontalAnchor + horizontalOffset;
        float margin = hasTarget ? trackingEdgeMargin : 0;
        if (useParts) margin = Mathf.Max(margin, cornerSize.x + size.x * .5f);
        if (hasTarget || useParts)
            x = area.width < 2 * margin ? area.width * .5f : Mathf.Clamp(x, margin, area.width - margin);

        var rect = tailImage.rectTransform;
        Vector2 anchor = new Vector2(horizontalAnchor, upward ? 1 : 0);
        Vector2 position = new Vector2(x - area.width * horizontalAnchor, upward ? -join : join);
        Quaternion rotation = Quaternion.Euler(0, 0, upward ? 180 : 0);
        // 반전 여부는 flipX만 소유하고, 기존 배율과 위/아래 방향은 유지한다.
        Vector3 scale = rect.localScale;
        scale.x = Mathf.Abs(scale.x) * (flipX ? -1 : 1);
        if (rect.anchorMin != anchor) rect.anchorMin = anchor;
        if (rect.anchorMax != anchor) rect.anchorMax = anchor;
        if (rect.pivot != new Vector2(.5f, 1)) rect.pivot = new Vector2(.5f, 1);
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.localRotation != rotation) rect.localRotation = rotation;
        if (rect.localScale != scale) rect.localScale = scale;
        bool visible = sprite != null && size.x > .001f && size.y > .001f;
        if (tailImage.enabled != visible) tailImage.enabled = visible;

        return useParts && visible && isActiveAndEnabled ?
            new Connection(new Rect(x - size.x * .5f, upward ? area.height - join : 0, size.x, join), upward) : default;
    }

    public readonly struct Connection
    {
        public readonly Rect Area;
        public readonly bool Upward;
        public Connection(Rect area, bool upward) { Area = area; Upward = upward; }
    }
}
