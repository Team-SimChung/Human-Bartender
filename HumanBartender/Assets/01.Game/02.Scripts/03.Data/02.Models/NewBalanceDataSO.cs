using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewBalanceConfig
{
    // ── 시작 상태 ────────────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("gold_start")] public int GoldStart { get; set; }
    [field: SerializeField][JsonProperty("reputation_start")] public int ReputationStart { get; set; }
    [field: SerializeField][JsonProperty("commute_in_time")] public string CommuteInTime { get; set; }
    [field: SerializeField][JsonProperty("commute_out_time")] public string CommuteOutTime { get; set; }
    [field: SerializeField][JsonProperty("data_schema_version")] public string DataSchemaVersion { get; set; }

    // ── 손님 대기 ────────────────────────────────────────────────────────
    /// <summary>착석 후 코스터를 기다려 주는 기준 시간. 성격에 따른 배율을 곱한 뒤 min/max로 자른다.</summary>
    [field: SerializeField][JsonProperty("coaster_base_sec")] public float CoasterBaseSec { get; set; }
    [field: SerializeField][JsonProperty("coaster_min_sec")] public float CoasterMinSec { get; set; }
    [field: SerializeField][JsonProperty("coaster_max_sec")] public float CoasterMaxSec { get; set; }

    /// <summary>제조 제한시간 위에 얹어 주는 서빙 대기 여유.</summary>
    [field: SerializeField][JsonProperty("serve_bonus_sec")] public float ServeBonusSec { get; set; }
    /// <summary>하루가 지날 때마다 서빙 여유에서 깎는 양. 뒤로 갈수록 빡빡해진다.</summary>
    [field: SerializeField][JsonProperty("serve_grace_per_day_sec")] public float ServeGracePerDaySec { get; set; }
    /// <summary>서빙 여유의 하한. 아무리 깎여도 이보다 짧아지지 않는다.</summary>
    [field: SerializeField][JsonProperty("serve_min_bonus_sec")] public float ServeMinBonusSec { get; set; }

    [field: SerializeField][JsonProperty("warn_yellow_ratio")] public float WarnYellowRatio { get; set; }
    [field: SerializeField][JsonProperty("warn_red_ratio")] public float WarnRedRatio { get; set; }

    // ── 손님 등장 ────────────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("first_spawn_delay_sec")] public float FirstSpawnDelaySec { get; set; }
    [field: SerializeField][JsonProperty("spawn_delay_default_sec")] public float SpawnDelayDefault { get; set; }
    [field: SerializeField][JsonProperty("reseat_delay_sec")] public float ReseatDelaySec { get; set; }
    [field: SerializeField][JsonProperty("next_round_delay_sec")] public float NextRoundDelaySec { get; set; }
    [field: SerializeField][JsonProperty("order_bark_gap_sec")] public float OrderBarkGapSec { get; set; }
    [field: SerializeField][JsonProperty("idle_min_sec")] public float IdleMinSec { get; set; }
    [field: SerializeField][JsonProperty("idle_max_sec")] public float IdleMaxSec { get; set; }
    [field: SerializeField][JsonProperty("drunk_vomit_chance")] public float DrunkVomitChance { get; set; }
    [field: SerializeField][JsonProperty("guest_acc_none_weight")] public int GuestAccNoneWeight { get; set; }

    // ── 저장·불러오기 (저장 시스템이 정본. 여기서는 값만 읽어 둔다) ──
    [field: SerializeField][JsonProperty("save_checkpoint_policy")] public string SaveCheckpointPolicy { get; set; }
    [field: SerializeField][JsonProperty("manual_save_allowed_phase")] public string ManualSaveAllowedPhase { get; set; }
    [field: SerializeField][JsonProperty("manual_save_slot_count")] public int ManualSaveSlotCount { get; set; }
    [field: SerializeField][JsonProperty("autosave_slot_count")] public int AutosaveSlotCount { get; set; }
    [field: SerializeField][JsonProperty("autosave_on_commute_in_enter")] public bool AutosaveOnCommuteInEnter { get; set; }
    [field: SerializeField][JsonProperty("autosave_on_bar_enter")] public bool AutosaveOnBarEnter { get; set; }
    [field: SerializeField][JsonProperty("autosave_on_part2_start")] public bool AutosaveOnPart2Start { get; set; }
    [field: SerializeField][JsonProperty("autosave_on_daily_sales_settlement_complete")] public bool AutosaveOnDailySalesSettlementComplete { get; set; }
    [field: SerializeField][JsonProperty("commute_in_resume_spot_id")] public string CommuteInResumeSpotId { get; set; }
    [field: SerializeField][JsonProperty("commute_out_resume_spot_id")] public string CommuteOutResumeSpotId { get; set; }
    [field: SerializeField][JsonProperty("bar_mid_session_autosave_enabled")] public bool BarMidSessionAutosaveEnabled { get; set; }

    /// <summary>같은 error_code가 이 횟수만큼 잇따르면 안전 이탈을 안내한다(구현·검증 계약 §10.2).</summary>
    [field: SerializeField][JsonProperty("data_error_safe_exit_after_same_code")] public int DataErrorSafeExitAfterSameCode { get; set; }

    /// <summary>주문이 지정되지 않은 손님에게 칵테일을 고르는 방식.</summary>
    [field: SerializeField][JsonProperty("random_order_sampling_mode")] public string RandomOrderSamplingMode { get; set; }

    /// <summary>서빙 제한시간 만료와 서빙이 같은 프레임에 겹쳤을 때 어느 쪽을 먼저 처리할지.</summary>
    [field: SerializeField][JsonProperty("serve_timeout_same_frame_priority")] public string ServeTimeoutSameFramePriority { get; set; }
    [field: SerializeField][JsonProperty("leave_coaster_rep")] public int LeaveCoasterRep { get; set; }
    [field: SerializeField][JsonProperty("leave_serve_rep")] public int LeaveServeRep { get; set; }

    // ── 제조 중 바 운영 정지 ─────────────────────────────────────────────
    /// <summary>바 타이머를 멈추는 시점의 이름. 어떤 화면에서 멈출지를 데이터가 정한다.</summary>
    [field: SerializeField][JsonProperty("craft_pause_start")] public string CraftPauseStart { get; set; }
    /// <summary>바 타이머를 다시 돌리는 시점의 이름.</summary>
    [field: SerializeField][JsonProperty("craft_pause_end")] public string CraftPauseEnd { get; set; }
    /// <summary>버리기를 골랐을 때 정지 상태를 유지하는지. 아직 바로 돌아간 게 아니라서 true다.</summary>
    [field: SerializeField][JsonProperty("discard_keeps_pause")] public bool DiscardKeepsPause { get; set; }

    // ── 강제 Sewage ──────────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("force_sewage_on_order_mismatch")] public bool ForceSewageOnOrderMismatch { get; set; }
    [field: SerializeField][JsonProperty("force_sewage_on_missing_core")] public bool ForceSewageOnMissingCore { get; set; }

    // ── 구성 오류 감점 ───────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("glass_mismatch_penalty")] public float GlassMismatchPenalty { get; set; }
    /// <summary>도구를 잘못 골랐을 때와 아예 고르지 않았을 때 모두 이 값을 쓴다.</summary>
    [field: SerializeField][JsonProperty("tool_mismatch_penalty")] public float ToolMismatchPenalty { get; set; }
    /// <summary>정답 재료를 빠뜨렸을 때 재료 1개당 감점. 핵심 재료 누락은 감점이 아니라 강제 Sewage다.</summary>
    [field: SerializeField][JsonProperty("missing_ingredient_penalty")] public float MissingIngredientPenalty { get; set; }
    [field: SerializeField][JsonProperty("extra_ingredient_penalty")] public float ExtraIngredientPenalty { get; set; }
    /// <summary>병따기 실패 1회당 개별 점수에서 깎는 값.</summary>
    [field: SerializeField][JsonProperty("open_penalty_per_failure")] public float OpenPenaltyPerFailure { get; set; }

    // ── 기믹 가중치와 집계 방식 ──────────────────────────────────────────
    [field: SerializeField][JsonProperty("weight_open")] public float WeightOpen { get; set; }
    [field: SerializeField][JsonProperty("weight_pour")] public float WeightPour { get; set; }
    [field: SerializeField][JsonProperty("weight_squeeze")] public float WeightSqueeze { get; set; }
    [field: SerializeField][JsonProperty("weight_powder")] public float WeightPowder { get; set; }
    [field: SerializeField][JsonProperty("weight_shake")] public float WeightShake { get; set; }
    [field: SerializeField][JsonProperty("weight_stir")] public float WeightStir { get; set; }
    /// <summary>필업의 가중치를 어디서 가져올지. 따르기와 같은 조작이라 그 값을 함께 쓴다.</summary>
    [field: SerializeField][JsonProperty("fill_up_weight_source")] public string FillUpWeightSource { get; set; }
    /// <summary>실제로 등장한 기믹의 가중치만 모아 100으로 정규화할지.</summary>
    [field: SerializeField][JsonProperty("normalize_present_gimmick_weights")] public bool NormalizePresentGimmickWeights { get; set; }
    [field: SerializeField][JsonProperty("score_aggregation_mode")] public string ScoreAggregationMode { get; set; }
    [field: SerializeField][JsonProperty("craft_score_formula")] public string CraftScoreFormula { get; set; }
    /// <summary>전체 제조시간을 어느 구간까지 재는지.</summary>
    [field: SerializeField][JsonProperty("craft_timer_scope")] public string CraftTimerScope { get; set; }

    // ── 단위 변환 ────────────────────────────────────────────────────────
    // Target과 Actual의 단위가 다를 때 ml로 맞춘 뒤 비교한다.
    [field: SerializeField][JsonProperty("unit_oz_to_ml")] public float UnitOzToMl { get; set; }
    [field: SerializeField][JsonProperty("unit_tsp_to_ml")] public float UnitTspToMl { get; set; }

    // ── 데모에서 빠진 기믹의 처리 ────────────────────────────────────────
    // 자동으로 넣어 주고 채점하지 않는다는 뜻이다. 나중에 플레이어 조작으로 바꿀 때 이 값만 고친다.
    [field: SerializeField][JsonProperty("squeeze_demo_behavior")] public string SqueezeDemoBehavior { get; set; }
    [field: SerializeField][JsonProperty("powder_demo_behavior")] public string PowderDemoBehavior { get; set; }
    [field: SerializeField][JsonProperty("ice_demo_behavior")] public string IceDemoBehavior { get; set; }

    // ── 병따기 기믹 ──────────────────────────────────────────────────────
    /// <summary>성공 연출을 마친 뒤 따르기로 넘어가기까지의 시간. 이 시간도 전체 제조시간에 들어간다.</summary>
    [field: SerializeField][JsonProperty("open_transition_delay_sec")] public float OpenTransitionDelaySec { get; set; }
    [field: SerializeField][JsonProperty("open_approach_sec")] public float OpenApproachSec { get; set; }
    [field: SerializeField][JsonProperty("open_judge_window_px")] public float OpenJudgeWindowPx { get; set; }
    [field: SerializeField][JsonProperty("open_start_radius_px")] public float OpenStartRadiusPx { get; set; }
    [field: SerializeField][JsonProperty("open_target_radius_px")] public float OpenTargetRadiusPx { get; set; }
    [field: SerializeField][JsonProperty("open_input_enabled_after_sec")] public float OpenInputEnabledAfterSec { get; set; }

    // ── 따르기·필업 기믹 ─────────────────────────────────────────────────
    // 필업은 따르기와 같은 조작이라 전용 값을 따로 두지 않고 이 값을 함께 쓴다.
    [field: SerializeField][JsonProperty("pour_start_angle_deg")] public float PourStartAngleDeg { get; set; }
    [field: SerializeField][JsonProperty("pour_max_tilt_angle_deg")] public float PourMaxTiltAngleDeg { get; set; }
    [field: SerializeField][JsonProperty("pour_tilt_speed_deg_per_sec")] public float PourTiltSpeedDegPerSec { get; set; }
    [field: SerializeField][JsonProperty("pour_emit_rate_ml_per_sec")] public float PourEmitRateMlPerSec { get; set; }
    [field: SerializeField][JsonProperty("pour_quantity_update_interval_sec")] public float PourQuantityUpdateIntervalSec { get; set; }

    // ── 셰이크 기믹 ──────────────────────────────────────────────────────
    /// <summary>성공·실패를 합쳐 이 수만큼 쌓이면 자동 종료된다. 점수의 분모이기도 하다.</summary>
    [field: SerializeField][JsonProperty("shake_target_stacks")] public int ShakeTargetStacks { get; set; }
    [field: SerializeField][JsonProperty("shake_bpm")] public float ShakeBpm { get; set; }
    [field: SerializeField][JsonProperty("shake_judge_radius_units")] public float ShakeJudgeRadiusUnits { get; set; }
    [field: SerializeField][JsonProperty("shake_node_lifetime_sec")] public float ShakeNodeLifetimeSec { get; set; }
    [field: SerializeField][JsonProperty("shake_pattern_node_min_count")] public int ShakePatternNodeMinCount { get; set; }
    [field: SerializeField][JsonProperty("shake_pattern_node_max_count")] public int ShakePatternNodeMaxCount { get; set; }

    // ── 스터 기믹 ────────────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("stir_target_stacks")] public int StirTargetStacks { get; set; }
    /// <summary>한 바퀴를 완성해야 하는 시간. 넘기면 실패 스택이 쌓인다. 별도 시간 점수로는 쓰지 않는다.</summary>
    [field: SerializeField][JsonProperty("stir_circle_limit_sec")] public float StirCircleLimitSec { get; set; }
    /// <summary>한 바퀴에 필요한 정답 입력 수.</summary>
    [field: SerializeField][JsonProperty("stir_inputs_per_circle")] public int StirInputsPerCircle { get; set; }
    /// <summary>잔 주변 게이지를 경고색으로 바꾸는 남은 비율.</summary>
    [field: SerializeField][JsonProperty("stir_warning_ratio")] public float StirWarningRatio { get; set; }
    [field: SerializeField][JsonProperty("stir_input_cooldown_sec")] public float StirInputCooldownSec { get; set; }

    // ── 연출·사운드 ──────────────────────────────────────────────────────
    [field: SerializeField][JsonProperty("typing_interval_ms")] public int TypingIntervalMs { get; set; }
    [field: SerializeField][JsonProperty("street_typing_interval_ms")] public int StreetTypingIntervalMs { get; set; }
    [field: SerializeField][JsonProperty("street_auto_next_delay_sec")] public float StreetAutoNextDelaySec { get; set; }
    [field: SerializeField][JsonProperty("sfx_guest_in")] public string SfxGuestIn { get; set; }
    [field: SerializeField][JsonProperty("sfx_guest_out")] public string SfxGuestOut { get; set; }
    [field: SerializeField][JsonProperty("sfx_drink_high")] public string SfxDrinkHigh { get; set; }
    [field: SerializeField][JsonProperty("sfx_drink_mid")] public string SfxDrinkMid { get; set; }
    [field: SerializeField][JsonProperty("sfx_drink_low")] public string SfxDrinkLow { get; set; }
    [field: SerializeField][JsonProperty("sfx_serve")] public string SfxServe { get; set; }
}

