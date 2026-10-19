using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldSpeechBubblePlacement { Above, Below }

/// <summary>CSV의 actor를 이 대화 그룹에 배치한 화자의 기준점과 연결함.</summary>
[DisallowMultipleComponent]
public sealed class WorldDialogueSpeakers : MonoBehaviour
{
    [Serializable]
    private sealed class Binding
    {
        [SerializeField] private string actor;
        [SerializeField] private Transform anchor;
        [SerializeField] private WorldSpeechBubblePlacement placement;
        [SerializeField] private Vector2 bubbleOffset = new(0, 8);
        public string Actor => actor;
        public Transform Anchor => anchor;
        public WorldSpeechBubblePlacement Placement => placement;
        public Vector2 BubbleOffset => bubbleOffset;
    }

    [SerializeField] private Binding[] speakers = Array.Empty<Binding>();

    public Transform Resolve(string actor)
    {
        if (TryResolve(actor, out var anchor, out _, out _)) return anchor;
        throw new InvalidOperationException(name + "에 연결되지 않은 화자 ID: " + actor);
    }

    public bool TryResolve(string actor, out Transform anchor, out WorldSpeechBubblePlacement placement, out Vector2 bubbleOffset)
    {
        anchor = null;
        placement = WorldSpeechBubblePlacement.Above;
        bubbleOffset = Vector2.zero;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in speakers ?? Array.Empty<Binding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Actor) || binding.Anchor == null)
                throw new InvalidOperationException(name + "의 화자 ID와 말풍선 기준점을 연결하세요.");
            if (!ids.Add(binding.Actor)) throw new InvalidOperationException("중복된 화자 ID: " + binding.Actor);
            if (binding.Actor != actor) continue;
            anchor = binding.Anchor;
            placement = binding.Placement;
            bubbleOffset = binding.BubbleOffset;
        }
        return anchor != null;
    }
}
