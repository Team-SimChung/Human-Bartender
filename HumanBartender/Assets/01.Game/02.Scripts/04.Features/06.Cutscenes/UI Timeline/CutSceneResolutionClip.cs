using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>캔버스 해상도(Reference Resolution) 변경을 정의하는 Timeline 클립 에셋. CutSceneResolutionBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneResolutionClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneResolutionBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneResolutionBehaviour>.Create(graph, template);
    }
}