[Serializable]
public struct NewGradeCutData
{
    [field: SerializeField][JsonProperty("grade")] public ENewGrade Grade { get; set; }
    [field: SerializeField][JsonProperty("min_pct")] public float MinPct { get; set; }
}

/// <summary>
/// 등급 하나의 정산 규칙. 판매금액과 팁, 환불을 각각 비율로 갖는다.
/// Sewage는 판매가 0이고 환불이 1이라 그 잔값만큼 손해가 난다.
/// </summary>
[Serializable]
public struct NewSettlementRule
{
    [field: SerializeField][JsonProperty("sale_rate")] public float SaleRate { get; set; }
    [field: SerializeField][JsonProperty("tip_rate")] public float TipRate { get; set; }
    [field: SerializeField][JsonProperty("refund_rate")] public float RefundRate { get; set; }
}

/// <summary>
/// 비율 한 구간과 그 구간의 점수 또는 감점. 수량 오차와 시간 초과가 같은 표를 쓰고 band_type으로 갈린다.
///
/// 경계 포함 여부를 데이터가 직접 들고 있다. 구간이 맞닿는 지점(0.05처럼)에서 어느 쪽에 들어가는지가
/// 점수를 가르기 때문에, 코드가 임의로 정하지 않고 표에 적힌 대로 판정한다.
/// </summary>
[Serializable]
public struct NewScoreBandData
{
    [field: SerializeField][JsonProperty("band_type")] public ENewScoreBandType BandType { get; set; }
    [field: SerializeField][JsonProperty("min_ratio")] public float MinRatio { get; set; }

