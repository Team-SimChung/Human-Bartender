using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum EMoveMode
{
    [Tooltip("캔버스 기준 절대 위치로 이동")]
    Absolute,

    [Tooltip("현재 이미지 위치 기준 상대 이동")]
    Relative,
}

/// <summary>
/// 클립 구간 동안 활성화된 Image를 이동.
///
/// Absolute: 앵커+오프셋으로 지정한 절대 위치 A → B
/// Relative: 현재 위치 기준 + deltaOffset만큼 이동
///
/// 예시 (Relative):
///   deltaX: 0.3, deltaY: 0   → 현재 위치에서 오른쪽으로 캔버스 30% 이동
///   deltaX: 0,   deltaY: -0.2 → 현재 위치에서 아래로 캔버스 20% 이동
/// </summary>
[Serializable]
public class CutSceneMoveBehaviour : PlayableBehaviour
{
    [Header("대상")]
    [Tooltip("ImageTrack에서 등록한 imagePath와 동일한 값")]
    public string imagePath;

    [Header("이동 모드")]
    public EMoveMode moveMode = EMoveMode.Absolute;

    [Header("Absolute 모드 — 절대 위치")]
    public AnchorType startAnchor = AnchorType.Left;
    public float startOffsetX = 0f;
    public float startOffsetY = 0f;

    public AnchorType endAnchor = AnchorType.Right;
    public float endOffsetX = 0f;
    public float endOffsetY = 0f;

    [Header("Relative 모드 — 현재 위치 기준 이동량 (캔버스 비율)")]
    [Tooltip("X 이동량 (0.3 = 캔버스 너비의 30% 오른쪽)")]
    public float deltaX = 0f;
    [Tooltip("Y 이동량 (0.2 = 캔버스 높이의 20% 위쪽)")]
    public float deltaY = 0f;

    [Header("이동")]
    public Ease moveEase = Ease.InOutCubic;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private Image targetImage;
    [NonSerialized] private bool initialized;
    [NonSerialized] private Vector2 startPos;
    [NonSerialized] private Vector2 endPos;

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
            RectTransform rect = targetImage.GetComponent<RectTransform>();

            switch (moveMode)
            {
                case EMoveMode.Absolute:
                    startPos = CalculateAbsolutePosition(startAnchor, startOffsetX, startOffsetY);
                    endPos   = CalculateAbsolutePosition(endAnchor,   endOffsetX,   endOffsetY);
                    rect.anchoredPosition = startPos;
                    break;

                case EMoveMode.Relative:
                    startPos = rect.anchoredPosition;   // 현재 위치 그대로
                    float w = manager.CanvasRect.rect.width;
                    float h = manager.CanvasRect.rect.height;
                    endPos = startPos + new Vector2(w * deltaX, h * deltaY);
                    break;
            }
        }

        float normalizedTime = (float)(playable.GetTime() / playable.GetDuration());
        normalizedTime = Mathf.Clamp01(normalizedTime);

        float easedTime = DOVirtual.EasedValue(0f, 1f, normalizedTime, moveEase);

        RectTransform rt = targetImage.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.Lerp(startPos, endPos, easedTime);
    }

    Vector2 CalculateAbsolutePosition(AnchorType anchor, float offsetX, float offsetY)
    {
        RectTransform canvasRect = manager.CanvasRect;
        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;

        Vector2 anchorPos = anchor switch
        {
            AnchorType.Center      => new Vector2(w * 0.5f, h * 0.5f),
            AnchorType.Left        => new Vector2(0,        h * 0.5f),
            AnchorType.Right       => new Vector2(w,        h * 0.5f),
            AnchorType.TopLeft     => new Vector2(0,        h),
            AnchorType.TopRight    => new Vector2(w,        h),
            AnchorType.BottomLeft  => new Vector2(0,        0),
            AnchorType.BottomRight => new Vector2(w,        0),
            _                      => new Vector2(w * 0.5f, h * 0.5f),
        };

        return new Vector2(w * offsetX, h * offsetY);
    }
}
