using UnityEngine.Playables;

/// <summary>Effect 트랙 믹서. 활성 클립의 이펙트 타입을 CutSceneTimelineManager의 오버레이에 적용한다.</summary>
public class CutSceneEffectMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        int inputCount = playable.GetInputCount();
        bool anyActive = false;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            anyActive = true;
            var inputPlayable = (ScriptPlayable<CutSceneEffectBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }
}
