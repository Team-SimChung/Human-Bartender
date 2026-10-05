using UnityEngine;
using UnityEngine.Playables;

/// <summary>Shake 트랙 믹서. 활성 클립의 흔들기 파라미터를 읽어 CutSceneTimelineManager의 루트에 DOTween Shake를 적용한다.</summary>
public class CutSceneShakeMixerBehaviour : PlayableBehaviour
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

            var inputPlayable = (ScriptPlayable<CutSceneShakeBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }
}
