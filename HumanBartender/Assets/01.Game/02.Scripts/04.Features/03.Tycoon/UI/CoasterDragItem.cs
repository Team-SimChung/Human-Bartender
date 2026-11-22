using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>공급함의 코스터를 포인터로 운반한다. 성공한 자리에는 월드 프리팹의 코스터가 남는다.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public class CoasterDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Canvas rootCanvas;
    public static CoasterDragItem Active { get; private set; }

    RectTransform rect;
    CanvasGroup group;
    Image image;
    RectTransform tray;
    RectTransform held;

    void Awake()
    {
        rect = (RectTransform)transform;
        tray = (RectTransform)rect.parent;
        group = GetComponent<CanvasGroup>();
        image = GetComponent<Image>();
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        ExpandTrayHitArea();
    }

    void ExpandTrayHitArea()
    {
        var corners = new Vector3[4];
        tray.GetWorldCorners(corners);
        Vector3 min = rect.InverseTransformPoint(corners[0]);
        Vector3 max = rect.InverseTransformPoint(corners[2]);
        Rect own = rect.rect;
        Vector4 padding = image.raycastPadding;
        image.raycastPadding = new Vector4(Mathf.Min(padding.x, min.x - own.xMin),
            Mathf.Min(padding.y, min.y - own.yMin), Mathf.Min(padding.z, own.xMax - max.x),
            Mathf.Min(padding.w, own.yMax - max.y));
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (Active != null || DrinkDragItem.Active != null) return;
        Active = this;
        var icon = new GameObject("Held Coaster", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        held = icon.rectTransform;
        held.SetParent(rootCanvas.transform, false);
        held.sizeDelta = rect.rect.size;
        icon.sprite = image.sprite;
        icon.color = image.color;
        icon.preserveAspect = image.preserveAspect;
        icon.raycastTarget = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        OnDrag(data);
    }

    public void OnDrag(PointerEventData data)
    {
        if (held == null) return;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rootCanvas.transform,
                data.position, camera, out Vector3 world)) held.position = world;
    }

    public void OnEndDrag(PointerEventData data) => ReturnToTray();
    void OnDisable() => ReturnToTray();

    void ReturnToTray()
    {
        if (Active != this) return;
        Active = null;
        if (held != null)
        {
            held.gameObject.SetActive(false);
            Destroy(held.gameObject);
            held = null;
        }
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }
}
