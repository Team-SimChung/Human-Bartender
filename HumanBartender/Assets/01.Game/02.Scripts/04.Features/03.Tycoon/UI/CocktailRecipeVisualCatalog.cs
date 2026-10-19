using System;
using UnityEngine;

/// <summary>CSV의 ID와 UI 스프라이트를 인스펙터에서 명시적으로 연결한다.</summary>
[CreateAssetMenu(fileName = "CocktailRecipeVisualCatalog", menuName = "UI/Cocktail Recipe Visual Catalog")]
public sealed class CocktailRecipeVisualCatalog : ScriptableObject
{
    [Serializable]
    private struct VisualEntry
    {
        [SerializeField] private string id;
        [SerializeField] private Sprite sprite;

        public string Id { get { return id; } }
        public Sprite Sprite { get { return sprite; } }
    }

    [SerializeField] private VisualEntry[] cocktailSprites;
    [SerializeField] private VisualEntry[] ingredientSprites;
    [Tooltip("테이블 위 완성 잔 전용 그림. 없는 종류는 기존 칵테일 그림을 사용한다.")]
    [SerializeField] private VisualEntry[] tableCocktailSprites;

    public Sprite GetCocktailSprite(string id)
    {
        return FindSprite(cocktailSprites, id);
    }

    public Sprite GetIngredientSprite(string id)
    {
        return FindSprite(ingredientSprites, id);
    }

    public Sprite GetTableCocktailSprite(string id)
    {
        return FindSprite(tableCocktailSprites, id) ?? GetCocktailSprite(id);
    }

    private static Sprite FindSprite(VisualEntry[] entries, string id)
    {
        if (entries == null || string.IsNullOrEmpty(id)) return null;

        foreach (VisualEntry entry in entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry.Sprite;
        }

        return null;
    }
}
