using UnityEngine;
using UnityEngine.Playables;

/// <summary>Zoom 트랙 믹서. 활성 클립의 줌 배율을 읽어 CutSceneTimelineManager의 루트 스케일에 적용한다.</summary>
public class CutSceneZoomMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var inputPlayable = (ScriptPlayable<CutSceneZoomBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        // Timeline 종료 시 스케일 복구는
        // CutSceneZoomBehaviour.OnBehaviourPause에서 처리
    }
}
