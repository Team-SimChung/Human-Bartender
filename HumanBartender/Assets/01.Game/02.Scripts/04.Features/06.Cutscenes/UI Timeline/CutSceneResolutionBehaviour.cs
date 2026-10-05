using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>
/// 클립 구간 동안 CanvasScaler의 referenceResolution을 변경.
/// 컷씬마다 해상도가 다를 때 캔버스 크기를 맞추는 용도.
///
/// 예시:
///   기본 UI:    1920×1080
///   컷씬 A:     1280×720
///   컷씬 B:     2560×1440
///
/// transition=true 면 이전 해상도에서 부드럽게 전환.
/// </summary>
[Serializable]
public class CutSceneResolutionBehaviour : PlayableBehaviour
{
    [Header("해상도")]
    [Tooltip("목표 Reference Resolution")]
    public Vector2 resolution = new Vector2(1920, 1080);

    [Header("전환")]
    [Tooltip("true면 클립 시작 시 이전 해상도에서 부드럽게 전환")]
    public bool transition = false;

    [Tooltip("전환 시간 (transition=true 일 때만)")]
    public float transitionDuration = 0.3f;

    public Ease transitionEase = Ease.InOutCubic;

    [Header("Match Width Or Height")]
    [Tooltip("0=Width 기준, 1=Height 기준, 0.5=중간")]
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] internal bool isActive;
    [NonSerialized] internal bool applied;
    [NonSerialized] internal Vector2 previousResolution;
    [NonSerialized] internal float previousMatch;
}
