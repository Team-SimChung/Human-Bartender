using System;

/// <summary>요청자가 넘기는 불변 주문 정보. 제조 세션과 스토리 타입을 포함하지 않는다.</summary>
public sealed class OrderDetails
{
    public string Id { get; }
    public string ReceiverId { get; }
    public string CocktailId { get; }
    public float TipMultiplier { get; }

    public OrderDetails(string id, string receiverId, string cocktailId, float tipMultiplier = 1f)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(receiverId) ||
            string.IsNullOrWhiteSpace(cocktailId))
            throw new ArgumentException("주문 ID, 수신자, 칵테일 ID가 필요합니다.");
        Id = id;
        ReceiverId = receiverId;
        CocktailId = cocktailId;
        TipMultiplier = tipMultiplier;
    }
}

public enum OrderState { Waiting, Serving, Completed, Cancelled, Failed }

/// <summary>주문 상태의 유일한 소유자. 상태 변경은 요청 컨트롤러만 수행한다.</summary>
public sealed class OrderRequest
{
    Action<OrderResult> callback;

    public OrderDetails Details { get; }
    public string Id => Details.Id;
    public OrderState State { get; private set; } = OrderState.Waiting;
    public bool IsTerminal => State == OrderState.Completed || State == OrderState.Cancelled || State == OrderState.Failed;
    public OrderResult Result { get; private set; }

    internal OrderRequest(OrderDetails details, Action<OrderResult> callback)
    {
        Details = details;
        this.callback = callback;
    }

    internal bool BeginServing()
    {
        if (State != OrderState.Waiting) return false;
        State = OrderState.Serving;
        return true;
    }

    internal bool End(OrderState state, ServeResult serve = null, string error = null)
    {
        if (IsTerminal) return false;
        if (state != OrderState.Completed && state != OrderState.Cancelled && state != OrderState.Failed)
            throw new ArgumentException("종료 상태가 필요합니다.");
        State = state;
        Result = new OrderResult(Id, state, serve, error);
        return true;
    }

    internal void Notify()
    {
        var notify = callback;
        callback = null;
        notify?.Invoke(Result);
    }
}
