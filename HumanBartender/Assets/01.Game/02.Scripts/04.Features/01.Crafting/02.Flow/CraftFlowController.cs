using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// UI 버튼과 외부 코드가 함께 사용하는 제조 관리자.
    /// 제조 요청·준비·순서·기록·판정·종료를 관리하고 오브젝트 실행은 GimmickRunner에 맡긴다.
/// 기존 씬 연결을 보존하기 위해 CraftFlowController 이름과 스크립트 GUID를 유지한다.
/// </summary>
public class CraftFlowController : MonoBehaviour
{
    [Inject] ISoundEffectPlayer sound;
    [Header("Scene")]
    [SerializeField] private GimmickRunner runner;
    [Tooltip("제조 결과를 보여주고 버리기/제공하기를 선택하는 화면. 비우면 완성 잔을 바로 트레이에 알린다.")]
    [SerializeField] private ServeScreen serveScreen;

    [Header("Data")]
    [SerializeField] private NewCocktailDataSO cocktailData;
    [Tooltip("현재 대기 주문. 대본이 지정한 미해금 레시피의 일시적 선택 권한을 확인한다.")]
    [SerializeField] private OrderRequestController orderRequests;
    [Tooltip("재료의 기본 동작(default_action)과 병 손질 여부(prep_action)를 읽는다.")]
    [SerializeField] private NewShelfItemDataSO shelfData;
    [Tooltip("점수 구간표·가중치·감점값을 읽는다. 비우면 제조는 되지만 등급을 낼 수 없다.")]
    [SerializeField] private NewBalanceDataSO balanceData;

    [Header("Test")]
    [Tooltip("테스트용: 정답 구성(잔·도구·재료)을 자동으로 고르고 바로 기믹을 실행한다.")]
    [SerializeField] private bool autoPrepareForTest = true;


    // 실행 중 상태
    readonly List<Func<string>> blockers = new();
    CancellationTokenSource cancellation;
    UniTaskCompletionSource<CraftSession> completionSource;
    NewCocktailData selected;
    bool running;
    bool finishing;

    // 외부 조회: 주문 상태는 OrderRequestController에서 관리한다.
    protected virtual ICraftExecutor Executor => runner;
    public CraftSession Current { get; private set; }
    public CraftPreparation Preparation { get; private set; }
    /// <summary>기믹 재시도에서는 준비 선택을 복원하므로 준비 화면을 만들지 않는다.</summary>
    public bool ShouldShowPreparation { get; private set; }
    public GimmickQueue Plan { get; private set; }
    public CraftJudgement Judgement { get; private set; }
    /// <summary>현재 요청의 최종 결과. 수락 직후 보관하면 준비 중 취소도 기다릴 수 있다.</summary>
    public UniTask<CraftSession> Completion { get; private set; }
    public bool IsPreparing => Current?.Phase == ECraftPhase.Preparing && !running && !finishing;
    public bool IsBusy => running || finishing || IsPreparing;
    public bool CanStartGimmicks => IsPreparing && Current.Actual.CanStartGimmicks && !NeedsBottleOpener;
    public bool NeedsBottleOpener => IsPreparing && RequiresBottleOpener(Current.Actual);
    public event Action<string> PreparationBlocked;
    public bool IsCraftFlowActive { get; private set; }
    public CraftedDrink PendingDrink { get; private set; }
    public bool IsAwaitingServeDecision { get; private set; }
    public event Action<CraftedDrink> DrinkReady;
    /// <summary>재시도를 수락해 기존 잔을 폐기했을 때 표시 쪽에 알린다.</summary>
    public event Action<CraftedDrink> DrinkDiscarded;

    // 외부 알림
    public event Action<CraftSession> CraftBegan;
    public event Action<CraftPreparation> PreparationChanged;
    public event Action<CraftSession, CraftJudgement> CraftCompleted;
    /// <summary>성공·실패·취소 모두 정리가 끝난 뒤 한 번 알린다.</summary>
    public event Action<CraftSession> CraftEnded;
    public event Action AvailabilityChanged;



    public event Action<bool> CraftFlowActiveChanged;

    // Unity 수명주기
    void OnEnable()
    {
        RefreshCraftAvailability();
    }

    void OnDisable()
    {
        CancelCurrentCraft();
        if (IsAwaitingServeDecision)
        {
            TryDiscardPendingDrink(PendingDrink);
            serveScreen?.Reset();
        }
        SetCraftFlowActive(false);
    }

    void OnDestroy() => CancelCurrentCraft();

    void SetCraftFlowActive(bool active)
    {
        if (IsCraftFlowActive == active) return;
        IsCraftFlowActive = active;
        Notify(CraftFlowActiveChanged, active);
    }

