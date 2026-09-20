using System;
using UnityEngine;
using UnityEngine.EventSystems;

public enum DrinkRemovalReason { Served, Discarded }

/// <summary>완성 잔의 드래그와 소비 상태. UI는 반환하고 제조 데이터는 재사용하지 않는다.</summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class DrinkDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform rect;
    CanvasGroup canvasGroup;
    Canvas rootCanvas;
    Transform trayParent;
    int trayIndex;
    bool consumed;
    bool dragging;
    bool released;
    Action<DrinkDragItem> release;

    public CraftedDrink Drink { get; private set; }
    public event Action<DrinkDragItem> Served;
    public event Action<DrinkDragItem, DrinkRemovalReason> Removed;

    void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(CraftedDrink drink, Canvas canvas, Action<DrinkDragItem> returnToPool = null)
    {
        if (rect == null) rect = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        Drink = drink;
        rootCanvas = canvas;
        release = returnToPool;
        consumed = dragging = released = false;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        trayParent = rect.parent;
        trayIndex = rect.GetSiblingIndex();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = Vector2.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Drink == null || consumed || released || rootCanvas == null) return;
        dragging = true;
        trayParent = rect.parent;
        trayIndex = rect.GetSiblingIndex();
        rect.SetParent(rootCanvas.transform, true);
        rect.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging && !consumed)
            rect.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData) => EndDrag();

    void OnDisable()
    {
        if (dragging) EndDrag();
    }

    void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        StoryServeDropTarget.SetRaycastEnabledWhileDragging(false);
        canvasGroup.blocksRaycasts = true;
        if (consumed) Release();
        else if (trayParent != null)
        {
            rect.SetParent(trayParent, false);
            rect.SetSiblingIndex(trayIndex);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    public void MarkServed() => Remove(DrinkRemovalReason.Served);
    public void MarkDiscarded() => Remove(DrinkRemovalReason.Discarded);

    void Remove(DrinkRemovalReason reason)
    {
        if (Drink == null || consumed || released) return;
        consumed = true;
        if (reason == DrinkRemovalReason.Discarded) Drink.Session.Discard();
        try
        {
            Removed?.Invoke(this, reason);
            if (reason == DrinkRemovalReason.Served) Served?.Invoke(this);
        }
        finally
        {
            if (!dragging) Release();
        }
    }

    void Release()
    {
        if (released) return;
        released = true;
        var returnToPool = release;
        release = null;
        Drink = null;
        Served = null;
        Removed = null;
        if (returnToPool != null) returnToPool(this);
        else Destroy(gameObject);
    }
}
