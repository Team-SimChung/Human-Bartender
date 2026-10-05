using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 확정된 제조 기록을 고른 칵테일의 정답과 견주어 craft_score와 craft_grade를 낸다.
///
/// 주문은 보지 않는다. 이 잔을 누구에게 낼지는 제조가 끝난 뒤에 정해지고, 주문과 맞는지·핵심 재료를
/// 빠뜨렸는지로 등급을 뒤집는 일은 손님 앞에 놓는 순간 서빙 쪽이 한다. 여기서 재는 것은
/// "고르기로 한 칵테일을 얼마나 잘 만들었는가" 하나뿐이다.
///
/// 100점에서 깎아 내려간다. 기믹 정확도, 잔·도구, 재료 누락·추가, 시간 초과 순이다.
///
/// 수치는 하나도 코드에 없다. 구간표도 가중치도 감점값도 balance.json에서 읽는다.
/// MonoBehaviour가 아니라서 씬 없이도 "이렇게 만들면 몇 점"을 확인할 수 있다.
/// </summary>
public static class CraftJudge
{
    public static CraftJudgement Evaluate(CraftSession session, NewCocktailData selected,
                                          NewShelfItemDataSO shelfData, NewBalanceDataBase balance)
    {
        var result = new CraftJudgement();

        if (session == null || balance == null)
        {
            result.Status = ENewScoreStatus.DataError;
            result.ErrorCode = "MISSING_CONTEXT";
            return result;
        }

        ActualCraft actual = session.Actual;

        result.ElapsedSec = actual.ElapsedManualSec;
        result.TimeLimitSec = selected.TimeLimitSec;

        // 주문은 보지 않는다. 이 잔을 누구에게 낼지는 아직 정해지지 않았고, 정해지더라도 주문과
        // 맞는지는 서빙 쪽이 판단한다. 제조는 "고른 칵테일을 얼마나 잘 만들었는가"만 따진다.
        CollectMissingCore(selected, actual, result);

        ScoreGimmicks(selected, actual, shelfData, balance, result);
        ScoreComposition(selected, actual, balance.Config, result);
        ScoreOvertime(balance, result);

        // 목표가 미확정인 채로 점수를 만들면 그럴듯한 가짜 등급이 나온다. 채점 자체를 막는다.
        if (result.Status == ENewScoreStatus.DataError) return result;

        result.CraftScore = Mathf.Clamp(100f - result.TotalPenalty, 0f, 100f);
        result.CraftGrade = ToGrade(balance, result.CraftScore.Value);

        return result;
    }

    /// <summary>
    /// 빠뜨린 핵심 재료를 모아 둔다. 여기서는 등급을 건드리지 않는다 —
    /// 강제 Sewage는 완성한 잔을 손님 앞에 놓은 뒤 서빙 쪽이 확정한다.
    ///
    /// 수량을 틀린 것은 누락이 아니다. 진을 조금만 넣었다면 핵심 재료는 있는 것이므로 수량 오차로 가고,
    /// 진을 아예 고르지 않았을 때만 여기 담긴다.
    /// </summary>
    static void CollectMissingCore(NewCocktailData selected, ActualCraft actual, CraftJudgement result)
    {
        foreach (string coreId in selected.GetCoreIngredientIds())
        {
            if (actual.HasIngredient(coreId)) continue;

            result.MissingCoreIngredientIds.Add(coreId);
        }
    }

    // ── 기믹 점수 ───────────────────────────────────────────────────────

    /// <summary>정답이 요구하는 기믹 하나.</summary>
    struct Expected
    {
        public ECraftGimmick Type;
        public string IngredientId;
        public float? TargetValue;
        public ENewUnit? TargetUnit;

        /// <summary>재료 자체를 고르지 않아 기믹이 만들어질 수 없었는지. 이 경우 평균에서 빠진다.</summary>
        public bool IngredientNotSelected;
    }

