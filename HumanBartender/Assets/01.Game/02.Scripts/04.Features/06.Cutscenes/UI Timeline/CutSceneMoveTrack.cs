using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.4f, 0.6f, 1f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneMoveClip))]
/// <summary>오브젝트 이동을 제어하는 Timeline 트랙. CutSceneMoveClip 클립을 사용한다.</summary>
public class CutSceneMoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneMoveMixerBehaviour>.Create(graph, inputCount);
    }
}
