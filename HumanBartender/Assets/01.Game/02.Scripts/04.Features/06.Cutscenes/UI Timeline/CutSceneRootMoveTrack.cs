using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


[TrackColor(0.3f, 0.8f, 0.6f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneRootMoveClip))]
/// <summary>컷씬 루트 패닝을 제어하는 Timeline 트랙. CutSceneRootMoveClip 클립을 사용한다.</summary>
public class CutSceneRootMoveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneRootMoveMixerBehaviour>.Create(graph, inputCount);
    }
}


