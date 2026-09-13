using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;

/// <summary>
/// 구형 실외 대사 경로가 쓰는 타입들.
///
/// 원래 NPCCharacterDayDataSO.cs가 SO와 함께 들고 있었는데, 그 SO와 데이터(Outside/*.json)는
/// 걷어냈다. 이 넷만 남는 이유는 InteractiveObjectEntity와 InteractiveEntityManager가 아직
/// 참조하기 때문이다 — 그 둘은 남겨 두기로 했다.
///
/// 채우는 데이터가 없으므로 실행 중에는 전부 비어 있다. FlowData를 만드는 유일한 곳이던
/// InteractiveEntityManager.InjectDialogue 호출부가 사라져서, flows는 항상 빈 리스트다.
/// </summary>
[Serializable]
[JsonConverter(typeof(StringEnumConverter))]
public enum ESelectionType
{
    [EnumMember(Value = "sequential")] Sequential,
    [EnumMember(Value = "conditional")] Conditional,
    [EnumMember(Value = "random")] Random
}

/// <summary>대사 흐름 하나에 걸리는 단일 조건.</summary>
public struct Condition
{
    [JsonProperty("type")] public EConditionCheckType Type { get; set; }
    [JsonProperty("flag_id")] public string FlagId { get; set; }
    [JsonProperty("character_id")] public string Character { get; set; }
    [JsonProperty("min")] public int Min { get; set; }
    [JsonProperty("bValue")] public bool BValue { get; set; }
}

/// <summary>
/// 스폰·대사 조건. type이 and면 Conditions를 모두 만족해야 한다.
/// Condition과 필드가 겹치는 것은 원본 그대로다 — 중첩 한 겹만 다르다.
/// </summary>
public struct OutsideCondition
{
    [JsonProperty("type")] public EConditionCheckType Type { get; set; }
    [JsonProperty("conditions")] public Condition[] Conditions { get; set; }
    [JsonProperty("flag_id")] public string FlagId { get; set; }
    [JsonProperty("character_id")] public string Character { get; set; }
    [JsonProperty("min")] public int Min { get; set; }
    [JsonProperty("bValue")] public bool BValue { get; set; }
}

/// <summary>대사 묶음 하나. 조건을 만족하면 Dialogues를 순서대로 재생한다.</summary>
[Serializable]
public struct FlowData
{
    [JsonProperty("flow_id")] public string FlowId { get; set; }
    [JsonProperty("condition")] public OutsideCondition? Conditions { get; set; }
    [JsonProperty("dialogues")] public DialogueData[] Dialogues { get; set; }
}
