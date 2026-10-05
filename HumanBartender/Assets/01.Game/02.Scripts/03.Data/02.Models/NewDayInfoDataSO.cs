using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewDayInfoData
{
    [field: SerializeField][JsonProperty("day")] public int Day { get; set; }
    [field: SerializeField][JsonProperty("start_phase")] public string StartPhase { get; set; }
    [field: SerializeField][JsonProperty("bgm_street")] public string BgmStreet { get; set; }
    [field: SerializeField][JsonProperty("bgm_bar")] public string BgmBar { get; set; }
    /// <summary>그날 나가는 고정 지출(임대료 등).</summary>
    [field: SerializeField][JsonProperty("upkeep_gold")] public int UpkeepGold { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
    // label_ko / label_en은 label:{ko,en} 한 덩어리로 합쳐졌다. 참조하는 곳이 없어 그대로 걷어낸다.
    [field: SerializeField][JsonProperty("label")] public LocalizedText Label { get; set; }
}

/// <summary>StreamingAssets/json/days.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewDayInfoDataSO", menuName = "Data/New/DayInfoDataSO")]
public class NewDayInfoDataSO : ScriptableObject
{
    public NewDayInfoData[] dayInfoData;
}
