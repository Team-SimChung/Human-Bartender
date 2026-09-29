using System.Collections.Generic;

/// <summary>
/// 제조 준비 단계 하나의 상태와 판정(칵테일 제조 준비 시스템 §3.7~3.9).
///
/// 준비 화면은 플레이어의 선택을 제한하지 않는다. 정답이 아닌 잔을 골라도, 도구를 안 골라도,
/// 정답보다 많은 재료를 골라도 막지 않는다 — 오선택은 준비 단계가 아니라 마지막 계산에서 드러나야
/// 하는 것이라, 여기서 미리 고쳐 주면 플레이어는 자기가 틀렸다는 사실 자체를 알지 못한다.
///
/// 그래서 이 객체가 하는 일은 막는 것이 아니라 알려 주는 것이다. 정답 구성을 다 갖췄는지(안내),
/// 다음으로 넘어갈 최소 조건을 채웠는지(다음 버튼), 노트를 봤으니 어디를 켤지(가이드) 세 가지다.
///
/// 실제 선택값은 ActualCraft가 들고 있고 여기서 복사하지 않는다. 두 벌이 되면 어느 쪽이 진짜인지
/// 알 수 없어진다.
/// </summary>
public class CraftPreparation
{
    readonly NewCocktailData cocktail;
    readonly ActualCraft actual;

    /// <summary>정답 선택형 재료. 가이드 점등과 준비 완료 판정이 같은 목록을 쓴다.</summary>
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
        if (!actual.DeselectIngredient(ingredientId))
            actual.SelectIngredient(ingredientId);

        Changed?.Invoke();
    }

    /// <summary>레시피 노트를 한 번 열어 봤는지. 가이드 점등의 조건이다(§3.7.8).</summary>
    public bool HasReadRecipeNote { get; private set; }

    public CraftPreparation(NewCocktailData cocktail, ActualCraft actual)
    {
        this.cocktail = cocktail;
        this.actual = actual;

        targetIngredientIds = cocktail.GetSelectableIngredientIds();
        Note = RecipeNote.Build(cocktail);
    }

    // ── 가이드 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 레시피 노트를 닫을 때 부른다. 이때부터 정답 오브젝트에 가이드가 켜진다.
    /// 한 번 켜지면 다시 끄지 않는다 — 노트를 봤다는 사실이 되돌려지지 않기 때문이다.
    /// </summary>
    public void MarkRecipeNoteRead()
    {
        HasReadRecipeNote = true;
    }

    /// <summary>가이드를 켤 때인지. 노트를 열어 보기 전에는 아무것도 점등하지 않는다.</summary>
    public bool IsGuideActive => HasReadRecipeNote;

    /// <summary>
    /// 이 선반 오브젝트에 가이드를 켜야 하는지. 정답 잔·도구·선택형 재료가 대상이다.
    ///
    /// 가이드가 켜져 있어도 플레이어의 선택을 막지는 않는다(§3.7.8). 점등된 것 대신 다른 것을
    /// 골라도 그대로 기록된다.
    /// </summary>
    public bool IsGuideTarget(string shelfItemId)
    {
        if (!IsGuideActive || string.IsNullOrEmpty(shelfItemId)) return false;

        if (shelfItemId == cocktail.Glass) return true;
        if (shelfItemId == cocktail.TargetToolId) return true;

        return targetIngredientIds.Contains(shelfItemId);
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
