using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>The story lesson accepts the existing coaster tray without a tycoon GuestSlot.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
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
        GetComponent<Image>().raycastTarget = false;
        onPlaced?.Invoke();
        onPlaced = null;
    }
}
