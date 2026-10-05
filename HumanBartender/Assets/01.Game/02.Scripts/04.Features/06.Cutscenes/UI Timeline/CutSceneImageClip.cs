using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>이미지 오브젝트 연출을 정의하는 Timeline 클립 에셋. CutSceneImageBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneImageClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneImageBehaviour template = new();

    // 클립이 지원하는 기능 플래그
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<CutSceneImageBehaviour>.Create(graph, template);
        return playable;
    }
}
