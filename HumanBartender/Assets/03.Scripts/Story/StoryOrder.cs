/// <summary>
/// 2부에서 살아 있는 주문 하나(2부 운영 명세 §8.2의 current_order).
///
/// order 스텝이 만들고 serve가 소비한다. 주문자와 정답 칵테일이 한 몸으로 묶여 있어야
/// craft와 serve가 같은 문맥을 본다 — 씬 제목이나 화면 가운데 인물로 서빙 대상을 추측하지 않는다.
///
/// 한 번에 하나만 산다. serve가 끝나면 완료로 넘기고, 다음 order가 오기 전에는 새로 만들지 않는다.
/// </summary>
public class StoryOrder
{
    /// <summary>주문자. order.actor 그대로다.</summary>
    public string GuestActorId { get; }

    /// <summary>정답 칵테일. order.arg의 "exact:&lt;id&gt;"에서 온다.</summary>
    public string OrderedCocktailId { get; }

    /// <summary>주문자가 앉은 자리. 완성 잔을 받을 코스터가 놓이는 곳이다.</summary>
    public ESlotType Seat { get; }

    /// <summary>정산과 서빙 결과를 식별하는 고정 id. 같은 id는 두 번 반영하지 않는다.</summary>
    public string Id { get; }

    /// <summary>이미 잔이 나갔는지. 한 주문에 두 번 커밋하지 않기 위한 표시다.</summary>
    public bool IsServed { get; private set; }

    public StoryOrder(string guestActorId, string orderedCocktailId, ESlotType seat, string id)
    {
        GuestActorId = guestActorId;
        OrderedCocktailId = orderedCocktailId;
        Seat = seat;
        Id = id;
    }

    public void MarkServed() => IsServed = true;
}
