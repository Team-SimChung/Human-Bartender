using System.Collections.Generic;

/// <summary>
/// 기믹 큐에 들어가는 항목 하나. "무엇을 어떤 기믹으로 수행할지"만 담고, 그 결과는 담지 않는다.
/// 수행이 끝나면 같은 내용이 GimmickResult로 옮겨 적힌다.
/// </summary>
public readonly struct GimmickStep
{
    public readonly ECraftGimmick Type;

    /// <summary>다룰 재료. 셰이크·스터는 재료를 쓰지 않아 null이다.</summary>
    public readonly string IngredientId;

    /// <summary>
    /// 목표 수량. 선택한 칵테일의 레시피에 있으면 그 값이고, 없으면 재료에 지정된 기본 목표다.
    /// 둘 다 없으면 null이라 오차를 계산할 수 없다.
    /// </summary>
    public readonly float? TargetValue;

    public readonly ENewUnit? TargetUnit;

    /// <summary>
    /// 이 재료가 선택한 칵테일의 정답 재료인지.
    ///
    /// 목표 수량을 화면에 보여줄지 정한다. 정답에 없는 재료를 골랐다면 알려 줄 정답이 없으므로
    /// 목표와 실시간 수량을 ???로 가리고 O.K도 띄우지 않는다. 가리는 건 화면뿐이고,
    /// 실제 투입량은 안에서 정상적으로 쌓여 결과에 남는다.
    /// </summary>
    public readonly bool IsRecipeIngredient;

    public bool HasTarget => TargetValue.HasValue && TargetValue.Value > 0f;

    /// <summary>화면에 목표와 실시간 수량을 그대로 보여줘도 되는지.</summary>
    public bool IsTargetVisible => IsRecipeIngredient && HasTarget;

    public GimmickStep(ECraftGimmick type, string ingredientId,
                       float? targetValue = null, ENewUnit? targetUnit = null,
                       bool isRecipeIngredient = false)
    {
        Type = type;
        IngredientId = ingredientId;
        TargetValue = targetValue;
        TargetUnit = targetUnit;
        IsRecipeIngredient = isRecipeIngredient;
    }

    public override string ToString()
    {
        string target = HasTarget ? $"{TargetValue.Value:0.##}{TargetUnit}" : "???";
        return IngredientId == null ? Type.ToString() : $"{Type}({IngredientId} {target})";
    }
}

/// <summary>
/// 만들어진 기믹 큐. 실행할 목록과 함께, 역할을 몰라 큐에 넣지 못한 재료도 같이 돌려준다.
/// 그런 재료를 조용히 빼면 플레이어가 고른 것이 화면에 나오지 않는데 원인을 찾을 단서가 없다.
/// </summary>
public class GimmickQueue
{
    public IReadOnlyList<GimmickStep> Steps { get; }

    /// <summary>제조 역할을 알 수 없어 기믹을 만들지 못한 재료. 비어 있는 게 정상이다.</summary>
    public IReadOnlyList<string> UnresolvedIngredientIds { get; }

    public int Count => Steps.Count;

    public GimmickQueue(IReadOnlyList<GimmickStep> steps, IReadOnlyList<string> unresolved)
    {
        Steps = steps;
        UnresolvedIngredientIds = unresolved;
    }
}

/// <summary>
/// 한 잔을 만드는 동안 공통 표시가 알아야 하는 값. 기믹이 바뀌어도 변하지 않아서 시작할 때 한 번 넘긴다.
/// </summary>
public readonly struct CraftRunDisplay
{
    /// <summary>선택한 칵테일의 전체 제조 제한시간.</summary>
    public readonly float TimeLimitSec;

    /// <summary>
    /// 시간을 ???로 가릴지. 플레이어가 고른 재료 중 정답이 하나도 없으면 제조시간도 알려 주지 않는다.
    /// 화면만 가리고 실제 시간은 그대로 재서 기록에 남긴다.
    /// </summary>
    public readonly bool HideTime;

    public CraftRunDisplay(float timeLimitSec, bool hideTime)
    {
        TimeLimitSec = timeLimitSec;
        HideTime = hideTime;
    }
}
