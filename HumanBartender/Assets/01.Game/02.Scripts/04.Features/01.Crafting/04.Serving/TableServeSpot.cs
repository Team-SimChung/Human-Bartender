using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>월드에 고정된 한 자리의 그림과 드롭 판정. 주문 승인과 대본 진행은 연결한 모드가 담당한다.</summary>
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public sealed class TableServeSpot : MonoBehaviour, IDropHandler, ICanvasRaycastFilter
{
    [SerializeField] Image shadow;
    [SerializeField] Image arrow;
    [SerializeField] Image coaster;
    [SerializeField] Image glass;

    Canvas worldCanvas;
    CocktailRecipeVisualCatalog visuals;
    Func<bool> canInteract, canPlaceCoaster, canServe;
    Action placeCoaster;
    Func<CraftedDrink, bool> serve;
    Sprite emptySprite;

    public void Initialize(Camera camera, CocktailRecipeVisualCatalog catalog, Func<bool> interaction)
    {
        worldCanvas = GetComponent<Canvas>();
        worldCanvas.worldCamera = camera;
        visuals = catalog;
        canInteract = interaction;
    }

    public void BindCoaster(Func<bool> available, Action place)
    {
        canPlaceCoaster = available;
        placeCoaster = place;
    }

    public void BindDrink(Func<bool> available, Func<CraftedDrink, bool> receive)
    {
        canServe = available;
        serve = receive;
    }

    bool AcceptsCoaster => canInteract() && canPlaceCoaster?.Invoke() == true;
    bool AcceptsDrink => canInteract() && serve != null && canServe?.Invoke() == true;

    public bool Refresh(bool visible)
    {
        worldCanvas.enabled = visible;
        bool waiting = visible && AcceptsCoaster;
        bool draggingDrink = DrinkDragItem.Active != null;
        shadow.gameObject.SetActive(waiting);
        arrow.gameObject.SetActive(waiting || (visible && draggingDrink && AcceptsDrink));
        return waiting && !draggingDrink && CoasterDragItem.Active == null;
    }

    public bool IsRaycastLocationValid(Vector2 position, Camera camera) =>
        worldCanvas != null && worldCanvas.enabled &&
        ((CoasterDragItem.Active != null && AcceptsCoaster) || (DrinkDragItem.Active != null && AcceptsDrink));

    public void OnDrop(PointerEventData data)
    {
        if (data.pointerDrag == null || !worldCanvas.enabled) return;
        if (CoasterDragItem.Active != null && data.pointerDrag == CoasterDragItem.Active.gameObject && AcceptsCoaster)
            placeCoaster();
        var item = DrinkDragItem.Active;
        if (item != null && data.pointerDrag == item.gameObject && AcceptsDrink && serve(item.Drink))
            item.MarkServed();
    }

    public void ShowCoaster() => coaster.gameObject.SetActive(true);

    public void ShowDrink(CraftedDrink drink)
    {
        emptySprite = visuals.GetTableGlassSprite(drink.GlassId);
        glass.rectTransform.sizeDelta = ((RectTransform)glass.transform.parent).rect.size;
        glass.sprite = visuals.GetTableCocktailSprite(drink.CocktailId);
        glass.color = glass.sprite == null ? drink.Color : Color.white;
        glass.gameObject.SetActive(true);
    }

    public void ShowEmptyGlass()
    {
        glass.sprite = emptySprite;
        if (emptySprite != null)
        {
            Vector2 size = emptySprite.rect.size;
            Vector2 bounds = ((RectTransform)glass.transform.parent).rect.size;
            float scale = Mathf.Min(1f, Mathf.Min(bounds.x / size.x, bounds.y / size.y));
            glass.rectTransform.sizeDelta = size * scale;
        }
        glass.color = Color.white;
        glass.gameObject.SetActive(emptySprite != null);
    }
}
