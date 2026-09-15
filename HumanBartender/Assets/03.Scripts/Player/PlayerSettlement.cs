using System.Collections.Generic;
using UnityEngine;

/// <summary>하루 영업 정산 기록을 누적하는 인터페이스. 제조 루프가 DI로 주입받아 쓴다.</summary>
interface ISettlementLog
{
    public void AddSalesQty(string id, int val);
    public void AddTip(string id, int val);
    public void SetCost(int cost);

}


/// <summary>
/// 하루치 판매 수량/팁/유지비를 누적하는 정산 데이터 SO. SettlementUI가 이 데이터를 읽어 정산 화면을 구성한다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerSettlement", menuName = "Scriptable Objects/PlayerSettlement")]
public class PlayerSettlement : ScriptableObject, ISettlementLog
{

    public int maintenanceCost;                             // 가게 유지비 (차감)
    public Dictionary<string, int> salesQty = new();        // 판매 내역: 메뉴명 -> 수량
    public Dictionary<string, int> tips = new();            // 팁 내역  : 메뉴명 -> 팁 금액

    /// <summary>하루 시작 시 정산 기록을 초기화한다.</summary>
    public void Init()
    {
        this.maintenanceCost = 0;
        this.salesQty = new Dictionary<string, int>();
        this.tips = new Dictionary<string, int>();
    }


    /// <summary>메뉴별 판매 수량을 누적한다.</summary>
    public void AddSalesQty(string id, int val)
    {
        if (salesQty.ContainsKey(id))
            salesQty[id] += val;
        else
            salesQty.Add(id, val);
    }
    /// <summary>메뉴별 팁 금액을 누적한다.</summary>
    public void AddTip(string id, int val)
    {
        if (tips.ContainsKey(id))
            tips[id] += val;
        else
            tips.Add(id, val);
    }
    /// <summary>가게 유지비를 설정한다.</summary>
    public void SetCost(int cost)
    {
        maintenanceCost = cost;
    }
}