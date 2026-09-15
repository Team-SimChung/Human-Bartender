using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 대본과 제조·서빙 시스템 사이의 창구.
///
/// IStoryPresenter와 같은 이유로 둔다. 실행기는 "언제 만들게 하고 언제 받아야 하는지"까지만 알고,
/// 칵테일 메뉴가 어디에 있는지·완성 잔을 무엇으로 끌어다 놓는지는 이 너머의 일이다.
/// 그래야 제조 화면이 바뀌어도 대본을 걷는 코드가 함께 바뀌지 않는다.
/// </summary>
public interface IStoryCraftGate
{
    /// <summary>
    /// 주문이 확정된 순간 한 번(§9의 OnOrderCreated). 주문자 좌석에 코스터를 띄우고
    /// 서빙 대상으로 이어 두는 것이 이쪽 일이다.
    /// </summary>
    void OpenOrder(StoryOrder order);

    /// <summary>
    /// 잔 하나를 다 만들 때까지 기다린다.
    ///
    /// tutorialCocktailId가 있으면 그 칵테일로 곧장 시작하고, 없으면 플레이어가 메뉴에서 고른다 —
    /// 주문과 다른 칵테일을 골라도 막지 않는다(§8.2). 틀린 잔인지는 낼 때 가려진다.
    /// </summary>
    UniTask<bool> RunCraftAsync(StoryOrder order, string tutorialCocktailId, CancellationToken token);

    /// <summary>
    /// 완성 잔이 주문자에게 놓일 때까지 기다린다. 다른 자리에 놓으면 되돌아가고 계속 기다린다.
    /// </summary>
    UniTask<CraftedDrink> WaitForServeAsync(StoryOrder order, CancellationToken token);

    /// <summary>
    /// 낸 잔을 확정한다. 주문 일치와 최종 등급을 가리고 매출을 당일 장부에 한 번 넣은 뒤,
    /// 뒤따르는 조건식이 읽을 결과 문맥을 돌려준다(§9.1의 커밋 순서).
    ///
    /// 채점하지 못한 잔은 null이다. 등급이 없는데 아무 값이나 채워 넣으면 그 잘못이
    /// 그럴듯한 분기로 둔갑한다.
    /// </summary>
    StoryResultContext CommitServe(StoryOrder order, CraftedDrink drink);

    /// <summary>주문이 끝나 코스터와 서빙 대상 연결을 거둔다.</summary>
    void CloseOrder(StoryOrder order);
}
