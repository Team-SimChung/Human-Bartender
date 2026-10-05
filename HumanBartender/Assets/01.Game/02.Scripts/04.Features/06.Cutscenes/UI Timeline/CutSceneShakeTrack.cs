using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.3f, 0.3f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneShakeClip))]
/// <summary>화면 흔들기를 제어하는 Timeline 트랙. CutSceneShakeClip 클립을 사용한다.</summary>
public class CutSceneShakeTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneShakeMixerBehaviour>.Create(graph, inputCount);
    }
}
