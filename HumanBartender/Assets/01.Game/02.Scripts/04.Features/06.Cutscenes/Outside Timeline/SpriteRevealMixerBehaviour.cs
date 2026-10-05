using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// SpriteReveal 트랙의 믹서. 활성 클립들의 _Progress 값을 블렌딩하여 SpriteRenderer에 적용한다.
/// 클립이 없는 구간은 머티리얼 기본값으로 보간한다.
/// </summary>
public class SpriteRevealMixerBehaviour : PlayableBehaviour
{
    static readonly int ProgressId = Shader.PropertyToID("_Progress");
    MaterialPropertyBlock block;
    bool initialized;
    float defaultProgress;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var sr = playerData as SpriteRenderer;
        if (sr == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        if (!initialized)
        {
            defaultProgress = (sr.sharedMaterial != null && sr.sharedMaterial.HasProperty(ProgressId))
                ? sr.sharedMaterial.GetFloat(ProgressId) : 0f;
            initialized = true;
        }

        int inputCount = playable.GetInputCount();
        float blended = 0f, totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;

            var input = (ScriptPlayable<SpriteRevealBehaviour>)playable.GetInput(i);
            var data  = input.GetBehaviour();

            double dur = input.GetDuration();
            float local = dur > 0 ? Mathf.Clamp01((float)(input.GetTime() / dur)) : 0f;

            blended     += Mathf.Lerp(data.startProgress, data.endProgress, local) * w;
            totalWeight += w;
        }

        // 클립이 없거나 페이드 구간일 땐 머티리얼 기본값으로 보간
        float final = blended + defaultProgress * (1f - Mathf.Clamp01(totalWeight));

        sr.GetPropertyBlock(block);
        block.SetFloat(ProgressId, final);
        sr.SetPropertyBlock(block);
    }
}
