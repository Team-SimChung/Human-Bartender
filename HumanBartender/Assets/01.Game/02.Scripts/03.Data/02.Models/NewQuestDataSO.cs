using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewQuestData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("title")] public LocalizedText Title { get; set; }
    [field: SerializeField][JsonProperty("kind")] public string Kind { get; set; }
    [field: SerializeField][JsonProperty("reward_effects")] public string RewardEffects { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

[Serializable]
public struct NewQuestStageData
{
    [field: SerializeField][JsonProperty("quest_id")] public string QuestId { get; set; }
    [field: SerializeField][JsonProperty("stage")] public int Stage { get; set; }
    [field: SerializeField][JsonProperty("goal")] public string Goal { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("on_complete")] public string OnComplete { get; set; }
}

[Serializable]
public class NewQuestDataBase
{
    [field: SerializeField][JsonProperty("quests")] public NewQuestData[] Quests { get; set; }
    [field: SerializeField][JsonProperty("stages")] public NewQuestStageData[] Stages { get; set; }
}

/// <summary>StreamingAssets/json/quests.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewQuestDataSO", menuName = "Data/New/QuestDataSO")]
public class NewQuestDataSO : ScriptableObject
{
    public NewQuestDataBase questData;
}
