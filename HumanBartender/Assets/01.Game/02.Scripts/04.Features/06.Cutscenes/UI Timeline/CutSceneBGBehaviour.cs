using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 배경 클립 데이터.
/// 실제 전환 로직은 MixerBehaviour에서 처리.
///
/// 배경이 캔버스보다 클 때:
///   bgScale로 크기 조절, bgOffset으로 보이는 영역 이동
/// </summary>
[Serializable]
public class CutSceneBGBehaviour : PlayableBehaviour
{
    [Header("배경 이미지")]
    [Tooltip("Resources/Cutscenes/ 하위 경로 (확장자 제외)")]
    public string bgPath;

    [Header("위치/크기")]
    [Tooltip("배경 오프셋 (캔버스 비율, 예: (0.1, 0) = 오른쪽으로 10% 이동)")]
    public Vector2 bgOffset = Vector2.zero;

    [Tooltip("배경 스케일 (1.0 = 캔버스에 맞춤, 1.5 = 150% 크기)")]
    public float bgScale = 1.0f;

    [Tooltip("배경 피벗 (0.5,0.5)=중앙 기준, (0,0.5)=왼쪽 기준")]
    public Vector2 bgPivot = new Vector2(0.5f, 0.5f);

    [Header("전환")]
    [Tooltip("등장 페이드 시간 (0이면 즉시)")]
    public float fadeInDuration = 0.3f;

    [Tooltip("퇴장 페이드 시간 (0이면 즉시)")]
    public float fadeOutDuration = 0.3f;

    [Tooltip("배경 색상 틴트 (White = 원본)")]
    public Color tint = Color.white;

    // ── 런타임 (Mixer에서 관리) ───────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] internal bool isActive;
    [NonSerialized] internal bool fadeInDone;
    [NonSerialized] internal bool fadeOutStarted;
    [NonSerialized] internal Sprite cachedSprite;
    [NonSerialized] internal bool spriteLoaded;

    public override void OnPlayableCreate(Playable playable)
    {
        isActive = false;
        fadeInDone = false;
        fadeOutStarted = false;
        spriteLoaded = false;
    }

    /// <summary> 스프라이트 로드 (한 번만) </summary>
    internal Sprite LoadSprite()
    {
        if (!spriteLoaded)
        {
            spriteLoaded = true;
            if (!string.IsNullOrEmpty(bgPath))
                cachedSprite = Resources.Load<Sprite>($"Cutscenes/{bgPath}");
        }
        return cachedSprite;
    }
}