    void OnCraftAccepted()
    {
        SetCraftFlowActive(true);
    }

    void OnCraftJudged(CraftSession session, CraftJudgement judgement)
    {
        if (judgement != null)
            Debug.Log(judgement.BuildReport(session.SelectedCocktailId) + BuildActualReport(session));
        else
            Debug.LogWarning("[CraftFlow] balanceData가 없어 등급을 계산하지 못했습니다.");
    }


    // UI 버튼 진입점
    public void CancelCurrentCraft()
    {
        if (Current != null) CancelCraft(Current.JobId);
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
            if (NeedsBottleOpener)
            {
                Notify(PreparationBlocked, "병따기가 필요한 재료가 있습니다. 오프너를 선택한 후 제조를 시작해 주세요.");
                return;
            }
            Debug.LogWarning("[CraftFlow] 잔과 재료를 최소한 하나씩 골라야 기믹으로 넘어갈 수 있습니다.");
            return;
        }
        StartGimmicksAsync().Forget();
    }

    /// <summary>결과 화면의 다시 제작 버튼. 이전 선택을 유지하고 첫 기믹부터 새로 실행한다.</summary>
    public void RetryGimmicks()
    {
        if (!TryRetryGimmicks(PendingDrink, out string reason))
            Debug.LogWarning($"[CraftFlow] {reason}");
    }

    /// <summary>결과 화면이 보관한 잔과 현재 잔이 일치할 때만 재시도한다. 거절하면 기존 잔을 유지한다.</summary>
    public bool TryRetryGimmicks(CraftedDrink drink, out string reason)
    {
        if (drink == null || !ReferenceEquals(PendingDrink, drink) ||
            !ReferenceEquals(Current, drink.Session) || drink.Session.Phase != ECraftPhase.Completed)
        {
            reason = "다시 제작할 완성 잔이 없거나 이전 제조의 결과입니다.";
            return false;
        }
        if (!TryBegin(drink.CocktailId, drink, out var session, out reason)) return false;
        if (ReferenceEquals(Current, session) && CanStartGimmicks) StartGimmicks();
        return true;
    }

    // 제조 가능 조건
    public string CraftBlockedReason() => CraftBlockedReason(null);

    string CraftBlockedReason(CraftedDrink replacingDrink)
    {
        if (PendingDrink != null && !ReferenceEquals(PendingDrink, replacingDrink))
            return "아직 내지 않은 잔이 있습니다.";
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
        Notify(AvailabilityChanged);
    }

    // 제조 시작과 준비 입력
    /// <summary>수락되면 작업 ID를 가진 세션을 반환한다. 거절은 진행 중인 작업을 변경하지 않는다.</summary>
    public bool TryBegin(string cocktailId, out CraftSession session, out string reason)
        => TryBegin(cocktailId, null, out session, out reason);

    bool TryBegin(string cocktailId, CraftedDrink retryDrink, out CraftSession session, out string reason)
    {
        session = null;
        if (!isActiveAndEnabled) { reason = "제조 컴포넌트가 비활성 상태입니다."; return false; }
        reason = IsBusy ? "이미 제조 또는 정리가 진행 중입니다." : CraftBlockedReason(retryDrink);
        if (reason != null) return false;
        if (cocktailData == null || !cocktailData.TryGet(cocktailId, out var cocktail))
        {
            reason = $"'{cocktailId}' 칵테일 데이터를 찾지 못했습니다.";
            return false;
        }

        bool isRequested = orderRequests != null && orderRequests.HasWaitingOrderForCocktail(cocktail.Id);
        if (!CocktailUnlockPolicy.CanSelect(cocktail, GameStateManager.Instance.CurrentDay, isRequested))
        {
            reason = "아직 해금되지 않은 칵테일 레시피입니다.";
            return false;
        }

        if (retryDrink != null)
        {
            if (!retryDrink.Session.Actual.CanStartGimmicks)
            {
                reason = "이전 제조의 잔과 재료 선택이 없어 기믹을 다시 시작할 수 없습니다.";
                return false;
            }
            if (RequiresBottleOpener(retryDrink.Session.Actual))
            {
                reason = "병따기가 필요한 재료가 있습니다. 오프너 선택을 확인해 주세요.";
                return false;
            }
        }

        selected = cocktail;
        Current = session = new CraftSession(cocktailId, retryDrink?.Session.OrderCocktailId);
        if (retryDrink != null)
        {
            var previous = retryDrink.Session.Actual;
            session.Actual.SetGlass(previous.GlassId);
            session.Actual.SetTool(previous.ToolId);
            foreach (string id in previous.IngredientIds) session.Actual.SelectIngredient(id);
        }
        Plan = null;
        Judgement = null;
        cancellation = new CancellationTokenSource();
        completionSource = new UniTaskCompletionSource<CraftSession>();
        Completion = completionSource.Task.Preserve();
        Preparation = new CraftPreparation(selected, session.Actual);
        ShouldShowPreparation = retryDrink == null;
        Preparation.Changed += NotifyPreparationChanged;
        if (retryDrink != null)
        {
            // 새 세션으로 제조 진입을 예약한 뒤 알린다. 표시 콜백이 새 제조를 중복 시작할 수 없다.
            retryDrink.Session.Discard();
            PendingDrink = null;
            IsAwaitingServeDecision = false;
            serveScreen?.Reset();
            Notify(DrinkDiscarded, retryDrink);
        }
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
        if (NeedsBottleOpener) Current.Actual.SetBottleOpener(true);
        NotifyPreparationChanged();
    }

    public List<NewShelfItemData> GetShelfGlasses() =>
        CraftShelf.GetGlasses(shelfData, GameStateManager.Instance.CurrentDay);
    public List<NewShelfItemData> GetShelfTools() =>
        CraftShelf.GetTools(shelfData, GameStateManager.Instance.CurrentDay);
    public List<NewShelfItemData> GetShelfIngredients(ENewShelfGroup group) =>
        CraftShelf.GetIngredients(shelfData, group, GameStateManager.Instance.CurrentDay);

    void NotifyPreparationChanged()
    {
        var preparation = Preparation;
        if (preparation == null) return;
        Notify(PreparationChanged, preparation);
    }

    void ClearPreparation()
    {
        ShouldShowPreparation = false;
        if (Preparation == null) return;
        Preparation.Changed -= NotifyPreparationChanged;
        Preparation = null;
        // 준비 화면의 표시 여부는 이 상태가 정본이다. 기믹이 시작되거나 준비가
        // 취소되면 구독 화면에도 같은 전환을 알린다.
        Notify(PreparationChanged, null);
    }

    // 기믹 실행과 종료
    /// <summary>준비된 작업을 실행한다. 반환 시에는 화면 정리와 종료 상태 확정까지 끝나 있다.</summary>
    public async UniTask<CraftSession> StartGimmicksAsync()
    {
        if (NeedsBottleOpener)
        {
            Notify(PreparationBlocked, "병따기가 필요한 재료가 있습니다. 오프너를 선택한 후 제조를 시작해 주세요.");
            return Current;
        }
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
            PendingDrink = new CraftedDrink(session, Judgement, selected);
            IsAwaitingServeDecision = serveScreen != null;
            if (!IsAwaitingServeDecision || !serveScreen.TryPlay(PendingDrink))
            {
                IsAwaitingServeDecision = false;
                Notify(DrinkReady, PendingDrink);
            }
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

    public bool ConsumeDrink(CraftedDrink drink)
    {
        if (drink == null || IsAwaitingServeDecision || !ReferenceEquals(PendingDrink, drink)) return false;
        PendingDrink = null;
        RefreshCraftAvailability();
        return true;
    }

    /// <summary>결과 화면에서 제공하기를 선택한 현재 잔만 기존 트레이에 알린다.</summary>
    public bool TryAcceptPendingDrink(CraftedDrink drink)
    {
        if (!IsAwaitingServeDecision || drink == null || !ReferenceEquals(PendingDrink, drink) ||
            drink.Session.Phase != ECraftPhase.Completed || !drink.CraftGrade.HasValue)
            return false;
        IsAwaitingServeDecision = false;
        Notify(DrinkReady, drink);
        RefreshCraftAvailability();
        return true;
    }

    /// <summary>현재 잔을 폐기한다. 재시도가 켜져 있으면 이전 선택을 복원해 첫 기믹부터 다시 실행한다.</summary>
    public bool TryDiscardPendingDrink(CraftedDrink drink, bool retryGimmicks = false)
    {
        if (!IsAwaitingServeDecision || drink == null || !ReferenceEquals(PendingDrink, drink) ||
            drink.Session.Phase != ECraftPhase.Completed)
            return false;
        if (retryGimmicks)
        {
            if (TryRetryGimmicks(drink, out string reason)) return true;
            Debug.LogWarning($"[CraftFlow] {reason}");
            return false;
        }
        IsAwaitingServeDecision = false;
        drink.Session.Discard();
        PendingDrink = null;
        Notify(DrinkDiscarded, drink);
        RefreshCraftAvailability();
        return true;
    }

    bool RequiresBottleOpener(ActualCraft actual) => !actual.IsBottleOpenerSelected &&
        shelfData != null && System.Linq.Enumerable.Any(actual.IngredientIds, shelfData.RequiresOpen);

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