    /// <summary>
    /// 구간의 상한을 적힌 그대로 받는 칸. 위로 열려 있는 마지막 구간은 값이 비어 있다
    /// (현재 데이터는 null이고, 예전에는 빈 문자열이었다).
    ///
    /// 숫자와 빈 값이 같은 칸에 섞여 있어 문자열로 받은 뒤 MaxRatio에서 해석한다. float?로 바로 받으면
    /// null은 넘어가지만 빈 문자열에서 터지므로, 두 표기를 모두 견디는 쪽으로 둔다.
    /// </summary>
    [field: SerializeField][JsonProperty("max_ratio")] public string MaxRatioRaw { get; set; }

    [field: SerializeField][JsonProperty("min_inclusive")] public bool MinInclusive { get; set; }
    [field: SerializeField][JsonProperty("max_inclusive")] public bool MaxInclusive { get; set; }
    [field: SerializeField][JsonProperty("score_or_penalty")] public float ScoreOrPenalty { get; set; }

    /// <summary>구간의 상한. 상한이 없으면 null이고, 그 구간은 위로 열려 있다.</summary>
    public float? MaxRatio =>
        float.TryParse(MaxRatioRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : (float?)null;

    /// <summary>비율이 이 구간에 들어가는지. 경계 포함 여부는 데이터에 적힌 대로 따른다.</summary>
    public bool Contains(float ratio)
    {
        if (MinInclusive ? ratio < MinRatio : ratio <= MinRatio) return false;

        float? max = MaxRatio;
        if (max == null) return true;

        return MaxInclusive ? ratio <= max.Value : ratio < max.Value;
    }
}

[Serializable]
public struct NewAffinityMatrixRow
{
    [field: SerializeField][JsonProperty("taste_tier")] public ENewTasteTier TasteTier { get; set; }
    [field: SerializeField][JsonProperty("excellent")] public int Excellent { get; set; }
    [field: SerializeField][JsonProperty("good")] public int Good { get; set; }
    [field: SerializeField][JsonProperty("decent")] public int Decent { get; set; }
    [field: SerializeField][JsonProperty("poor")] public int Poor { get; set; }
    [field: SerializeField][JsonProperty("sewage")] public int Sewage { get; set; }
}

[Serializable]
public class NewBalanceDataBase
{
    [field: SerializeField][JsonProperty("config")] public NewBalanceConfig Config { get; set; }
    [field: SerializeField][JsonProperty("grade_cuts")] public NewGradeCutData[] GradeCuts { get; set; }
    /// <summary>등급별 판매·팁·환불 비율. 키는 ENewGrade의 문자열 표기다.</summary>
    [JsonProperty("settlement_rules")] public Dictionary<string, NewSettlementRule> SettlementRules { get; set; }
    /// <summary>수량 오차와 시간 초과 구간이 한 배열에 섞여 있다. band_type으로 갈라 쓴다.</summary>
    [field: SerializeField][JsonProperty("score_bands")] public NewScoreBandData[] ScoreBands { get; set; }
    [field: SerializeField][JsonProperty("affinity_matrix")] public NewAffinityMatrixRow[] AffinityMatrix { get; set; }

