using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIIngredientPopupView : UIPopupView
{
    [SerializeField] NewShelfItemDataSO shelfData;
    [SerializeField] CocktailRecipeVisualCatalog visuals;
    [SerializeField] TMP_Text ingredientName;
    [SerializeField] Image ingredientIcon;

    protected override bool Prepare(string id)
    {
        if (shelfData == null || visuals == null || ingredientName == null || ingredientIcon == null)
            return false;
        if (!shelfData.TryGet(id, out var item) || !item.IsIngredient) return false;
        var sprite = visuals.GetIngredientSprite(id);
        if (sprite == null)
        {
            Debug.LogWarning($"[Popup] Ingredient sprite not found: {id}", this);
            return false;
        }
        ingredientName.text = string.IsNullOrWhiteSpace(item.Name.Ko) ? item.Id : item.Name.Ko;
        ingredientIcon.sprite = sprite;
        return true;
    }
}
