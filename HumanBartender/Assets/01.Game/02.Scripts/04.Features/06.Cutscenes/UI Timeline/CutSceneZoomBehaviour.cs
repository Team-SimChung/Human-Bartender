using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 클립 구간 동안 cutSceneRoot를 스케일링하여 카메라 줌 효과.
/// RootMoveTrack(패닝)과 병렬로 사용 가능.
///
/// 예시:
///   줌인:    startScale=1.0  endScale=1.5
///   줌아웃:  startScale=1.5  endScale=1.0
///   펀치줌:  startScale=1.0  endScale=1.2  (짧은 클립)
/// </summary>
[Serializable]
public class CutSceneZoomBehaviour : PlayableBehaviour
{
    [Header("줌")]
    [Tooltip("시작 스케일 (1.0 = 원본)")]
    public float startScale = 1.0f;

    [Tooltip("끝 스케일")]
    public float endScale = 1.5f;

    public Ease zoomEase = Ease.InOutCubic;

    [Header("줌 중심점")]
    [Tooltip("줌 중심 피벗 (0.5,0.5)=화면 중앙, (0,0)=좌하단")]
    public Vector2 zoomPivot = new Vector2(0.5f, 0.5f);

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private bool initialized;
    [NonSerialized] private Vector2 prevPivot;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null || manager.CutSceneRoot == null) return;

        RectTransform root = manager.CutSceneRoot;

        if (!initialized)
        {
            initialized = true;
            prevPivot = root.pivot;

            // 줌 중심점 변경 + 위치 보정
            Vector2 pivotDelta = zoomPivot - prevPivot;
            Vector2 size = root.rect.size;
            root.pivot = zoomPivot;
            root.anchoredPosition += new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);

            root.localScale = Vector3.one * startScale;
        }

        float normalizedTime = (float)(playable.GetTime() / playable.GetDuration());
        normalizedTime = Mathf.Clamp01(normalizedTime);

        float easedTime = DOVirtual.EasedValue(0f, 1f, normalizedTime, zoomEase);
        float scale = Mathf.Lerp(startScale, endScale, easedTime);

        root.localScale = Vector3.one * scale;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (manager == null || manager.CutSceneRoot == null) return;

        RectTransform root = manager.CutSceneRoot;

        // 피벗 복구 + 위치 보정
        if (initialized)
        {
            Vector2 pivotDelta = prevPivot - root.pivot;
            Vector2 size = root.rect.size;
            root.pivot = prevPivot;
            root.anchoredPosition += new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);

            root.localScale = Vector3.one;
            initialized = false;
        }
    }
}
