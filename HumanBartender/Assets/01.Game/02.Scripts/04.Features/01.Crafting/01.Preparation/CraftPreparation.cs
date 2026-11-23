using System.Collections.Generic;

/// <summary>
/// 제조 준비 단계 하나의 상태와 판정(칵테일 제조 준비 시스템 §3.7~3.9).
///
/// 준비 화면에서는 재료 선택 상한까지 자유롭게 조합할 수 있다. 오선택은 마지막 계산에서 판정한다.
///
/// 정답 구성과 제조 시작 조건을 확인하고, 튜토리얼에 레시피 노트 확인 여부를 알려 준다.
///
/// 실제 선택값은 ActualCraft가 들고 있고 여기서 복사하지 않는다. 두 벌이 되면 어느 쪽이 진짜인지
/// 알 수 없어진다.
/// </summary>
public class CraftPreparation
{
    private const int IngredientLimit = 8;

    readonly NewCocktailData cocktail;
    readonly ActualCraft actual;

    /// <summary>준비 완료 판정에 쓰는 정답 선택형 재료.</summary>
    readonly List<string> targetIngredientIds;

    bool announcedReady;

    /// <summary>이 칵테일의 레시피 노트 내용.</summary>
    public RecipeNote Note { get; }

    /// <summary>만들기로 고른 칵테일. 정답 잔·도구를 견줄 때 쓴다.</summary>
    public NewCocktailData Cocktail => cocktail;

    /// <summary>선택이 바뀔 때마다 발생한다. 선반 표시·하단 트레이·다음 버튼이 이 신호로 갱신된다.</summary>
    public event System.Action Changed;

    // ── 선택 ────────────────────────────────────────────────────────────

    /// <summary>실제로 고른 잔. 아직 안 골랐으면 null이다.</summary>
    public string GlassId => actual.GlassId;

    /// <summary>실제로 고른 도구. 도구는 안 골라도 되므로 null일 수 있다.</summary>
    public string ToolId => actual.ToolId;
    public bool IsBottleOpenerSelected => actual.IsBottleOpenerSelected;

    public void ToggleBottleOpener()
    {
        ToggleTool(NewToolIds.BottleOpener);
    }

    /// <summary>고른 재료를 고른 순서대로.</summary>
    public IReadOnlyList<string> IngredientIds => actual.IngredientIds;
    public bool CanAddIngredient => IngredientIds.Count < IngredientLimit;

    /// <summary>이 선반 항목을 지금 고른 상태인지. 선반 칸의 선택 표시가 이걸 본다.</summary>
    public bool IsSelected(string shelfItemId)
    {
        return shelfItemId == actual.GlassId
            || shelfItemId == actual.ToolId
            || actual.HasIngredient(shelfItemId);
    }

    /// <summary>잔을 고른다. 같은 잔을 다시 고르면 선택이 풀린다(§3.1.1).</summary>
    public void ToggleGlass(string glassId)
    {
        actual.SetGlass(actual.GlassId == glassId ? null : glassId);
        Changed?.Invoke();
    }

    /// <summary>도구를 고른다. 같은 도구를 다시 고르면 선택이 풀린다(§3.2.1).</summary>
    public void ToggleTool(string toolId)
    {
        actual.SetTool(actual.ToolId == toolId ? null : toolId);
        Changed?.Invoke();
    }

    /// <summary>재료를 고르거나 이미 고른 재료라면 뺀다. 선반과 하단 트레이가 같은 동작을 쓴다.</summary>
    public void ToggleIngredient(string ingredientId)
    {
        if (!actual.DeselectIngredient(ingredientId) && (!CanAddIngredient || !actual.SelectIngredient(ingredientId)))
            return;

        Changed?.Invoke();
    }

    /// <summary>레시피 노트 확인을 마쳤는지. 튜토리얼 진행에 사용한다.</summary>
    public bool HasReadRecipeNote { get; private set; }

    public CraftPreparation(NewCocktailData cocktail, ActualCraft actual)
    {
        this.cocktail = cocktail;
        this.actual = actual;

        targetIngredientIds = cocktail.GetSelectableIngredientIds();
        Note = RecipeNote.Build(cocktail);
    }

    /// <summary>플레이어가 레시피 노트를 닫았음을 기록한다.</summary>
    public void MarkRecipeNoteRead()
    {
        HasReadRecipeNote = true;
    }

    // ── 진행 조건 ───────────────────────────────────────────────────────

    /// <summary>
    /// 기믹으로 넘어갈 수 있는지(§3.8.2). 잔 하나와 선택형 재료 한 종류면 된다.
    /// 도구는 조건이 아니다 — 도구를 안 고른 채로 넘어가면 믹스 기믹이 만들어지지 않고,
    /// 그것 자체가 마지막 계산에서 실패 요소로 잡힌다.
    /// </summary>
    public bool CanProceed => actual.CanStartGimmicks;

    /// <summary>
    /// 정답 구성을 모두 갖췄는지(§3.8.1). 정답 잔·정답 도구·정답 선택형 재료가 판정 대상이고,
    /// 선반에 오브젝트가 없는 스퀴즈·파우더는 빠진다.
    ///
    /// 정답보다 많이 고른 것은 여기서 보지 않는다. 명세의 판정 대상이 "정답을 모두 골랐는가"라서,
    /// 곁들여 고른 재료가 있어도 안내는 그대로 나간다.
    /// </summary>
    public bool IsTargetSetupComplete
    {
        get
        {
            if (actual.GlassId != cocktail.Glass) return false;
            if (actual.ToolId != cocktail.TargetToolId) return false;

            foreach (string ingredientId in targetIngredientIds)
            {
                if (!actual.HasIngredient(ingredientId)) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 자동 준비 완료 안내를 지금 띄워야 하는지(§3.8.1). 조건을 채운 순간 한 번만 true다.
    ///
    /// 한 제조 시도에서 한 번만 알린다. 골랐다 뺐다 하는 동안 같은 팝업이 계속 뜨면 안내가 아니라
    /// 방해가 된다. 선택을 바꿀 때마다 부르면 되고, 두 번째부터는 알아서 false다.
    /// </summary>
    public bool ConsumeReadyAnnouncement()
    {
        if (announcedReady) return false;
        if (!IsTargetSetupComplete) return false;

        announcedReady = true;
        return true;
    }
}
