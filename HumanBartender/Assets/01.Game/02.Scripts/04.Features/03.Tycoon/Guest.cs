using System.Collections.Generic;
using UnityEngine;

public class Guest
{
    public string id;
    public string characterId; // 단골(regular_slots)인 경우 지정된 캐릭터 id. 랜덤 손님이면 null.
    public string targetCocktailId;

    // 손님별 난이도(tier)는 데이터에서 사라졌다. 이제 웨이브 데이터가 주문 칵테일을 직접 지정하고,
    // 서빙 여유는 손님이 아니라 진행 일차로 정해진다.
    public string personality; // random_waves.json 전용, 단골은 null
    public float tipMultiplier = 1f; // personalities.json의 tip_mult
    public float patienceMultiplier = 1f; // personalities.json의 patience_mult. 단골은 성격이 없어 1이다.

    /// <summary>personalities.json의 think_chance. 이 확률로만 order_think(주문 고민) 대사가 나온다.</summary>
    public float thinkChance = 1f;
    public int maxRounds = 1;

    /// <summary>
    /// 웨이브·단골 데이터가 지정한 주문(random_waves/regular_slots의 order). 비어 있으면 회차마다 추첨한다.
    /// 회차가 넘어갈 때 다시 봐야 해서 원래 값을 그대로 들고 있는다 — 지정 주문 손님은 매 회차 같은 것을 시킨다.
    /// </summary>
    public string specifiedOrder;
    public float delaySec; // 이 손님이 앞선 손님에 이어 등장하기까지의 대기 시간(초)

    public bool isRegular;

    /// <summary>몇 번째 주문을 받고 있는지. 첫 주문 대사가 나가면 1이 된다.</summary>
    public int orderRound = 0;

    /// <summary>앞으로 더 시킬 수 있는 주문 수.</summary>
    public int RemainingOrderCount => Mathf.Max(0, maxRounds - orderRound);

    /// <summary>
    /// 회차별 정산 기록. 다음 잔으로 넘어가도 지난 회차를 지우거나 합치지 않는다.
    /// 합계(SessionTotal)는 확인용이며 당일 매출에 다시 더하지 않는다.
    /// </summary>
    public readonly List<OrderSettlement> settlements = new();

    /// <summary>이 손님이 앉아 있는 동안 낸 값의 합. 검증·로그용이다.</summary>
    public int SessionTotal
    {
        get
        {
            int total = 0;
            foreach (var settlement in settlements) total += settlement.SettlementAmount;
            return total;
        }
    }

    /// <summary>
    /// 주문 대사를 이미 말했는지. 주문을 듣기 전에는 잔을 받지 않는다 —
    /// 무엇을 시킬지 말하지도 않은 손님 앞에 잔이 놓이면 그 주문 장면 자체가 없던 일이 된다.
    /// </summary>
    public OrderRequest CurrentOrder { get; internal set; }
    public bool hasOrdered => CurrentOrder?.State == OrderState.Waiting;

    /// <summary>
    /// 이번 회차 서빙 제한시간이 끝나는 시각(BarOperationClock 기준, 초). 잔이 나갔거나 대기 중이 아니면 0이다.
    /// 드롭과 인내심 종료가 같은 프레임에 겹쳤을 때 선후를 가르는 데 쓴다(운영 명세 §7.2).
    /// </summary>
    public float serveDeadlineSec;

    public bool isDrunk = false;

    public GuestBodyAppearance appearance; // 랜덤 손님에게 배정된 파츠 조합(guest_bodies.json). 단골(isRegular)이면 null.
    public GuestBodySprites bodySprites; // appearance를 addressable로 로드한 스프라이트. 로딩이 끝나기 전에는 null.
}
