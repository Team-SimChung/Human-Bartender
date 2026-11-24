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
    [Tooltip("태그 색(도구명 강조색·기본색)은 이 프리팹이 가진다.")]
    [SerializeField] private RecipeTagChip tagPrefab;
    [SerializeField] private RectTransform ingredientList;
    [SerializeField] private RecipeIngredientCell ingredientPrefab;
    [SerializeField] private NewShelfItemDataSO shelfData;
    [Tooltip("선반 데이터에 없는 병따개 칸의 이름(마우스를 올리면 보이는 이름). 병을 따야 하는 재료가 있는 레시피에만 칸이 생긴다.")]
    [SerializeField] private string bottleOpenerName = CraftPrepStageScreen.DefaultBottleOpenerName;

    [Header("Scrollable layout")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private GridLayoutGroup ingredientGrid;
    [SerializeField] private RectTransform descriptionSection;
    [SerializeField] private RectTransform recipeSection;
    [Tooltip("연결하면 태그를 여러 줄에 배치하고 칵테일 이미지와 재료 영역을 아래로 이동합니다.")]
    [SerializeField] private RectTransform cocktailImageFrame;
    [Tooltip("태그 사이의 가로·세로 간격입니다.")]
    [SerializeField] private Vector2 tagSpacing = new Vector2(4f, 4f);
    [Tooltip("재료, 설명, 제조법 영역 사이의 간격입니다.")]
    [SerializeField, Min(0f)] private float sectionGap = 12f;
    [Tooltip("설명과 제조법 글 아래의 여백입니다.")]
    [SerializeField, Min(0f)] private float textBottomInset = 10f;
    [Tooltip("스크롤 내용 끝의 여백입니다.")]
    [SerializeField, Min(0f)] private float contentBottomInset = 20f;

    [Header("Ingredient name tooltip")]
    [SerializeField] private RectTransform tooltipCanvas;
    [SerializeField] private RecipeIngredientNameTooltip tooltipPrefab;

    private readonly List<RecipeTagChip> tagChips = new List<RecipeTagChip>();
    private readonly List<RecipeIngredientCell> ingredientCells = new List<RecipeIngredientCell>();
    private RecipeIngredientNameTooltip tooltip;
    private RecipeIngredientCell hoveredIngredient;
    private float ingredientTop;
    private float minimumDescriptionTop;
    private float initialTagHeight;
    private float initialImageTop;
    private Vector2 initialImagePosition;

    private void Awake()
    {
        if (ingredientList != null) ingredientTop = -ingredientList.anchoredPosition.y;
        if (descriptionSection != null) minimumDescriptionTop = -descriptionSection.anchoredPosition.y;
        if (tagList != null) initialTagHeight = tagList.rect.height;
        if (cocktailImageFrame != null) initialImageTop = -cocktailImageFrame.anchoredPosition.y;
        if (cocktailImage != null) initialImagePosition = cocktailImage.rectTransform.anchoredPosition;
    }

    public void Show(NewCocktailData cocktail, CocktailRecipeVisualCatalog visuals)
    {
        HideActiveTooltip();
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
            Vector2 offset = sprite != null ? visuals.GetRecipePreviewOffset(cocktail.Id) : Vector2.zero;
            cocktailImage.rectTransform.anchoredPosition = initialImagePosition + offset;
        }

        Canvas.ForceUpdateCanvases();
        PopulateTags(cocktail);
        PopulateIngredients(cocktail, visuals);
        UpdateScrollableLayout();
    }

    public void Hide()
    {
        HideActiveTooltip();
        gameObject.SetActive(false);
    }

    public void ShowIngredientName(RecipeIngredientCell cell, string name, Vector2 pointerPosition)
    {
        if (tooltipCanvas == null || tooltipPrefab == null)
        {
            Debug.LogError("[CocktailRecipePreview] Tooltip Canvas와 Tooltip Prefab을 연결해 주세요.", this);
            return;
        }

        if (tooltip == null)
        {
            tooltip = Instantiate(tooltipPrefab, tooltipCanvas);
            tooltip.gameObject.SetActive(false);
        }

        hoveredIngredient = cell;
        tooltip.Show(name, pointerPosition, tooltipCanvas);
    }

    public void MoveIngredientName(RecipeIngredientCell cell, Vector2 pointerPosition)
    {
        if (cell == hoveredIngredient && tooltip != null)
            tooltip.Move(pointerPosition, tooltipCanvas);
    }

    public void HideIngredientName(RecipeIngredientCell cell)
    {
        if (cell == hoveredIngredient) HideActiveTooltip();
    }

    private void OnDisable()
    {
        HideActiveTooltip();
    }

    private void OnDestroy()
    {
        if (tooltip != null) Destroy(tooltip.gameObject);
    }

    private void HideActiveTooltip()
    {
        hoveredIngredient = null;
        if (tooltip != null) tooltip.Hide();
    }

    private void PopulateTags(NewCocktailData cocktail)
    {
        ClearTagChips();
        if (tagList == null || tagPrefab == null)
        {
            Debug.LogError("[CocktailRecipePreview] Tag List와 Tag Prefab을 연결해 주세요.", this);
            return;
        }

        // 웹과 같이 첫 태그만 도구명(제조법)이고 강조색을 쓴다. 나머지 태그는 갈래와 상관없이 기본색이다.
        AddTag(cocktail.Mix.ToString().ToUpperInvariant(), true);
        if (cocktail.Tags == null) return;

        foreach (NewCocktailTag tag in cocktail.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Ko)) continue;
            AddTag(tag.Ko, false);
        }
    }

    private void AddTag(string value, bool isToolTag)
    {
        RecipeTagChip chip = Instantiate(tagPrefab, tagList);
        float maximumWidth = cocktailImageFrame != null && tagList.rect.width > 0f ? tagList.rect.width : float.PositiveInfinity;
        chip.Bind(value, isToolTag, maximumWidth);
        tagChips.Add(chip);
    }

    private void PopulateIngredients(NewCocktailData cocktail, CocktailRecipeVisualCatalog visuals)
    {
        ClearIngredientCells();
        if (ingredientList == null || ingredientPrefab == null)
        {
            Debug.LogError("[CocktailRecipePreview] Ingredient List와 Ingredient Prefab을 연결해 주세요.", this);
            return;
        }
        var added = new HashSet<string>();
        AddRequiredItem(cocktail.Glass, visuals, added);
        AddRequiredItem(cocktail.TargetToolId, visuals, added);
        if (cocktail.Recipe == null) return;

        // 웹 bar-views.js와 같이 잔 → 믹스 도구 → 병따개 → 재료 순이다. 병을 따야 하는 재료가 있을 때만 병따개를 넣는다.
        if (RequiresBottleOpener(cocktail.Recipe)) AddRequiredItem(NewToolIds.BottleOpener, visuals, added);

        foreach (NewCocktailRecipeStep step in cocktail.Recipe)
        {
            AddRequiredItem(step.Ingredient, visuals, added);
        }
    }

    private bool RequiresBottleOpener(NewCocktailRecipeStep[] recipe)
    {
        if (shelfData == null) return false;
        foreach (NewCocktailRecipeStep step in recipe)
            if (shelfData.RequiresOpen(step.Ingredient)) return true;
        return false;
    }

    private void AddRequiredItem(string id, CocktailRecipeVisualCatalog visuals, HashSet<string> added)
    {
        if (string.IsNullOrWhiteSpace(id) || !added.Add(id)) return;
        AddIngredient(id, visuals);
    }

    private void AddIngredient(string id, CocktailRecipeVisualCatalog visuals)
    {
        Sprite sprite = visuals != null ? visuals.GetRecipeIngredientSprite(id) : null;
        string name = id == NewToolIds.BottleOpener ? bottleOpenerName
            : shelfData != null && shelfData.TryGet(id, out NewShelfItemData item) ? item.Name.Ko : id;
        RecipeIngredientCell cell = Instantiate(ingredientPrefab, ingredientList);
        cell.Bind(sprite, name, this);
        ingredientCells.Add(cell);
    }

    private void ClearTagChips()
    {
        foreach (RecipeTagChip chip in tagChips)
        {
            if (chip == null) continue;
            chip.gameObject.SetActive(false);
            Destroy(chip.gameObject);
        }
        tagChips.Clear();
    }

    private void ClearIngredientCells()
    {
        foreach (RecipeIngredientCell cell in ingredientCells)
        {
            if (cell == null) continue;
            cell.gameObject.SetActive(false);
            Destroy(cell.gameObject);
        }
        ingredientCells.Clear();
    }

    private void UpdateScrollableLayout()
    {
        if (scrollRect == null || scrollRect.viewport == null || scrollContent == null || ingredientList == null || ingredientGrid == null ||
            descriptionSection == null || recipeSection == null || descriptionText == null || recipeText == null)
        {
            Debug.LogError("[CocktailRecipePreview] Scroll Rect, Content, Grid, 설명/제조법 영역을 연결해 주세요.", this);
            return;
        }

        float tagOverflow = LayoutTagRows();
        float gridTop = ingredientTop + tagOverflow;
        SetTop(ingredientList, gridTop);

        int columns = Mathf.Max(1, ingredientGrid.constraintCount);
        int rows = Mathf.CeilToInt((float)ingredientCells.Count / columns);
        float ingredientHeight = ingredientGrid.padding.top + ingredientGrid.padding.bottom;
        if (rows > 0)
            ingredientHeight += rows * ingredientGrid.cellSize.y + (rows - 1) * ingredientGrid.spacing.y;
        ingredientList.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ingredientHeight);

        float descriptionTop = Mathf.Max(minimumDescriptionTop + tagOverflow, gridTop + ingredientHeight + sectionGap);
        float descriptionHeight = SetTextSectionHeight(descriptionSection, descriptionText, descriptionTop);
        float recipeTop = descriptionTop + descriptionHeight + sectionGap;
        float recipeHeight = SetTextSectionHeight(recipeSection, recipeText, recipeTop);
        float contentHeight = recipeTop + recipeHeight + contentBottomInset;
        contentHeight = Mathf.Max(contentHeight, scrollRect.viewport.rect.height);
        scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private float LayoutTagRows()
    {
        if (cocktailImageFrame == null || tagList == null) return 0f;

        float width = tagList.rect.width;
        float left = 0f;
        float top = 0f;
        float rowHeight = 0f;
        foreach (RecipeTagChip chip in tagChips)
        {
            var rect = (RectTransform)chip.transform;
            Vector2 size = rect.rect.size;
            if (left > 0f && left + size.x > width)
            {
                top += rowHeight + tagSpacing.y;
                left = 0f;
                rowHeight = 0f;
            }

            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, left, size.x);
            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, top, size.y);
            left += size.x + tagSpacing.x;
            rowHeight = Mathf.Max(rowHeight, size.y);
        }

        float height = Mathf.Max(initialTagHeight, top + rowHeight);
        tagList.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float overflow = height - initialTagHeight;
        SetTop(cocktailImageFrame, initialImageTop + overflow);
        return overflow;
    }

    private float SetTextSectionHeight(RectTransform section, TextMeshProUGUI text, float top)
    {
        SetTop(section, top);
        float preferredHeight = text.GetPreferredValues(text.text, text.rectTransform.rect.width, float.PositiveInfinity).y;
        float textHeight = Mathf.Ceil(Mathf.Max(text.fontSize, preferredHeight));
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
        float sectionHeight = -text.rectTransform.anchoredPosition.y + textHeight + textBottomInset;
        section.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sectionHeight);
        return sectionHeight;
    }

    private static void SetTop(RectTransform rect, float top) =>
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
}
