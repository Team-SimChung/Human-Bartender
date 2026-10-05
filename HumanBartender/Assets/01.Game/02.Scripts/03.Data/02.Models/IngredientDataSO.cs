using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>재료 데이터 배열을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "IngrediantDataSO", menuName = "Scriptable Objects/IngrediantDataSO")]
public class IngredientDataSO : ScriptableObject
{
    public IngredientDataBase ingredientData;
}

[Serializable]
public class IngredientDataBase
{
    [JsonProperty("ingredients")] public IngredientData[] Ingredients { get; set; }
}

[Serializable]
public struct IngredientData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("name_en")] public string NameEn { get; set; }
    [JsonProperty("category")] public string Category { get; set; }
    [JsonProperty("sprite")] public string Sprite { get; set; }
    [JsonProperty("max_count")] public int MaxCount { get; set; }
}