    static void ScoreGimmicks(NewCocktailData selected, ActualCraft actual,
                              NewShelfItemDataSO shelfData, NewBalanceDataBase balance,
                              CraftJudgement result)
    {
        List<Expected> expected = BuildExpected(selected, actual, shelfData);

        var used = new HashSet<int>();
        var sums = new Dictionary<ECraftGimmick, float>();
        var buckets = new Dictionary<ECraftGimmick, GimmickTypeScore>();

        foreach (var want in expected)
        {
            // 고르지 않은 재료는 기믹이 생길 수 없었다. 이건 기믹을 못한 게 아니라 재료를 빠뜨린 것이라
            // 재료 누락 감점에서 한 번만 다룬다. 여기서 0점으로 또 세면 같은 실수를 두 번 깎는다.
            if (want.IngredientNotSelected) continue;

            ECraftGimmick family = ToFamily(want.Type);
            GimmickTypeScore bucket = GetBucket(buckets, result, family);
            bucket.ExpectedCount++;

            GimmickResult matched = FindResult(actual, want, used);

            if (matched == null)
            {
                bucket.MissingCount++;
                Add(sums, family, 0f);
                continue;
            }

            bucket.MatchedCount++;

            float score = ScoreOne(want, matched, balance, out bool dataError);
            if (dataError) bucket.HasDataError = true;

            Add(sums, family, score);
        }

        WeighTypes(sums, buckets, balance.Config, result);
    }

    /// <summary>
    /// 정답이 요구하는 기믹 목록을 만든다. 채점하지 않는 줄(자동으로 넣어 주는 재료)은 넣지 않는다 —
    /// 플레이어가 손대지 않은 것으로 점수를 매길 수는 없다.
    /// </summary>
    static List<Expected> BuildExpected(NewCocktailData selected, ActualCraft actual,
                                        NewShelfItemDataSO shelfData)
    {
        var list = new List<Expected>();

        foreach (var step in selected.Recipe ?? System.Array.Empty<NewCocktailRecipeStep>())
        {
            if (!step.Scored) continue;

            bool notSelected = step.IsSelectable && !actual.HasIngredient(step.Ingredient);

            // 병 손질은 재료의 동작을 대신하지 않고 앞에 한 단계 더 붙는다.
            if (shelfData != null && shelfData.RequiresOpen(step.Ingredient))
            {
                list.Add(new Expected
                {
                    Type = ECraftGimmick.Open,
                    IngredientId = step.Ingredient,
                    IngredientNotSelected = notSelected,
                });
            }

            list.Add(new Expected
            {
                Type = ToGimmick(step.Action),
                IngredientId = step.Ingredient,
                TargetValue = step.Qty,
                TargetUnit = step.Unit,
                IngredientNotSelected = notSelected,
            });
        }

        // 믹스는 재료가 아니라 도구가 정한다. 정답 도구를 안 골랐다면 이 기믹은 미수행으로 남는다.
        ENewMixMethod targetMix = selected.TargetMixMethod;
        if (targetMix == ENewMixMethod.Shake || targetMix == ENewMixMethod.Stir)
        {
            list.Add(new Expected
            {
                Type = targetMix == ENewMixMethod.Shake ? ECraftGimmick.Shake : ECraftGimmick.Stir,
            });
        }

        return list;
    }

    /// <summary>
    /// 정답 항목에 이어 붙일 실제 결과를 찾는다. 종류와 대상 재료가 모두 같아야 한다.
    ///
    /// 같은 종류의 기믹을 오선택 재료로 수행했더라도 정답 자리를 대신하지 못한다.
    /// 위스키를 정확한 양으로 따랐다고 해서 빠뜨린 럼의 점수가 되지는 않는다.
    /// </summary>
    static GimmickResult FindResult(ActualCraft actual, Expected want, HashSet<int> used)
    {
        for (int i = 0; i < actual.GimmickResults.Count; i++)
        {
            if (used.Contains(i)) continue;

            GimmickResult candidate = actual.GimmickResults[i];
            if (candidate.Type != want.Type) continue;
            if (candidate.IngredientId != want.IngredientId) continue;

            used.Add(i);
            return candidate;
        }

        return null;
    }

    static float ScoreOne(Expected want, GimmickResult matched, NewBalanceDataBase balance,
                          out bool dataError)
    {
        dataError = false;
        NewBalanceConfig config = balance.Config;

        switch (want.Type)
        {
            case ECraftGimmick.Open:
                // 첫 시도에 열면 감점이 없고, 실패할 때마다 정해진 만큼 깎는다.
                if (!matched.Completed) return 0f;
                return Mathf.Max(0f, 100f - matched.FailureCount * config.OpenPenaltyPerFailure);

            case ECraftGimmick.Shake:
            case ECraftGimmick.Stir:
                // 못 채운 스택이 그대로 점수에 반영된다. 실패를 또 깎지 않는다.
                return Mathf.Min(100f, matched.CompletionScore * 100f);

            default:
                return ScoreQuantity(want, matched, balance, out dataError);
        }
    }

