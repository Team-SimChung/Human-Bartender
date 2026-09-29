using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewRandomWaveData
{
    [field: SerializeField][JsonProperty("day")] public int Day { get; set; }
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }

    /// <summary>
    /// 이 손님이 주문할 칵테일 id. 비어 있으면 정해두지 않았다는 뜻이라, 그날 해금된 칵테일 중에서 고른다.
    /// 스토리상 특정 칵테일을 주문해야 하는 손님만 값을 채운다.
    /// </summary>
    [field: SerializeField][JsonProperty("order")] public string Order { get; set; }

    [field: SerializeField][JsonProperty("personality")] public string Personality { get; set; }
    [field: SerializeField][JsonProperty("delay_sec")] public float DelaySec { get; set; }
    [field: SerializeField][JsonProperty("max_rounds")] public int MaxRounds { get; set; }
    [field: SerializeField][JsonProperty("branch_choice")] public bool BranchChoice { get; set; }
}

/// <summary>StreamingAssets/json/random_waves.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewRandomWaveDataSO", menuName = "Data/New/RandomWaveDataSO")]
public class NewRandomWaveDataSO : ScriptableObject
{
    public NewRandomWaveData[] randomWaveData;
}
