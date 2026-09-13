using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>대화 라인의 유형을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum EDialogueType
{
    None,

    [EnumMember(Value = "system")]
    System,

    [EnumMember(Value = "monologue")]
    Monologue,

    [EnumMember(Value = "normal")]
    Normal,

    [EnumMember(Value = "choice")]
    Choice,

    [EnumMember(Value = "choice_root")]
    ChoiceRoot,

    [EnumMember(Value = "condition_branch")]
    ConditionBranch,
}


[JsonConverter(typeof(StringEnumConverter))]
public enum EConditionCheckType
{
    None,

    [EnumMember(Value = "affinity")]
    Affinity,

    [EnumMember(Value = "skill")]
    Skill,

    [EnumMember(Value = "money")]
    Money,

    [EnumMember(Value = "flag")]
    Flag,

    [EnumMember(Value = "and")]
    And,
}

/// <summary>대화 트리거의 실행 유형을 나타내는 열거형.</summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum ETriggetType
{
    None,

    [EnumMember(Value = "effect")]
    Effect,

    [EnumMember(Value = "start_craft")]
    StartCraft,

    [EnumMember(Value = "customer_enter")]
    CustomerEnter,

    [EnumMember(Value = "customer_exit")]
    CustomerExit,

    [EnumMember(Value = "start_cutscene")]
    StartCutScene,

    [EnumMember(Value = "set_stat")]
    SetStat, 
    
    [EnumMember(Value = "add_stat")]
    AddStat,

    [EnumMember(Value = "apply_effects")]
    ApplyEffect,

    [EnumMember(Value = "character_action")]
    CharacterAction,

    [EnumMember(Value = "day_end")]
    Day_End,

    [EnumMember(Value = "set_flag")]
    SetFlag,

    [EnumMember(Value = "money_change")]
    MoneyChange,
}


[Serializable]
public struct SceneData
{
    [JsonProperty("scene_id")] public string SceneId { get; set; }
    [JsonProperty("customer")] public string Customer { get; set; }
    [JsonProperty("dialogues")] public DialogueData[] Dialogues { get; set; }
}

[Serializable]
public struct DialogueData
{
    [JsonProperty("id")] public string Id { get; set; }
    [JsonProperty("speaker")] public string Speaker { get; set; }
    [JsonProperty("type")] public EDialogueType Type { get; set; }
    [JsonProperty("text")] public string Text { get; set; }
    [JsonProperty("expression")] public string Expression { get; set; }
    [JsonProperty("next")] public string Next { get; set; }
    [JsonProperty("choices")] public ChoiceData[] Choices { get; set; }
    [JsonProperty("trigger")] public TriggerData? Trigger { get; set; }
    [JsonProperty("triggers")] public TriggerData[] Triggers { get; set; }
    [JsonProperty("next_conditions")] public NextConditions? Nextconditions { get; set; }
}

public struct NextConditions
{
    [JsonProperty("stat")] public EConditionCheckType Stat { get; set; }
    [JsonProperty("character")] public string Character { get; set; }
    [JsonProperty("min_amount")] public int MinAmount{ get; set; }
    [JsonProperty("default")] public string Default { get; set; }
    [JsonProperty("branches")] public BranchData[] Branches { get; set; }
}

public struct BranchData
{
    [JsonProperty("goto")] public string Goto { get; set; }
}

[Serializable]
public struct ChoiceData
{
    [JsonProperty("text")] public string Text { get; set; }
    [JsonProperty("next")] public string Next { get; set; }
    [JsonProperty("choice_effect")] public string ChoiceEffect { get; set; }
    [JsonProperty("condition")] public ChoiceCondition? Condition { get; set; }
}

public struct ChoiceCondition
{
    [JsonProperty("operator")] public string Operator { get; set; }
    [JsonProperty("checks")] public ChoiceConditionCheck[] Checks { get; set; }
}
public struct ChoiceConditionCheck
{
    [JsonProperty("type")] public EConditionCheckType Type { get; set; }
    [JsonProperty("character")] public string Character { get; set; }
    [JsonProperty("min_tier")] public string minTier { get; set; }
    [JsonProperty("min_amount")] public int? minAmount { get; set; }
}


[Serializable]
public struct TriggerData
{
    [JsonProperty("type")] public ETriggetType Type { get; set; }
    [JsonProperty("data")] public TriggerDetailData Data { get; set; }
}

[Serializable]
public struct TriggerDetailData
{
    [JsonProperty("character_id")] public string CharacterId { get; set; }
    [JsonProperty("slot")] public string Slot { get; set; }

    [JsonProperty("flag_id")] public string FlagId { get; set; }
    [JsonProperty("value")] public int Value { get; set; }
    [JsonProperty("bValue")] public bool BValue { get; set; }

    [JsonProperty("amount")] public int Amount { get; set; }

    [JsonProperty("sfx")] public string Sfx { get; set; }
    [JsonProperty("sfx_mode")] public string SfxMode { get; set; }
    [JsonProperty("bgm")] public string Bgm { get; set; }

    
    [JsonProperty("animation")] public string Animation { get; set; }
    [JsonProperty("anim_id")] public string AnimId { get; set; }

    
    [JsonProperty("craft_event_id")] public string CraftEventId { get; set; }


    [JsonProperty("camera_type")] public ECameraZoomType CameraType{ get; set; }

    [JsonProperty("effect_type")] public EEffectType EffectType { get; set; }
    
    [JsonProperty("duration")] public float? Duration { get; set; }


    
    [JsonProperty("enter_effect")] public string EnterEffect { get; set; }
    [JsonProperty("enter_duration")] public float? EnterDuration { get; set; }

    
    [JsonProperty("exit_effect")] public string ExitEffect { get; set; }
    [JsonProperty("exit_duration")] public float? ExitDuration { get; set; }

}

[Serializable]
public class DayDatabBase
{
    [JsonProperty("day")] public int Day { get; set; }
    [JsonProperty("scenes")] public SceneData[] Scenes { get; set; }
}

/// <summary>하루치 씬/대화/트리거 데이터를 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "DayDataSO", menuName = "Data/DayDataSO")]
public class DayDataSO : ScriptableObject
{
    public DayDatabBase dayData;
}