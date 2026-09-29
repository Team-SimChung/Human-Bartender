using System.Collections.Generic;

/// <summary>
/// 제조 준비 화면의 선반에 무엇을 내놓을지 고른다(칵테일 제조 준비 시스템 §3.3).
///
/// 어느 선반에 놓일지는 shelf_items.json의 shelf_group이 정한다. category는 기믹·재료 분류라
/// 진열 위치를 정하는 데 쓰지 않는다 — 둘을 섞으면 병맥주가 술 선반으로 가는 식으로 어긋난다.
///
/// 자동으로 넣어 주는 재료(스퀴즈·파우더)는 shelf_group이 비어 있어 여기서 저절로 빠진다.
/// 선반에 오브젝트가 없으니 고를 수도, 가이드로 점등할 수도 없다.
/// </summary>
public static class CraftShelf
{
    private enum ItemFilter { Glass, Tool, Ingredient }

    /// <summary>잔 선반에 놓을 잔.</summary>
    public static List<NewShelfItemData> GetGlasses(NewShelfItemDataSO shelfData, int day)
    {
        return Collect(shelfData, ItemFilter.Glass, default, day);
    }

    /// <summary>도구 선반에 놓을 제조 도구.</summary>
    public static List<NewShelfItemData> GetTools(NewShelfItemDataSO shelfData, int day)
    {
        return Collect(shelfData, ItemFilter.Tool, default, day);
    }

    /// <summary>
    /// 재료 선반 하나에 놓을 재료. liquor는 술 선반, fridge는 냉장고다.
    /// 술인지 아닌지로 나누지 않고 데이터가 적어 둔 선반을 그대로 따른다 — 병맥주는 냉장고다.
    /// </summary>
    public static List<NewShelfItemData> GetIngredients(NewShelfItemDataSO shelfData, ENewShelfGroup group, int day)
    {
        return Collect(shelfData, ItemFilter.Ingredient, group, day);
    }

    /// <summary>
    /// 오늘 진열할 수 있는 것만 남긴다. unlock_day가 오늘보다 뒤인 물건은 아직 가게에 없다.
    ///
    /// unlock_when(조건부 해금)은 보지 않는다. 진행 상태를 읽어야 판단할 수 있는 값이라,
    /// 선반이 임의로 해석하면 조건을 채우지 않은 재료가 조용히 진열된다.
    /// </summary>
    private static List<NewShelfItemData> Collect(NewShelfItemDataSO shelfData, ItemFilter filter,
        ENewShelfGroup group, int day)
    {
        var items = new List<NewShelfItemData>();
        if (shelfData == null || shelfData.shelfItemData == null) return items;

        foreach (var item in shelfData.shelfItemData)
        {
            if (!Matches(item, filter, group)) continue;
            if (item.UnlockDay > day) continue;

            items.Add(item);
        }

        return items;
    }

    private static bool Matches(NewShelfItemData item, ItemFilter filter, ENewShelfGroup group)
    {
        switch (filter)
        {
            case ItemFilter.Glass: return item.Kind == ENewShelfKind.Glass;
            case ItemFilter.Tool: return item.Kind == ENewShelfKind.Tool;
            default: return item.IsIngredient && item.ShelfGroup == group;
        }
    }
}
