using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[Serializable]
/// <summary>화면 이펙트(페이드 인/아웃 등) 한 구간의 타입과 파라미터를 정의하는 PlayableBehaviour.</summary>
public class CutSceneEffectBehaviour : PlayableBehaviour
{
    public EEffectType effectType = EEffectType.FadeIn;
    [Range(0f, 1f)] public float intensity = 0.5f;

    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private bool initialized;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        Image overlay = manager.EffectOverlay;
        if (overlay == null) return;

        float normalizedTime = (float)(playable.GetTime() / playable.GetDuration());
        normalizedTime = Mathf.Clamp01(normalizedTime);

        switch (effectType)
        {
            case EEffectType.FadeIn:
                // 밝은 상태 → 어두워짐
                overlay.gameObject.SetActive(true);
                overlay.color = new Color(0, 0, 0, normalizedTime);
                break;

            case EEffectType.FadeOut:
                // 어두운 상태 → 밝아짐
                overlay.gameObject.SetActive(true);
                overlay.color = new Color(0, 0, 0, 1f - normalizedTime);
                break;

            case EEffectType.FlashWhite:
                // 흰색 → 투명
                overlay.gameObject.SetActive(true);
                overlay.color = new Color(1, 1, 1, 1f - normalizedTime);
                break;

            case EEffectType.Dim:
                // 0 → intensity까지
                overlay.gameObject.SetActive(true);
                overlay.color = new Color(0, 0, 0, intensity * normalizedTime);
                break;

            case EEffectType.Vignette:
                overlay.gameObject.SetActive(true);
                overlay.color = new Color(0, 0, 0, intensity * normalizedTime);
                break;

            case EEffectType.ScreenShake:
                if (!initialized)
                {
                    initialized = true;
                    float dur = (float)playable.GetDuration();
                    manager.CanvasRect.DOShakeAnchorPos(dur, intensity * 10f, 20, 90, false, true);
                }
                break;

            case EEffectType.ZoomPulse:
                if (!initialized)
                {
                    initialized = true;
                    float dur = (float)playable.GetDuration();
                    float scale = 1f + intensity * 0.1f;
                    var seq = DOTween.Sequence();
                    seq.Append(manager.CanvasRect.DOScale(scale, dur * 0.5f).SetEase(Ease.OutQuad));
                    seq.Append(manager.CanvasRect.DOScale(1f, dur * 0.5f).SetEase(Ease.InQuad));
                }
                break;
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (manager == null) return;

        // 클립 종료 시 오버레이 정리
        switch (effectType)
        {
            case EEffectType.FadeOut:
            case EEffectType.FlashWhite:
                if (manager.EffectOverlay != null)
                    manager.EffectOverlay.gameObject.SetActive(false);
                break;
        }

        initialized = false;
    }
}
