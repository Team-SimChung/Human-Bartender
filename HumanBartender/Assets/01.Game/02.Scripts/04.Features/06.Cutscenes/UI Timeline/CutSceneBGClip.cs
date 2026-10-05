using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>배경 이미지 전환을 정의하는 Timeline 클립 에셋. CutSceneBGBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneBGClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneBGBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneBGBehaviour>.Create(graph, template);
    }
}
