using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>오브젝트 이동 연출을 정의하는 Timeline 클립 에셋. CutSceneMoveBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneMoveClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneMoveBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneMoveBehaviour>.Create(graph, template);
    }
}
