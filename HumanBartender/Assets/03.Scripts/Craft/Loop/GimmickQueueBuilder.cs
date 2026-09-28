using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 제조 준비가 끝난 시점의 선택 내용을 실제로 플레이할 기믹 목록으로 바꾼다.
///
/// 만드는 기준이 항목마다 다르다는 게 핵심이다.
/// 직접 고른 재료는 플레이어가 고른 그대로 만들고, 자동으로 넣어 주는 재료(스퀴즈·파우더)는 선반에
/// 없으니 선택한 칵테일의 레시피에서 불러오며, 믹스는 정답 제조법이 아니라 실제로 고른 도구가 정한다.
///
/// 부족한 재료를 정답에 맞춰 채워 넣지 않는다. 진과 소다수가 필요한 칵테일에서 럼만 골랐다면
/// 따르기(럼) 하나만 만들어지고, 빠진 둘은 빠진 채로 계산 단계까지 간다.
///
/// MonoBehaviour가 아니다. 씬을 띄우지 않고도 "이렇게 고르면 이런 큐가 나온다"를 확인할 수 있어야
/// 하기 때문이다.
/// </summary>
public static class GimmickQueueBuilder
{
    /// <summary>
    /// 기믹 큐를 만든다.
    /// </summary>
    /// <param name="selected">플레이어가 만들기로 고른 칵테일. 목표 수량과 자동 호출 재료의 출처다.</param>
    /// <param name="actual">실제로 고른 잔·도구·재료.</param>
    /// <param name="shelfData">재료의 기본 동작과 병 손질 여부를 읽어올 선반 데이터.</param>
    /// <remarks>
    /// 큐를 만들면서 actual에 자동 호출 재료 목록도 함께 채운다. 무엇이 불려 왔는지는 제조 기록의
    /// 일부이고, 그걸 아는 건 레시피를 훑는 이 함수뿐이라 여기서 같이 남긴다.
    /// </remarks>
    public static GimmickQueue Build(NewCocktailData selected, ActualCraft actual, NewShelfItemDataSO shelfData)
    {
        var steps = new List<GimmickStep>();
        var unresolved = new List<string>();

        AddSelectedIngredientSteps(selected, actual, shelfData, steps, unresolved);
        ApplyAutoSteps(selected, actual, steps);
        AddMixStep(actual, steps);

        // 1차 정렬은 기믹 종류의 고정 순서다. 열거형 선언 순서가 곧 그 순서라서 값 비교로 끝난다.
        // OrderBy는 같은 값끼리의 순서를 흐트러뜨리지 않으므로, 위에서 넣은 순서 —
        // 재료는 선택 순서, 자동 호출은 레시피 순서 — 가 2차 정렬로 그대로 남는다.
        var ordered = steps.OrderBy(GetStepType).ToList();

        return new GimmickQueue(ordered, unresolved);
    }

    private static int GetStepType(GimmickStep step)
    {
        return (int)step.Type;
    }

    /// <summary>
    /// 선반에서 직접 고른 재료의 기믹. 선택 순서대로 훑으므로 같은 종류끼리는 이 순서가 유지된다.
    /// </summary>
    static void AddSelectedIngredientSteps(NewCocktailData selected, ActualCraft actual,
                                           NewShelfItemDataSO shelfData,
                                           List<GimmickStep> steps, List<string> unresolved)
    {
        foreach (string ingredientId in actual.IngredientIds)
        {
            ECraftGimmick? gimmick = ResolveGimmick(selected, ingredientId, shelfData);

            if (gimmick == null)
            {
                // 기본 동작을 모르는 재료다. 조용히 빼면 고른 것이 화면에 안 나오는데 이유를 알 수 없으니
                // 목록에 남겨 부르는 쪽이 알 수 있게 한다.
                unresolved.Add(ingredientId);
                continue;
            }

            // 스퀴즈·파우더 재료는 선반에 없으니 원래 여기 들어올 수 없다. 그래도 들어왔다면
            // 레시피를 보는 쪽에서 이미 처리했으므로 여기서 또 만들지 않는다.
            if (gimmick == ECraftGimmick.Squeeze || gimmick == ECraftGimmick.Powder)
                continue;

            // 병 손질은 재료의 동작을 대신하는 게 아니라 앞에 한 단계 더 붙는 것이다.
            // 병맥주는 뚜껑을 딴 다음에 따르기까지 해야 제조가 끝난다.
            if (shelfData != null && shelfData.RequiresOpen(ingredientId))
                steps.Add(new GimmickStep(ECraftGimmick.Open, ingredientId));

            bool inRecipe = TryFindRecipeStep(selected, ingredientId, out _);
            (float? qty, ENewUnit? unit) = FindTarget(selected, ingredientId, shelfData);

            steps.Add(new GimmickStep(gimmick.Value, ingredientId, qty, unit, inRecipe));
        }
    }

