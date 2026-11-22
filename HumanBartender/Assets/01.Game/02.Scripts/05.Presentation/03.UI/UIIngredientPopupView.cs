using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재료·아이템 획득 알림. 칵테일 재료는 그림과 이름을, 재료가 아닌 획득 아이템(기어 부품 등)은 그림 없이
/// '이름 획득!'을 보여 준다(웹 outside-notices.js와 같은 구분).
/// </summary>
public sealed class UIIngredientPopupView : UIPopupView
{
    [Serializable]
    public sealed class AcquiredItem
    {
        [Tooltip("대본 CSV effect 행의 arg에 적힌 아이템 ID.")]
        public string Id;
        public string NameKo;
    }

    [SerializeField] NewShelfItemDataSO shelfData;
    [SerializeField] CocktailRecipeVisualCatalog visuals;
    [SerializeField] TMP_Text ingredientName;
    [Tooltip("재료 그림을 담는 묶음입니다. 그림이 없는 아이템 알림에서는 숨깁니다.")]
    [SerializeField] GameObject iconFrame;
    [SerializeField] Image ingredientIcon;

    [Header("Copy")]
    [Tooltip("칵테일 재료가 아닌 획득 아이템. 웹 outside-notices.js의 questItems와 같다.")]
    [SerializeField] AcquiredItem[] acquiredItems =
    {
        new() { Id = "gear_part", NameKo = "기어 부품" },
        new() { Id = "machine_church_emblem", NameKo = "기신교의 엠블럼" },
    };

    /// <summary>재료가 아닌 획득 아이템이면 표시 이름을 돌려준다.</summary>
    public bool TryGetAcquiredItemName(string id, out string name)
    {
        foreach (var item in acquiredItems ?? Array.Empty<AcquiredItem>())
        {
            if (item == null || item.Id != id) continue;
            name = string.IsNullOrWhiteSpace(item.NameKo) ? item.Id : item.NameKo;
            return true;
        }
        name = null;
        return false;
    }

    protected override bool Prepare(string id)
    {
        if (ingredientName == null) return false;
        if (TryGetAcquiredItemName(id, out string itemName))
        {
            if (iconFrame == null)
            {
                Debug.LogWarning("[Popup] 아이템 알림에 Icon Frame을 연결하세요.", this);
                return false;
            }
            ingredientName.text = itemName + " 획득!";
            iconFrame.SetActive(false);
            return true;
        }

        if (shelfData == null || visuals == null || ingredientIcon == null)
            return false;
        if (!shelfData.TryGet(id, out var item) || !item.IsIngredient) return false;
        var sprite = visuals.GetIngredientSprite(id);
        if (sprite == null)
        {
            Debug.LogWarning($"[Popup] Ingredient sprite not found: {id}", this);
            return false;
        }
        if (iconFrame != null) iconFrame.SetActive(true);
        ingredientName.text = string.IsNullOrWhiteSpace(item.Name.Ko) ? item.Id : item.Name.Ko;
        ingredientIcon.sprite = sprite;
        return true;
    }
}
