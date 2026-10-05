using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewCharacterData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("name")] public LocalizedText Name { get; set; }
    [field: SerializeField][JsonProperty("name_color")] public string NameColor { get; set; }
    [field: SerializeField][JsonProperty("role")] public ENewCharacterRole Role { get; set; }
    [field: SerializeField][JsonProperty("affinity")] public bool Affinity { get; set; }
    [field: SerializeField][JsonProperty("alive_flag")] public string AliveFlag { get; set; }
    [field: SerializeField][JsonProperty("expressions")] public string[] Expressions { get; set; }
    [field: SerializeField][JsonProperty("base_body")] public string BaseBody { get; set; }
    [field: SerializeField][JsonProperty("enter_sfx")] public string EnterSfx { get; set; }
    [field: SerializeField][JsonProperty("exit_sfx")] public string ExitSfx { get; set; }
}

/// <summary>StreamingAssets/json/characters.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewCharacterDataSO", menuName = "Data/New/CharacterDataSO")]
public class NewCharacterDataSO : ScriptableObject
{
    public NewCharacterData[] characterData;
}
