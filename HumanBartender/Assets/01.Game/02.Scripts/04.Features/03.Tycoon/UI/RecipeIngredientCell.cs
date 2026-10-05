using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>레시피 미리보기에서 재료 하나의 이미지 또는 이름을 표시한다.</summary>
public sealed class RecipeIngredientCell : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI fallbackName;

    private CocktailRecipePreview preview;
    private string ingredientName;

    public void Bind(Sprite sprite, string name, CocktailRecipePreview owner)
    {
        if (icon == null || fallbackName == null)
        {
            Debug.LogError("[RecipeIngredientCell] Icon과 Fallback Name을 프리팹 인스펙터에 연결해 주세요.", this);
            return;
        }

        preview = owner;
        ingredientName = name;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        icon.preserveAspect = true;
        fallbackName.text = name;
        fallbackName.gameObject.SetActive(sprite == null);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (preview != null) preview.ShowIngredientName(this, ingredientName, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (preview != null) preview.MoveIngredientName(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (preview != null) preview.HideIngredientName(this);
    }

    private void OnDisable()
    {
        if (preview != null) preview.HideIngredientName(this);
    }
}
