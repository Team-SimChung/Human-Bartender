using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>대사 버블 표시를 정의하는 Timeline 클립 에셋. CutSceneDialogueBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneDialogueClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneDialogueBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneDialogueBehaviour>.Create(graph, template);
    }
}
