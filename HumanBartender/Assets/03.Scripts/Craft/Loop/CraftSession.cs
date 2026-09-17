/// <summary>
/// 칵테일 한 잔을 만드는 시도 하나.
///
/// 시도마다 새로 만든다. 전역 저장소 한 칸을 돌려쓰지 않는 이유는, 버리고 다시 만들거나 한 손님이
/// 두 잔을 시키는 경우에 이전 기록이 그대로 남아 있어야 하기 때문이다. 끝난 시도는 이 객체째로
/// 보관하면 되고, 새 시도는 새 객체로 시작한다.
///
/// 손님 주문과 플레이어 선택을 한 객체 안에 나란히 들고 있되 서로 덮어쓰지 않는다.
/// 둘을 비교하는 것이 곧 주문 일치 판정이라서, 한쪽이 다른 쪽으로 흡수되면 판정 자체가 사라진다.
/// </summary>
public class CraftSession
{
    public string JobId { get; } = System.Guid.NewGuid().ToString("N");
    public string FailureReason { get; private set; }
    /// <summary>
    /// 손님이 주문한 칵테일. 아직 어느 손님에게 낼지 정해지지 않았다면 null이다.
    ///
    /// 제조 화면은 특정 손님 전용으로 열리지 않는다. 주문이 여럿 쌓여 있어도 플레이어는 공통 화면에서
    /// 만들 칵테일을 직접 고르고, 완성한 잔을 누구 앞에 놓을지는 그다음에 정한다. 그래서 이 값이
    /// 제조 내내 비어 있을 수 있다.
    /// </summary>
    public string OrderCocktailId { get; private set; }

    /// <summary>플레이어가 메뉴에서 만들기로 고른 칵테일. 완성 결과물의 정체성은 언제나 이 값이다.</summary>
    public string SelectedCocktailId { get; private set; }

    public ActualCraft Actual { get; } = new();
    public CraftTimer Timer { get; } = new();

    public ECraftPhase Phase { get; private set; } = ECraftPhase.Preparing;

    /// <summary>지금 수행 중인 기믹이 큐에서 몇 번째인지. 기믹이 시작되기 전에는 -1이다.</summary>
    public int CurrentGimmickIndex { get; private set; } = -1;

    /// <summary>
    /// 주문한 칵테일과 만든 칵테일이 같은지. 대상 손님이 아직 정해지지 않았다면 판단하지 않고 null이다.
    /// 불일치는 아무리 정확하게 만들어도 뒤집히지 않는 조건이라, 모르는 상태를 일치로 봐서는 안 된다.
    /// </summary>
    public bool? OrderMatches =>
        OrderCocktailId == null ? null : OrderCocktailId == SelectedCocktailId;

    public CraftSession(string selectedCocktailId, string orderCocktailId = null)
    {
        SelectedCocktailId = selectedCocktailId;
        OrderCocktailId = orderCocktailId;
    }

    /// <summary>
    /// 대상 손님이 정해져 주문 칵테일을 알게 됐을 때 부른다. 한 번 정해지면 바뀌지 않는다 —
    /// 손님의 주문은 플레이어가 무엇을 고르든 그대로 유지되는 값이기 때문이다.
    /// </summary>
    public void AssignOrderCocktail(string orderCocktailId)
    {
        if (OrderCocktailId != null) return;

        OrderCocktailId = orderCocktailId;
    }

    /// <summary>제조 준비를 마치고 첫 기믹으로 넘어간다. 전체 제조시간은 여기서부터 흐른다.</summary>
    public void BeginGimmicks()
    {
        if (Phase != ECraftPhase.Preparing) return;

        Phase = ECraftPhase.Playing;
        CurrentGimmickIndex = 0;
        Timer.Start();
    }

    /// <summary>현재 기믹을 끝내고 다음 차례로 넘긴다.</summary>
    public void AdvanceGimmick()
    {
        if (Phase != ECraftPhase.Playing) return;

        CurrentGimmickIndex++;
    }

    /// <summary>
    /// 마지막 기믹까지 끝나 제조 기록을 확정한다. 이 시점의 시간이 곧 전체 제조시간이고,
    /// 그 뒤로는 남아 있는 입력이 여러 번 들어와도 다시 확정하지 않는다.
    /// </summary>
    public void Complete()
    {
        if (Phase != ECraftPhase.Playing) return;

        Timer.Stop();
        Actual.FixElapsedManual(Timer.ElapsedSec);
        Phase = ECraftPhase.Completed;
    }

    /// <summary>
    /// 결과를 보고 버리기를 골랐을 때 부른다. 기록은 지우지 않는다 —
    /// 폐기한 시도도 그 손님에게 실제로 일어난 일이라 남겨 둔다.
    /// </summary>
    public void Discard()
    {
        if (Phase != ECraftPhase.Completed) return;
        Phase = ECraftPhase.Discarded;
    }

    public void Cancel() => EndUnsuccessful(ECraftPhase.Cancelled, null);
    public void Fail(string reason) => EndUnsuccessful(ECraftPhase.Failed, reason);

    void EndUnsuccessful(ECraftPhase phase, string reason)
    {
        if (Phase != ECraftPhase.Preparing && Phase != ECraftPhase.Playing) return;
        Timer.Stop();
        Actual.FixElapsedManual(Timer.ElapsedSec);
        FailureReason = reason;
        Phase = phase;
    }
}
