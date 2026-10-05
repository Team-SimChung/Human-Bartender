using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Scrollbar))]
[DefaultExecutionOrder(100)]
/// <summary>
/// Scrollbar 핸들을 value 위치 기반으로 배치하고 크기를 네이티브 스프라이트 크기로 고정하는 컴포넌트.
/// Awake 시 HandleDragHandler를 핸들에 자동 부착하여 점프 없는 드래그를 지원한다.
/// </summary>
public class PositionBasedScrollbar : MonoBehaviour
{
    Scrollbar sb;
    RectTransform handleRect;
    Vector2 nativeSize;

    void Awake()
    {
        sb = GetComponent<Scrollbar>();
        handleRect = sb.handleRect;
        var slidingArea = handleRect.parent as RectTransform;

        var img = handleRect.GetComponent<Image>();
        if (img != null)
        {
            img.SetNativeSize();
            img.raycastTarget = true; // 클릭 받아야 함
        }
        nativeSize = handleRect.rect.size;

        // 핸들에 드래그 핸들러 자동 부착
        var drag = handleRect.GetComponent<HandleDragHandler>();
        if (drag == null) drag = handleRect.gameObject.AddComponent<HandleDragHandler>();
        drag.Setup(sb, slidingArea);
    }

    void LateUpdate()
    {
        float t = sb.value;
        if (sb.direction == Scrollbar.Direction.TopToBottom ||
            sb.direction == Scrollbar.Direction.RightToLeft)
            t = 1f - t;

        bool vertical = sb.direction == Scrollbar.Direction.BottomToTop ||
                        sb.direction == Scrollbar.Direction.TopToBottom;

        if (vertical)
        {
            handleRect.anchorMin = new Vector2(0.5f, t);
            handleRect.anchorMax = new Vector2(0.5f, t);
            handleRect.pivot = new Vector2(0.5f, t);
        }
        else
        {
            handleRect.anchorMin = new Vector2(t, 0.5f);
            handleRect.anchorMax = new Vector2(t, 0.5f);
            handleRect.pivot = new Vector2(t, 0.5f);
        }

        handleRect.sizeDelta = nativeSize;
        handleRect.anchoredPosition = Vector2.zero;
    }
}