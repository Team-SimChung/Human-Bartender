using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Texts
{
    [field: SerializeField][JsonProperty("ko")] public string Ko { get; set; }
    [field: SerializeField][JsonProperty("en")] public string En { get; set; }
}

/// <summary>
/// 길거리 스크립트의 선택지 하나.
/// </summary>
[Serializable]
public struct NewStreetOptionData
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("seq")] public int Seq;
    [JsonProperty("text")] public Texts Text;
    [JsonProperty("when")] public string When;

    /// <summary>고를 수 없을 때 보여줄 이유. 화면에 그대로 나오는 문구라 언어별로 들어 있다. 고를 수 있으면 null이다.</summary>
    [JsonProperty("lock_reason")] public Texts LockReason;

    /// <summary>이 선택지를 고른 뒤 이어서 실행할 스텝.</summary>
    [JsonProperty("result_steps")] public Step[] ResultSteps;
}

[Serializable]
public struct ResultStep
{
    [JsonProperty("type")] public string Type;
    [JsonProperty("effects")] public string Effects;
    [JsonProperty("scene_id")] public string SceneId;
}

/// <summary>
/// 선택지 세부 항목(options) 데이터 구조체
/// </summary>
[Serializable]
public struct ChoiceOption
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("seq")] public int Seq;
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)] public Texts Text;
    [JsonProperty("when")] public string When; 
    [JsonProperty("lock_reason", NullValueHandling = NullValueHandling.Ignore)] public Texts LockReason;
    [JsonProperty("result_steps")] public ResultStep[] ResultSteps;
}

[Serializable]
public struct Step
{
    [JsonProperty("seq")] public int Seq;
    [JsonProperty("type")] public string Type;
    [JsonProperty("actor")] public string Actor;
    [JsonProperty("dialogue_id")] public string DialogueId;
    [JsonProperty("arg")] public string Arg; 
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public Texts Text;
    [JsonProperty("when")] public string When;
    [JsonProperty("effects")] public string Effects;
    /// <summary>"wait"면 이 스텝이 끝날 때까지 다음 스텝을 진행하지 않는다.</summary>
    [JsonProperty("sync")] public string Sync;

    /// <summary>type이 goto일 때 옮겨 갈 씬 id.</summary>
    [JsonProperty("scene_id")] public string SceneId;

    /// <summary>
    /// 이 스텝에 딸린 선택지. 다른 스크립트 파일의 choices와 달리 스텝 안에 직접 들어 있고,
    /// 고른 뒤 이어갈 내용도 goto가 아니라 result_steps로 품고 있다.
    /// </summary>
    [JsonProperty("options")] public NewStreetOptionData[] Options;
}

[Serializable]
public struct NewSceneData
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("day")] public int? Day;

    [JsonProperty("phase")] public ENewScenePhase Phase;
    [JsonProperty("seq")] public int Seq;
    [JsonProperty("start_mode")] public ENewSceneTrigger StartMode;
    [JsonProperty("when")] public string When;
    [JsonProperty("title")] public string Title;
    [JsonProperty("skippable")] public bool Skippable;
    [JsonProperty("group")] public ENewStreetGroup? Group;
    [JsonProperty("steps")] public Step[] Steps;
    [JsonProperty("note")] public string Note;
}

[Serializable]
public struct NewStreetData
{
    [JsonProperty("place")] public string Place;
    [JsonProperty("scenes")] public NewSceneData[] Scenes;
}

/// <summary>Street.json 단일 객체 구조와 1:1 대응되는 ScriptableObject</summary>
[CreateAssetMenu(fileName = "NewStreetDataSO", menuName = "Data/New/StreetDataSO")]
public class NewStreetDataSO : ScriptableObject
{
    public NewStreetData newStreetData;

    // Fast-lookup Dictionary (Inspector 미노출)
    private Dictionary<string, NewSceneData> _sceneDict;

    /// <summary>
    /// Scene ID를 Key로 하는 SceneData 딕셔너리 프로퍼티
    /// </summary>
    public Dictionary<string, NewSceneData> SceneDict
    {
        get
        {
            if (_sceneDict == null)
            {
                InitializeDictionary();
            }
            return _sceneDict;
        }
    }

    /// <summary>
    /// Scenes 배열을 기반으로 Scene ID 딕셔너리를 초기화합니다.
    /// </summary>
    public void InitializeDictionary()
    {
        _sceneDict = new Dictionary<string, NewSceneData>();

        if (newStreetData.Scenes == null) return;

        foreach (var scene in newStreetData.Scenes)
        {
            if (string.IsNullOrEmpty(scene.Id)) continue;

            if (!_sceneDict.ContainsKey(scene.Id))
            {
                _sceneDict.Add(scene.Id, scene);
            }
            else
            {
                Debug.LogWarning($"[NewStreetDataSO] 중복된 Scene ID가 존재합니다: {scene.Id}");
            }
        }
    }

    /// <summary>
    /// Scene ID로 NewSceneData를 안전하게 검색합니다.
    /// </summary>
    public bool TryGetSceneData(string sceneId, out NewSceneData sceneData)
    {
        return SceneDict.TryGetValue(sceneId, out sceneData);
    }

    /// <summary>
    /// [요청 기능] Scene ID를 Key값으로 전달하여 해당 Scene의 Steps 배열을 바로 가져옵니다.
    /// </summary>
    public bool TryGetSteps(string sceneId, out Step[] steps)
    {
        if (TryGetSceneData(sceneId, out NewSceneData sceneData))
        {
            steps = sceneData.Steps;
            return true;
        }

        steps = null;
        return false;
    }

    private void OnValidate()
    {
        InitializeDictionary();
    }
}