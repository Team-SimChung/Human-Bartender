using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>CSV의 actor를 이 대화 그룹에 배치한 화자의 기준점과 연결함.</summary>
[DisallowMultipleComponent]
public sealed class WorldDialogueSpeakers : MonoBehaviour
{
    [Serializable]
    private sealed class Binding
    {
        [SerializeField] private string actor;
        [SerializeField] private Transform anchor;
        public string Actor => actor;
        public Transform Anchor => anchor;
    }

    [SerializeField] private Binding[] speakers = Array.Empty<Binding>();

    public Transform Resolve(string actor)
    {
        Transform result = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in speakers)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Actor) || binding.Anchor == null)
                throw new InvalidOperationException(name + "의 화자 ID와 말풍선 기준점을 연결하세요.");
            if (!ids.Add(binding.Actor)) throw new InvalidOperationException("중복된 화자 ID: " + binding.Actor);
            if (binding.Actor == actor) result = binding.Anchor;
        }
        if (result == null) throw new InvalidOperationException(name + "에 연결되지 않은 화자 ID: " + actor);
        return result;
    }
}
