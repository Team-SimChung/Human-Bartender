using System.Collections.Generic;

/// <summary>
/// 선택한 칵테일의 정답 구성을 제조 기록에 그대로 채워 넣는다.
///
/// 두 곳이 같은 목록을 필요로 한다. 하나는 제조 준비 화면이 없는 동안 흐름을 돌려 보기 위한
/// 자동 준비이고, 다른 하나는 데이터가 어떤 큐를 만드는지 확인하는 에디터 도구다.
/// 나중에는 준비 화면의 가이드 점등과 자동 준비 완료 판정도 같은 목록을 쓴다 —
/// "정답 구성이 무엇인가"를 세 곳이 따로 계산하면 어긋난다.
/// </summary>
public static class CraftPreset
{
    /// <summary>정답 잔·도구·선택형 재료를 골라 놓는다. 이미 고른 것이 있으면 덮어쓴다.</summary>
    public static void ApplyTargetSetup(NewCocktailData cocktail, ActualCraft actual)
    {
        if (actual == null) return;

        actual.SetGlass(cocktail.Glass);
        actual.SetTool(cocktail.TargetToolId);

        List<string> selectable = cocktail.GetSelectableIngredientIds();
        foreach (string ingredientId in selectable)
            actual.SelectIngredient(ingredientId);
    }
}
