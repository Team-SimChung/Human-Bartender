using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 클립 구간 동안 DynamicSpeechBubble을 사용하여 말풍선 + 타이핑 표시.
///
/// 타이핑 중 매 프레임:
///   1. 보이는 글자 수 계산
///   2. 보이는 텍스트만큼 ResizeToFit → 말풍선 실시간 확장
///   3. maxVisibleCharacters 업데이트
///
/// targetImagePath가 있으면 해당 이미지를 따라다님.
/// </summary>
[Serializable]
public class CutSceneDialogueBehaviour : PlayableBehaviour
{
    [Header("대사")]
    [TextArea(3, 8)]
    [Tooltip("표시할 텍스트")]
    public string text = "";

    [Tooltip("캐릭터 이름 (비어있으면 이름 숨김)")]
    public string speakerName = "";

    [Header("타이핑")]
    [Tooltip("초당 타이핑 글자 수")]
    public float charsPerSecond = 30f;

    [Tooltip("타이핑 시작 전 대기 시간 (말풍선 등장 후)")]
    public float typingDelay = 0.2f;

    [Tooltip("말풍선 size localScale")]
    public float sizeScale = 0.5f;

    [Header("말풍선 위치")]
    [Tooltip("따라갈 이미지의 imagePath (비어있으면 고정 위치)")]
    public string targetImagePath = "";

    [Tooltip("캔버스 비율 기준 오프셋")]
    public Vector2 offset = new Vector2(0f, 0.15f);

    [Tooltip("고정 위치일 때 앵커")]
    public AnchorType anchor = AnchorType.Center;

    [Header("등장 / 퇴장")]
    public EEneterPreset enterType = EEneterPreset.ScaleUp;
    public float enterDuration = 0.2f;
    public Ease enterEase = Ease.OutBack;

    public EExitPreset exitType = EExitPreset.ScaleDown;
    public float exitDuration = 0.15f;
    public Ease exitEase = Ease.InBack;

    [Header("말풍선 인덱스")]
    [Tooltip("Manager의 말풍선 리스트에서 몇 번째를 사용할지 (동시 다중 말풍선)")]
    public int bubbleIndex = 0;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] internal bool isActive;
    [NonSerialized] internal bool enterDone;
    [NonSerialized] internal bool exitStarted;
    [NonSerialized] internal int lastVisibleCount;
    [NonSerialized] internal int prevVisibleCount;

    public override void OnPlayableCreate(Playable playable)
    {
        isActive = false;
        enterDone = false;
        exitStarted = false;
        lastVisibleCount = 0;
        prevVisibleCount = -1;
    }
}
