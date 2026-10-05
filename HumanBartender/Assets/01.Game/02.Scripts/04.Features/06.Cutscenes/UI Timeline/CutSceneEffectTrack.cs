using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


[TrackColor(1f, 1f, 0.4f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneEffectClip))]
/// <summary>화면 이펙트 오버레이를 제어하는 Timeline 트랙. CutSceneEffectClip 클립을 사용한다.</summary>
public class CutSceneEffectTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneEffectMixerBehaviour>.Create(graph, inputCount);
    }
}
