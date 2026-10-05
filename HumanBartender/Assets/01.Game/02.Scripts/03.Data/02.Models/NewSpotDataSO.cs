using Newtonsoft.Json;
using Spine;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct NewSpotData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("area")] public string Area { get; set; }
    [field: SerializeField][JsonProperty("desc")] public string Desc { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }

    // JSON에 직렬화되지 않고, 런타임 씬에서 채워 넣을 위치/회전 값 (값 타입 복사)
    [JsonIgnore] public Vector3 Position { get; set; }
    [JsonIgnore] public Quaternion Rotation { get; set; }
}

/// <summary>StreamingAssets/json/spots.json을 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NewSpotDataSO", menuName = "Data/New/SpotDataSO")]
public class NewSpotDataSO : ScriptableObject
{
    public NewSpotData[] spotData;
    // 빠른 검색을 위한 Fast-lookup Dictionary (Inspector 미노출)
    private Dictionary<string, NewSpotData> _dict;

    public Dictionary<string, NewSpotData> Dict
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
    /// 배열 데이터를 기반으로 딕셔너리를 초기화합니다.
    /// </summary>
    public void InitializeDictionary()
    {
        _dict = new Dictionary<string, NewSpotData>();

        if (spotData == null) return;

        foreach (var data in spotData)
        {
            if (string.IsNullOrEmpty(data.Id)) continue;

            if (!_dict.ContainsKey(data.Id))
            {
                _dict.Add(data.Id, data);
            }
            else
            {
                Debug.LogWarning($"[SpotDataSO] 중복된 ID가 존재합니다: {data.Id}");
            }
        }
    }

    /// <summary>
    /// 씬 상의 SpotAnchor들이 자신의 위치/회전 좌표(값)를 딕셔너리에 등록합니다.
    /// </summary>
    public bool RegisterSpotTransform(string id, Vector3 position, Quaternion rotation)
    {
        if (Dict.TryGetValue(id, out var data))
        {
            data.Position = position;
            data.Rotation = rotation;
            _dict[id] = data; // struct 특성상 수정한 복사본을 딕셔너리에 다시 덮어씀
            return true;
        }

        Debug.LogWarning($"[SpotDataSO] JSON 데이터에 존재하지 않는 ID입니다: {id}");
        return false;
    }

    /// <summary>
    /// ID로 데이터를 가져오는 안전한 접근 메서드
    /// </summary>
    public bool TryGetData(string id, out NewSpotData data)
    {
        return Dict.TryGetValue(id, out data);
    }

    /// <summary>
    /// 에디터에서 데이터 배열 수정 시 딕셔너리 즉각 재구성
    /// </summary>
    private void OnValidate()
    {
        InitializeDictionary();
    }
}
