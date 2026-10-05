using System.Collections.Generic;
using System.Text;

/// <summary>
/// 채점 단위 하나의 결과. 같은 계열의 기믹이 여러 번 나오면 평균낸 대표 점수를 담는다.
///
/// 따르기와 필업은 한 계열로 묶인다. 조작이 같고 순서상 위치만 다른 것이라, 따로 평균내면
/// 필업이 한 번뿐인 칵테일에서 그 한 번의 점수가 따르기 전체와 같은 무게를 갖게 된다.
/// </summary>
public class GimmickTypeScore
{
    /// <summary>이 계열을 대표하는 기믹 종류. 따르기 계열은 Pour로 적는다.</summary>
    public ECraftGimmick Family;

    /// <summary>선택한 칵테일이 요구하는 이 계열의 채점 항목 수. 평균의 분모다.</summary>
    public int ExpectedCount;

    /// <summary>실제 결과와 이어진 수.</summary>
    public int MatchedCount;

    /// <summary>수행하지 않아 0점으로 평균에 들어간 수.</summary>
    public int MissingCount;

    /// <summary>0~100. 개별 점수의 산술평균이다.</summary>
    public float RepresentativeScore;

    /// <summary>목표 수량이나 단위가 미확정이라 점수를 낼 수 없었는지.</summary>
    public bool HasDataError;
}

/// <summary>
/// 한 잔의 제조 판정. 여기서 나오는 것은 craft_score와 craft_grade까지다.
///
/// 최종 등급은 이 결과가 정하지 않는다. 주문과 맞는지, 핵심 재료를 빠뜨렸는지는 완성한 잔을
/// 손님 앞에 놓는 순간에야 판단할 수 있어서, 그 판정과 final_grade는 서빙 쪽이 갖는다.
/// 여기서는 서빙이 쓸 재료(missing_core_ingredient_ids)만 챙겨 둔다.
///
/// 점수만 남기면 왜 그 등급이 나왔는지 알 수 없어서 항목별 감점을 함께 담는다.
/// </summary>
public class CraftJudgement
{
    /// <summary>정상 채점인지, 데이터 오류로 채점을 막았는지.</summary>
    public ENewScoreStatus Status = ENewScoreStatus.Scored;

    /// <summary>데이터 오류의 원인 코드. 정상 채점이면 null이다.</summary>
    public string ErrorCode;

    /// <summary>0~100. 데이터 오류로 채점하지 못했으면 null이다.</summary>
    public float? CraftScore;

    /// <summary>제조 등급. 데이터 오류로 채점하지 못했으면 null이다.</summary>
    public ENewGrade? CraftGrade;

    public readonly List<GimmickTypeScore> GimmickScores = new();

    /// <summary>계열별 대표 점수를 가중 합산한 값(0~100).</summary>
    public float WeightedGimmickScore;

    // ── 항목별 감점 ─────────────────────────────────────────────────────
    public float GimmickErrorPenalty;
    public float GlassPenalty;
    public float ToolPenalty;
    public float MissingIngredientPenalty;
    public float ExtraIngredientPenalty;
    public float OvertimePenalty;

    public readonly List<string> MissingIngredientIds = new();
    public readonly List<string> ExtraIngredientIds = new();

    /// <summary>
    /// 빠뜨린 핵심 재료. 제조 단계는 기록만 하고 등급을 뒤집지 않는다 —
    /// 강제 Sewage는 손님에게 낸 뒤에 서빙 쪽이 판단한다.
    /// </summary>
    public readonly List<string> MissingCoreIngredientIds = new();

    public float ElapsedSec;
    public float TimeLimitSec;
    public float OvertimeRatio;

    public float TotalPenalty =>
        GimmickErrorPenalty + GlassPenalty + ToolPenalty +
        MissingIngredientPenalty + ExtraIngredientPenalty + OvertimePenalty;

    /// <summary>콘솔에 찍을 한 덩어리 보고서.</summary>
    public string BuildReport(string cocktailId)
    {
        var sb = new StringBuilder();

        if (Status == ENewScoreStatus.DataError)
        {
            sb.AppendLine($"[CraftJudge] {cocktailId} — 채점 불가 ({ErrorCode})");
            sb.AppendLine("  목표 수량이나 설정값이 미확정입니다. 임의로 점수를 만들지 않았습니다.");
            AppendMissingCore(sb);
            return sb.ToString();
        }

        sb.AppendLine($"[CraftJudge] {cocktailId} — 등급 {CraftGrade} / {CraftScore.Value:0.0}점");
        sb.AppendLine($"  기믹 가중 점수 {WeightedGimmickScore:0.0} → 감점 {GimmickErrorPenalty:0.0}");

        foreach (var score in GimmickScores)
        {
            string missing = score.MissingCount > 0 ? $" (미수행 {score.MissingCount})" : string.Empty;
            string label = score.Family == ECraftGimmick.Pour ? "Pour계열" : score.Family.ToString();

            sb.AppendLine($"    {label,-9} {score.RepresentativeScore,6:0.0}점  " +
                          $"{score.MatchedCount}/{score.ExpectedCount}{missing}");
        }

        AppendPenalty(sb, "잔 불일치", GlassPenalty);
        AppendPenalty(sb, "도구 불일치·미선택", ToolPenalty);
        AppendPenalty(sb, $"재료 누락 ({string.Join(", ", MissingIngredientIds)})", MissingIngredientPenalty);
        AppendPenalty(sb, $"추가 재료 ({string.Join(", ", ExtraIngredientIds)})", ExtraIngredientPenalty);

        sb.AppendLine($"  제조시간 {ElapsedSec:0.0} / {TimeLimitSec:0.0}초" +
                      (OvertimePenalty > 0f
                          ? $" — 초과율 {OvertimeRatio:P0}, 감점 {OvertimePenalty:0.0}"
                          : " — 초과 없음"));

        sb.AppendLine($"  총 감점 {TotalPenalty:0.0}");

        AppendMissingCore(sb);

        return sb.ToString();
    }

    /// <summary>핵심 재료를 빠뜨렸다면 알린다. 등급을 바꾸지는 않지만 서빙에서 뒤집힐 잔이다.</summary>
    void AppendMissingCore(StringBuilder sb)
    {
        if (MissingCoreIngredientIds.Count == 0) return;

        sb.AppendLine($"  ※ 핵심 재료 누락 ({string.Join(", ", MissingCoreIngredientIds)}) — " +
                      "서빙 시점에 Sewage로 확정됩니다.");
    }

    static void AppendPenalty(StringBuilder sb, string label, float penalty)
    {
        if (penalty <= 0f) return;

        sb.AppendLine($"  {label} — 감점 {penalty:0.0}");
    }
}