    /// <summary>
    /// 이 재료를 어떤 기믹으로 실행할지 정한다.
    ///
    /// 선택한 칵테일의 레시피에 있는 재료라면 그 줄에 적힌 동작을 따른다. 레시피에 없는 재료 —
    /// 즉 오선택한 재료라면 그 재료의 기본 동작을 쓴다. 정답 재료가 비운 자리를 물려받지 않는다는
    /// 뜻이다. 따르기 자리가 비었다고 해서 필업 재료를 거기에 끼워 넣지 않는다.
    /// </summary>
    static ECraftGimmick? ResolveGimmick(NewCocktailData selected, string ingredientId,
                                         NewShelfItemDataSO shelfData)
    {
        if (TryFindRecipeStep(selected, ingredientId, out var recipeStep))
            return ToGimmick(recipeStep.Action);

        ENewRecipeAction? action = shelfData != null ? shelfData.GetDefaultAction(ingredientId) : null;
        return action == null ? null : ToGimmick(action.Value);
    }

    /// <summary>
    /// 선반에 없어서 플레이어가 고를 수 없는 재료를 레시피에서 불러온다. 지금은 스퀴즈와 파우더다.
    ///
    /// auto_apply가 켜진 줄은 기믹을 만들지 않는다. 시스템이 알아서 넣어 주기로 한 재료라 플레이어가
    /// 할 일이 없고, 실제로 그 기믹 화면도 아직 없다. 대신 무엇이 들어갔는지는 제조 기록에 남긴다 —
    /// 넣긴 넣은 것이라 완성 결과에 포함되어야 한다.
    ///
    /// 나중에 플레이어가 직접 짜고 뿌리는 방식으로 바뀌면 데이터의 auto_apply만 끄면 된다.
    /// 그러면 아래 분기를 타고 큐에 들어가므로 이 코드는 그대로 둔 채 전환된다.
    /// </summary>
    static void ApplyAutoSteps(NewCocktailData selected, ActualCraft actual, List<GimmickStep> steps)
    {
        var squeezeIds = new List<string>();
        var powderIds = new List<string>();

        if (selected.Recipe != null)
        {
            foreach (var recipeStep in selected.Recipe)
            {
                // 플레이어가 선반에서 고르는 재료는 실제 선택을 보고 따로 처리한다.
                if (recipeStep.Action != ENewRecipeAction.Squeeze &&
                    recipeStep.Action != ENewRecipeAction.Powder) continue;

                bool isSqueeze = recipeStep.Action == ENewRecipeAction.Squeeze;

                if (recipeStep.AutoApply)
                {
                    // 시스템이 넣어 준다. 기믹은 없고 기록만 남는다.
                    (isSqueeze ? squeezeIds : powderIds).Add(recipeStep.Ingredient);
                    continue;
                }

                // 플레이어가 직접 하는 재료다. 선반에 없으니 레시피에서 불러와 큐에 넣는다.
                steps.Add(new GimmickStep(isSqueeze ? ECraftGimmick.Squeeze : ECraftGimmick.Powder,
                                          recipeStep.Ingredient,
                                          recipeStep.Qty, recipeStep.Unit, isRecipeIngredient: true));
            }
        }

        // 자동으로 들어간 재료는 플레이어가 고른 재료와 섞지 않는다. 재료 누락이나 추가 선택을
        // 따질 때 이 둘은 셈에서 빠지기 때문이다.
        actual.SetAutoCalledIngredients(squeezeIds, powderIds);
    }

    /// <summary>
    /// 믹스. 선택한 칵테일의 정답 제조법이 아니라 실제로 고른 도구가 정한다.
    /// 정답이 셰이크인데 믹싱 글라스를 골랐다면 스터가 실행되고, 도구를 안 골랐다면 믹스는 없다.
    /// </summary>
    static void AddMixStep(ActualCraft actual, List<GimmickStep> steps)
    {
        switch (actual.ActualMix)
        {
            case ENewMixMethod.Shake:
                steps.Add(new GimmickStep(ECraftGimmick.Shake, null));
                break;

            case ENewMixMethod.Stir:
                steps.Add(new GimmickStep(ECraftGimmick.Stir, null));
                break;
        }
    }

    /// <summary>
    /// 이 재료의 목표 수량을 찾는다. 레시피에 있으면 그 값이 정답 목표이고, 없으면 —
    /// 즉 오선택한 재료라면 — 재료에 지정된 기본 목표를 쓴다.
    ///
    /// 기본 목표는 정답이 아니다. 화면에 얼마쯤 넣으면 되는지 보여주기 위한 값이고,
    /// 정답에 없는 재료라는 사실은 계산 단계에서 따로 처리한다.
    /// </summary>
    static (float?, ENewUnit?) FindTarget(NewCocktailData selected, string ingredientId,
                                          NewShelfItemDataSO shelfData)
    {
        if (TryFindRecipeStep(selected, ingredientId, out var step))
            return (step.Qty, step.Unit);

        return shelfData != null ? shelfData.GetDefaultTarget(ingredientId) : (null, null);
    }

    static bool TryFindRecipeStep(NewCocktailData selected, string ingredientId,
                                  out NewCocktailRecipeStep found)
    {
        found = default;
        if (selected.Recipe == null || string.IsNullOrEmpty(ingredientId)) return false;

        foreach (var step in selected.Recipe)
        {
            if (step.Ingredient != ingredientId) continue;

            found = step;
            return true;
        }

        return false;
    }

    static ECraftGimmick ToGimmick(ENewRecipeAction action) => action switch
    {
        ENewRecipeAction.Pour => ECraftGimmick.Pour,
        ENewRecipeAction.FillUp => ECraftGimmick.FillUp,
        ENewRecipeAction.Squeeze => ECraftGimmick.Squeeze,
        _ => ECraftGimmick.Powder,
    };
}
