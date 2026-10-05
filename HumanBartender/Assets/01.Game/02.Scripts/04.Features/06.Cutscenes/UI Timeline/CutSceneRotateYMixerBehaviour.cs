using UnityEngine;
using UnityEngine.Playables;

/// <summary>RotateY 트랙 믹서. 활성 클립의 Y축 회전 각도를 읽어 CutSceneTimelineManager의 대상 오브젝트에 적용한다.</summary>
public class CutSceneRotateYMixerBehaviour : PlayableBehaviour
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

            var inputPlayable = (ScriptPlayable<CutSceneRotateYBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }
}
