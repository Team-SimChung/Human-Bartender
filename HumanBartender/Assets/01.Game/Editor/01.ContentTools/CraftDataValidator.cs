// Editor/CraftDataValidator.cs
// 메뉴: Tools > Craft > Validate Craft Data

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 제조 루프가 기대는 데이터 규칙이 실제로 지켜지는지 검사한다.
///
/// 기믹 큐 생성과 등급 계산은 재료의 default_action, 칵테일의 핵심 재료 같은 값을 그대로 믿고 돌아간다.
/// 그 값이 비어 있거나 서로 어긋나면 기믹이 조용히 빠지거나 늘 0점이 나오는 식으로 드러나는데,
/// 그때는 원인이 데이터인지 코드인지 구분하기 어렵다. 그래서 값을 쓰기 전에 여기서 먼저 걸러낸다.
///
/// StreamingAssets의 CSV를 직접 읽는다. SO 에셋에 직렬화되어 남아 있는 값이 아니라 정본을 본다.
/// </summary>
public static class CraftDataValidator
{
    const string CocktailFile = "cocktails";
    const string ShelfItemFile = "shelf_items";
    const string BalanceFile = "balance";

    readonly struct Issue
    {
        public readonly string Scope;
        public readonly string Message;

        public Issue(string scope, string message)
        {
            Scope = scope;
            Message = message;
        }
    }

