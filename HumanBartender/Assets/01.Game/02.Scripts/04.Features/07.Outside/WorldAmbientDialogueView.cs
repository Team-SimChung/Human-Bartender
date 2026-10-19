using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
    private Func<bool> isPaused;
    private float typingDelay;
    private float readTime;
    private float minimumReadTime;
    private float maximumReadTime;
    private float charactersPerSecond;
    private Vector2 authoredMaximumSize;
    private bool showing;
    internal Transform Speaker => speaker;
    internal DynamicSpeechBubble Bubble => bubble;
    internal bool IsVisible => visibility != null && visibility.alpha > 0;

    internal void Initialize(DynamicSpeechBubble value, RectTransform parent, NewTextTagDataSO textTags, float delay)
    {
        bubble = value;
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
        await bubble.TextPlayer.PlayAsync(new TypingData(text, "", Vector3.zero, Color.white, actor == "luna"),
            tags, typingDelay, token, wait: WaitAsync);
        readTime = Mathf.Clamp(bubble.textLabel.textInfo.characterCount / Mathf.Max(1, charactersPerSecond),
            minimumReadTime, Mathf.Max(minimumReadTime, maximumReadTime));
    }

    public UniTask WaitUntilReadyAsync(CancellationToken token) => WaitAsync(0, token);
    public UniTask WaitAfterLineAsync(CancellationToken token) => WaitAsync(readTime, token);

    private async UniTask WaitAsync(float seconds, CancellationToken token)
    {
        float remaining = seconds;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (isPaused?.Invoke() != true && remaining <= 0) return;
            await UniTask.NextFrame(token);
            if (isPaused?.Invoke() != true) remaining -= Time.deltaTime;
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
        if (!showing || speaker == null || camera == null || isPaused?.Invoke() == true)
        {
            visibility.alpha = 0;
            return;
        }
        Vector3 world = speaker.position;
        Vector3 viewport = camera.WorldToViewportPoint(world);
        bool onScreen = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
        visibility.alpha = onScreen ? 1 : 0;
        if (!onScreen) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, camera.WorldToScreenPoint(world), null, out var anchor);
        var rect = bubble.bubble;
        Vector2 size = Vector2.Scale(rect.rect.size, new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));
        Vector2 position = anchor + target.BubbleOffset + new Vector2((rect.pivot.x - .5f) * size.x, rect.pivot.y * size.y);
        rect.position = layer.TransformPoint(WorldInteractionView.ClampToCanvas(layer.rect, size, rect.pivot, position, 8));
        var tail = (RectTransform)bubble.centerArrow.transform;
        Vector3 tailPosition = tail.localPosition;
        tailPosition.x = Mathf.Clamp(rect.InverseTransformPoint(layer.TransformPoint(anchor)).x, rect.rect.xMin + 12, rect.rect.xMax - 12);
        tail.localPosition = tailPosition;
    }
}
