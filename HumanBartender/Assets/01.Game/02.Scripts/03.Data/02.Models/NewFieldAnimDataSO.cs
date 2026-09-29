using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewFieldAnimData
{
    [field: SerializeField][JsonProperty("character_id")] public string CharacterId { get; set; }
    [field: SerializeField][JsonProperty("action")] public string Action { get; set; }
    [field: SerializeField][JsonProperty("resource_key")] public string ResourceKey { get; set; }
    [field: SerializeField][JsonProperty("status")] public string Status { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>StreamingAssets/json/field_anims.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewFieldAnimDataSO", menuName = "Data/New/FieldAnimDataSO")]
public class NewFieldAnimDataSO : ScriptableObject
{
    public NewFieldAnimData[] fieldAnimData;
}
