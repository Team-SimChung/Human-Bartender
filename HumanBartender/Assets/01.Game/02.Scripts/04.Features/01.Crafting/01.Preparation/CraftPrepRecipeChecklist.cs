using System.Collections.Generic;

/// <summary>레시피 버튼 호버 목록 한 줄의 갈래. 화면에는 잔·도구·재료로 표시한다.</summary>
public enum ECraftPrepChecklistKind { Glass, Tool, Ingredient }

/// <summary>레시피 버튼 호버 목록의 한 줄. 이 칵테일에 필요한 항목 하나와 지금 담았는지.</summary>
public readonly struct CraftPrepChecklistEntry
{
    public CraftPrepChecklistEntry(string id, ECraftPrepChecklistKind kind, bool packed)
    {
        Id = id;
        Kind = kind;
        Packed = packed;
    }

    public string Id { get; }
    public ECraftPrepChecklistKind Kind { get; }
    public bool Packed { get; }
}

/// <summary>
/// 제조 준비 화면의 레시피 버튼에 펼쳐 보일 준비물 목록을 만든다. 웹 bar-views.js prepRecipeBrief와 같은 순서다:
/// 잔 → 믹스 도구(셰이크면 셰이커, 스터면 믹싱 글라스) → 병을 따야 하는 재료가 있으면 병따개 → 직접 고르는 재료.
/// 자동으로 넣어 주는 재료(auto_apply)는 고를 수 없으므로 목록에 넣지 않는다.
/// </summary>
public static class CraftPrepRecipeChecklist
{
    /// <summary><paramref name="into"/>를 비우고 다시 채운다. 선택이 바뀔 때마다 부르므로 목록을 새로 만들지 않는다.</summary>
    public static void Build(CraftPreparation preparation, NewShelfItemDataSO shelfData, List<CraftPrepChecklistEntry> into)
    {
        into.Clear();
        if (preparation == null) return;

        NewCocktailData cocktail = preparation.Cocktail;
        if (!string.IsNullOrEmpty(cocktail.Glass))
            into.Add(new CraftPrepChecklistEntry(cocktail.Glass, ECraftPrepChecklistKind.Glass,
                preparation.GlassId == cocktail.Glass));

        string tool = cocktail.TargetToolId;
        if (!string.IsNullOrEmpty(tool))
            into.Add(new CraftPrepChecklistEntry(tool, ECraftPrepChecklistKind.Tool, preparation.ToolId == tool));

        List<string> ingredients = cocktail.GetSelectableIngredientIds();
        if (shelfData != null && ingredients.Exists(shelfData.RequiresOpen))
            into.Add(new CraftPrepChecklistEntry(NewToolIds.BottleOpener, ECraftPrepChecklistKind.Tool,
                preparation.IsBottleOpenerSelected));

        foreach (string id in ingredients)
            into.Add(new CraftPrepChecklistEntry(id, ECraftPrepChecklistKind.Ingredient, Contains(preparation.IngredientIds, id)));
    }

    private static bool Contains(IReadOnlyList<string> ids, string id)
    {
        for (int index = 0; index < ids.Count; index++)
            if (ids[index] == id) return true;
        return false;
    }
}
