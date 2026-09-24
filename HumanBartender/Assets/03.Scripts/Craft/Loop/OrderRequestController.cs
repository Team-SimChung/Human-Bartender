using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>외부 주문의 단일 진입점. 제조 컨트롤러나 제조 세션을 소유하지 않는다.</summary>
public class OrderRequestController : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] CraftServingView servingView;

    [Header("Settlement")]
    [SerializeField] NewCocktailDataSO cocktailData;
    [SerializeField] NewBalanceDataSO balanceData;
    [Inject] DailySales sales;

    sealed class Entry
    {
        public OrderRequest Request;
        public IOrderPresentation Presentation;
        public Func<bool> CanServe;
        public Func<ServeResult, CancellationToken, UniTask> BeforeSettlement;
        public readonly CancellationTokenSource Lifetime = new();
    }

    readonly Dictionary<string, Entry> requests = new();
    readonly HashSet<CraftedDrink> consumedDrinks = new();
    UniTaskCompletionSource idle = CompletedIdle();
    bool closingPhase;
    IServeProcessor processor;

    public int ActiveCount => requests.Count;
    static UniTaskCompletionSource CompletedIdle()
    {
        var source = new UniTaskCompletionSource();
        source.TrySetResult();
        return source;
    }
    protected virtual IServeProcessor Processor => processor ??= new ServeProcessor(
        cocktailData, balanceData, sales);

    /// <summary>주문 담당이 호출한다. 콜백은 성공·취소·실패 모두 정리 후 한 번 호출된다.</summary>
    public OrderRequest Request(OrderDetails details, Action<OrderResult> onFinished,
        IOrderPresentation presentation = null,
        Func<ServeResult, CancellationToken, UniTask> beforeSettlement = null,
        Func<bool> canServe = null)
    {
        if (!isActiveAndEnabled) throw new InvalidOperationException("주문 컨트롤러가 비활성 상태입니다.");
        if (closingPhase) throw new InvalidOperationException("국면 종료 중에는 주문을 받을 수 없습니다.");
        if (details == null) throw new ArgumentNullException(nameof(details));
        if (requests.ContainsKey(details.Id)) throw new InvalidOperationException("이미 등록된 주문입니다: " + details.Id);
        if (presentation == null)
        {
            if (servingView == null) throw new InvalidOperationException("CraftServingView 연결이 필요합니다.");
            presentation = servingView.CreatePresentation(details);
        }
        var entry = new Entry
        {
            Request = new OrderRequest(details, onFinished),
            Presentation = presentation,
            BeforeSettlement = beforeSettlement,
            CanServe = canServe
        };
        if (requests.Count == 0) idle = new UniTaskCompletionSource();
        requests.Add(details.Id, entry);
        try { presentation.Open(drink => TryServe(entry, drink)); }
        catch (Exception exception)
        {
            entry.Request.End(OrderState.Failed, error: exception.Message);
            FinishAsync(entry).Forget();
        }
        return entry.Request;
    }

    /// <summary>고정된 손님 슬롯 등의 드롭 담당이 호출한다.</summary>
    public bool TryServe(string orderId, CraftedDrink drink) =>
        orderId != null && requests.TryGetValue(orderId, out var entry) && TryServe(entry, drink);

    /// <summary>주문 담당이 호출한다. 제조 및 트레이에는 접근하지 않는다.</summary>
    public bool CancelOrder(string orderId)
    {
        if (orderId == null || !requests.TryGetValue(orderId, out var entry)) return false;
        bool wasWaiting = entry.Request.State == OrderState.Waiting;
        if (!entry.Request.End(OrderState.Cancelled)) return false;
        entry.Lifetime.Cancel();
        if (wasWaiting) FinishAsync(entry).Forget();
        return true;
    }

    /// <summary>주문 없는 기존 tutorial 스텝의 메뉴 개방 전용.</summary>
    public void OpenMenu()
    {
        if (servingView == null) throw new InvalidOperationException("CraftServingView 연결이 필요합니다.");
        servingView.OpenUi();
    }

    bool TryServe(Entry entry, CraftedDrink drink)
    {
        if (!requests.TryGetValue(entry.Request.Id, out var current) || !ReferenceEquals(current, entry) ||
            entry.Request.State != OrderState.Waiting || drink == null || consumedDrinks.Contains(drink))
            return false;
        if (entry.CanServe != null && !entry.CanServe()) return false;

        ServeResult result;
        try { result = Processor.Evaluate(entry.Request.Details, drink); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        if (result == null || !entry.Request.BeginServing()) return false;
        consumedDrinks.Add(drink);
        CompleteAsync(entry, result).Forget();
        return true;
    }

    async UniTask CompleteAsync(Entry entry, ServeResult result)
    {
        try
        {
            // 드롭한 잔의 MarkServed/OnEndDrag를 먼저 끝낸다.
            await UniTask.NextFrame(cancellationToken: entry.Lifetime.Token);
            if (entry.BeforeSettlement != null)
                await entry.BeforeSettlement(result, entry.Lifetime.Token);
            entry.Lifetime.Token.ThrowIfCancellationRequested();
            Processor.Apply(result);
            entry.Request.End(OrderState.Completed, result);
        }
        catch (OperationCanceledException) when (entry.Lifetime.IsCancellationRequested)
        {
            entry.Request.End(OrderState.Cancelled);
        }
        catch (Exception exception)
        {
            entry.Request.End(OrderState.Failed, result, exception.Message);
            Debug.LogException(exception);
        }
        await FinishAsync(entry);
    }

    async UniTask FinishAsync(Entry entry)
    {
        // 동일 ID는 정리가 끝날 때까지 예약한다. 늦은 해제가 새 요청을 지우지 않는다.
        try
        {
            try { entry.Presentation.Unbind(); }
            finally { await entry.Presentation.CloseAsync(); }
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            requests.Remove(entry.Request.Id);
            if (requests.Count == 0) idle.TrySetResult();
            entry.Lifetime.Dispose();
            entry.BeforeSettlement = null;
            entry.CanServe = null;
            try { entry.Request.Notify(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    void OnDisable()
    {
        foreach (var id in new List<string>(requests.Keys)) CancelOrder(id);
    }

    public async UniTask CancelAllAndWaitAsync()
    {
        closingPhase = true;
        try
        {
            foreach (var id in new List<string>(requests.Keys)) CancelOrder(id);
            await idle.Task;
        }
        finally { closingPhase = false; }
    }

    public void CloseIdleView()
    {
        if (requests.Count == 0) servingView?.CloseUi();
    }
}
