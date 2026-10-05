using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.5f, 1f, 0.5f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneImageClip))]
/// <summary>이미지 오브젝트 연출을 제어하는 Timeline 트랙. CutSceneImageClip 클립을 사용한다.</summary>
public class CutSceneImageTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneImageMixerBehaviour>.Create(graph, inputCount);
    }
}
