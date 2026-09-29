using System;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewCutSceneRefData
{
    [field: SerializeField][JsonProperty("id")] public string Id { get; set; }
    [field: SerializeField][JsonProperty("kind")] public ENewCutSceneKind Kind { get; set; }
    [field: SerializeField][JsonProperty("resource_key")] public string ResourceKey { get; set; }
    [field: SerializeField][JsonProperty("note")] public string Note { get; set; }
}

/// <summary>CSV에서 읽은 컷신 참조 캐시.</summary>
[CreateAssetMenu(fileName = "NewCutSceneDataSO", menuName = "Data/New/CutSceneDataSO")]
public class NewCutSceneDataSO : ScriptableObject
{
    public NewCutSceneRefData[] cutSceneData;

    /// <summary>
    /// 컷씬 id로 참조 항목을 찾는다. 없으면 false.
    ///
    /// 대본(2부 timeline 스텝)이 적어 놓은 id를 그대로 받는 자리라, 없는 id는 흔한 오타다.
    /// 부르는 쪽이 어느 id를 못 찾았는지 말할 수 있게 예외 대신 false로 돌려준다.
    /// </summary>
    public bool TryGet(string id, out NewCutSceneRefData cutScene)
    {
        cutScene = default;
        if (string.IsNullOrEmpty(id) || cutSceneData == null) return false;

        foreach (var candidate in cutSceneData)
        {
            if (candidate.Id != id) continue;

            cutScene = candidate;
            return true;
        }

        return false;
    }
}
