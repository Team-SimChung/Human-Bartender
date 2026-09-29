using UnityEngine;
using UnityEngine.Playables;

/// <summary>SpriteAnim 트랙 믹서. 활성 클립의 애니메이션 정보를 읽어 CutSceneTimelineManager의 스프라이트 애니메이션을 제어한다.</summary>
public class CutSceneSpriteAnimMixerBehaviour : PlayableBehaviour
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

            var inputPlayable = (ScriptPlayable<CutSceneSpriteAnimBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;
        }
    }
}
