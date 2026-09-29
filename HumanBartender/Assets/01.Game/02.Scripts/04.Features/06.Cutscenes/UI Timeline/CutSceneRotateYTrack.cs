using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.6f, 0.2f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneRotateYClip))]
/// <summary>오브젝트 Y축 회전을 제어하는 Timeline 트랙. CutSceneRotateYClip 클립을 사용한다.</summary>
public class CutSceneRotateYTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneRotateYMixerBehaviour>.Create(graph, inputCount);
    }
}
