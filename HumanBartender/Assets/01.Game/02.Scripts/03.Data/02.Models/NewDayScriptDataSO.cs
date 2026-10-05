using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// script/common.json, script/day_1~3.json 등 하나의 씬 스텝을 나타내는 구조체.
/// type(say/move/fx/enter/exit/sfx/choice/order/craft/serve 등)에 따라 arg의 의미가 달라진다.
/// </summary>
[Serializable]
public struct NewDialogueStepData
{
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }
    [field: SerializeField][JsonProperty("type")] public ENewStepType Type { get; set; }

    /// <summary>
    /// 화면에 보이는 대사에 붙는 고정 ID(2부 운영 명세 §12.3.1). 스텝을 끼워 넣거나 seq를 바꿔도
    /// 이 값은 그대로 둔다 — 읽은 대사를 이 값으로 기억하기 때문에, 다시 매기면 남의 읽음 기록이
    /// 엉뚱한 대사에 붙는다. say와 text가 있는 order만 가지며 나머지는 null이다.
    /// </summary>
    [field: SerializeField][JsonProperty("dialogue_id")] public string DialogueId { get; set; }

    [field: SerializeField][JsonProperty("actor")] public string Actor { get; set; }
    [field: SerializeField][JsonProperty("arg")] public string Arg { get; set; }
    [JsonProperty("text")] public LocalizedText? Text { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("effects")] public string Effects { get; set; }
    [field: SerializeField][JsonProperty("sync")] public string Sync { get; set; }
}

[Serializable]
public struct NewScriptSceneData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("day")] public int Day { get; set; }
    [field: SerializeField][JsonProperty("phase")] public ENewScenePhase Phase { get; set; }
    [field: SerializeField][JsonProperty("seq")] public int Seq { get; set; }
    [field: SerializeField][JsonProperty("trigger")] public ENewSceneTrigger Trigger { get; set; }
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
    [field: SerializeField][JsonProperty("title")] public string Title { get; set; }
    [field: SerializeField][JsonProperty("skippable")] public bool Skippable { get; set; }
    [field: SerializeField][JsonProperty("group")] public string Group { get; set; }
    [field: SerializeField][JsonProperty("steps")] public NewDialogueStepData[] Steps { get; set; }
}

[Serializable]
public class NewDayScriptBase
{
    /// <summary>
    /// 이 대본이 벌어지는 장소. script/bar/dayN.json이 쓰고, day_N.json에는 없다.
    ///
    /// 두 계열이 파일 머리를 다르게 쓴다 — 거리·집 쪽은 day를, 바 쪽은 place를 적고 일차는 씬마다
    /// 따로 들고 있다. 한쪽만 읽으면 나머지 파일의 머리가 통째로 비므로 둘 다 받는다.
    /// </summary>
    [field: SerializeField][JsonProperty("place")] public string Place { get; set; }

    /// <summary>이 파일이 다루는 일차. bar 대본에는 없고, 그때는 씬의 day를 본다.</summary>
    [field: SerializeField][JsonProperty("day")] public int Day { get; set; }
    [field: SerializeField][JsonProperty("scenes")] public NewScriptSceneData[] Scenes { get; set; }
    /// <summary>
    /// 선택지 묶음. 키가 선택지 id고, choice 타입 스텝의 arg가 이 키를 가리킨다.
    /// Dictionary라 [SerializeField]로는 인스펙터에 안 보이지만 역직렬화에는 문제가 없다.
    /// </summary>
    [JsonProperty("choices")] public Dictionary<string, NewChoiceOptionData[]> Choices { get; set; }
}

/// <summary>script/common.json 또는 script/day_N.json 한 파일을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewDayScriptDataSO", menuName = "Data/New/DayScriptDataSO")]
public class NewDayScriptDataSO : ScriptableObject
{
    public NewDayScriptBase dayScriptData;
}
