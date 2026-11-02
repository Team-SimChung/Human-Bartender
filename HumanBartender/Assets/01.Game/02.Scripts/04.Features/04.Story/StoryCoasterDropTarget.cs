using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// The story lesson accepts the existing coaster tray without a tycoon GuestSlot.
/// 놓인 코스터 그림은 같은 자리의 StoryServeZoneLayout이 프리팹 위치에 표시한다.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image), typeof(StoryServeZoneLayout))]
public sealed class StoryCoasterDropTarget : MonoBehaviour, IDropHandler
{
    Action onPlaced;
    public CoasterDragItem PlacedCoaster { get; private set; }

    public void Bind(Action completed)
    {
        onPlaced = completed;
        GetComponent<Image>().raycastTarget = true;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (PlacedCoaster != null || eventData.pointerDrag == null ||
            !eventData.pointerDrag.TryGetComponent<CoasterDragItem>(out var coaster)) return;
        PlacedCoaster = coaster;
        coaster.PlaceAt((RectTransform)transform, Vector2.zero);
        var sourceImage = coaster.GetComponent<Image>();
        GetComponent<StoryServeZoneLayout>().ShowCoaster(sourceImage != null ? sourceImage.sprite : null,
            sourceImage != null ? sourceImage.color : Color.white);
        StartCoroutine(ReturnSupplyAfterDrop(coaster));
        GetComponent<Image>().raycastTarget = false;
        onPlaced?.Invoke();
        onPlaced = null;
    }

    IEnumerator ReturnSupplyAfterDrop(CoasterDragItem item)
    {
        // OnEndDrag first completes the physical drop; then the supply is available to the other seat.
        yield return null;
        if (item != null) item.ReturnToTray();
        PlacedCoaster = null;
    }
}