    /// <summary>
    /// 수량 오차율에 해당하는 개별 기믹 점수를 찾는다. 어느 구간에도 걸리지 않으면 false —
    /// 그때는 임의의 점수를 만들지 않고 데이터 오류로 처리해야 한다.
    /// </summary>
    public bool TryGetQuantityScore(float errorRatio, out float score)
    {
        return TryGetBandValue(ENewScoreBandType.Quantity, errorRatio, out score);
    }

    /// <summary>전체 제조시간 초과율에 해당하는 감점을 찾는다.</summary>
    public bool TryGetOvertimePenalty(float overtimeRatio, out float penalty)
    {
        return TryGetBandValue(ENewScoreBandType.Overtime, overtimeRatio, out penalty);
    }

    bool TryGetBandValue(ENewScoreBandType type, float ratio, out float value)
    {
        value = 0f;
        if (ScoreBands == null) return false;

        foreach (var band in ScoreBands)
        {
            if (band.BandType != type) continue;
            if (!band.Contains(ratio)) continue;

            value = band.ScoreOrPenalty;
            return true;
        }

        return false;
    }

    /// <summary>등급에 해당하는 정산 규칙을 찾는다.</summary>
    public bool TryGetSettlementRule(ENewGrade grade, out NewSettlementRule rule)
    {
        rule = default;
        if (SettlementRules == null) return false;

        return SettlementRules.TryGetValue(grade.ToString().ToLowerInvariant(), out rule);
    }
}

/// <summary>StreamingAssets/json/balance.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewBalanceDataSO", menuName = "Data/New/BalanceDataSO")]
public class NewBalanceDataSO : ScriptableObject
{
    public NewBalanceDataBase balanceData;
}
