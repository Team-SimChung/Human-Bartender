using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.1f, 0.1f, 0.1f)]
[TrackClipType(typeof(SpriteRevealClip))]
[TrackBindingType(typeof(SpriteRenderer))]
/// <summary>SpriteRenderer의 _Progress 셰이더 프로퍼티를 제어하는 Timeline 트랙.</summary>
public class SpriteRevealTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<SpriteRevealMixerBehaviour>.Create(graph, inputCount);
    }
}
