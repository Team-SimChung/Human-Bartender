using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>카메라 줌(스케일) 연출을 정의하는 Timeline 클립 에셋. CutSceneZoomBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneZoomClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneZoomBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneZoomBehaviour>.Create(graph, template);
    }
}
