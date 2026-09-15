// Editor/GimmickQueuePreview.cs
// 메뉴: Tools > Craft > Preview Gimmick Queue

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 지금 데이터로 칵테일마다 어떤 기믹 큐가 만들어지는지 뽑아 본다.
///
/// 큐는 레시피와 재료의 기본 동작이 맞물려 나오는 결과라 데이터만 봐서는 순서를 가늠하기 어렵다.
/// 필업 줄이 빠져 있거나 제조법이 비어 있으면 기믹 하나가 통째로 사라지는데, 게임을 돌려 보기 전까지
/// 그걸 알 방법이 없다. 그래서 정답 구성으로 골랐을 때의 큐를 한 번에 늘어놓고 눈으로 확인한다.
///
/// 실제 플레이는 플레이어가 고른 대로 만들어지므로 여기 나오는 것은 "정답대로 골랐을 때"의 모습이다.
/// </summary>
public static class GimmickQueuePreview
{
    const string CocktailFile = "cocktails";
    const string ShelfItemFile = "shelf_items";

    [MenuItem("Tools/Craft/Preview Gimmick Queue")]
    public static void Run()
    {
        var catalog = CsvDataReader.LoadDirectory(System.IO.Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var cocktails = catalog.Read<NewCocktailData[]>(CocktailFile);
        var shelfItems = catalog.Read<NewShelfItemData[]>(ShelfItemFile);

        if (cocktails == null)
        {
            Debug.LogError("[GimmickQueue] Cocktails.csv를 읽지 못했습니다.");
            return;
        }

        var shelfData = ScriptableObject.CreateInstance<NewShelfItemDataSO>();
        shelfData.shelfItemData = shelfItems;

        var problems = new List<string>();
        var sb = new StringBuilder();

        sb.AppendLine($"[GimmickQueue] 정답 구성으로 골랐을 때의 기믹 큐 — 칵테일 {cocktails.Length}종");
        sb.AppendLine();

        foreach (var cocktail in cocktails)
        {
            var actual = new ActualCraft();
            CraftPreset.ApplyTargetSetup(cocktail, actual);

            GimmickQueue queue = GimmickQueueBuilder.Build(cocktail, actual, shelfData);

            string mark = cocktail.Status == ENewDataStatus.Confirmed ? "" : "  (tbd)";
            sb.AppendLine($"{cocktail.Id}{mark}");
            sb.AppendLine(queue.Count == 0
                ? "    (기믹 없음)"
                : "    " + string.Join(" → ", queue.Steps));

            // 아직 채우는 중인 칵테일은 빈 곳이 많은 게 정상이라 확정된 것만 따진다.
            if (cocktail.Status == ENewDataStatus.Confirmed)
                CollectProblems(cocktail, actual, queue, problems);
        }

        if (problems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── 확인이 필요한 항목 (status=confirmed 만) ──");
            foreach (string problem in problems)
                sb.AppendLine("  " + problem);
        }

        Object.DestroyImmediate(shelfData);

        if (problems.Count > 0) Debug.LogWarning(sb.ToString());
        else Debug.Log(sb.ToString());
    }

    /// <summary>큐가 비었거나, 기믹이 빠졌거나, 채점하는데 목표 수량이 없는 자리를 모은다.</summary>
    static void CollectProblems(NewCocktailData cocktail, ActualCraft actual,
                               GimmickQueue queue, List<string> problems)
    {
        if (!actual.CanStartGimmicks)
            problems.Add($"{cocktail.Id}: 정답 구성으로도 진입 조건(잔 + 선택형 재료 1종)을 못 채웁니다.");

        foreach (string ingredientId in queue.UnresolvedIngredientIds)
            problems.Add($"{cocktail.Id}: '{ingredientId}'의 default_action을 몰라 큐에서 빠졌습니다.");

        // 제조법이 있는데 믹스 기믹이 안 나왔다면 정답 도구를 못 찾았다는 뜻이다.
        bool wantsMix = cocktail.Mix == ENewMixMethod.Shake || cocktail.Mix == ENewMixMethod.Stir;
        bool hasMix = queue.Steps.Any(s => s.Type == ECraftGimmick.Shake || s.Type == ECraftGimmick.Stir);

        if (wantsMix && !hasMix)
            problems.Add($"{cocktail.Id}: mix가 {cocktail.Mix}인데 믹스 기믹이 만들어지지 않았습니다.");

        // 레시피에 있는 줄이 큐에 안 들어갔다면 어딘가에서 빠진 것이다.
        // 자동으로 넣어 주는 줄은 원래 기믹이 없으므로 셈에서 뺀다.
        if (cocktail.Recipe != null)
        {
            foreach (var step in cocktail.Recipe)
            {
                if (step.AutoApply) continue;
                if (queue.Steps.Any(s => s.IngredientId == step.Ingredient)) continue;

                problems.Add($"{cocktail.Id}: 레시피의 '{step.Ingredient}'({step.Action})가 큐에 없습니다.");
            }
        }

        foreach (var queueStep in queue.Steps)
        {
            // 병따기와 믹스는 원래 수량을 쓰지 않는다.
            if (queueStep.Type == ECraftGimmick.Open ||
                queueStep.Type == ECraftGimmick.Shake ||
                queueStep.Type == ECraftGimmick.Stir) continue;

            if (!queueStep.HasTarget)
                problems.Add($"{cocktail.Id}: {queueStep.Type}({queueStep.IngredientId})의 목표 수량이 없습니다.");
        }
    }
}
