using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 칵테일 한 잔을 만드는 흐름의 순서를 쥐고 있는 곳.
///
/// 메뉴에서 칵테일을 고르는 순간 제조 시도 하나를 열고, 준비가 끝나면 기믹 큐를 만들어 실행기에
/// 넘기고, 다 끝나면 기록을 확정해 알린다. 실제 일은 큐 빌더와 실행기가 하고 여기서는 순서만 안다.
///
/// 손님·코스터·정산은 다루지 않는다. 2부의 스토리 제조도 결국 같은 준비·기믹·계산을 쓰게 되는데,
/// 여기에 1부 전용 개념이 섞이면 그때 통째로 다시 만들어야 한다.
/// </summary>
public class CraftFlowController : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("칵테일을 고르는 좌측 메뉴. 여기서 고른 순간 제조가 시작된다.")]
    [SerializeField] CraftMenuPanel menuPanel;
    [Tooltip("메뉴가 들어 있는 좌측 슬라이드 패널. 제조가 시작되면 닫는다.")]
    [SerializeField] LeftSlidePanel craftPanel;
    [SerializeField] GimmickRunner runner;

    [Header("Data")]
    [SerializeField] NewCocktailDataSO cocktailData;
    [Tooltip("재료의 기본 동작(default_action)과 병 손질 여부(prep_action)를 읽는다.")]
    [SerializeField] NewShelfItemDataSO shelfData;
    [Tooltip("점수 구간표·가중치·감점값을 읽는다. 비우면 제조는 되지만 등급을 낼 수 없다.")]
    [SerializeField] NewBalanceDataSO balanceData;

    [Header("Test")]
    [Tooltip("제조 준비 화면이 아직 없어서, 켜 두면 정답 구성(잔·도구·재료)을 자동으로 골라 바로 기믹으로 넘어간다. " +
             "준비 화면이 생기면 끄고 지우면 된다.")]
    [SerializeField] bool autoPrepareForTest = true;

    /// <summary>진행 중인 제조 시도. 메뉴에서 고르기 전에는 null이다.</summary>
    public CraftSession Current { get; private set; }

    /// <summary>제조 준비 중인지. 준비 화면이 잔·도구·재료를 이 상태에서만 바꿀 수 있다.</summary>
    public bool IsPreparing => Current != null && Current.Phase == ECraftPhase.Preparing;

    /// <summary>제조가 시작돼 시도가 열렸을 때 발생한다. 준비 화면을 여는 쪽이 쓴다.</summary>
    public event Action<CraftSession> CraftBegan;

    /// <summary>모든 기믹이 끝나 기록이 확정됐을 때 발생한다. 판정 결과가 함께 온다.</summary>
    public event Action<CraftSession, CraftJudgement> CraftCompleted;

    /// <summary>
    /// 새 제조를 막을 조건들. 각각 막을 이유를 돌려주고, 막을 이유가 없으면 null을 돌려준다.
    ///
    /// 제조 자체는 언제 시작해도 되는 일이라 여기서는 그 조건을 알지 못한다. 1부의 사정 — 아직 내지
    /// 않은 잔이 남았다, 받을 주문이 없다 — 은 각각 다른 곳이 쥐고 있어서 밖에서 끼워 넣는다.
    /// 이유가 여럿이라 하나만 두지 않는다. 하나뿐이면 나중에 등록한 쪽이 앞의 것을 지운다.
    /// </summary>
    readonly List<Func<string>> craftBlockers = new();

    /// <summary>제조를 막을 조건을 등록한다. 막을 이유가 없을 때 null을 돌려주는 함수를 넣는다.</summary>
    public void AddCraftBlocker(Func<string> blocker)
    {
        if (blocker == null || craftBlockers.Contains(blocker)) return;

        craftBlockers.Add(blocker);
        RefreshCraftAvailability();
    }

    /// <summary>등록한 조건을 뗀다. 걸어 둔 채로 사라지면 제조가 영영 막힌다.</summary>
    public void RemoveCraftBlocker(Func<string> blocker)
    {
        if (blocker == null || !craftBlockers.Remove(blocker)) return;

        RefreshCraftAvailability();
    }

    /// <summary>지금 제조를 막는 첫 번째 이유. 막을 것이 없으면 null이다.</summary>
    public string CraftBlockedReason()
    {
        foreach (var blocker in craftBlockers)
        {
            string reason = blocker();
            if (reason != null) return reason;
        }

        return null;
    }

    /// <summary>
    /// 제조 흐름 안에 있는지. 칵테일 메뉴를 열 때 들어가고, 제조가 끝나 잔이 나올 때 빠져나온다.
    /// </summary>
    public bool IsCraftFlowActive { get; private set; }

    /// <summary>
    /// IsCraftFlowActive가 바뀔 때 발생한다. 바 운영 타이머를 멈추고 다시 흘리는 쪽이 쓴다.
    ///
    /// 제조는 손님을 모르므로 무엇을 멈춰야 하는지도 알지 못한다. 여기서는 제조 흐름에 들어갔다는
    /// 사실만 알리고, 그동안 무엇을 세지 않을지는 받는 쪽이 정한다.
    /// </summary>
    public event Action<bool> CraftFlowActiveChanged;

    /// <summary>
    /// 진행 중인 제조 준비. 메뉴에서 칵테일을 고른 뒤부터 기믹이 시작되기 전까지만 있고, 그 밖에는 null이다.
    /// 준비 화면이 선반·노트·가이드·다음 버튼을 이 객체에 물어본다.
    /// </summary>
    public CraftPreparation Preparation { get; private set; }

    /// <summary>
    /// 준비 단계의 선택이 바뀔 때 발생한다. 선반 표시, 하단 트레이, 다음 버튼, 가이드가 이 신호로 갱신된다.
    /// </summary>
    public event Action<CraftPreparation> PreparationChanged;

    /// <summary>
    /// 정답 잔·도구·선택형 재료를 모두 고른 순간 한 번 발생한다. 준비 완료 안내 팝업을 띄우는 쪽이 쓴다.
    /// </summary>
    public event Action<CraftPreparation> PreparationReady;

    void OnEnable()
    {
        if (menuPanel == null) return;

        menuPanel.CraftStarted += OnCraftStarted;
        menuPanel.CraftFlowActiveChanged += SetCraftFlowActive;

        if (craftPanel != null) craftPanel.OpenChanged += OnCraftPanelOpenChanged;
    }

    void OnDisable()
    {
        if (menuPanel == null) return;

        menuPanel.CraftStarted -= OnCraftStarted;
        menuPanel.CraftFlowActiveChanged -= SetCraftFlowActive;

        if (craftPanel != null) craftPanel.OpenChanged -= OnCraftPanelOpenChanged;

        // 제조 흐름 안에서 꺼졌다면 멈춰 둔 것을 풀어 준다. 그러지 않으면 바 시간이 멈춘 채로 남는다.
        SetCraftFlowActive(false);
    }

    /// <summary>
    /// 좌측 패널이 닫히면 제조 흐름에서도 빠져나온 것으로 본다.
    ///
    /// 칵테일 목록을 열어 둔 채 패널의 토글 버튼으로 바로 닫을 수 있어서, 목록의 뒤로 버튼만
    /// 믿으면 흐름이 열린 채로 남는다. 그러면 바 운영 시계가 멈춘 채로 남아 손님이 더 들어오지 않는다.
    ///
    /// 제조를 시작하면서 패널을 접는 경우는 제외한다 — 그때는 흐름이 이어지는 중이다.
    /// </summary>
    void OnCraftPanelOpenChanged(bool open)
    {
        if (open || IsCraftBusy) return;

        SetCraftFlowActive(false);
    }

    /// <summary>제조 시도가 진행 중인지(준비 또는 기믹 수행). 끝났거나 시작 전이면 false다.</summary>
    bool IsCraftBusy =>
        Current != null && (Current.Phase == ECraftPhase.Preparing || Current.Phase == ECraftPhase.Playing);

    void SetCraftFlowActive(bool active)
    {
        if (IsCraftFlowActive == active) return;

        IsCraftFlowActive = active;
        CraftFlowActiveChanged?.Invoke(active);
    }

    /// <summary>
    /// 메뉴에서 칵테일을 고르면 제조 시도를 연다.
    ///
    /// 이 시점에는 어느 손님에게 낼지 정해지지 않는다. 제조 화면은 손님별로 열리는 게 아니라
    /// 공통이고, 완성한 잔을 누구 앞에 놓을지는 다 만든 뒤에 정하기 때문이다.
    /// </summary>
    void OnCraftStarted(string cocktailId)
    {
        BeginCraft(cocktailId);
    }

    /// <summary>
    /// 칵테일 하나를 정해 제조 시도를 연다. 메뉴에서 고른 것과 같은 자리로, 메뉴 없이 시작해야 하는
    /// 곳(준비 화면 테스트 씬 등)이 쓴다.
    /// </summary>
    public void BeginCraft(string cocktailId)
    {
        if (IsCraftBusy)
        {
            Debug.LogWarning("[CraftFlow] 이미 제조 준비 또는 기믹이 진행 중입니다. 새 제조를 시작하지 않습니다.");
            return;
        }

        string blockedReason = CraftBlockedReason();
        if (blockedReason != null)
        {
            Debug.LogWarning($"[CraftFlow] {blockedReason}");
            return;
        }

        if (!cocktailData.TryGet(cocktailId, out NewCocktailData selected))
        {
            Debug.LogError($"[CraftFlow] '{cocktailId}' 칵테일을 데이터에서 찾지 못했습니다.");
            return;
        }

        // 세션을 먼저 연다. 아래에서 패널을 접을 때 OnCraftPanelOpenChanged가 불리는데, 그때
        // 진행 중인 시도가 없으면 제조 흐름이 끝난 것으로 보고 바 시계를 다시 흘려보낸다.
        Current = new CraftSession(cocktailId);
        Preparation = new CraftPreparation(selected, Current.Actual);
        Preparation.Changed += NotifyPreparationChanged;

        // 고르는 일이 끝났으므로 메뉴를 접고 처음 화면으로 되돌린다. 상세 뷰를 켠 채로 닫으면
        // 다음에 열었을 때 지난번 칵테일 설명이 그대로 남아 있다.
        menuPanel?.ResetToMenu();
        RefreshCraftAvailability();

        CraftBegan?.Invoke(Current);
        PreparationChanged?.Invoke(Preparation);

        if (autoPrepareForTest)
        {
            AutoPrepare();
            StartGimmicks();
        }
    }

    /// <summary>
    /// 지금 제조를 시작할 수 있는지 다시 보고 메뉴의 시작 버튼에 반영한다. 막는 조건이 바뀌면 부른다.
    /// 눌러도 막히는 버튼을 살려 두면 왜 안 되는지 알 방법이 없어서, 막히는 동안에는 버튼을 꺼 둔다.
    /// </summary>
    public void RefreshCraftAvailability()
    {
        bool enabled = !IsCraftBusy && CraftBlockedReason() == null;

        menuPanel?.SetCraftEnabled(enabled);
        craftPanel?.SetToggleInteractable(enabled);
    }

    // ── 제조 준비 (준비 화면이 부를 자리) ───────────────────────────────

    // 고르는 일 자체는 CraftPreparation이 한다. 여기서 같은 토글을 한 벌 더 두면 준비 화면이
    // 컨트롤러 없이는 돌지 않게 되고, 두 구현이 갈라진다.

    /// <summary>잔을 고른다. 같은 잔을 다시 고르면 선택이 풀린다(§3.1.1).</summary>
    public void ToggleGlass(string glassId)
    {
        if (!IsPreparing) return;

        Preparation?.ToggleGlass(glassId);
    }

    /// <summary>도구를 고른다. 같은 도구를 다시 고르면 선택이 풀린다(§3.2.1).</summary>
    public void ToggleTool(string toolId)
    {
        if (!IsPreparing) return;

        Preparation?.ToggleTool(toolId);
    }

    /// <summary>재료를 고르거나 이미 고른 재료라면 뺀다. 선반과 하단 트레이가 같은 동작을 쓴다.</summary>
    public void ToggleIngredient(string ingredientId)
    {
        if (!IsPreparing) return;

        Preparation?.ToggleIngredient(ingredientId);
    }

    /// <summary>잔을 지정해서 고른다. 토글이 아니라 값을 그대로 넣는다.</summary>
    public void SelectGlass(string glassId)
    {
        if (!IsPreparing) return;

        Current.Actual.SetGlass(glassId);
        NotifyPreparationChanged();
    }

    /// <summary>도구를 지정해서 고른다. 토글이 아니라 값을 그대로 넣는다.</summary>
    public void SelectTool(string toolId)
    {
        if (!IsPreparing) return;

        Current.Actual.SetTool(toolId);
        NotifyPreparationChanged();
    }

    /// <summary>레시피 노트를 닫았을 때 부른다. 이때부터 정답 오브젝트에 가이드가 켜진다(§3.7.5).</summary>
    public void MarkRecipeNoteRead()
    {
        if (!IsPreparing || Preparation == null) return;

        Preparation.MarkRecipeNoteRead();
        PreparationChanged?.Invoke(Preparation);
    }

    void NotifyPreparationChanged()
    {
        if (Preparation == null) return;

        PreparationChanged?.Invoke(Preparation);

        // 정답 구성을 다 갖춘 순간 한 번만 알린다. 안내일 뿐이라 여기서 기믹으로 넘기지 않는다 —
        // 넘어가는 것은 플레이어가 확인 버튼을 눌러 정한다(§3.8.1).
        if (Preparation.ConsumeReadyAnnouncement())
            PreparationReady?.Invoke(Preparation);
    }


    // ── 선반 진열 ───────────────────────────────────────────────────────

    /// <summary>오늘 잔 선반에 놓을 잔.</summary>
    public List<NewShelfItemData> GetShelfGlasses()
    {
        return CraftShelf.GetGlasses(shelfData, GameStateManager.Instance.CurrentDay);
    }

    /// <summary>오늘 도구 선반에 놓을 도구.</summary>
    public List<NewShelfItemData> GetShelfTools()
    {
        return CraftShelf.GetTools(shelfData, GameStateManager.Instance.CurrentDay);
    }

    /// <summary>오늘 재료 선반 하나에 놓을 재료. liquor는 술 선반, fridge는 냉장고다.</summary>
    public List<NewShelfItemData> GetShelfIngredients(ENewShelfGroup group)
    {
        return CraftShelf.GetIngredients(shelfData, group, GameStateManager.Instance.CurrentDay);
    }

    /// <summary>기믹으로 넘어갈 수 있는지. 잔 하나와 재료 한 종류면 된다.</summary>
    public bool CanStartGimmicks => IsPreparing && Current.Actual.CanStartGimmicks;

    /// <summary>준비를 마치고 기믹 큐를 만들어 실행한다.</summary>
    public void StartGimmicks()
    {
        if (!CanStartGimmicks)
        {
            Debug.LogWarning("[CraftFlow] 잔과 재료를 최소한 하나씩 골라야 기믹으로 넘어갈 수 있습니다.");
            return;
        }

        // 준비 단계는 여기서 끝난다. 기믹 도중에도 준비 화면이 살아 있으면 잔이나 재료를 바꿀 수 있는
        // 것처럼 보이는데, 그 시점에는 이미 큐가 만들어진 뒤라 바꿔도 반영되지 않는다.
        Preparation = null;

        RunAsync(Current).Forget();
    }

    async UniTaskVoid RunAsync(CraftSession session)
    {
        try
        {
            cocktailData.TryGet(session.SelectedCocktailId, out NewCocktailData selected);

            GimmickQueue queue = GimmickQueueBuilder.Build(selected, session.Actual, shelfData);
            LogQueue(session, queue);

            if (queue.Count == 0)
            {
                Debug.LogError("[CraftFlow] 만들어진 기믹이 없습니다. 재료의 default_action 데이터를 확인하세요.");
                return;
            }

            var display = new CraftRunDisplay(selected.TimeLimitSec, ShouldHideTime(selected, session));

            await runner.RunAsync(session, queue, display, this.GetCancellationTokenOnDestroy());

            CraftJudgement judgement = Judge(session, selected);

            CraftCompleted?.Invoke(session, judgement);
        }
        finally
        {
            // 어떤 식으로 끝나든 — 정상 완료든, 기믹 도중 취소·예외든 — 제조 흐름은 반드시 닫는다.
            // 여기를 건너뛰면 바 운영 시계가 멈춘 채로 남아, 손님이 더 들어오지 않고 코스터를 놓아도
            // 주문 대사로 넘어가지 않는다. 화면에는 아무 표시도 나지 않아 원인을 찾기 어렵다.
            //
            // 결과 화면이 붙으면 이 자리는 '제공하기'를 누른 시점으로 옮긴다(balance.json의 craft_pause_end).
            SetCraftFlowActive(false);
            RefreshCraftAvailability();
        }
    }

    /// <summary>
    /// 만든 결과를 정답과 견주어 등급을 낸다. 결과는 로그로도 남긴다 —
    /// 결과 화면이 아직 없어서, 지금은 이게 점수를 확인할 수 있는 유일한 통로다.
    /// </summary>
    CraftJudgement Judge(CraftSession session, NewCocktailData selected)
    {
        if (balanceData == null || balanceData.balanceData == null)
        {
            Debug.LogWarning("[CraftFlow] balanceData가 없어 등급을 계산하지 못했습니다.");
            return null;
        }

        CraftJudgement judgement =
            CraftJudge.Evaluate(session, selected, shelfData, balanceData.balanceData);

        Debug.Log(judgement.BuildReport(session.SelectedCocktailId) + BuildActualReport(session));

        return judgement;
    }

    /// <summary>
    /// 무엇을 얼마나 넣었는지 그대로 늘어놓는다. 점수만 보면 왜 그렇게 나왔는지 알 수 없어서,
    /// 판정 근거가 된 실제 기록을 같이 남긴다.
    /// </summary>
    static string BuildActualReport(CraftSession session)
    {
        ActualCraft actual = session.Actual;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  ── 실제 제조 ──");
        sb.AppendLine($"    잔 {actual.GlassId ?? "없음"} / 도구 {actual.ToolId ?? "없음"}");
        sb.AppendLine($"    고른 재료: {string.Join(", ", actual.IngredientIds)}");

        if (actual.AutoSqueezeIds.Count > 0 || actual.AutoPowderIds.Count > 0)
        {
            var auto = new List<string>();
            auto.AddRange(actual.AutoSqueezeIds);
            auto.AddRange(actual.AutoPowderIds);
            sb.AppendLine($"    자동 투입: {string.Join(", ", auto)}");
        }

        foreach (var gimmick in actual.GimmickResults)
        {
            string detail = gimmick.Type switch
            {
                ECraftGimmick.Open =>
                    $"{gimmick.AttemptCount}번째 시도에 성공 (실패 {gimmick.FailureCount})",
                ECraftGimmick.Shake or ECraftGimmick.Stir =>
                    $"성공 {gimmick.SuccessCount} / 실패 {gimmick.FailureCount} / 목표 {gimmick.TargetStackCount}",
                _ => gimmick.HasTarget
                    ? $"{gimmick.ActualValue:0.00} / {gimmick.TargetValue.Value:0.00}{gimmick.TargetUnit}"
                    : $"{gimmick.ActualValue:0.00} (목표 없음)",
            };

            sb.AppendLine($"    {gimmick.Type,-8} {gimmick.IngredientId ?? "-",-16} {detail}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 제조시간을 ???로 가릴지 정한다.
    ///
    /// 플레이어가 직접 고른 재료 중 정답이 하나도 없으면 이 잔에 대해 알려 줄 정답 정보가 없다.
    /// 그때는 목표 수량뿐 아니라 제한시간도 가린다. 자동으로 들어가는 재료는 언제나 레시피에서 오므로
    /// 이 판단에 넣지 않는다 — 그것까지 세면 정답을 하나도 못 골라도 시간이 보인다.
    /// </summary>
    static bool ShouldHideTime(NewCocktailData selected, CraftSession session)
    {
        foreach (string ingredientId in session.Actual.IngredientIds)
        {
            foreach (var step in selected.Recipe ?? System.Array.Empty<NewCocktailRecipeStep>())
            {
                if (step.IsSelectable && step.Ingredient == ingredientId) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 어떤 큐가 나왔는지 한 줄로 남긴다. 고른 것과 실제로 실행되는 것이 다를 때 —
    /// 오선택이나 재료 누락 — 원인을 바로 알아보기 위한 기록이다.
    /// </summary>
    void LogQueue(CraftSession session, GimmickQueue queue)
    {
        Debug.Log($"[CraftFlow] {session.SelectedCocktailId} 기믹 큐 ({queue.Count}): " +
                  string.Join(" → ", queue.Steps));

        if (queue.UnresolvedIngredientIds.Count > 0)
        {
            Debug.LogError("[CraftFlow] 기본 동작(default_action)을 몰라 큐에서 빠진 재료: " +
                           string.Join(", ", queue.UnresolvedIngredientIds));
        }
    }

    // ── 테스트용 자동 준비 ──────────────────────────────────────────────

    /// <summary>
    /// 제조 준비 화면이 생기기 전까지, 선택한 칵테일의 정답 구성을 그대로 골라 준다.
    /// 큐 생성과 기믹 실행을 실제로 돌려 보기 위한 임시 수단이라 준비 화면이 붙으면 지운다.
    /// </summary>
    void AutoPrepare()
    {
        if (!cocktailData.TryGet(Current.SelectedCocktailId, out NewCocktailData selected)) return;

        CraftPreset.ApplyTargetSetup(selected, Current.Actual);

        // 레시피에 직접 선택형 재료가 하나도 없으면 진입 조건을 못 채운다.
        // 필업 행이 아직 데이터에 없는 칵테일에서 실제로 일어날 수 있어 미리 알린다.
        if (!Current.Actual.CanStartGimmicks)
        {
            Debug.LogError($"[CraftFlow] '{Current.SelectedCocktailId}'의 정답 구성으로는 기믹에 들어갈 수 없습니다. " +
                           "레시피에 직접 선택형 재료가 있는지 확인하세요.");
        }
    }
}
