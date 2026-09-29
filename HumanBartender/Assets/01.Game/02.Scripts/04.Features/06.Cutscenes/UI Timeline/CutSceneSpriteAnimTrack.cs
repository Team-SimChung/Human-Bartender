using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.65f, 0.45f, 0.85f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneSpriteAnimClip))]
/// <summary>스프라이트 애니메이션 재생을 제어하는 Timeline 트랙. CutSceneSpriteAnimClip 클립을 사용한다.</summary>
public class CutSceneSpriteAnimTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneSpriteAnimMixerBehaviour>.Create(graph, inputCount);
    }
}
