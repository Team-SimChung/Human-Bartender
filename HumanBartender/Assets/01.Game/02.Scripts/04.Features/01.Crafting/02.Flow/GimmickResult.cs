/// <summary>
/// 기믹 하나를 끝냈을 때 남는 확정 결과. 한 번 만들어지면 바뀌지 않는다 —
/// 기믹을 넘어간 뒤 앞으로 돌아가 수량을 고치는 기능은 없기 때문이다.
///
/// 기믹마다 기록하는 게 다르다. 따르기는 수량, 병따기는 시도 횟수, 셰이크·스터는 성공·실패 스택이다.
/// 그런데도 타입을 나누지 않고 하나로 둔 이유는, 계산 단계가 이 목록을 순서대로 훑으며 종류별로
/// 갈라 보기 때문이다. 상속으로 나누면 그 훑는 코드 전체에 형변환이 퍼진다.
///
/// 대신 아무 필드나 채우지 못하도록 생성자를 막고 종류별 팩토리만 열어 뒀다.
/// 따르기 결과에 스택 수가 들어가는 일은 문법적으로 일어나지 않는다.
/// </summary>
public class GimmickResult
{
    public ECraftGimmick Type { get; private set; }

    /// <summary>이 기믹이 다루는 재료. 셰이크·스터처럼 재료를 쓰지 않는 기믹은 null이다.</summary>
    public string IngredientId { get; private set; }

    public ECraftEndType EndType { get; private set; }

    /// <summary>이 기믹을 확정한 시점의 전체 제조시간(초). 기믹별 소요시간이 아니라 누적값이다.</summary>
    public float CompletedAtSec { get; private set; }

    // ── 수량형 (따르기·스퀴즈·파우더·필업) ──────────────────────────────

    /// <summary>
    /// 목표 수량. null이면 목표가 없다는 뜻이고, 두 경우가 있다.
    /// 하나는 정답 레시피에 없는 재료를 골라 비교할 대상 자체가 없는 경우이고,
    /// 다른 하나는 레시피에는 있지만 목표 수량이 아직 정해지지 않은 경우다.
    /// 어느 쪽이든 0을 넣으면 안 된다 — 오차율은 목표로 나누는 계산이라 가짜 점수가 나온다.
    /// </summary>
    public float? TargetValue { get; private set; }

    public ENewUnit? TargetUnit { get; private set; }

    /// <summary>플레이어가 실제로 넣은 양. 목표에 못 미치든 넘기든 실제 값 그대로다.</summary>
    public float ActualValue { get; private set; }

    // ── 병따기 ──────────────────────────────────────────────────────────

    /// <summary>성공한 입력까지 포함한 전체 시도 횟수. 첫 시도에 성공하면 1이다.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>병뚜껑을 끝내 열었는지. 정상 흐름에서는 성공해야만 기믹이 끝나므로 항상 true다.</summary>
    public bool Completed { get; private set; }

    // ── 병따기와 스택형이 함께 쓴다 ─────────────────────────────────────

    /// <summary>실패 횟수. 병따기는 성공 전까지의 실패, 셰이크·스터는 실패 스택 수다.</summary>
    public int FailureCount { get; private set; }

    // ── 스택형 (셰이크·스터) ────────────────────────────────────────────

    public int SuccessCount { get; private set; }

    /// <summary>
    /// 목표 스택 수. 중간에 다음 버튼으로 끊어도 이 값은 줄지 않는다 —
    /// 못 채운 스택은 성공으로 보정하지 않고 그대로 완성도에 반영된다.
    /// </summary>
    public int TargetStackCount { get; private set; }

    // ── 파생값 ──────────────────────────────────────────────────────────

    /// <summary>비교할 목표가 있어 수량 오차를 계산할 수 있는지.</summary>
    public bool HasTarget => TargetValue.HasValue && TargetValue.Value > 0f;

    /// <summary>목표에 도달했는지. 목표가 없으면 판단하지 않는다.</summary>
    public bool ReachedTarget => HasTarget && ActualValue >= TargetValue.Value;

    /// <summary>셰이크·스터의 완성도 0~1. 성공 스택을 목표 스택으로 나눈 값이다.</summary>
    public float CompletionScore =>
        TargetStackCount <= 0 ? 0f : SuccessCount / (float)TargetStackCount;

    GimmickResult() { }

    /// <summary>따르기·스퀴즈·파우더·필업의 결과.</summary>
    public static GimmickResult Quantity(ECraftGimmick type, string ingredientId,
                                         float? targetValue, ENewUnit? targetUnit, float actualValue,
                                         ECraftEndType endType, float completedAtSec)
    {
        return new GimmickResult
        {
            Type = type,
            IngredientId = ingredientId,
            TargetValue = targetValue,
            TargetUnit = targetUnit,
            ActualValue = actualValue,
            EndType = endType,
            CompletedAtSec = completedAtSec,
        };
    }

    /// <summary>병따기의 결과. 성공까지 몇 번 걸렸는지가 판정 재료다.</summary>
    public static GimmickResult Open(string ingredientId, int attemptCount, int failureCount,
                                     bool completed, float completedAtSec)
    {
        return new GimmickResult
        {
            Type = ECraftGimmick.Open,
            IngredientId = ingredientId,
            AttemptCount = attemptCount,
            FailureCount = failureCount,
            Completed = completed,
            // 병따기는 다음 버튼이 없다. 열려야만 끝나므로 종료 방식이 하나뿐이다.
            EndType = ECraftEndType.AutoSuccess,
            CompletedAtSec = completedAtSec,
        };
    }

    /// <summary>셰이크·스터의 결과.</summary>
    public static GimmickResult Mix(ECraftGimmick type, int successCount, int failureCount,
                                    int targetStackCount, ECraftEndType endType, float completedAtSec)
    {
        return new GimmickResult
        {
            Type = type,
            SuccessCount = successCount,
            FailureCount = failureCount,
            TargetStackCount = targetStackCount,
            EndType = endType,
            CompletedAtSec = completedAtSec,
        };
    }
}
