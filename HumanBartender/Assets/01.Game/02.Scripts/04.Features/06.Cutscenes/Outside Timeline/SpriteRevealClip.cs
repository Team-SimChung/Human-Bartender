using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
/// <summary>SpriteRevealBehaviour를 Timeline 클립으로 생성하는 PlayableAsset.</summary>
public class SpriteRevealClip : PlayableAsset, ITimelineClipAsset
{
    public SpriteRevealBehaviour template = new SpriteRevealBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<SpriteRevealBehaviour>.Create(graph, template);
    }
}
