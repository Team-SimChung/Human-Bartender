using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인터랙트 포인트 하나가 열어 주는 대화. 한 지점이 일차와 진행 상태에 따라 여러 대화를 갖는다.
/// </summary>
[Serializable]
public struct NewInteractDialogueFlowData
{
    [field: SerializeField][JsonProperty("scene_id")] public string SceneId { get; set; }

    /// <summary>
    /// 이 대화가 열리는 일차. null이면 특정 일차에 매이지 않는다는 뜻이고, 그때는 when이 조건을 든다.
    /// 값 타입으로 두면 데이터의 null을 넣지 못해 파일 전체가 역직렬화에 실패한다.
    /// </summary>
    [JsonProperty("day")] public int? Day { get; set; }

    /// <summary>같은 지점 안에서의 순서. 작은 것부터 소비한다.</summary>
    [field: SerializeField][JsonProperty("flow_seq")] public int FlowSeq { get; set; }

    /// <summary>한 번만 볼지(once) 계속 볼지(repeat).</summary>
    [field: SerializeField][JsonProperty("play_type")] public EPlayType PlayType { get; set; }

    /// <summary>이 대화가 열리는 조건식. 조건이 없으면 null이다.</summary>
    [field: SerializeField][JsonProperty("when")] public string When { get; set; }
}

/// <summary>
/// 출퇴근 구간에서 상호작용할 수 있는 지점 하나.
///
/// 스키마가 한 차례 바뀌었다. 예전 모델은 spot·actor·trigger·scene_or_shop·selection을 들고 있었는데
/// 지금 데이터에는 그 이름이 하나도 없고, 대신 source_id·spot_id·spawn_when·activation_mode·
/// action_type·dialogue_flows를 쓴다. 이름이 어긋나면 Newtonsoft가 조용히 넘어가면서 값이 전부
/// 비어 버리므로, 데이터에 적힌 이름을 그대로 따른다.
/// </summary>
[Serializable]
public struct NewInteractPointData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    /// <summary>이 지점이 사람인지 사물인지. 예전에는 npc였고 지금은 actor다.</summary>
    [field: SerializeField][JsonProperty("kind")] public ENewInteractKind Kind { get; set; }

    /// <summary>실제로 놓이는 대상의 id(캐릭터 또는 오브젝트).</summary>
    [field: SerializeField][JsonProperty("source_id")] public string SourceId { get; set; }

    /// <summary>이 지점이 놓이는 장소.</summary>
    [field: SerializeField][JsonProperty("spot_id")] public string SpotId { get; set; }

    /// <summary>바라보는 방향(left/right). 정해지지 않았으면 null이다.</summary>
    [field: SerializeField][JsonProperty("facing")] public string Facing { get; set; }

    [field: SerializeField][JsonProperty("phase")] public EGameFlow Phase { get; set; }

    /// <summary>이 지점이 생기는 조건식.</summary>
    [field: SerializeField][JsonProperty("spawn_when")] public string SpawnWhen { get; set; }

    /// <summary>다가가면 켜지는지(proximity) 눌러야 켜지는지(interact).</summary>
    [field: SerializeField][JsonProperty("activation_mode")] public EActivationMode ActivationMode { get; set; }

    /// <summary>상호작용이 열리는 조건식. 조건이 없으면 null이다.</summary>
    [field: SerializeField][JsonProperty("interact_when")] public string InteractWhen { get; set; }

    /// <summary>같은 자리에 여럿이 겹칠 때의 우선순위.</summary>
    [field: SerializeField][JsonProperty("priority")] public int Priority { get; set; }

    /// <summary>상호작용했을 때 하는 일(dialogue/transition).</summary>
    [field: SerializeField][JsonProperty("action_type")] public EActionType ActionType { get; set; }

    /// <summary>action_type이 transition일 때 옮겨 갈 대상.</summary>
    [field: SerializeField][JsonProperty("action_ref")] public string ActionRef { get; set; }

    /// <summary>action_type이 dialogue일 때 열리는 대화 목록.</summary>
    [field: SerializeField][JsonProperty("dialogue_flows")] public NewInteractDialogueFlowData[] DialogueFlows { get; set; }

    /// <summary>기획 메모. 런타임에는 쓰지 않는다.</summary>
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>StreamingAssets/json/interact_points.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewInteractPointDataSO", menuName = "Data/New/InteractPointDataSO")]
public class NewInteractPointDataSO : ScriptableObject
{
    public NewInteractPointData[] interactPointData;

    // Fast-lookup Dictionary (Inspector에 직렬화되지 않음)
    private Dictionary<string, NewInteractPointData> _dict;

    /// <summary>
    /// 딕셔너리 프로퍼티 (최초 접근 시 캐싱)
    /// </summary>
    public Dictionary<string, NewInteractPointData> Dict
    {
        get
        {
            if (_dict == null)
            {
                InitializeDictionary();
            }
            return _dict;
        }
    }

    /// <summary>
    /// ScriptableObject 로드 시 또는 배열 수정 후 캐시 갱신
    /// </summary>
    public void InitializeDictionary()
    {
        _dict = new Dictionary<string, NewInteractPointData>();

        if (interactPointData == null) return;

        foreach (var data in interactPointData)
        {
            if (string.IsNullOrEmpty(data.Id)) continue;

            if (!_dict.ContainsKey(data.Id))
            {
                _dict.Add(data.Id, data);
            }
            else
            {
                Debug.LogWarning($"[NewInteractPointDataSO] 중복된 ID가 존재합니다: {data.Id}");
            }
        }
    }

    /// <summary>
    /// ID로 데이터를 가져오는 안전한 접근 메서드
    /// </summary>
    public bool TryGetData(string id, out NewInteractPointData data)
    {
        return Dict.TryGetValue(id, out data);
    }

    /// <summary>
    /// 에디터에서 데이터 변경 시 딕셔너리를 재구성합니다.
    /// </summary>
    private void OnValidate()
    {
        InitializeDictionary();
    }
}