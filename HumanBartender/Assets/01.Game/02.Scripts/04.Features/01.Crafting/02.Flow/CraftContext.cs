using System.Collections.Generic;

/// <summary>
/// 기믹 하나가 도는 동안 바뀌지 않는, 이번 제조 시도 전체의 사정.
///
/// GimmickStep은 "재료 하나를 어떻게 다뤄라"는 지시라서 잔이 들어갈 자리가 없다. 그런데 잔은
/// 모든 기믹이 알아야 한다 — 따르는 잔, 셰이커에서 옮겨 담을 잔, 스터하는 믹싱글라스가 전부
/// 플레이어가 고른 것이다. 도구와 지금까지 고른 재료도 마찬가지다.
///
/// 읽기만 하라고 이 모양으로 넘긴다. CraftSession을 통째로 주면 기믹이 기록을 고칠 수 있게 되는데,
/// 결과를 적는 것은 실행기의 일이지 기믹의 일이 아니다.
///
/// 지금은 아무 기믹도 이 값을 그리는 데 쓰지 않는다. 리소스가 나오면 각자 여기서 가져가면 되도록
/// 통로만 먼저 뚫어 둔 것이다.
/// </summary>
public readonly struct CraftContext
{
    /// <summary>플레이어가 만들기로 고른 칵테일. 완성 결과물의 정체성은 언제나 이 값이다.</summary>
    public string SelectedCocktailId { get; }

    /// <summary>실제로 고른 잔. 기믹 진입의 필수 조건이라 비어 있지 않다.</summary>
    public string GlassId { get; }

    /// <summary>실제로 고른 도구. 안 골라도 되므로 null일 수 있다.</summary>
    public string ToolId { get; }

    /// <summary>고른 도구에서 정해지는 믹스 방식. 도구를 안 골랐으면 None이다.</summary>
    public ENewMixMethod ActualMix { get; }

    // 목록은 뒤에 두고 프로퍼티로 감싼다. 구조체라 default(CraftContext)가 만들어질 수 있는데,
    // 그때 목록이 null이면 읽는 쪽이 전부 null 검사를 해야 한다. 빈 목록으로 돌려주는 편이 낫다.
    readonly IReadOnlyList<string> ingredientIds;
    readonly IReadOnlyList<string> autoSqueezeIds;
    readonly IReadOnlyList<string> autoPowderIds;

    /// <summary>선반에서 직접 고른 재료를 고른 순서대로.</summary>
    public IReadOnlyList<string> IngredientIds => ingredientIds ?? System.Array.Empty<string>();

    /// <summary>레시피를 보고 시스템이 넣어 준 스퀴즈 재료.</summary>
    public IReadOnlyList<string> AutoSqueezeIds => autoSqueezeIds ?? System.Array.Empty<string>();

    /// <summary>레시피를 보고 시스템이 넣어 준 파우더 재료.</summary>
    public IReadOnlyList<string> AutoPowderIds => autoPowderIds ?? System.Array.Empty<string>();

    CraftContext(string selectedCocktailId, ActualCraft actual)
    {
        SelectedCocktailId = selectedCocktailId;
        GlassId = actual.GlassId;
        ToolId = actual.ToolId;
        ActualMix = actual.ActualMix;
        ingredientIds = actual.IngredientIds;
        autoSqueezeIds = actual.AutoSqueezeIds;
        autoPowderIds = actual.AutoPowderIds;
    }

    /// <summary>
    /// 진행 중인 시도에서 읽어 온다. 큐를 시작할 때 한 번 만들어 모든 스텝에 같은 것을 넘긴다 —
    /// 잔과 도구는 기믹이 도는 동안 바뀌지 않는다.
    /// </summary>
    public static CraftContext From(CraftSession session)
    {
        return session == null ? default : new CraftContext(session.SelectedCocktailId, session.Actual);
    }
}
