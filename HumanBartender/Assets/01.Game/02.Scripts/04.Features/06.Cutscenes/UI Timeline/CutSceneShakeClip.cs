using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>화면 흔들기(Shake) 연출을 정의하는 Timeline 클립 에셋. CutSceneShakeBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneShakeClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneShakeBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneShakeBehaviour>.Create(graph, template);
    }
}
