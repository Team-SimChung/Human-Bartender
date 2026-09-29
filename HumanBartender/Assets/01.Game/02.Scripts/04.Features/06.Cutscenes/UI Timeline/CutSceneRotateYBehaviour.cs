using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>
/// 클립 구간 동안 활성화된 Image를 Z축 회전.
///
/// 예시:
///   살짝 기울이기:    startAngle=0,  endAngle=15
///   한 바퀴 회전:     startAngle=0,  endAngle=360
///   흔들리는 효과:    pingPong=true, startAngle=-10, endAngle=10
/// </summary>
[Serializable]
public class CutSceneRotateYBehaviour : PlayableBehaviour
{
    [Header("대상")]
    [Tooltip("ImageTrack에서 등록한 imagePath와 동일한 값")]
    public string imagePath;

    [Header("회전")]
    [Tooltip("시작 각도 (Y축)")]
    public float startAngle = 0f;

    [Tooltip("끝 각도 (Y축)")]
    public float endAngle = 360f;

    public Ease rotateEase = Ease.InOutCubic;

    [Header("반복")]
    [Tooltip("PingPong 반복 (흔들리는 연출)")]
    public bool pingPong = false;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private Image targetImage;
    [NonSerialized] private bool initialized;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
        targetImage = null;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        if (targetImage == null)
        {
            targetImage = manager.GetActiveImage(imagePath);
            if (targetImage == null) return;
        }

        if (!initialized)
        {
            initialized = true;
            targetImage.GetComponent<RectTransform>().localRotation =
                Quaternion.Euler(0, startAngle, 0);
        }

        float normalizedTime = (float)(playable.GetTime() / playable.GetDuration());
        normalizedTime = Mathf.Clamp01(normalizedTime);

        float easedTime;

        if (pingPong)
        {
            // 0→1→0 왕복
            float t = normalizedTime * 2f;
            if (t > 1f) t = 2f - t;
            easedTime = DOVirtual.EasedValue(0f, 1f, t, rotateEase);
        }
        else
        {
            easedTime = DOVirtual.EasedValue(0f, 1f, normalizedTime, rotateEase);
        }

        float angle = Mathf.Lerp(startAngle, endAngle, easedTime);
        targetImage.GetComponent<RectTransform>().localRotation =
            Quaternion.Euler(0, angle, 0);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        targetImage = null;
        initialized = false;
    }
}
