using UnityEngine;
using UnityEngine.Playables;

/// <summary>Move 트랙 믹서. 활성 클립의 이동 데이터를 읽어 CutSceneTimelineManager의 오브젝트를 이동시킨다.</summary>
public class CutSceneMoveMixerBehaviour : PlayableBehaviour
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

            var inputPlayable = (ScriptPlayable<CutSceneMoveBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }
}
