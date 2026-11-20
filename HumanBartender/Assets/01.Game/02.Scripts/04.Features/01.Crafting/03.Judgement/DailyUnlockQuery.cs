using System;
using System.Collections.Generic;

/// <summary>그날 새로 쓸 수 있게 된 항목. 조건부 획득 알림과는 별개다.</summary>
public readonly struct DailyUnlocks
{
    readonly IReadOnlyList<NewShelfItemData> ingredients;
    readonly IReadOnlyList<NewCocktailData> cocktails;
    public IReadOnlyList<NewShelfItemData> Ingredients => ingredients ?? Array.Empty<NewShelfItemData>();
    public IReadOnlyList<NewCocktailData> Cocktails => cocktails ?? Array.Empty<NewCocktailData>();
    public bool IsEmpty => Ingredients.Count == 0 && Cocktails.Count == 0;

    public DailyUnlocks(IReadOnlyList<NewShelfItemData> ingredients, IReadOnlyList<NewCocktailData> cocktails)
    {
        this.ingredients = ingredients;
        this.cocktails = cocktails;
    }
}

public static class DailyUnlockQuery
{
    public static DailyUnlocks Collect(int day, IEnumerable<NewShelfItemData> items, IEnumerable<NewCocktailData> cocktails)
    {
        if (day <= 0) return default;
        var newIngredients = new List<NewShelfItemData>();
        var newCocktails = new List<NewCocktailData>();
        if (items != null)
            foreach (var item in items)
                if (item.IsIngredient && item.UnlockDay == day &&
                    (string.IsNullOrWhiteSpace(item.UnlockWhen) || item.UnlockWhen == "\\N"))
                    newIngredients.Add(item);
        if (cocktails != null)
            foreach (var cocktail in cocktails)
                if (cocktail.UnlockDay == day && CocktailUnlockPolicy.IsUnlocked(cocktail, day))
                    newCocktails.Add(cocktail);
        return new DailyUnlocks(newIngredients, newCocktails);
    }
}