    /// <summary>
    /// 목표와 실제의 차이를 비율로 바꿔 구간표에서 점수를 찾는다.
    /// 모자란 쪽과 넘친 쪽을 나누지 않는다 — 같은 양만큼 벗어났으면 같은 점수다.
    /// </summary>
    static float ScoreQuantity(Expected want, GimmickResult matched, NewBalanceDataBase balance,
                               out bool dataError)
    {
        dataError = false;

        float? targetMl = ToMl(want.TargetValue, want.TargetUnit, balance.Config);
        float? actualMl = ToMl(matched.ActualValue, matched.TargetUnit ?? want.TargetUnit, balance.Config);

        // 목표가 없으면 오차를 낼 수 없다. 임의의 점수를 지어내지 않고 데이터 오류로 남긴다.
        if (targetMl == null || targetMl.Value <= 0f || actualMl == null)
        {
            dataError = true;
            return 0f;
        }

        float errorRatio = Mathf.Abs(actualMl.Value - targetMl.Value) / targetMl.Value;

        if (balance.TryGetQuantityScore(errorRatio, out float score)) return score;

        dataError = true;
        return 0f;
    }

    /// <summary>
    /// 종류별 대표 점수에 가중치를 걸어 하나의 점수로 합친다.
    ///
    /// 실제로 등장한 종류의 가중치만 모아 100이 되도록 다시 나눈다. 그러지 않으면 기믹이 적은 칵테일이
    /// 아무리 잘 만들어도 만점을 받을 수 없다 — 없는 기믹의 몫이 그대로 빠지기 때문이다.
    /// </summary>
    static void WeighTypes(Dictionary<ECraftGimmick, float> sums,
                           Dictionary<ECraftGimmick, GimmickTypeScore> buckets,
                           NewBalanceConfig config, CraftJudgement result)
    {
        float totalWeight = 0f;
        float weighted = 0f;

        foreach (var pair in buckets)
        {
            GimmickTypeScore bucket = pair.Value;
            if (bucket.ExpectedCount <= 0) continue;

            bucket.RepresentativeScore = sums[pair.Key] / bucket.ExpectedCount;

            float weight = WeightOf(pair.Key, config);
            totalWeight += weight;
            weighted += bucket.RepresentativeScore * weight;

            if (!bucket.HasDataError) continue;

            result.Status = ENewScoreStatus.DataError;
            result.ErrorCode ??= "TARGET_QTY_MISSING";
        }

        // 기믹이 하나도 없으면 깎을 것도 없다.
        if (totalWeight <= 0f)
        {
            result.WeightedGimmickScore = 100f;
            result.GimmickErrorPenalty = 0f;
            return;
        }

        result.WeightedGimmickScore = weighted / totalWeight;
        result.GimmickErrorPenalty = 100f - result.WeightedGimmickScore;
    }

    /// <summary>
    /// 채점할 때 어느 계열로 묶이는지. 필업은 따르기와 조작이 같고 순서상 위치만 달라서 함께 평균낸다.
    ///
    /// 따로 평균내면 필업이 한 번뿐인 칵테일에서 그 한 번이 따르기 여러 번과 같은 무게를 갖는다.
    /// </summary>
    static ECraftGimmick ToFamily(ECraftGimmick type) =>
        type == ECraftGimmick.FillUp ? ECraftGimmick.Pour : type;

    static float WeightOf(ECraftGimmick family, NewBalanceConfig config) => family switch
    {
        ECraftGimmick.Open => config.WeightOpen,
        ECraftGimmick.Pour => config.WeightPour,
        ECraftGimmick.Squeeze => config.WeightSqueeze,
        ECraftGimmick.Powder => config.WeightPowder,
        ECraftGimmick.Shake => config.WeightShake,
        ECraftGimmick.Stir => config.WeightStir,
        _ => 0f,
    };

