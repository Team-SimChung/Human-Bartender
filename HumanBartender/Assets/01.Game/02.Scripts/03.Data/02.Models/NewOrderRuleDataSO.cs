using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewOrderRuleData
{
    [field: SerializeField][JsonProperty("order_id")] public string OrderId { get; set; }
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("verdict")] public ENewOrderVerdict Verdict { get; set; }
    [field: SerializeField][JsonProperty("effects")] public string Effects { get; set; }
    [field: SerializeField][JsonProperty("react")] public string React { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>StreamingAssets/json/order_rules.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewOrderRuleDataSO", menuName = "Data/New/OrderRuleDataSO")]
public class NewOrderRuleDataSO : ScriptableObject
{
    public NewOrderRuleData[] orderRuleData;
}
