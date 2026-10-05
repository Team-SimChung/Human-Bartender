using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
/// <summary>Spine 스프라이트 애니메이션 재생을 정의하는 Timeline 클립 에셋. CutSceneSpriteAnimBehaviour 인스턴스를 생성한다.</summary>
public class CutSceneSpriteAnimClip : PlayableAsset, ITimelineClipAsset
{
    public CutSceneSpriteAnimBehaviour template = new();

    public ClipCaps clipCaps => ClipCaps.Looping;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutSceneSpriteAnimBehaviour>.Create(graph, template);
    }
}
