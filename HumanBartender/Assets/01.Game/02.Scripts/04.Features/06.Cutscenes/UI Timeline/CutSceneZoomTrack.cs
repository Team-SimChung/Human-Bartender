using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.7f, 0.7f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneZoomClip))]
/// <summary>카메라 줌을 제어하는 Timeline 트랙. CutSceneZoomClip 클립을 사용한다.</summary>
public class CutSceneZoomTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneZoomMixerBehaviour>.Create(graph, inputCount);
    }
}
