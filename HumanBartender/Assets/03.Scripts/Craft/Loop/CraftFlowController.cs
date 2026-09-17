using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// UI 버튼과 외부 코드가 함께 사용하는 제조 관리자.
/// 요청·준비·순서·기록·판정·종료를 관리하고 오브젝트 실행은 GimmickRunner에 맡긴다.
/// 기존 씬 연결을 보존하기 위해 CraftFlowController 이름과 스크립트 GUID를 유지한다.
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
    [Tooltip("테스트용: 정답 구성(잔·도구·재료)을 자동으로 고르고 바로 기믹을 실행한다.")]
    [SerializeField] bool autoPrepareForTest = true;


    public bool IsCraftFlowActive { get; private set; }
    public event Action<bool> CraftFlowActiveChanged;

    public void CancelCurrentCraft()
    {
        if (Current != null) CancelCraft(Current.JobId);
    }

    void UpdateAvailability()
    {
        bool available = !IsBusy && CraftBlockedReason() == null;
        menuPanel?.SetCraftEnabled(available);
        craftPanel?.SetToggleInteractable(available);
    }
    void OnEnable()
    {
        if (menuPanel != null)
        {
            menuPanel.CraftStarted += BeginCraft;
            menuPanel.CraftFlowActiveChanged += OnMenuFlowChanged;
        }
        if (craftPanel != null) craftPanel.OpenChanged += OnCraftPanelOpenChanged;
        RefreshCraftAvailability();
    }

    void OnDisable()
    {
        if (menuPanel != null)
        {
            menuPanel.CraftStarted -= BeginCraft;
            menuPanel.CraftFlowActiveChanged -= OnMenuFlowChanged;
        }
        if (craftPanel != null) craftPanel.OpenChanged -= OnCraftPanelOpenChanged;
        CancelCurrentCraft();
        SetCraftFlowActive(false);
    }

    void OnDestroy() => CancelCurrentCraft();

    void OnMenuFlowChanged(bool active)
    {
        if (!active && IsBusy) return;
        SetCraftFlowActive(active);
    }

    void OnCraftPanelOpenChanged(bool open)
    {
        if (!open && !IsBusy) SetCraftFlowActive(false);
    }

    void SetCraftFlowActive(bool active)
    {
        if (IsCraftFlowActive == active) return;
        IsCraftFlowActive = active;
        Notify(CraftFlowActiveChanged, active);
    }

    void OnCraftAccepted()
    {
        SetCraftFlowActive(true);
        menuPanel?.ResetToMenu();
        UpdateAvailability();
    }

    void OnCraftJudged(CraftSession session, CraftJudgement judgement)
    {
        if (judgement != null)
            Debug.Log(judgement.BuildReport(session.SelectedCocktailId) + BuildActualReport(session));
        else
            Debug.LogWarning("[CraftFlow] balanceData가 없어 등급을 계산하지 못했습니다.");
    }


    public void BeginCraft(string cocktailId)
    {
        if (!isActiveAndEnabled) return;
        if (!TryBegin(cocktailId, out _, out string reason))
        {
            Debug.LogWarning($"[CraftFlow] {reason}");
            return;
        }
        if (autoPrepareForTest && IsPreparing)
        {
            AutoPrepare();
            StartGimmicks();
        }
    }

    public void StartGimmicks()
    {
        if (!CanStartGimmicks)
        {
            Debug.LogWarning("[CraftFlow] 잔과 재료를 최소한 하나씩 골라야 기믹으로 넘어갈 수 있습니다.");
            return;
        }
        StartGimmicksAsync().Forget();
    }

    protected virtual ICraftExecutor Executor => runner;
    readonly List<Func<string>> blockers = new();
    CancellationTokenSource cancellation;
    UniTaskCompletionSource<CraftSession> completionSource;
    NewCocktailData selected;
    bool running;
    bool finishing;

    public CraftSession Current { get; private set; }
    public CraftPreparation Preparation { get; private set; }
    public GimmickQueue Plan { get; private set; }
    public CraftJudgement Judgement { get; private set; }
    /// <summary>현재 요청의 최종 결과. 수락 직후 보관하면 준비 중 취소도 기다릴 수 있다.</summary>
    public UniTask<CraftSession> Completion { get; private set; }
    public bool IsPreparing => Current?.Phase == ECraftPhase.Preparing && !running && !finishing;
    public bool IsBusy => running || finishing || IsPreparing;
    public bool CanStartGimmicks => IsPreparing && Current.Actual.CanStartGimmicks;

    public event Action<CraftSession> CraftBegan;
    public event Action<CraftPreparation> PreparationChanged;
    public event Action<CraftSession, CraftJudgement> CraftCompleted;
    /// <summary>성공·실패·취소 모두 정리가 끝난 뒤 한 번 알린다.</summary>
    public event Action<CraftSession> CraftEnded;
    public event Action AvailabilityChanged;


    public string CraftBlockedReason()
    {
        foreach (var blocker in blockers.ToArray())
        {
            string reason = blocker();
            if (reason != null) return reason;
        }
        return null;
    }

    public void AddCraftBlocker(Func<string> blocker)
    {
        if (blocker == null || blockers.Contains(blocker)) return;
        blockers.Add(blocker);
        RefreshCraftAvailability();
    }

    public void RemoveCraftBlocker(Func<string> blocker)
    {
        if (blocker != null && blockers.Remove(blocker)) RefreshCraftAvailability();
    }

    public void RefreshCraftAvailability()
    {
        UpdateAvailability();
        Notify(AvailabilityChanged);
    }

    /// <summary>수락되면 작업 ID를 가진 세션을 반환한다. 거절은 진행 중인 작업을 변경하지 않는다.</summary>
    public bool TryBegin(string cocktailId, out CraftSession session, out string reason)
    {
        session = null;
        if (!isActiveAndEnabled) { reason = "제조 컴포넌트가 비활성 상태입니다."; return false; }
        reason = IsBusy ? "이미 제조 또는 정리가 진행 중입니다." : CraftBlockedReason();
        if (reason != null) return false;
        if (cocktailData == null || !cocktailData.TryGet(cocktailId, out selected))
        {
            reason = $"'{cocktailId}' 칵테일 데이터를 찾지 못했습니다.";
            return false;
        }

        Current = session = new CraftSession(cocktailId);
        Plan = null;
        Judgement = null;
        cancellation = new CancellationTokenSource();
        completionSource = new UniTaskCompletionSource<CraftSession>();
        Completion = completionSource.Task.Preserve();
        Preparation = new CraftPreparation(selected, session.Actual);
        Preparation.Changed += NotifyPreparationChanged;
        Notify(OnCraftAccepted);
        Notify(CraftBegan, session);
        if (IsPreparing) NotifyPreparationChanged();
        RefreshCraftAvailability();
        return true;
    }

    public void ToggleGlass(string id) { if (IsPreparing) Preparation.ToggleGlass(id); }
    public void ToggleTool(string id) { if (IsPreparing) Preparation.ToggleTool(id); }
    public void ToggleIngredient(string id) { if (IsPreparing) Preparation.ToggleIngredient(id); }
    public void SelectGlass(string id)
    {
        if (!IsPreparing) return;
        Current.Actual.SetGlass(id);
        NotifyPreparationChanged();
    }
    public void SelectTool(string id)
    {
        if (!IsPreparing) return;
        Current.Actual.SetTool(id);
        NotifyPreparationChanged();
    }
    public void MarkRecipeNoteRead()
    {
        if (!IsPreparing) return;
        Preparation.MarkRecipeNoteRead();
        Notify(PreparationChanged, Preparation);
    }
    public void AutoPrepare()
    {
        if (!IsPreparing) return;
        CraftPreset.ApplyTargetSetup(selected, Current.Actual);
        NotifyPreparationChanged();
    }

    void NotifyPreparationChanged()
    {
        var preparation = Preparation;
        if (preparation == null) return;
        Notify(PreparationChanged, preparation);
    }

    void ClearPreparation()
    {
        if (Preparation == null) return;
        Preparation.Changed -= NotifyPreparationChanged;
        Preparation = null;
    }

    /// <summary>준비된 작업을 실행한다. 반환 시에는 화면 정리와 종료 상태 확정까지 끝나 있다.</summary>
    public async UniTask<CraftSession> StartGimmicksAsync()
    {
        if (!CanStartGimmicks)
            throw new InvalidOperationException("제조 준비 중에 잔과 재료를 선택해야 합니다.");

        var executor = Executor;
        var session = Current;
        var token = cancellation.Token;
        running = true;
        ClearPreparation();
        try
        {
            Plan = GimmickQueueBuilder.Build(selected, session.Actual, shelfData);
            if (Plan.UnresolvedIngredientIds.Count > 0)
                throw new InvalidOperationException("기믹 동작을 찾을 수 없는 재료: " +
                    string.Join(", ", Plan.UnresolvedIngredientIds));
            if (Plan.Count == 0)
                throw new InvalidOperationException("만들어진 기믹이 없습니다.");
            if (executor == null)
                throw new InvalidOperationException("기믹 실행 담당이 연결되지 않았습니다.");

            token.ThrowIfCancellationRequested();
            session.BeginGimmicks();
            var context = CraftContext.From(session);
            // Begin 도중 실패해도 화면을 복구한다.
            try
            {
                executor.Begin(session.Timer, new CraftRunDisplay(selected.TimeLimitSec, ShouldHideTime(session)));
                foreach (var step in Plan.Steps)
                {
                    token.ThrowIfCancellationRequested();
                    var result = await executor.ExecuteAsync(step, context, session.Timer, token);
                    token.ThrowIfCancellationRequested();
                    if (result == null)
                        throw new InvalidOperationException($"'{step.Type}' 기믹의 결과가 없습니다.");
                    session.Actual.Record(result);
                    session.AdvanceGimmick();
                }
            }
            finally
            {
                executor.End();
            }

            token.ThrowIfCancellationRequested();
            session.Timer.Stop();
            session.Actual.FixElapsedManual(session.Timer.ElapsedSec);
            Judgement = balanceData != null && balanceData.balanceData != null
                ? CraftJudge.Evaluate(session, selected, shelfData, balanceData.balanceData) : null;
            session.Complete();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            session.Cancel();
        }
        catch (Exception exception)
        {
            session.Fail(exception.Message);
        }
        finally
        {
            Finish(session);
        }
        return session;
    }

    /// <summary>이전 호출이 새 제조를 멈추지 않도록 작업 ID가 일치할 때만 취소한다.</summary>
    public bool CancelCraft(string jobId)
    {
        if (Current == null || Current.JobId != jobId || !IsBusy || finishing) return false;
        if (running)
        {
            cancellation.Cancel();
        }
        else
        {
            ClearPreparation();
            Current.Cancel();
            Finish(Current);
        }
        return true;
    }

    /// <summary>해당 작업의 실제 정리와 종료 알림이 끝날 때까지 기다린다.</summary>
    public async UniTask<bool> CancelCraftAsync(string jobId)
    {
        var completion = Completion;
        if (!CancelCraft(jobId)) return false;
        await completion;
        return true;
    }

    void Finish(CraftSession session)
    {
        var completedRequest = completionSource;
        finishing = true;
        running = false;
        cancellation?.Dispose();
        cancellation = null;
        if (session.Phase == ECraftPhase.Completed)
        {
            Notify<CraftSession, CraftJudgement>(OnCraftJudged, session, Judgement);
            Notify(CraftCompleted, session, Judgement);
        }
        SetCraftFlowActive(false);
        if (session.Phase == ECraftPhase.Failed)
            Debug.LogWarning($"[CraftFlow] 제조 실패 ({session.JobId}): {session.FailureReason}");
        Notify(CraftEnded, session);
        finishing = false;
        RefreshCraftAvailability();
        completedRequest.TrySetResult(session);
    }

    bool ShouldHideTime(CraftSession session)
    {
        foreach (string id in session.Actual.IngredientIds)
            foreach (var step in selected.Recipe ?? Array.Empty<NewCocktailRecipeStep>())
                if (step.IsSelectable && step.Ingredient == id) return false;
        return true;
    }

    // 표시 측 예외가 작업 종료와 다른 구독자에 대한 알림을 막지 않도록 한다.
    static void Notify(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception e) { Debug.LogException(e); }
    }
    static void Notify<T>(Action<T> handlers, T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
            try { handler(value); } catch (Exception e) { Debug.LogException(e); }
    }
    static void Notify<T, U>(Action<T, U> handlers, T first, U second)
    {
        if (handlers == null) return;
        foreach (Action<T, U> handler in handlers.GetInvocationList())
            try { handler(first, second); } catch (Exception e) { Debug.LogException(e); }
    }
    public List<NewShelfItemData> GetShelfGlasses() =>
        CraftShelf.GetGlasses(shelfData, GameStateManager.Instance.CurrentDay);
    public List<NewShelfItemData> GetShelfTools() =>
        CraftShelf.GetTools(shelfData, GameStateManager.Instance.CurrentDay);
    public List<NewShelfItemData> GetShelfIngredients(ENewShelfGroup group) =>
        CraftShelf.GetIngredients(shelfData, group, GameStateManager.Instance.CurrentDay);

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



}
