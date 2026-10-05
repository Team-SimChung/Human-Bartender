using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewEndingData
{
    [field: SerializeField][JsonProperty("priority")] public int Priority { get; set; }
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("scene_id")] public string SceneId { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>StreamingAssets/json/endings.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewEndingDataSO", menuName = "Data/New/EndingDataSO")]
public class NewEndingDataSO : ScriptableObject
{
    public NewEndingData[] endingData;
}
