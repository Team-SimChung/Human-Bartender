using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewTasteData
{
    [field: SerializeField][JsonProperty("character_id")] public string CharacterId { get; set; }
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("tier")] public ENewTasteTier Tier { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>StreamingAssets/json/tastes.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewTasteDataSO", menuName = "Data/New/TasteDataSO")]
public class NewTasteDataSO : ScriptableObject
{
    public NewTasteData[] tasteData;
}
