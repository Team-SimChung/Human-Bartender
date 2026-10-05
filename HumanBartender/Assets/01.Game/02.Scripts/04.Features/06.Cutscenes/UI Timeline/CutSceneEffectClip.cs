using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>화면 이펙트 연출을 정의하는 Timeline 클립 에셋. CutSceneEffectBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneEffectClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneEffectBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneEffectBehaviour>.Create(graph, template);
    }
}
