using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 클립 구간 동안 cutSceneRoot를 흔들어 카메라 쉐이킹 효과.
/// DOTween.DOShake와 달리 매 프레임 직접 제어하므로 Timeline 스크러빙에 대응.
///
/// 강도 변화:
///   fadeIn:  클립 시작 시 0에서 intensity까지 서서히 증가
///   fadeOut: 클립 끝에서 intensity에서 0으로 서서히 감소
///   둘 다 켜면 시작-최고-끝 (0 → intensity → 0) 곡선
///
/// 예시:
///   폭발 충격:   intensity=15, frequency=25, fadeIn=0.05, fadeOut=0.3
///   지진 진동:   intensity=5,  frequency=12, fadeIn=0.5,  fadeOut=0.5
///   긴장감 떨림: intensity=2,  frequency=30, fadeIn=0,    fadeOut=0
/// </summary>
[Serializable]
public class CutSceneShakeBehaviour : PlayableBehaviour
{
    [Header("흔들림")]
    [Tooltip("최대 흔들림 세기 (픽셀)")]
    public float intensity = 10f;

    [Tooltip("초당 흔들림 횟수")]
    public float frequency = 20f;

    [Header("방향")]
    [Tooltip("X축 흔들림")]
    public bool shakeX = true;
    [Tooltip("Y축 흔들림")]
    public bool shakeY = true;

    [Header("페이드")]
    [Tooltip("시작 시 강도가 0에서 올라가는 시간 (초). 0이면 즉시 최대 강도")]
    public float fadeInDuration = 0f;
    [Tooltip("끝에서 강도가 0으로 내려가는 시간 (초). 0이면 즉시 종료")]
    public float fadeOutDuration = 0.2f;

    [Header("노이즈")]
    [Tooltip("랜덤 시드 (같은 값이면 같은 흔들림 패턴). 0이면 자동")]
    public int seed = 0;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private bool initialized;
    [NonSerialized] private Vector2 originalPos;
    [NonSerialized] private int actualSeed;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null || manager.CutSceneRoot == null) return;

        RectTransform root = manager.CutSceneBGRoot;

        if (!initialized)
        {
            initialized = true;
            originalPos = root.anchoredPosition;
            actualSeed = seed != 0 ? seed : GetHashCode();
        }

        double time = playable.GetTime();
        double duration = playable.GetDuration();
        float normalizedTime = Mathf.Clamp01((float)(time / duration));

        // 강도 계산 (fadeIn/fadeOut 적용)
        float currentIntensity = CalculateIntensity(normalizedTime, (float)duration);

        if (currentIntensity <= 0.001f)
        {
            root.anchoredPosition = originalPos;
            return;
        }

        // Perlin 노이즈 기반 흔들림 (시간 기반이라 스크러빙에도 일관된 결과)
        float t = (float)time * frequency;

        float offsetX = 0f;
        float offsetY = 0f;

        if (shakeX)
        {
            // 서로 다른 시드로 X/Y 독립적인 노이즈
            float noiseX = Mathf.PerlinNoise(t + actualSeed * 0.1f, 0f) * 2f - 1f;
            offsetX = noiseX * currentIntensity;
        }

        if (shakeY)
        {
            float noiseY = Mathf.PerlinNoise(0f, t + actualSeed * 0.7f) * 2f - 1f;
            offsetY = noiseY * currentIntensity;
        }

        root.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);
    }

    float CalculateIntensity(float normalizedTime, float clipDuration)
    {
        float result = intensity;

        // fadeIn: 클립 시작에서 서서히 증가
        if (fadeInDuration > 0 && clipDuration > 0)
        {
            float fadeInNormalized = fadeInDuration / clipDuration;
            if (normalizedTime < fadeInNormalized)
            {
                result *= normalizedTime / fadeInNormalized;
            }
        }

        // fadeOut: 클립 끝에서 서서히 감소
        if (fadeOutDuration > 0 && clipDuration > 0)
        {
            float fadeOutStart = 1f - (fadeOutDuration / clipDuration);
            if (normalizedTime > fadeOutStart)
            {
                float fadeOutProgress = (normalizedTime - fadeOutStart) / (1f - fadeOutStart);
                result *= 1f - fadeOutProgress;
            }
        }

        return result;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // 클립 종료 시 원래 위치로 복구
        if (initialized && manager != null && manager.CutSceneRoot != null)
        {
            manager.CutSceneRoot.anchoredPosition = originalPos;
            initialized = false;
        }
    }
}
