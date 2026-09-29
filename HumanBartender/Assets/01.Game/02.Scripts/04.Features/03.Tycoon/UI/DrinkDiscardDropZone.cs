using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>완성 잔을 비운다. 주문 취소와 서빙 정산은 호출하지 않는다.</summary>
public sealed class DrinkDiscardDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag;
        if (dragged != null && dragged.TryGetComponent(out DrinkDragItem item))
            item.MarkDiscarded();
    }
}
