using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewBarkData
{
    [field: SerializeField][JsonProperty("voice_id")] public string VoiceId { get; set; }
    /// <summary>대사와 함께 띄울 표정 id. null이면 표정을 바꾸지 않는다.</summary>
    [field: SerializeField][JsonProperty("expression")] public string Expression { get; set; }
    [field: SerializeField][JsonProperty("situation")] public string Situation { get; set; }
    [field: SerializeField][JsonProperty("text")] public LocalizedText Text { get; set; }
    [field: SerializeField][JsonProperty("weight")] public int Weight { get; set; }
}

/// <summary>StreamingAssets/csv/guests/barks.csv에서 읽은 1부 손님 대사를 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewBarkDataSO", menuName = "Data/New/BarkDataSO")]
public class NewBarkDataSO : ScriptableObject
{
    public NewBarkData[] barkData;
}
