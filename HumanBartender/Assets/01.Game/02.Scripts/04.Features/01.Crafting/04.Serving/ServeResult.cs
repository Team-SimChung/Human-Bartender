/// <summary>판정 및 정산 계산 결과. 제조 품질과 주문 일치를 별도로 보존한다.</summary>
public sealed class ServeResult
{
    public string OrderId { get; }
    public string ReceiverId { get; }
    public string OrderedCocktailId { get; }
    public string ServedCocktailId { get; }
    public ENewGrade CraftGrade { get; }
    public ENewGrade FinalGrade { get; }
    public bool OrderMatch { get; }
    public OrderSettlement Settlement { get; }

    public ServeResult(OrderDetails order, CraftedDrink drink, ENewGrade finalGrade,
        bool orderMatch, OrderSettlement settlement)
    {
        OrderId = order.Id;
        ReceiverId = order.ReceiverId;
        OrderedCocktailId = order.CocktailId;
        ServedCocktailId = drink.CocktailId;
        CraftGrade = drink.CraftGrade.Value;
        FinalGrade = finalGrade;
        OrderMatch = orderMatch;
        Settlement = settlement;
    }
}

/// <summary>내부 정리가 끝난 뒤 한 번 전달하는 완료/취소/실패 알림.</summary>
public sealed class OrderResult
{
    public string OrderId { get; }
    public OrderState State { get; }
    public ServeResult Serve { get; }
    public string Error { get; }
    public bool Completed => State == OrderState.Completed;

    internal OrderResult(string id, OrderState state, ServeResult serve, string error)
    {
        OrderId = id;
        State = state;
        Serve = serve;
        Error = error;
    }
}
