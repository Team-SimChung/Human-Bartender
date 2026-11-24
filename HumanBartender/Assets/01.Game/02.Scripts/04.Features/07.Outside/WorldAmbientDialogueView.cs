using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>주변 대화 한 세션의 말풍선과 자동 진행 시계를 관리함. 입력과 플레이어 이동은 다루지 않음.</summary>
[DefaultExecutionOrder(10001)]
public sealed class WorldAmbientDialogueView : MonoBehaviour, IDialoguePresenter, IDialogueAutomaticPlayback
{
    private DynamicSpeechBubble bubble;
    private RectTransform layer;
    private CanvasGroup visibility;
    private NewTextTagDataSO tags;
    private WorldInteractionTarget target;
    private Transform player;
    private Transform speaker;
    private WorldAmbientDialogueDistanceGate distanceGate;
    private bool inListeningRange;
    private Func<bool> isPaused;
    private float typingDelay;
    private float readTime;
    private float minimumReadTime;
    private float maximumReadTime;
    private float charactersPerSecond;
    private Vector2 authoredMaximumSize;
    private bool showing;
    [SerializeField, Min(0f)] private float backgroundAudioRange = 5f;
    internal Transform Speaker => speaker;
    internal DynamicSpeechBubble Bubble => bubble;
    internal bool IsVisible => visibility != null && visibility.alpha > 0;
    internal bool IsPlaybackPaused => isPaused?.Invoke() == true || IsDistancePaused;
    internal bool IsDistancePaused
    {
        get
        {
            if (distanceGate == null || !distanceGate.isActiveAndEnabled || speaker == null || player == null)
            {
                inListeningRange = false;
                return false;
            }
            inListeningRange = distanceGate.Contains(player.position, speaker.position, inListeningRange);
            return !inListeningRange;
        }
    }

    internal void Initialize(DynamicSpeechBubble value, RectTransform parent, NewTextTagDataSO textTags, float delay,
        ISoundEffectPlayer sound = null)
    {
        bubble = value;
        if (!bubble.centerArrow.TryGetComponent<SpeechBubbleTail>(out _))
            throw new InvalidOperationException("[WorldAmbientDialogue] Bubble Prefab의 Center Tail 구성이 필요합니다.");
        bubble.TextPlayer.ConfigureAudio(sound, true, CanHear);
        layer = parent;
        tags = textTags;
        typingDelay = delay;
        authoredMaximumSize = bubble.maxSize;
        visibility = GetComponent<CanvasGroup>();
        if (visibility == null) visibility = gameObject.AddComponent<CanvasGroup>();
        visibility.interactable = visibility.blocksRaycasts = false;
        visibility.alpha = 0;
        var prompt = bubble.GetComponent<DialogueInputPrompt>();
        if (prompt != null) prompt.SetWaiting(false);
    }

    internal void Begin(WorldInteractionTarget source, Transform playerTransform, Func<bool> paused,
        float minimum, float maximum, float readingSpeed)
    {
        target = source;
        distanceGate = source.GetComponent<WorldAmbientDialogueDistanceGate>();
        inListeningRange = false;
        player = playerTransform;
        isPaused = paused;
        minimumReadTime = minimum;
        maximumReadTime = maximum;
        charactersPerSecond = readingSpeed;
    }

    public async UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
    {
        speaker = target.ResolveSpeaker(actor, player);
        if (speaker == null) throw new InvalidOperationException("주변 대화의 화자 기준점이 없습니다: " + actor);
        showing = true;
        bubble.centerArrow.SetActive(true);
        bubble.leftArrow.SetActive(false);
        bubble.rightArrow.SetActive(false);
        float scale = Mathf.Max(.001f, Mathf.Abs(bubble.bubble.localScale.x));
        bubble.maxSize = new Vector2(Mathf.Min(authoredMaximumSize.x, (layer.rect.width - 20) / scale), authoredMaximumSize.y);
        Follow();
        typingOwed = 0f;
        await bubble.TextPlayer.PlayAsync(new TypingData(text, "", Vector3.zero, Color.white, actor == "luna"),
            tags, typingDelay, token, wait: TypingWaitAsync, periodRhythm: true);
        readTime = Mathf.Clamp(bubble.textLabel.textInfo.characterCount / Mathf.Max(1, charactersPerSecond),
            minimumReadTime, Mathf.Max(minimumReadTime, maximumReadTime));
    }

    public UniTask WaitUntilReadyAsync(CancellationToken token) => WaitAsync(0, token);
    public UniTask WaitAfterLineAsync(CancellationToken token) => WaitAsync(readTime, token);

    private bool CanHear() => showing && !IsPlaybackPaused && speaker != null && player != null &&
        Vector2.Distance(speaker.position, player.position) <= backgroundAudioRange;

    // 타이핑은 남은 시간을 다음 글자로 넘긴다(QA-51). 한 프레임보다 짧은 간격이 두 프레임씩 걸려 느려지지 않게 한다.
    private float typingOwed;

    private async UniTask TypingWaitAsync(float seconds, CancellationToken token)
    {
        if (seconds > 0f) typingOwed += seconds;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!IsPlaybackPaused && typingOwed <= 0) return;
            await UniTask.NextFrame(token);
            if (!IsPlaybackPaused) typingOwed -= Time.deltaTime;
        }
    }

    private async UniTask WaitAsync(float seconds, CancellationToken token)
    {
        float remaining = seconds;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (!IsPlaybackPaused && remaining <= 0) return;
            await UniTask.NextFrame(token);
            if (!IsPlaybackPaused) remaining -= Time.deltaTime;
        }
    }

    // 자동 대화는 E키와 클릭으로 펼치거나 넘기지 않음.
    public void SkipTyping() { }
    public EActivationMode GetPlayMode() => EActivationMode.Proximity;
    public void HideDialogue() { showing = false; if (visibility != null) visibility.alpha = 0; }
    public void EndScene() { HideDialogue(); if (bubble != null) bubble.TextPlayer.Stop(); }
    public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected)
        => throw new InvalidOperationException("주변 자동 대화에는 선택지를 사용할 수 없습니다.");

    private void LateUpdate() => Follow();
    private void Follow()
    {
        if (visibility == null) return;
        var camera = Camera.main;
        if (!showing || speaker == null || camera == null || IsPlaybackPaused)
        {
            visibility.alpha = 0;
            return;
        }
        var screen = WorldInteractionView.ProjectWorldPoint(camera, camera.GetComponent<PixelPerfectCamera>(), speaker.position);
        Rect viewport = camera.pixelRect;
        bool onScreen = screen.z > 0 && screen.x >= viewport.xMin && screen.x <= viewport.xMax &&
            screen.y >= viewport.yMin && screen.y <= viewport.yMax;
        visibility.alpha = onScreen ? 1 : 0;
        if (!onScreen) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out var anchor);
        var rect = bubble.bubble;
        Vector2 size = Vector2.Scale(rect.rect.size, new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));
        Vector2 position = anchor + target.BubbleOffset + new Vector2((rect.pivot.x - .5f) * size.x, rect.pivot.y * size.y);
        rect.position = layer.TransformPoint(WorldInteractionView.ClampToCanvas(layer.rect, size, rect.pivot, position, 8));
        WorldInteractionView.PointBubbleTail(bubble, layer, anchor, false);
    }
}
