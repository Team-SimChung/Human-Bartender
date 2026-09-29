using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Dialogue 트랙 믹서. 활성 클립에서 대사 데이터를 읽어 CutSceneTimelineManager의 대사 버블에 표시한다.</summary>
public class CutSceneDialogueMixerBehaviour : PlayableBehaviour
{
    readonly TimelineTransitionScope transitions = new();
    private CutSceneTimelineManager manager;

    static readonly System.Collections.Generic.Dictionary<AnchorType, Vector2> AnchorMap = new()
    {
        { AnchorType.Center,      new Vector2(0.5f, 0.5f) },
        { AnchorType.Left,        new Vector2(0.0f, 0.5f) },
        { AnchorType.Right,       new Vector2(1.0f, 0.5f) },
        { AnchorType.TopLeft,     new Vector2(0.0f, 1.0f) },
        { AnchorType.TopRight,    new Vector2(1.0f, 1.0f) },
        { AnchorType.BottomLeft,  new Vector2(0.0f, 0.0f) },
        { AnchorType.BottomRight, new Vector2(1.0f, 0.0f) },
    };

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<CutSceneDialogueBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            behaviour.manager = manager;

            if (weight > 0f && !behaviour.isActive)
            {
                behaviour.isActive = true;
                behaviour.exitStarted = false;
                behaviour.enterDone = false;
                behaviour.lastVisibleCount = 0;
                behaviour.prevVisibleCount = -1;

                var bubble = manager.GetDialogueBubble(behaviour.bubbleIndex);
                if (bubble == null) continue;

                SetupBubble(bubble, behaviour);
                transitions.RunAsync(bubble, manager, token => PlayBubbleEnter(bubble, behaviour, token)).Forget();
            }
            else if (weight > 0f && behaviour.isActive)
            {
                // ── 클립 진행 중 → 타이핑 + 리사이즈 ─────────────────
                var bubble = manager.GetDialogueBubble(behaviour.bubbleIndex);
                if (bubble == null) continue;

                UpdateTyping(bubble, behaviour, inputPlayable);

                // 대상 이미지 추적
                if (!string.IsNullOrEmpty(behaviour.targetImagePath))
                    FollowTargetImage(bubble, behaviour);
            }
            else if (weight <= 0f && behaviour.isActive)
            {
                // ── 클립 종료 → 말풍선 퇴장 ──────────────────────────
                behaviour.isActive = false;

                var bubble = manager.GetDialogueBubble(behaviour.bubbleIndex);
                if (bubble == null) continue;

                if (!behaviour.exitStarted)
                {
                    behaviour.exitStarted = true;
                    transitions.RunAsync(bubble, manager, token => PlayBubbleExit(bubble, behaviour, token)).Forget();
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  세팅
    // ══════════════════════════════════════════════════════════════════

    void SetupBubble(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b)
    {
        // 1. 오브젝트 활성화
        bubble.gameObject.SetActive(true);

        RectTransform rect = bubble.GetComponent<RectTransform>();

        // [핵심 수정] 레이아웃 계산이 무시되지 않도록 스케일을 정상(1) 상태로 둡니다.
        rect.localScale = Vector3.one;

        // 이름 처리
        if (bubble.nameLabel != null)
        {
            if (!string.IsNullOrEmpty(b.speakerName))
            {
                bubble.nameLabel.text = b.speakerName;
                bubble.nameLabel.gameObject.SetActive(true);
            }
            else
            {
                bubble.nameLabel.gameObject.SetActive(false);
            }
        }

        // 2. 스케일이 1인 정상 상태에서 텍스트 레이아웃을 완벽하게 계산합니다.
        bubble.PrepareForText(b.text);

        // 위치 설정
        PositionBubble(bubble, b);

        // 3. 계산이 모두 끝난 후, 등장 애니메이션(ScaleUp 등)을 위해 스케일을 0으로 접어둡니다.
        rect.localScale = Vector3.zero;
    }

    void PositionBubble(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b)
    {
        RectTransform rect = bubble.GetComponent<RectTransform>();
        RectTransform canvasRect = manager.CanvasRect;
        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;

        // 대상 이미지가 있으면 그 위치 기준
        if (!string.IsNullOrEmpty(b.targetImagePath))
        {
            Image targetImg = manager.GetActiveImage(b.targetImagePath);
            if (targetImg != null)
            {
                RectTransform targetRect = targetImg.GetComponent<RectTransform>();
                rect.anchorMin = targetRect.anchorMin;
                rect.anchorMax = targetRect.anchorMax;
                rect.pivot = new Vector2(0.5f, 0f); // 말풍선 하단이 캐릭터 위

                rect.anchoredPosition = targetRect.anchoredPosition
                    + new Vector2(w * b.offset.x, h * b.offset.y);
                return;
            }
        }

        // 고정 위치
        Vector2 anchorVec = AnchorMap.TryGetValue(b.anchor, out var v) ? v : new Vector2(0.5f, 0.5f);
        rect.anchorMin = anchorVec;
        rect.anchorMax = anchorVec;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(w * b.offset.x, h * b.offset.y);
    }

    void FollowTargetImage(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b)
    {
        Image targetImg = manager.GetActiveImage(b.targetImagePath);
        if (targetImg == null) return;

        RectTransform rect = bubble.GetComponent<RectTransform>();
        RectTransform targetRect = targetImg.GetComponent<RectTransform>();
        RectTransform canvasRect = manager.CanvasRect;

        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;
        rect.anchoredPosition = targetRect.anchoredPosition
            + new Vector2(w * b.offset.x, h * b.offset.y);
    }

    // ══════════════════════════════════════════════════════════════════
    //  타이핑 + 실시간 리사이즈
    // ══════════════════════════════════════════════════════════════════

    void UpdateTyping(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b,
                      ScriptPlayable<CutSceneDialogueBehaviour> playable)
    {
        float elapsed = (float)playable.GetTime();
        float typingStart = b.enterDuration + b.typingDelay;

        if (elapsed < typingStart)
        {
            bubble.textLabel.maxVisibleCharacters = 0;
            bubble.UpdateForVisible(0); // 초기 최소 크기 유지
            return;
        }

        float typingElapsed = elapsed - typingStart;

        // [수정] 원본 문자열의 Length 대신 파싱 완료된 TotalVisibleCharacters를 사용합니다.
        int totalChars = bubble.TotalVisibleCharacters;
        int visibleChars = Mathf.Min(Mathf.FloorToInt(typingElapsed * b.charsPerSecond), totalChars);

        // 글자 수가 변했을 때만 리사이즈 (매 프레임 호출 방지)
        if (visibleChars != b.prevVisibleCount)
        {
            b.prevVisibleCount = visibleChars;
            bubble.textLabel.maxVisibleCharacters = visibleChars;

            // [수정] 현재까지 보이는 글자 수로 말풍선 크기 및 마진 재계산
            bubble.UpdateForVisible(visibleChars);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  등장 / 퇴장
    // ══════════════════════════════════════════════════════════════════

    async UniTask PlayBubbleEnter(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b, CancellationToken token)
    {
        RectTransform rect = bubble.GetComponent<RectTransform>();
        rect.DOKill();

        switch (b.enterType)
        {
            case EEneterPreset.ScaleUp:
                rect.localScale = Vector3.zero;
                await rect.DOScale(b.sizeScale, b.enterDuration).SetEase(b.enterEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEneterPreset.FadeIn:
                var cg = GetOrAddCanvasGroup(bubble.gameObject);
                cg.alpha = 0f;
                rect.localScale = Vector3.one * b.sizeScale;
                await DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, b.enterDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEneterPreset.SlideUp:
                {
                    float h = manager.CanvasRect.rect.height;
                    Vector2 dest = rect.anchoredPosition;
                    rect.anchoredPosition = dest - new Vector2(0, h * 0.1f);
                    rect.localScale = Vector3.one * b.sizeScale;
                    await rect.DOAnchorPos(dest, b.enterDuration).SetEase(b.enterEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                    break;
                }
            case EEneterPreset.SlideDown:
                {
                    float h = manager.CanvasRect.rect.height;
                    Vector2 dest = rect.anchoredPosition;
                    rect.anchoredPosition = dest + new Vector2(0, h * 0.1f);
                    rect.localScale = Vector3.one * b.sizeScale;
                    await rect.DOAnchorPos(dest, b.enterDuration).SetEase(b.enterEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                    break;
                }

            case EEneterPreset.Cut:
                rect.localScale = Vector3.one * b.sizeScale;
                break;

            default:
                rect.localScale = Vector3.one * b.sizeScale;
                break;
        }

        b.enterDone = true;
    }

    async UniTask PlayBubbleExit(DynamicSpeechBubble bubble, CutSceneDialogueBehaviour b, CancellationToken token)
    {
        RectTransform rect = bubble.GetComponent<RectTransform>();
        rect.DOKill();

        switch (b.exitType)
        {
            case EExitPreset.ScaleDown:
                await rect.DOScale(0f, b.exitDuration).SetEase(b.exitEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EExitPreset.FadeOut:
                var cg = GetOrAddCanvasGroup(bubble.gameObject);
                await DOTween.To(() => cg.alpha, x => cg.alpha = x, 0f, b.exitDuration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                cg.alpha = 1f; // 복구
                break;

            case EExitPreset.Cut:
                break;

            default:
                rect.localScale = Vector3.zero;
                break;
        }

        // 정리
        bubble.gameObject.SetActive(false);
        bubble.textLabel.text = "";
        bubble.textLabel.maxVisibleCharacters = 99999;
        rect.localScale = Vector3.one;

        // [수정] 말풍선을 다시 최소 크기로 접어둡니다.
        bubble.ResetToMinSize();
    }

    // ══════════════════════════════════════════════════════════════════
    //  유틸
    // ══════════════════════════════════════════════════════════════════

    CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        transitions.CancelAll();
        if (manager != null)
            manager.HideAllDialogueBubbles();
    }
}