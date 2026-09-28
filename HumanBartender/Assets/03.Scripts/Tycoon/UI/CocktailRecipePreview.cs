using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>호버한 칵테일의 설명과 재료를 오른쪽 카드에 표시한다.</summary>
public sealed class CocktailRecipePreview : MonoBehaviour
{
    [Header("Text and image")]
    [SerializeField] private TextMeshProUGUI englishNameText;
    [SerializeField] private TextMeshProUGUI cocktailNameText;
    [SerializeField] private Image cocktailImage;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI recipeText;

    [Header("Dynamic contents")]
    [SerializeField] private RectTransform tagList;
    [SerializeField] private RectTransform ingredientList;
    [SerializeField] private NewShelfItemDataSO shelfData;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Color tagBackgroundColor = new Color(0.08f, 0.16f, 0.23f, 1f);
    [SerializeField] private Color ingredientBackgroundColor = new Color(0.10f, 0.18f, 0.26f, 1f);

    public void Show(NewCocktailData cocktail, CocktailRecipeVisualCatalog visuals)
    {
        gameObject.SetActive(true);

        if (englishNameText != null) englishNameText.text = cocktail.Name.En;
        if (cocktailNameText != null) cocktailNameText.text = cocktail.Name.Ko;
        if (descriptionText != null) descriptionText.text = cocktail.Flavor.Ko;
        if (recipeText != null) recipeText.text = cocktail.RecipeDesc.Ko;

        if (cocktailImage != null)
        {
            Sprite sprite = visuals != null ? visuals.GetCocktailSprite(cocktail.Id) : null;
            cocktailImage.sprite = sprite;
            cocktailImage.enabled = sprite != null;
            cocktailImage.preserveAspect = true;
        }

        PopulateTags(cocktail);
        PopulateIngredients(cocktail, visuals);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void PopulateTags(NewCocktailData cocktail)
    {
        if (tagList == null) return;
        ClearChildren(tagList);

        AddTag(cocktail.Mix.ToString().ToUpperInvariant());
        if (cocktail.Tags == null) return;

        foreach (NewCocktailTag tag in cocktail.Tags)
        {
            if (!string.IsNullOrWhiteSpace(tag.Ko)) AddTag(tag.Ko);
        }
    }

    private void AddTag(string value)
    {
        var chip = new GameObject("Tag " + value, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        chip.transform.SetParent(tagList, false);

        Image background = chip.GetComponent<Image>();
        background.color = tagBackgroundColor;
        background.raycastTarget = false;

        var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(chip.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.fontSize = 11f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = value;

        LayoutElement layout = chip.GetComponent<LayoutElement>();
        layout.preferredWidth = Mathf.Max(38f, label.GetPreferredValues(value).x + 16f);
        layout.preferredHeight = 20f;
    }

    private void PopulateIngredients(NewCocktailData cocktail, CocktailRecipeVisualCatalog visuals)
    {
        if (ingredientList == null) return;
        ClearChildren(ingredientList);
        if (cocktail.Recipe == null) return;

        var added = new HashSet<string>();
        foreach (NewCocktailRecipeStep step in cocktail.Recipe)
        {
            if (string.IsNullOrEmpty(step.Ingredient) || !added.Add(step.Ingredient)) continue;
            AddIngredient(step.Ingredient, visuals);
        }
    }

    private void AddIngredient(string id, CocktailRecipeVisualCatalog visuals)
    {
        var cell = new GameObject("Ingredient " + id, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        cell.transform.SetParent(ingredientList, false);

        Image background = cell.GetComponent<Image>();
        background.color = ingredientBackgroundColor;
        background.raycastTarget = false;

        LayoutElement layout = cell.GetComponent<LayoutElement>();
        layout.preferredWidth = 48f;
        layout.preferredHeight = 48f;

        Sprite sprite = visuals != null ? visuals.GetIngredientSprite(id) : null;
        if (sprite != null)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(cell.transform, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);

            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return;
        }

        var textObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(cell.transform, false);
        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(2f, 2f);
        textRect.offsetMax = new Vector2(-2f, -2f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontAsset;
        text.fontSize = 9f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = shelfData != null && shelfData.TryGet(id, out NewShelfItemData item)
            ? item.Name.Ko : id;
    }

    private static void ClearChildren(RectTransform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            GameObject child = parent.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }
}
