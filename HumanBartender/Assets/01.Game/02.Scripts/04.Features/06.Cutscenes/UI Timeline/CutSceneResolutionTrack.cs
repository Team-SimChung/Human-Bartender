using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.9f, 0.4f, 0.6f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneResolutionClip))]
/// <summary>캔버스 해상도를 제어하는 Timeline 트랙. CutSceneResolutionClip 클립을 사용한다.</summary>
public class CutSceneResolutionTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneResolutionMixerBehaviour>.Create(graph, inputCount);
    }
}
