using System.Collections.Generic;

/// <summary>
/// 플레이어가 실제로 고르고 수행한 제조 기록.
///
/// 선택한 칵테일의 정답 레시피와 나란히 두되 절대 섞지 않는다. 시스템은 오선택을 정답으로 고쳐 주지
/// 않기 때문에, 여기 담긴 값이 곧 화면에 나오는 것이고 판정 대상이다. 진 대신 럼을 골랐다면
/// 럼 병이 나오고 럼이 기록되며, 그 차이는 마지막 계산에서만 점수로 드러난다.
/// </summary>
public class ActualCraft
{
    readonly List<string> ingredientIds = new();
    readonly List<string> autoSqueezeIds = new();
    readonly List<string> autoPowderIds = new();
    readonly List<GimmickResult> gimmickResults = new();

    /// <summary>실제로 고른 잔. 잔은 기믹 진입의 필수 조건이라 제조가 시작됐다면 반드시 채워져 있다.</summary>
    public string GlassId { get; private set; }

    /// <summary>
    /// 실제로 고른 도구. 도구는 진입 필수 조건이 아니라서 비어 있을 수 있고,
    /// 그때는 믹스 기믹 자체가 만들어지지 않는다.
    /// </summary>
    public string ToolId { get; private set; }
    public bool IsBottleOpenerSelected => ToolId == NewToolIds.BottleOpener;

    public void SetBottleOpener(bool selected)
    {
        if (selected) SetTool(NewToolIds.BottleOpener);
        else if (IsBottleOpenerSelected) SetTool(null);
    }

    /// <summary>
    /// 선반에서 직접 고른 재료. 리스트 순서가 곧 선택 순서다.
    /// 같은 역할의 재료를 여러 개 고르면 이 순서대로 기믹이 실행된다.
    /// </summary>
    public IReadOnlyList<string> IngredientIds => ingredientIds;

    /// <summary>레시피를 보고 시스템이 불러온 스퀴즈 재료. 플레이어가 고르는 게 아니라서 따로 둔다.</summary>
    public IReadOnlyList<string> AutoSqueezeIds => autoSqueezeIds;

    /// <summary>레시피를 보고 시스템이 불러온 파우더 재료.</summary>
    public IReadOnlyList<string> AutoPowderIds => autoPowderIds;

    /// <summary>끝난 기믹의 결과. 실행한 순서대로 쌓인다.</summary>
    public IReadOnlyList<GimmickResult> GimmickResults => gimmickResults;

    /// <summary>
    /// 한 잔의 제조시간. 플레이어가 실제로 조작할 수 있었던 시간만 더한 값이다.
    /// 연출과 화면 전환은 빠져 있어서, 손이 늦은 것만 재고 연출이 긴 것은 재지 않는다.
    /// </summary>
    public float ElapsedManualSec { get; private set; }

    /// <summary>
    /// 실제로 실행되는 믹스 방식. 저장하지 않고 고른 도구에서 그때그때 구한다.
    /// 정답 도구를 구하는 NewToolIds.FromMix와 같은 대응표를 반대 방향으로 쓰기 때문에 둘이 어긋날 수 없다.
    /// 선택한 칵테일의 정답 제조법이 셰이크라도, 믹싱 글라스를 골랐다면 여기서는 스터가 나온다.
    /// </summary>
    public ENewMixMethod ActualMix => NewToolIds.ToMix(ToolId);

    /// <summary>
    /// 기믹 플레이로 넘어갈 수 있는 최소 조건. 잔 하나와 직접 고른 재료 한 종류다.
    /// 정답인지, 몇 개인지, 도구를 골랐는지는 묻지 않는다 — 그 판단은 전부 마지막 계산으로 미룬다.
    /// </summary>
    public bool CanStartGimmicks => !string.IsNullOrEmpty(GlassId) && ingredientIds.Count > 0;

    // ── 제조 준비 단계의 선택 ───────────────────────────────────────────

    /// <summary>잔을 고른다. 잔은 하나뿐이라 이미 고른 게 있으면 교체된다. null을 넣으면 선택 해제다.</summary>
    public void SetGlass(string glassId)
    {
        GlassId = glassId;
    }

    /// <summary>도구를 고른다. 잔과 같은 방식이며, 고르지 않은 상태도 정상이다.</summary>
    public void SetTool(string toolId)
    {
        ToolId = toolId;
    }

    /// <summary>재료를 고른다. 이미 고른 재료면 아무 일도 하지 않고 false를 반환한다.</summary>
    public bool SelectIngredient(string ingredientId)
    {
        if (string.IsNullOrEmpty(ingredientId)) return false;
        if (ingredientIds.Contains(ingredientId)) return false;

        ingredientIds.Add(ingredientId);
        return true;
    }

    /// <summary>
    /// 고른 재료를 뺀다. 뒤에 있던 재료들의 선택 순서는 그대로 앞으로 당겨진다 —
    /// 순서를 리스트 위치로 표현하기 때문에 별도로 번호를 다시 매길 필요가 없다.
    /// </summary>
    public bool DeselectIngredient(string ingredientId)
    {
        return ingredientIds.Remove(ingredientId);
    }

    public bool HasIngredient(string ingredientId)
    {
        return ingredientIds.Contains(ingredientId);
    }

    /// <summary>
    /// 선택한 칵테일의 레시피에서 읽어온 자동 호출 재료를 채운다.
    /// 플레이어가 고르는 게 아니므로 제조 준비를 마치는 시점에 한 번 넣는다.
    /// </summary>
    public void SetAutoCalledIngredients(IEnumerable<string> squeezeIds, IEnumerable<string> powderIds)
    {
        autoSqueezeIds.Clear();
        autoPowderIds.Clear();

        if (squeezeIds != null) autoSqueezeIds.AddRange(squeezeIds);
        if (powderIds != null) autoPowderIds.AddRange(powderIds);
    }

    // ── 기믹 수행 결과 ──────────────────────────────────────────────────

    /// <summary>끝난 기믹의 결과를 덧붙인다. 이미 넣은 결과는 고치지 않는다.</summary>
    public void Record(GimmickResult result)
    {
        if (result == null) return;

        gimmickResults.Add(result);
    }

    /// <summary>
    /// 마지막 기믹이 끝난 순간의 제조시간을 고정한다.
    /// 이 뒤의 페이드아웃과 결과 연출은 제조가 아니므로 포함하지 않는다.
    /// </summary>
    public void FixElapsedManual(float elapsedSec)
    {
        ElapsedManualSec = elapsedSec;
    }
}
