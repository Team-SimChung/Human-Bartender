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

    [Serializable]
    private struct RecipePreviewOffset
    {
        [SerializeField] private string id;
        [SerializeField] private Vector2 offset;

        public string Id => id;
        public Vector2 Offset => offset;
    }

    [SerializeField] private VisualEntry[] cocktailSprites;
    [Tooltip("레시피 상세 이미지의 투명 여백을 보정하기 위해 기본 위치에 더하는 UI 좌표입니다.")]
    [SerializeField] private RecipePreviewOffset[] recipePreviewOffsets;
    [SerializeField] private VisualEntry[] ingredientSprites;
    [Tooltip("레시피 상세에는 제조 준비 화면의 재료 그림을 우선 연결한다. 연결이 없으면 공용 재료 그림을 사용한다.")]
    [SerializeField] private VisualEntry[] recipeIngredientSprites;
    [Tooltip("테이블 위 완성 잔 전용 그림. 없는 종류는 기존 칵테일 그림을 사용한다.")]
    [SerializeField] private VisualEntry[] tableCocktailSprites;
    [Tooltip("실제 사용한 잔 ID에 대응하는 테이블용 빈잔.")]
    [SerializeField] private VisualEntry[] tableGlassSprites;
    [Tooltip("따르기 미니게임에서 기울이는 '뚜껑 딴 병' 그림(웹 pour-fluid.js openArt, *_off.png). " +
             "레시피·해금 화면은 닫힌 병(Ingredient Sprites)을 그대로 쓴다. 없는 재료는 Ingredient Sprites로 대신한다.")]
    [SerializeField] private VisualEntry[] pourBottleSprites;

    public Sprite GetTableGlassSprite(string glassId) => FindSprite(tableGlassSprites, glassId);

    /// <summary>따르기 미니게임의 병 그림. 따로 정하지 않은 재료는 재료 그림을 쓴다.</summary>
    public Sprite GetPourBottleSprite(string id) => FindSprite(pourBottleSprites, id) ?? GetIngredientSprite(id);

    public Sprite GetCocktailSprite(string id)
    {
        return FindSprite(cocktailSprites, id);
    }

    public Vector2 GetRecipePreviewOffset(string id)
    {
        if (recipePreviewOffsets == null || string.IsNullOrEmpty(id)) return Vector2.zero;
        foreach (RecipePreviewOffset entry in recipePreviewOffsets)
            if (string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry.Offset;
        return Vector2.zero;
    }

    public Sprite GetIngredientSprite(string id)
    {
        return FindSprite(ingredientSprites, id);
    }

    public Sprite GetRecipeIngredientSprite(string id) => FindSprite(recipeIngredientSprites, id) ?? GetIngredientSprite(id);

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
