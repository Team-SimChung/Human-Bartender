using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>컷씬 루트(패닝) 이동을 정의하는 Timeline 클립 에셋. CutSceneRootMoveBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneRootMoveClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneRootMoveBehaviour rootTemplate = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneRootMoveBehaviour>.Create(graph, rootTemplate);
    }
}
