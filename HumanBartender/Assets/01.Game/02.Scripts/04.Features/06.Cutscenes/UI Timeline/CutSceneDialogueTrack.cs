using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.95f, 0.85f, 0.4f)]
[TrackBindingType(typeof(CutSceneTimelineManager))]
[TrackClipType(typeof(CutSceneDialogueClip))]
/// <summary>컷씬 대사 버블을 제어하는 Timeline 트랙. CutSceneDialogueClip 클립을 사용한다.</summary>
public class CutSceneDialogueTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutSceneDialogueMixerBehaviour>.Create(graph, inputCount);
    }
}
