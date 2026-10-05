using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewPersonalityData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("name")] public LocalizedText Name { get; set; }
    [field: SerializeField][JsonProperty("tip_mult")] public float TipMult { get; set; }
    [field: SerializeField][JsonProperty("patience_mult")] public float PatienceMult { get; set; }
    [field: SerializeField][JsonProperty("think_chance")] public float ThinkChance { get; set; }
}

/// <summary>StreamingAssets/json/personalities.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewPersonalityDataSO", menuName = "Data/New/PersonalityDataSO")]
public class NewPersonalityDataSO : ScriptableObject
{
    public NewPersonalityData[] personalityData;
}