    // ── 구성 오류 ───────────────────────────────────────────────────────

    static void ScoreComposition(NewCocktailData selected, ActualCraft actual,
                                 NewBalanceConfig config, CraftJudgement result)
    {
        if (actual.GlassId != selected.Glass)
            result.GlassPenalty = config.GlassMismatchPenalty;

        // 도구를 아예 안 고른 것도 잘못 고른 것과 같게 본다. 어느 쪽이든 정답 믹스가 나오지 않는다.
        string targetTool = selected.TargetToolId;
        if (!string.IsNullOrEmpty(targetTool) && actual.ToolId != targetTool)
            result.ToolPenalty = config.ToolMismatchPenalty;

        var expectedIds = new HashSet<string>();

        foreach (var step in selected.Recipe ?? System.Array.Empty<NewCocktailRecipeStep>())
        {
            if (!step.IsSelectable) continue;

            expectedIds.Add(step.Ingredient);

            // 핵심 재료 누락은 이미 되돌릴 수 없는 조건에서 다뤘으므로 여기까지 오지 않는다.
            if (!actual.HasIngredient(step.Ingredient))
                result.MissingIngredientIds.Add(step.Ingredient);
        }

        foreach (string id in actual.IngredientIds)
        {
            if (!expectedIds.Contains(id)) result.ExtraIngredientIds.Add(id);
        }

        result.MissingIngredientPenalty = result.MissingIngredientIds.Count * config.MissingIngredientPenalty;
        result.ExtraIngredientPenalty = result.ExtraIngredientIds.Count * config.ExtraIngredientPenalty;
    }

    // ── 시간 ────────────────────────────────────────────────────────────

    static void ScoreOvertime(NewBalanceDataBase balance, CraftJudgement result)
    {
        if (result.TimeLimitSec <= 0f) return;

        result.OvertimeRatio =
            Mathf.Max(0f, result.ElapsedSec - result.TimeLimitSec) / result.TimeLimitSec;

        if (balance.TryGetOvertimePenalty(result.OvertimeRatio, out float penalty))
            result.OvertimePenalty = penalty;
    }

    // ── 등급 ────────────────────────────────────────────────────────────

    /// <summary>점수를 등급으로 바꾼다. 닿는 구간 중 기준선이 가장 높은 것을 쓴다.</summary>
    static ENewGrade ToGrade(NewBalanceDataBase balance, float score)
    {
        ENewGrade best = ENewGrade.Sewage;
        float bestMin = float.MinValue;

        foreach (var cut in balance.GradeCuts ?? System.Array.Empty<NewGradeCutData>())
        {
            if (score < cut.MinPct) continue;
            if (cut.MinPct < bestMin) continue;

            best = cut.Grade;
            bestMin = cut.MinPct;
        }

        return best;
    }

    // ── 도우미 ──────────────────────────────────────────────────────────

    static float? ToMl(float? value, ENewUnit? unit, NewBalanceConfig config)
    {
        if (value == null) return null;

        return unit switch
        {
            ENewUnit.Oz => config.UnitOzToMl > 0f ? value.Value * config.UnitOzToMl : (float?)null,
            ENewUnit.Tsp => config.UnitTspToMl > 0f ? value.Value * config.UnitTspToMl : (float?)null,
            ENewUnit.Ml => value.Value,
            _ => null,
        };
    }

    static ECraftGimmick ToGimmick(ENewRecipeAction action) => action switch
    {
        ENewRecipeAction.Pour => ECraftGimmick.Pour,
        ENewRecipeAction.FillUp => ECraftGimmick.FillUp,
        ENewRecipeAction.Squeeze => ECraftGimmick.Squeeze,
        _ => ECraftGimmick.Powder,
    };

    static void Add(Dictionary<ECraftGimmick, float> sums, ECraftGimmick family, float score)
    {
        sums.TryGetValue(family, out float current);
        sums[family] = current + score;
    }

    static GimmickTypeScore GetBucket(Dictionary<ECraftGimmick, GimmickTypeScore> buckets,
                                      CraftJudgement result, ECraftGimmick family)
    {
        if (buckets.TryGetValue(family, out var found)) return found;

        var created = new GimmickTypeScore { Family = family };
        buckets[family] = created;
        result.GimmickScores.Add(created);

        return created;
    }
}
