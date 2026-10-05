using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewDossierData
{
    [field: SerializeField][JsonProperty("character_id")] public string CharacterId { get; set; }
    [field: SerializeField][JsonProperty("min_affinity")] public int MinAffinity { get; set; }
    [field: SerializeField][JsonProperty("kind")] public ENewDossierKind Kind { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("text")] public LocalizedText Text { get; set; }
}

/// <summary>StreamingAssets/json/dossier.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewDossierDataSO", menuName = "Data/New/DossierDataSO")]
public class NewDossierDataSO : ScriptableObject
{
    public NewDossierData[] dossierData;
}
