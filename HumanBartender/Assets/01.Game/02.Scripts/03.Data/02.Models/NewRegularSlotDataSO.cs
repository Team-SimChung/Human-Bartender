using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewRegularSlotData
{
    [field: SerializeField][JsonProperty("day")] public int Day { get; set; }
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }
    [field: SerializeField][JsonProperty("character")] public string Character { get; set; }

    /// <summary>
    /// 이 단골이 주문할 칵테일 id. 비어 있으면 그날 해금된 칵테일 중에서 고른다.
    /// </summary>
    [field: SerializeField][JsonProperty("order")] public string Order { get; set; }

    [field: SerializeField][JsonProperty("delay_sec")] public float DelaySec { get; set; }
    [field: SerializeField][JsonProperty("max_rounds")] public int MaxRounds { get; set; }
    [field: SerializeField][JsonProperty("branch_choice")] public bool BranchChoice { get; set; }
    [field: SerializeField][JsonProperty("cameo_scene")] public string CameoScene { get; set; }
    [field: SerializeField][JsonProperty("must_serve")] public bool MustServe { get; set; }
    [field: SerializeField][JsonProperty("serve_effects")] public string ServeEffects { get; set; }
}

/// <summary>StreamingAssets/json/regular_slots.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewRegularSlotDataSO", menuName = "Data/New/RegularSlotDataSO")]
public class NewRegularSlotDataSO : ScriptableObject
{
    public NewRegularSlotData[] regularSlotData;
}
