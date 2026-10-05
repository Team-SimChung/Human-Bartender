using System.Collections.Generic;

/// <summary>
/// 레시피 노트에 적히는 재료 한 줄.
///
/// 선반에서 직접 골라야 하는 재료만 들어온다. 스퀴즈·파우더는 선반에 오브젝트가 없어 "골라야 할
/// 것"이 아니므로 목록에서 빠지고, 필요하면 제조 방법 설명문에서만 언급된다(§3.7.2).
/// </summary>
public readonly struct RecipeNoteIngredient
{
    public string IngredientId { get; }

    /// <summary>목표 수량. 아직 정해지지 않은 줄은 null이다 — 없는 값을 0으로 메우지 않는다.</summary>
    public float? Qty { get; }

    public ENewUnit? Unit { get; }

    public ENewRecipeAction Action { get; }

    /// <summary>수량 대신 "가득"으로 적히는 줄인지. 필업은 목표 수량이 잔 크기에서 나온다.</summary>
    public bool IsFillUp => Action == ENewRecipeAction.FillUp;

    public RecipeNoteIngredient(string ingredientId, float? qty, ENewUnit? unit, ENewRecipeAction action)
    {
        IngredientId = ingredientId;
        Qty = qty;
        Unit = unit;
        Action = action;
    }
}

/// <summary>
/// 레시피 노트에 표시할 내용(칵테일 제조 준비 시스템 §3.7).
///
/// 플레이어가 노트에서 얻어야 하는 것은 두 가지로 나뉜다. 재료 목록은 "선반에서 무엇을 골라야
/// 하는가"이고, 제조 방법 설명문은 "실제로 어떤 순서로 만드는가"다. 그래서 자동으로 들어가는
/// 재료는 목록에서 빼고 설명문에만 남긴다.
///
/// 문장을 만들지 않고 데이터를 그대로 옮긴다. 제조 방법은 cocktails.json의 recipe_desc가 정본이라
/// 여기서 "셰이커에 넣고 흔든다" 같은 문장을 지어내면 데이터와 두 벌이 된다.
/// </summary>
public class RecipeNote
{
    public string CocktailId { get; }

    public LocalizedText Name { get; }

    /// <summary>제조 방법 설명문(recipe_desc). 표시할 언어는 부르는 쪽이 고른다.</summary>
    public LocalizedText Description { get; }

    /// <summary>목표 잔.</summary>
    public string TargetGlassId { get; }

    /// <summary>목표 도구. 도구가 필요 없는 빌드 계열이면 null이다.</summary>
    public string TargetToolId { get; }

    /// <summary>선반에서 직접 골라야 하는 정답 재료와 목표량. 노트의 재료 영역에 그대로 적힌다.</summary>
    public IReadOnlyList<RecipeNoteIngredient> Ingredients { get; }

    RecipeNote(NewCocktailData cocktail, List<RecipeNoteIngredient> ingredients)
    {
        CocktailId = cocktail.Id;
        Name = cocktail.Name;
        Description = cocktail.RecipeDesc;
        TargetGlassId = cocktail.Glass;
        TargetToolId = cocktail.TargetToolId;
        Ingredients = ingredients;
    }

    /// <summary>선택한 칵테일의 레시피에서 노트 내용을 만든다.</summary>
    public static RecipeNote Build(NewCocktailData cocktail)
    {
        var ingredients = new List<RecipeNoteIngredient>();

        foreach (var step in cocktail.Recipe ?? System.Array.Empty<NewCocktailRecipeStep>())
        {
            if (!step.IsSelectable) continue;

            ingredients.Add(new RecipeNoteIngredient(step.Ingredient, step.Qty, step.Unit, step.Action));
        }

        return new RecipeNote(cocktail, ingredients);
    }
}
