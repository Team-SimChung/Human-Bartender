using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>오브젝트 Y축 회전을 정의하는 Timeline 클립 에셋. CutSceneRotateYBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneRotateYClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneRotateYBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneRotateYBehaviour>.Create(graph, template);
    }
}
