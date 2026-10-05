using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.3f, 0.3f, 0.5f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneBGClip))]
/// <summary>배경 이미지 전환을 제어하는 Timeline 트랙. CutSceneBGClip 클립을 사용한다.</summary>
public class CutSceneBGTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneBGMixerBehaviour>.Create(graph, inputCount);
    }
}
