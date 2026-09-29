using System.Collections.Generic;
using UnityEngine;

/// <summary>완성 잔을 표시하고 드래그 UI 인스턴스를 재사용한다.</summary>
public class ServeTrayUI : MonoBehaviour
{
    [SerializeField] private CraftFlowController craftFlow;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform trayRoot;
    [SerializeField] private RectTransform poolRoot;
    [SerializeField] private DrinkDragItem drinkPrefab;

    private readonly List<(DrinkDragItem item, int frame)> cachedItems = new();
    private readonly List<DrinkDragItem> ownedItems = new();
    private readonly List<DrinkDragItem> items = new();

    public int Count { get { return items.Count; } }

    private void Awake()
    {
        if (rootCanvas == null || trayRoot == null || poolRoot == null || drinkPrefab == null)
        {
            Debug.LogError("[ServeTray] Canvas, Tray Root, Pool Root, Drink Prefab을 연결해 주세요.", this);
            return;
        }

        poolRoot.gameObject.SetActive(false);
        ReturnItem(CreateItem());
    }

    private void OnEnable()
    {
        if (trayRoot != null) trayRoot.gameObject.SetActive(true);
        foreach (DrinkDragItem item in items)
            if (item != null) item.gameObject.SetActive(true);

        if (craftFlow == null)
        {
            Debug.LogError("[ServeTray] Craft Flow를 연결해 주세요.", this);
            return;
        }

        craftFlow.DrinkReady += AddDrink;
        CraftedDrink pending = craftFlow.PendingDrink;
        if (pending != null && !ContainsDrink(pending)) AddDrink(pending);
    }

    private void OnDisable()
    {
        foreach (DrinkDragItem item in items.ToArray())
            if (item != null) item.gameObject.SetActive(false);
        if (trayRoot != null) trayRoot.gameObject.SetActive(false);
        if (craftFlow != null) craftFlow.DrinkReady -= AddDrink;
    }

    private bool ContainsDrink(CraftedDrink drink)
    {
        foreach (DrinkDragItem item in items)
            if (item != null && ReferenceEquals(item.Drink, drink)) return true;
        return false;
    }

    public void AddDrink(CraftedDrink drink)
    {
        if (trayRoot == null || drink == null || ContainsDrink(drink)) return;

        DrinkDragItem item = TakeItem();
        if (item == null) return;
        item.transform.SetParent(trayRoot, false);
        item.Initialize(drink, rootCanvas, ReturnItem);
        item.name = $"Drink {drink.CocktailId}";
        item.Removed += OnItemRemoved;
        items.Add(item);
        item.gameObject.SetActive(true);

        craftFlow?.RefreshCraftAvailability();
        Debug.Log($"[ServeTray] {drink.DisplayName} 완성 — 트레이에 올렸습니다. (총 {items.Count}잔)");
    }

    /// <summary>폐기 버튼에서 호출한다. 먼저 완성된 음료부터 한 잔씩 폐기한다.</summary>
    public void DiscardDrink()
    {
        if (!isActiveAndEnabled) return;
        foreach (DrinkDragItem item in items)
        {
            if (item == null || item.Drink == null) continue;
            item.MarkDiscarded();
            return;
        }
    }

    private void OnItemRemoved(DrinkDragItem item, DrinkRemovalReason reason)
    {
        craftFlow?.ConsumeDrink(item.Drink);
        item.Removed -= OnItemRemoved;
        items.Remove(item);
        craftFlow?.RefreshCraftAvailability();
    }

    private DrinkDragItem CreateItem()
    {
        if (drinkPrefab == null || trayRoot == null) return null;
        DrinkDragItem item = Instantiate(drinkPrefab, trayRoot);
        ownedItems.Add(item);
        return item;
    }

    private DrinkDragItem TakeItem()
    {
        for (int i = cachedItems.Count - 1; i >= 0; i--)
        {
            var cached = cachedItems[i];
            if (cached.frame >= Time.frameCount) continue;
            cachedItems.RemoveAt(i);
            if (cached.item != null) return cached.item;
        }
        return CreateItem();
    }

    private void ReturnItem(DrinkDragItem item)
    {
        if (item == null || poolRoot == null) return;
        item.gameObject.SetActive(false);
        item.transform.SetParent(poolRoot, false);
        cachedItems.Add((item, Time.frameCount));
    }

    private void OnDestroy()
    {
        foreach (DrinkDragItem item in ownedItems)
            if (item != null) Destroy(item.gameObject);
    }
}