    [MenuItem("Tools/Craft/Validate Craft Data")]
    public static void Run()
    {
        var catalog = CsvDataReader.LoadDirectory(System.IO.Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var cocktails = catalog.Read<NewCocktailData[]>(CocktailFile);
        var shelfItems = catalog.Read<NewShelfItemData[]>(ShelfItemFile);
        var balance = catalog.Read<NewBalanceDataBase>(BalanceFile);

        if (cocktails == null || shelfItems == null || balance == null)
        {
            Debug.LogError("[CraftData] CSV를 읽지 못했습니다. StreamingAssets/csv 경로를 확인하세요.");
            return;
        }

        var errors = new List<Issue>();
        var warnings = new List<Issue>();

        var itemById = new Dictionary<string, NewShelfItemData>();
        foreach (var item in shelfItems)
        {
            if (item.Id == null) continue;
            itemById[item.Id] = item;
        }

        ValidateShelfItems(shelfItems, errors, warnings);
        ValidateCocktails(cocktails, itemById, errors, warnings);
        ValidateJudgementConfig(balance, errors, warnings);

        Report(cocktails, shelfItems.Length, errors, warnings);
    }

    // ── 선반 ────────────────────────────────────────────────────────────

    static void ValidateShelfItems(NewShelfItemData[] items, List<Issue> errors, List<Issue> warnings)
    {
        foreach (var item in items)
        {
            if (!item.IsIngredient) continue;

            string scope = $"재료 {item.Id}";

            // default_action이 없으면 이 재료로는 어떤 기믹도 만들 수 없다.
            // 오선택으로 골랐을 때 조용히 사라진다.
            if (item.DefaultAction == null)
            {
                errors.Add(new Issue(scope, "default_action이 없습니다. 기믹 큐에 들어가지 못합니다."));
                continue;
            }

            // 병 손질은 재료의 동작을 대체하지 않고 앞에 붙는 단계다. 따라서 여는 재료는 결국 따라야 한다.
            if (item.RequiresOpen && item.DefaultAction != ENewRecipeAction.Pour)
            {
                errors.Add(new Issue(scope,
                    $"prep_action이 open인데 default_action이 {item.DefaultAction}입니다. " +
                    "뚜껑을 딴 뒤에는 따르기가 와야 합니다."));
            }

            // 액체가 화면에 보이는 기믹은 재료 색을 그대로 쓴다. 색이 없으면 부을 것을 그릴 수 없다.
            if (item.IsSelectable && !item.TryGetLiquidColor(out _))
                warnings.Add(new Issue(scope, "액체 색상값이 없습니다. 따르기·필업 화면에서 쓸 색이 없습니다."));
        }
    }

    // ── 칵테일 ──────────────────────────────────────────────────────────

    static void ValidateCocktails(NewCocktailData[] cocktails,
                                  Dictionary<string, NewShelfItemData> itemById,
                                  List<Issue> errors, List<Issue> warnings)
    {
        foreach (var cocktail in cocktails)
        {
            string scope = $"칵테일 {cocktail.Id}";

            // 아직 채우는 중인 칵테일은 빈 곳이 많은 게 정상이다. 확정된 것만 엄격하게 본다.
            bool strict = cocktail.Status == ENewDataStatus.Confirmed;

            ValidateGlassAndTool(cocktail, itemById, scope, strict, errors, warnings);
            ValidateRecipe(cocktail, itemById, scope, strict, errors, warnings);
        }
    }

    static void ValidateGlassAndTool(NewCocktailData cocktail,
                                     Dictionary<string, NewShelfItemData> itemById,
                                     string scope, bool strict,
                                     List<Issue> errors, List<Issue> warnings)
    {
        var bucket = strict ? errors : warnings;

        if (string.IsNullOrEmpty(cocktail.Glass))
        {
            bucket.Add(new Issue(scope, "정답 잔(glass)이 없습니다. 잔 판정을 할 수 없습니다."));
        }
        else if (!itemById.TryGetValue(cocktail.Glass, out var glass))
        {
            bucket.Add(new Issue(scope, $"정답 잔 '{cocktail.Glass}'이 shelf_items에 없습니다."));
        }
        else if (glass.Kind != ENewShelfKind.Glass)
        {
            errors.Add(new Issue(scope, $"정답 잔 '{cocktail.Glass}'의 kind가 {glass.Kind}입니다."));
        }

        // 판정에 쓰는 제조법과 표시용 제조법이 갈라지면, 화면에 보이는 것과 실제 정답 도구가 달라진다.
        // target_mix_method가 비어 있을 때도 여기서 걸린다 — 기본값이 Build라 조용히 넘어가기 때문이다.
        if (cocktail.TargetMixMethod != cocktail.Mix)
        {
            errors.Add(new Issue(scope,
                $"mix는 {cocktail.Mix}인데 target_mix_method는 {cocktail.TargetMixMethod}입니다."));
        }

        // 병 손질도 칵테일과 재료 양쪽에 적혀 있다. 어긋나면 어느 쪽이 맞는지 알 수 없다.
        bool ingredientNeedsOpen = false;
        foreach (var step in cocktail.Recipe ?? System.Array.Empty<NewCocktailRecipeStep>())
        {
            if (itemById.TryGetValue(step.Ingredient ?? "", out var ing) && ing.RequiresOpen)
                ingredientNeedsOpen = true;
        }

        bool cocktailNeedsOpen = cocktail.TargetPrepAction == ENewPrepAction.Open;
        if (cocktailNeedsOpen != ingredientNeedsOpen)
        {
            warnings.Add(new Issue(scope,
                $"target_prep_action은 {(cocktailNeedsOpen ? "open" : "없음")}인데 " +
                $"재료 쪽 prep_action은 {(ingredientNeedsOpen ? "open" : "없음")}입니다."));
        }

        // 도구가 없는 칵테일(빌드 계열)은 정상이다. 도구가 필요한 제조법일 때만 선반에 있는지 본다.
        string toolId = cocktail.TargetToolId;
        if (string.IsNullOrEmpty(toolId)) return;

        if (!itemById.TryGetValue(toolId, out var tool))
            errors.Add(new Issue(scope, $"제조법 {cocktail.Mix}에 필요한 도구 '{toolId}'이 shelf_items에 없습니다."));
        else if (tool.Kind != ENewShelfKind.Tool)
            errors.Add(new Issue(scope, $"도구 '{toolId}'의 kind가 {tool.Kind}입니다."));
    }

    static void ValidateRecipe(NewCocktailData cocktail,
                               Dictionary<string, NewShelfItemData> itemById,
                               string scope, bool strict,
                               List<Issue> errors, List<Issue> warnings)
    {
        var bucket = strict ? errors : warnings;

        if (cocktail.Recipe == null || cocktail.Recipe.Length == 0)
        {
            bucket.Add(new Issue(scope, "레시피가 비어 있습니다."));
            return;
        }

        int coreCount = 0;

        foreach (var step in cocktail.Recipe)
        {
            if (!itemById.TryGetValue(step.Ingredient ?? "", out var item))
            {
                errors.Add(new Issue(scope, $"레시피의 재료 '{step.Ingredient}'가 shelf_items에 없습니다."));
                continue;
            }

            if (!item.IsIngredient)
            {
                errors.Add(new Issue(scope, $"레시피의 '{step.Ingredient}'는 재료가 아니라 {item.Kind}입니다."));
                continue;
            }

            if (step.IsCore)
            {
                coreCount++;

                // 자동으로 넣어 주는 재료는 플레이어가 뺄 수 없다. 절대 누락될 수 없으니 핵심 재료로
                // 둘 이유가 없고, 두면 강제 Sewage 조건이 영원히 성립하지 않는 죽은 규칙이 된다.
                if (step.AutoApply)
                {
                    errors.Add(new Issue(scope,
                        $"'{step.Ingredient}'는 auto_apply인데 is_core입니다. 누락될 수 없어 판정이 무의미합니다."));
                }
            }

            // 레시피의 action과 재료의 default_action이 다르면, 같은 재료가 정답으로 쓰일 때와
            // 오선택으로 쓰일 때 서로 다른 기믹으로 실행된다. 그러면 정답 기믹과 Actual이 연결되지
            // 않아 늘 0점이 된다.
            if (item.DefaultAction != null && item.DefaultAction != step.Action)
            {
                warnings.Add(new Issue(scope,
                    $"'{step.Ingredient}'의 레시피 action은 {step.Action}인데 " +
                    $"재료 default_action은 {item.DefaultAction}입니다."));
            }

            // 채점하는 줄에는 목표 수량이 있어야 한다. 채점하지 않는 줄(데모에서 자동 처리)은 없어도 된다.
            if (step.Scored && !step.HasTarget)
            {
                bucket.Add(new Issue(scope,
                    $"'{step.Ingredient}'({step.Action})는 scored인데 목표 수량이 없습니다. " +
                    "수량 판정이 DATA_ERROR가 됩니다."));
            }

            // 자동으로 넣어 주면서 채점까지 하면, 플레이어가 손대지 않은 결과로 점수를 매기게 된다.
            if (step.AutoApply && step.Scored)
            {
                warnings.Add(new Issue(scope,
                    $"'{step.Ingredient}'가 auto_apply이면서 scored입니다. 조작 없이 점수가 매겨집니다."));
            }
        }

        // 핵심 재료가 하나도 없으면 어떤 재료를 빠뜨려도 강제 Sewage가 나오지 않는다.
        if (coreCount == 0)
            bucket.Add(new Issue(scope, "핵심 재료(is_core)가 하나도 없습니다. 강제 Sewage 판정이 성립하지 않습니다."));
    }

    // ── 판정 Config ─────────────────────────────────────────────────────

    static void ValidateJudgementConfig(NewBalanceDataBase balance, List<Issue> errors, List<Issue> warnings)
    {
        const string scope = "balance.json";

        ValidateBands(scope, balance, ENewScoreBandType.Quantity, errors);
        ValidateBands(scope, balance, ENewScoreBandType.Overtime, errors);

        if (balance.Config.ShakeTargetStacks <= 0)
            errors.Add(new Issue(scope, "shake_target_stacks가 0 이하입니다. 셰이크 점수의 분모가 됩니다."));

        if (balance.Config.StirTargetStacks <= 0)
            errors.Add(new Issue(scope, "stir_target_stacks가 0 이하입니다. 스터 점수의 분모가 됩니다."));

        if (balance.Config.StirCircleLimitSec <= 0f)
            errors.Add(new Issue(scope, "stir_circle_limit_sec가 0 이하입니다. 한 바퀴를 완성할 시간이 없습니다."));

        // 단위가 섞인 레시피가 있어서 ml 환산 없이는 오차를 비교할 수 없다.
        if (balance.Config.UnitOzToMl <= 0f || balance.Config.UnitTspToMl <= 0f)
            errors.Add(new Issue(scope, "unit_oz_to_ml 또는 unit_tsp_to_ml가 비어 있습니다. 단위 변환을 할 수 없습니다."));

        if (balance.SettlementRules == null || balance.SettlementRules.Count == 0)
        {
            errors.Add(new Issue(scope, "settlement_rules가 비어 있습니다."));
        }
        else
        {
            foreach (ENewGrade grade in System.Enum.GetValues(typeof(ENewGrade)))
            {
                if (!balance.TryGetSettlementRule(grade, out _))
                    errors.Add(new Issue(scope, $"settlement_rules에 '{grade}' 등급이 없습니다."));
            }
        }

        if (balance.GradeCuts == null || balance.GradeCuts.Length == 0)
            errors.Add(new Issue(scope, "grade_cuts가 비어 있습니다."));
    }

    /// <summary>
    /// 구간표에 빈틈이나 겹침이 없는지 확인한다. 대표 지점 몇 개를 실제로 조회해 보는 방식이라,
    /// 경계 포함 여부(min_inclusive/max_inclusive)가 잘못 들어간 경우도 같이 잡힌다.
    /// </summary>
    static void ValidateBands(string scope, NewBalanceDataBase balance,
                              ENewScoreBandType type, List<Issue> errors)
    {
        var bands = balance.ScoreBands?.Where(b => b.BandType == type).ToArray();

        if (bands == null || bands.Length == 0)
        {
            errors.Add(new Issue(scope, $"score_bands에 {type} 구간이 없습니다."));
            return;
        }

        // 마지막 구간이 닫혀 있으면 그보다 큰 값이 어느 구간에도 걸리지 않는다.
        if (bands.All(b => b.MaxRatio != null))
            errors.Add(new Issue(scope, $"{type} 구간에 상한 없는 마지막 구간이 없습니다. 큰 값이 판정에서 빠집니다."));

        // 경계와 그 주변을 훑어 어느 구간에도 안 걸리는 값이 있는지 본다.
        var probes = new List<float> { 0f };
        foreach (var band in bands)
        {
            probes.Add(band.MinRatio);
            probes.Add(band.MinRatio + 0.001f);
            if (band.MaxRatio != null)
            {
                probes.Add(band.MaxRatio.Value);
                probes.Add(band.MaxRatio.Value + 0.001f);
            }
        }

        foreach (float probe in probes.Distinct())
        {
            int hits = bands.Count(b => b.Contains(probe));

            if (hits == 0)
                errors.Add(new Issue(scope, $"{type} 구간이 비율 {probe:0.###}을 담지 못합니다."));
            else if (hits > 1)
                errors.Add(new Issue(scope, $"{type} 구간이 비율 {probe:0.###}에서 {hits}개 겹칩니다."));
        }
    }

    // ── 결과 출력 ───────────────────────────────────────────────────────

    static void Report(NewCocktailData[] cocktails, int shelfItemCount,
                       List<Issue> errors, List<Issue> warnings)
    {
        int confirmed = cocktails.Count(c => c.Status == ENewDataStatus.Confirmed);

        var sb = new StringBuilder();
        sb.AppendLine($"[CraftData] 칵테일 {cocktails.Length}종(확정 {confirmed} / 미확정 {cocktails.Length - confirmed}) " +
                      $"· 선반 아이템 {shelfItemCount}개 — 오류 {errors.Count}건, 경고 {warnings.Count}건");
        sb.AppendLine("  · status가 tbd인 칵테일은 빈 값을 경고로만 봅니다.");

        Append(sb, "오류 (구현이 이 값에 의존합니다)", errors);
        Append(sb, "경고 (진행은 되지만 확인이 필요합니다)", warnings);

        if (errors.Count > 0) Debug.LogError(sb.ToString());
        else if (warnings.Count > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    static void Append(StringBuilder sb, string title, List<Issue> issues)
    {
        if (issues.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"── {title} ──");

        foreach (var group in issues.GroupBy(i => i.Scope))
        {
            sb.AppendLine(group.Key);
            foreach (var issue in group)
                sb.AppendLine($"    {issue.Message}");
        }
    }
}
