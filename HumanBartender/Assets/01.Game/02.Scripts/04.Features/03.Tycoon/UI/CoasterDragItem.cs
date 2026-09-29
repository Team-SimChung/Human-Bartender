using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 우측 하단 코스터 인벤토리에 놓인 코스터 아이콘 하나. uGUI 화면 좌표 기준으로 드래그하며,
/// CoasterDropZone 위에 놓이면 그 자리(손님 앞)에 고정되어 더 이상 드래그되지 않고, 그 외의 곳에
/// 놓이면 원래 자리로 되돌아간다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class CoasterDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Canvas rootCanvas; // 드래그 중 다른 UI 위로 그려지도록 옮겨갈 최상위 Canvas

    RectTransform rect;
    CanvasGroup canvasGroup;
    Transform originalParent;
    Vector2 originalAnchoredPosition;
    bool droppedOnValidZone;
    RectTransform placedParent;
    Vector2 placedOffset;
    bool isPlaced;

    // 트레이에서의 제자리. 손님 앞에 놓을 때 부모와 anchor/pivot을 통째로 바꾸기 때문에,
    // 되돌리려면 시작할 때의 값을 그대로 들고 있어야 한다.
    Transform trayParent;
    Vector2 trayAnchoredPosition;
    Vector2 trayAnchorMin;
    Vector2 trayAnchorMax;
    Vector2 trayPivot;

    void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();

        trayParent = rect.parent;
        trayAnchoredPosition = rect.anchoredPosition;
        trayAnchorMin = rect.anchorMin;
        trayAnchorMax = rect.anchorMax;
        trayPivot = rect.pivot;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isPlaced) return;

        droppedOnValidZone = false;
        originalParent = rect.parent;
        originalAnchoredPosition = rect.anchoredPosition;

        rect.SetParent(rootCanvas.transform, true);
        canvasGroup.blocksRaycasts = false; // 드롭존이 자기 자신에게 가려지지 않도록
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPlaced) return;

        rect.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPlaced) return;

        canvasGroup.blocksRaycasts = true;

        if (droppedOnValidZone)
        {
            rect.SetParent(placedParent, false);

            // 트레이 아이콘의 anchor/pivot(우측 하단 등)이 그대로 남아있으면 드롭존 안에서 한쪽으로 치우쳐 보이므로,
            // 중앙 기준으로 재설정한 뒤 offset을 적용한다.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = placedOffset;

            isPlaced = true;
            return;
        }

        rect.SetParent(originalParent, true);
        rect.anchoredPosition = originalAnchoredPosition;
    }

    /// <summary>
    /// CoasterDropZone.OnDrop에서 배치가 성공했을 때 호출한다. dropZoneRect의 중앙을 기준으로
    /// offset만큼 떨어진 위치(손님 앞)에 고정된다. offset은 CoasterDropZone 인스펙터에서 조정할 수 있다.
    /// </summary>
    public void PlaceAt(RectTransform dropZoneRect, Vector2 offset)
    {
        droppedOnValidZone = true;
        placedParent = dropZoneRect;
        placedOffset = offset;
    }

    /// <summary>
    /// 손님이 자리를 떠 놓여 있던 코스터를 치울 때 CoasterDropZone에서 호출한다.
    ///
    /// 없애지 않고 트레이의 제자리로 되돌린다. 코스터는 쓰고 버리는 물건이 아니라 치웠다가 다시 쓰는
    /// 물건이고, 되돌리지 않으면 손님을 트레이에 놓인 개수만큼만 받고 나서 트레이가 비어 아무도
    /// 응대할 수 없게 된다.
    /// </summary>
    public void ReturnToTray()
    {
        isPlaced = false;
        droppedOnValidZone = false;

        rect.SetParent(trayParent, false);
        rect.anchorMin = trayAnchorMin;
        rect.anchorMax = trayAnchorMax;
        rect.pivot = trayPivot;
        rect.anchoredPosition = trayAnchoredPosition;

        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
    }
}
