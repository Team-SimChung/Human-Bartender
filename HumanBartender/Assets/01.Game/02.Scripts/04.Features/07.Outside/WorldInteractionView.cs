using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using VContainer;

/// <summary>Canvas_Dialogue/InteractionLayer 전용 UI. 공용 대사창의 위치/타이핑 상태를 건드리지 않음.</summary>
[DefaultExecutionOrder(10000)]
public sealed class WorldInteractionView : MonoBehaviour, IDialoguePresenter, IDialogueInputPrompt, IDialogueInputFeedback,
    IDialogueChoicePrompt
{
    [SerializeField] private DynamicSpeechBubble bubblePrefab;
    [SerializeField] private WorldInteractionImageView imageViewerPrefab;
    [SerializeField] private WorldInteractionHintView interactionHintPrefab;
    [SerializeField] private WorldInteractionChoiceView choicesPrefab;
    [SerializeField] private RectTransform ambientLayer;
    [Tooltip("선택창은 웹처럼 루나가 바라보는 반대쪽(등 뒤)에 뜹니다. 루나와 선택창 가장자리 사이의 가로 간격입니다.")]
    [SerializeField, Min(0)] private float choiceSideGap = 14;
    [Tooltip("선택창 첫 항목의 세로 중심. 루나 위치에서 이만큼(월드 단위) 위입니다. 웹 world-choices.js의 head(루나 y + 0.16)와 같습니다. " +
        "줄 수·프리팹 Scale과 상관없이 첫 항목이 이 높이에 오고, 소지금 줄이 있으면 그만큼 아래로 내려갑니다(웹과 같음).")]
    [SerializeField] private float choiceHeadHeight = .16f;
    [SerializeField] private NewTextTagDataSO textTags;
    [Header("Typing")]
    [Tooltip("거리·집 대사가 나오는 속도(초당 글자 수). 웹 기본 배속 55. 마침표 멈춤은 DialoguePresentationSettings가 정한다.")]
    [SerializeField, Min(1)] private float charactersPerSecond = 55;
    [Tooltip("테라스 대사가 나오는 속도(초당 글자 수). 웹 기본 배속 60.")]
    [SerializeField, Min(1)] private float terraceCharactersPerSecond = 60;
    [Inject] private ICameraImpulse cameraImpulse;
    [Inject] private ISoundEffectPlayer sound;
    private Vector2 authoredMaximumSize;
    private CanvasGroup bubbleVisibility;
    private CanvasGroup choicesVisibility;
    private RectTransform root;
    private Canvas canvas;
    private DynamicSpeechBubble bubble;
    private DialogueInputPrompt prompt;
    private WorldInteractionHintView hint;
    private WorldInteractionImageView imageViewer;
    private WorldInteractionChoiceView choices;
    private WorldInteractionTarget target;
    private Transform speaker;
    private Transform player;
    private PlayerMovement2D playerMovement;
    private WorldDialogueSpeakers sceneSpeakers;
    private WorldSpeechBubblePlacement placement;
    private Vector2 speakerOffset;
    private bool explicitSpeaker;
    private bool imageMode;
    private bool built;
    private bool hasDialogue;
    private float? choiceConfirmGuard;
    private static readonly List<RaycastResult> pointerHits = new();
    private Action cancelSession;
    private Func<int> readMoney;
    internal DynamicSpeechBubble Bubble => bubble;
    public bool IsImageVisible => imageViewer != null && imageViewer.gameObject.activeSelf;
    internal WorldInteractionHintView Hint => hint;
    internal WorldInteractionImageView ImageViewer => imageViewer;
    internal WorldInteractionChoiceView Choices => choices;
    public bool IsChoosing => choices != null && choices.IsOpen;

    internal WorldAmbientDialogueView CreateAmbientView()
    {
        if (!built || ambientLayer == null) throw new InvalidOperationException("InteractionLayer의 Ambient Layer를 연결하세요.");
        var instance = Instantiate(bubblePrefab, ambientLayer);
        try
        {
            instance.name = "AmbientSpeechBubble";
            var presenter = instance.gameObject.AddComponent<WorldAmbientDialogueView>();
            presenter.Initialize(instance, ambientLayer, textTags, 1f / charactersPerSecond, sound);
            return presenter;
        }
        catch { Destroy(instance.gameObject); throw; }
    }

    public void Initialize(Action onAdvance, Action onClose, Func<int> moneyReader = null, Action onDisabled = null)
    {
        if (built) return;
        // 화면 비활성화는 사용자 취소 선택이 아니라 세션 정리다.
        cancelSession = onDisabled ?? onClose;
        readMoney = moneyReader;
        if (bubblePrefab == null) throw new InvalidOperationException("InteractionLayer의 Bubble Prefab이 없습니다.");
        if (imageViewerPrefab == null || !imageViewerPrefab.IsConfigured)
            throw new InvalidOperationException("InteractionLayer의 Image Viewer Prefab과 Inspection Image를 연결하세요.");
        if (interactionHintPrefab == null || !interactionHintPrefab.IsConfigured)
            throw new InvalidOperationException("InteractionLayer의 Interaction Hint Prefab과 Button/Label/Key Icon을 연결하세요.");
        if (choicesPrefab == null)
            throw new InvalidOperationException("InteractionLayer의 Choices Prefab을 연결하세요.");
        if (!choicesPrefab.IsConfigured)
            throw new InvalidOperationException(choicesPrefab.ConfigurationError);
        root = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            throw new InvalidOperationException("InteractionLayer는 Screen Space Overlay Canvas 아래에 배치하세요.");
        imageViewer = Instantiate(imageViewerPrefab, root);
        imageViewer.name = "ImageViewer";

        bubble = Instantiate(bubblePrefab, root);
        bubble.name = "SpeechBubble";
        authoredMaximumSize = bubble.maxSize;
        if (!bubble.centerArrow.TryGetComponent<SpeechBubbleTail>(out _))
            throw new InvalidOperationException("[WorldInteraction] Bubble Prefab의 Center Tail 구성이 필요합니다.");
        bubbleVisibility = bubble.GetComponent<CanvasGroup>();
        if (bubbleVisibility == null) bubbleVisibility = bubble.gameObject.AddComponent<CanvasGroup>();
        // 말풍선이 클릭을 가로채지 않게 함. 외형 설정은 프리팹 값을 사용함.
        bubbleVisibility.blocksRaycasts = false;
        bubbleVisibility.interactable = false;
        prompt = bubble.GetComponent<DialogueInputPrompt>();
        if (cameraImpulse != null)
        {
            bubble.TextPlayer.ShakeSpanRevealed += cameraImpulse.PlayVerticalPulse;
            bubble.TextPlayer.MotionStopped += cameraImpulse.StopImpulse;
        }

        hint = Instantiate(interactionHintPrefab, root);
        hint.name = "InteractionHint";
        hint.Initialize(onAdvance, onClose);
        choices = Instantiate(choicesPrefab, root);
        choices.name = "InteractionChoices";
        choicesVisibility = choices.GetComponent<CanvasGroup>();
        built = true;
        EndScene();
    }

    public void ShowHint(WorldInteractionTarget value)
    {
        if (!built || hint == null) return;
        if (value != null) hint.ShowInteraction(value.Label);
        else hint.Hide();
    }

    private Transform speakerOverride;
    private bool automatic;
    private bool terraceDialogue;

    /// <param name="terrace">테라스 대화. 대사 속도를 테라스 기준(terraceCharactersPerSecond)으로 바꾼다.</param>
    /// <param name="choiceConfirmGuard">이 장면 선택지의 확정 보호 시간(초). 비우면 선택지 프리팹 값을 쓴다.</param>
    public void Begin(WorldInteractionTarget value, Transform playerTransform, Transform anchor = null, bool autoAdvance = false,
        WorldDialogueSpeakers speakers = null, bool terrace = false, float? choiceConfirmGuard = null)
    {
        if (!built) throw new InvalidOperationException("상호작용 UI가 초기화되지 않았습니다.");
        EndScene();
        target = value;
        player = playerTransform;
        playerMovement = playerTransform != null ? playerTransform.GetComponent<PlayerMovement2D>() : null;
        speakerOverride = anchor;
        sceneSpeakers = speakers;
        automatic = autoAdvance;
        terraceDialogue = terrace;
        this.choiceConfirmGuard = choiceConfirmGuard;
        bubble.TextPlayer.ConfigureAudio(sound, automatic);
        imageMode = value.Output == WorldInteractionOutput.Image;
        if (imageMode && value.InspectionImage == null) throw new InvalidOperationException("확대할 이미지가 없습니다.");
        bubbleVisibility.alpha = 1;
        if (imageMode) imageViewer.Show(value.InspectionImage);
        SetWaitingForInput(false);
    }

    public async UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
    {
        if (target == null) throw new InvalidOperationException("상호작용 대상이 사라졌습니다.");
        OpenBubble(actor, requireSpeaker: true);
        float typingDelay = 1f / (terraceDialogue ? terraceCharactersPerSecond : charactersPerSecond);
        await bubble.TextPlayer.PlayAsync(new TypingData(text, "", Vector3.zero, Color.white, actor == "luna"), textTags, typingDelay, token,
            periodRhythm: true);
        Follow();
    }

    /// <summary>선택지 행의 질문을 타이핑 없이 표시한다. 선택하는 동안 같은 말풍선이 남는다.</summary>
    public void ShowChoicePrompt(string actor, string text)
    {
        if (target == null) throw new InvalidOperationException("상호작용 대상이 사라졌습니다.");
        if (string.IsNullOrWhiteSpace(text)) return;
        // 화자 연결이 없는 선택지 행도 선택은 진행되어야 하므로 상호작용 대상 위에 표시한다.
        OpenBubble(actor, requireSpeaker: false);
        bubble.nameLabel.text = "";
        bubble.TextPlayer.ShowInstant(text, textTags);
        Follow();
    }

    private void OpenBubble(string actor, bool requireSpeaker)
    {
        choices.Hide();
        hasDialogue = true;
        var bindings = sceneSpeakers != null ? sceneSpeakers : target.GetComponent<WorldDialogueSpeakers>();
        placement = WorldSpeechBubblePlacement.Above;
        speakerOffset = target.BubbleOffset;
        explicitSpeaker = bindings != null && bindings.TryResolve(actor, out speaker, out placement, out speakerOffset);
        if (!explicitSpeaker)
        {
            speaker = actor == "luna" ? player : speakerOverride != null ? speakerOverride :
                requireSpeaker ? target.ResolveSpeaker(actor, player) : ResolveSpeakerOrTarget(actor);
            speakerOffset = target.BubbleOffset;
        }
        bubble.gameObject.SetActive(true);
        bubble.centerArrow.SetActive(!imageMode);
        bubble.leftArrow.SetActive(false);
        bubble.rightArrow.SetActive(false);
        float scale = Mathf.Max(.001f, Mathf.Abs(bubble.bubble.localScale.x));
        bubble.maxSize = new Vector2(Mathf.Min(authoredMaximumSize.x, (root.rect.width - 20) / scale), authoredMaximumSize.y);
        Follow();
    }

    private Transform ResolveSpeakerOrTarget(string actor)
    {
        var own = target.GetComponent<WorldDialogueSpeakers>();
        if (own != null && own.TryResolve(actor, out var anchor, out _, out _)) return anchor;
        if (own != null) Debug.LogWarning($"[WorldInteraction] 선택지 질문의 화자 ID가 연결되지 않아 대상 위에 표시합니다: {actor}", target);
        return target.SpeechAnchor;
    }

    public void SkipTyping()
    {
        if (bubble == null || bubble.TextPlayer.CurrentSessionId == 0 || bubble.TextPlayer.IsHolding) return;
        PlayAdvanceFeedback();
        bubble.TextPlayer.Skip();
    }
    public void PlayAdvanceFeedback()
    {
        if (automatic) return;
        string key = DialoguePresentationSettings.Shared?.AdvanceSoundKey;
        if (!string.IsNullOrWhiteSpace(key)) sound?.PlaySE(key);
    }
    public void SetWaitingForInput(bool waiting)
    {
        if (prompt != null) prompt.SetWaiting(waiting);
        if (target == null || hint == null) return;
        // 대화 중에는 하단 '펼치기·다음' 안내를 띄우지 않는다(웹과 같음). 진행은 E와 말풍선 클릭으로 한다.
        // 선택지의 E 표시는 커서가 담당하며 방송은 자동으로 진행하므로, 조사 이미지의 나가기 안내만 남긴다.
        if (imageMode && !IsChoosing && !automatic) hint.ShowExit();
        else hint.Hide();
    }

    /// <summary>화면 좌표가 지금 진행을 기다리는 대사 말풍선 위인지. 위쪽 캔버스의 UI가 가리고 있으면 제외한다.</summary>
    internal bool IsDialoguePointer(Vector2 screenPoint)
    {
        if (!built || target == null || imageMode || IsChoosing || !bubble.gameObject.activeInHierarchy ||
            bubbleVisibility.alpha <= 0) return false;
        if (!RectTransformUtility.RectangleContainsScreenPoint(bubble.bubble, screenPoint, null)) return false;
        var events = EventSystem.current;
        if (events == null) return true;
        pointerHits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = screenPoint }, pointerHits);
        var own = canvas.rootCanvas;
        foreach (var hit in pointerHits)
        {
            var other = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Canvas>() : null;
            if (other != null && other.rootCanvas != own && other.rootCanvas.sortingOrder > own.sortingOrder) return false;
        }
        return true;
    }
    public EActivationMode GetPlayMode() => automatic ? EActivationMode.Proximity : EActivationMode.Interact;
    public void HideDialogue()
    {
        SetWaitingForInput(false);
        if (bubble != null) { bubble.TextPlayer.Stop(); bubble.gameObject.SetActive(false); }
    }
    public void EndScene()
    {
        HideDialogue();
        target = null;
        speaker = null;
        player = null;
        playerMovement = null;
        speakerOverride = null;
        sceneSpeakers = null;
        explicitSpeaker = false;
        placement = WorldSpeechBubblePlacement.Above;
        automatic = false;
        terraceDialogue = false;
        hasDialogue = false;
        choiceConfirmGuard = null;
        if (!built) return;
        if (choices != null) choices.Hide();
        if (imageViewer != null) imageViewer.Hide();
        ShowHint(null);
    }
    private void OnDisable()
    {
        cancelSession?.Invoke();
        EndScene();
    }
    public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected)
    {
        ShowChoices(options, onSelected, readMoney?.Invoke());
    }

    /// <summary>
    /// 사물 메뉴(TV·소파·엘리베이터). 대화 선택지와 위치·크기 규칙이 같고, 웹처럼 소지금 줄은 없다.
    /// 메시지(저장 결과·확인 문구)는 타이핑 없이 루나 위에 표시한다.
    /// </summary>
    public async UniTask<NewStreetOptionData> ChooseMenuAsync(NewStreetOptionData[] options, string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (!string.IsNullOrEmpty(message)) ShowChoicePrompt("luna", message);
            var completion = new UniTaskCompletionSource<NewStreetOptionData>();
            ShowChoices(options, option => completion.TrySetResult(option), null);
            return await completion.Task.AttachExternalCancellation(token);
        }
        finally
        {
            if (choices != null) choices.Hide();
            HideDialogue();
        }
    }

    private void ShowChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected, int? money)
    {
        if (target == null) throw new InvalidOperationException("상호작용 대상이 사라졌습니다.");
        // 선택 중에는 직전 질문을 다시 표시함.
        bubble.gameObject.SetActive(hasDialogue);
        if (prompt != null) prompt.SetWaiting(false);
        // 대화 선택지와 사물 메뉴가 같은 프리팹 크기·폭·위치 규칙을 쓴다(Scale은 프리팹 값 그대로). 소지금 줄만 다르다.
        choices.Show(options, onSelected, GameStateManager.Instance.Language, money, choiceConfirmGuard);
        hint.Hide();
        Follow();
    }

    public void ConfirmChoice() => choices?.Confirm();
    public void MoveChoice(int direction) => choices?.MoveSelection(direction);
    public void ScrollChoice(int direction) => choices?.ScrollSelection(direction);

    private void LateUpdate() => Follow();
    private void Follow()
    {
        if (!built || target == null) return;
        var camera = Camera.main;
        var pixelPerfect = camera != null ? camera.GetComponent<PixelPerfectCamera>() : null;
        if (imageMode)
        {
            PlaceBubbleAt(new Vector2(0, root.rect.yMin + 33));
        }
        else if (speaker != null && camera != null && bubble.gameObject.activeSelf)
        {
            bool below = placement == WorldSpeechBubblePlacement.Below;
            Vector3 position = speaker.position + (!explicitSpeaker && speaker == player ? Vector3.up * .4f : Vector3.zero);
            Vector3 screen = ProjectWorldPoint(camera, pixelPerfect, position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var anchor);
            Vector2 bottomCenter = anchor + speakerOffset;
            if (below) bottomCenter.y -= ScaledSize(bubble.bubble).y;
            PlaceBubbleAt(bottomCenter);
            // 타이핑 작업을 끄지 않고 카메라 뒤에 있는 말풍선만 숨김.
            bubbleVisibility.alpha = screen.z > 0 ? 1 : 0;
            // 화면 가장자리에서도 꼬리가 화자의 위치를 가리키도록 보정함.
            PointBubbleTail(bubble, root, anchor, below);
        }
        if (IsChoosing && player != null && camera != null)
        {
            Vector3 screen = ProjectWorldPoint(camera, pixelPerfect, player.position + Vector3.up * choiceHeadHeight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var headPoint);
            bool right = ChoicesOnRight(playerMovement != null ? playerMovement.IsFacingRight : (bool?)null,
                screen.x, ProjectWorldPoint(camera, pixelPerfect, target.SpeechAnchor.position).x);
            float side = right ? 1 : -1;
            choices.SetSide(right);
            var size = ScaledSize(choices.Rect);
            // 웹(world-choices.js: top = head − 버튼 높이의 절반)처럼 위쪽 끝을 첫 항목 높이의 절반만큼 head 위에 둔다.
            float top = ChoiceTop(headPoint.y, choices.FirstRowHeight * Mathf.Abs(choices.Rect.localScale.y));
            if (hasDialogue && bubble.gameObject.activeSelf)
            {
                float bubbleBottom = root.InverseTransformPoint(bubble.bubble.TransformPoint(bubble.bubble.rect.min)).y;
                top = Mathf.Min(top, bubbleBottom - 8);
            }
            var position = new Vector2(headPoint.x + side * (size.x * .5f + choiceSideGap),
                ChoicePivotY(top, size.y, choices.Rect.pivot.y));
            choices.Rect.anchoredPosition = ClampToCanvas(root.rect, size, choices.Rect.pivot, position, 8);
            choicesVisibility.alpha = screen.z > 0 ? 1 : 0;
            choicesVisibility.blocksRaycasts = screen.z > 0;
        }
    }

    /// <summary>
    /// 선택창을 루나 오른쪽에 띄울지. 웹(world-choices.js)과 같이 루나가 바라보는 반대쪽(등 뒤)에 띄운다.
    /// 바라보는 방향을 모르면(이동 컴포넌트 없음) 대상을 바라본다고 본다.
    /// </summary>
    internal static bool ChoicesOnRight(bool? facingRight, float playerX, float targetX) =>
        !(facingRight ?? targetX > playerX);

    /// <summary>첫 항목의 중심을 head에 둘 때 선택창 위쪽 끝. firstRowHeight는 화면(InteractionLayer) 단위 높이다.</summary>
    internal static float ChoiceTop(float head, float firstRowHeight) => head + firstRowHeight * .5f;

    /// <summary>위쪽 끝을 top에 맞출 때 선택창 피벗의 세로 위치.</summary>
    internal static float ChoicePivotY(float top, float height, float pivotY) => top - height * (1 - pivotY);

    /// <summary>LateUpdate에서 렌더링에 쓰일 현재 프레임의 화소 격자로 월드 기준점을 투영한다.</summary>
    internal static Vector3 ProjectWorldPoint(Camera camera, PixelPerfectCamera pixelPerfect, Vector3 worldPosition)
    {
        if (!camera.orthographic || pixelPerfect == null || !pixelPerfect.isActiveAndEnabled)
            return camera.WorldToScreenPoint(worldPosition);

        // URP의 PixelSnap은 렌더 직전에 worldToCameraMatrix를 갱신한다.
        // 이전 행렬을 읽는 대신 현재 Transform과 같은 반올림 규칙을 쓴다.
        Vector3 cameraPosition = camera.transform.position;
        Vector3 snappedCamera = pixelPerfect.RoundToPixel(cameraPosition);
        snappedCamera.z = cameraPosition.z * 2 - snappedCamera.z;
        Vector3 relative = camera.transform.InverseTransformDirection(pixelPerfect.RoundToPixel(worldPosition) - snappedCamera);
        Vector4 clip = camera.projectionMatrix * new Vector4(relative.x, relative.y, -relative.z, 1);
        Rect viewport = camera.pixelRect;
        return new Vector3(
            viewport.xMin + (clip.x / clip.w + 1) * .5f * viewport.width,
            viewport.yMin + (clip.y / clip.w + 1) * .5f * viewport.height,
            relative.z);
    }

    private void PlaceBubbleAt(Vector2 bottomCenter)
    {
        var rect = bubble.bubble;
        Vector2 size = ScaledSize(rect);
        Vector2 pivotPosition = bottomCenter + new Vector2((rect.pivot.x - .5f) * size.x, rect.pivot.y * size.y);
        Vector2 position = ClampToCanvas(root.rect, size, rect.pivot, pivotPosition, 8);
        rect.position = root.TransformPoint(position);
    }

    private static Vector2 ScaledSize(RectTransform rect) => Vector2.Scale(rect.rect.size,
        new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));

    internal static void PointBubbleTail(DynamicSpeechBubble bubble, RectTransform layer, Vector2 speaker, bool below)
    {
        Rect bounds = layer.rect;
        GameObject selected = speaker.x < bounds.xMin ? bubble.leftArrow :
            speaker.x > bounds.xMax ? bubble.rightArrow : bubble.centerArrow;
        if (selected == null) selected = bubble.centerArrow;

        bubble.centerArrow.SetActive(bubble.centerArrow == selected);
        if (bubble.leftArrow != null) bubble.leftArrow.SetActive(bubble.leftArrow == selected);
        if (bubble.rightArrow != null) bubble.rightArrow.SetActive(bubble.rightArrow == selected);
        if (selected.TryGetComponent<SpeechBubbleTail>(out var tail))
            tail.SetTarget(layer.TransformPoint(speaker), below);
    }

    public static Vector2 ClampToCanvas(Rect bounds, Vector2 size, Vector2 pivot, Vector2 position, float margin)
    {
        float minX = bounds.xMin + margin + size.x * pivot.x;
        float maxX = bounds.xMax - margin - size.x * (1 - pivot.x);
        float minY = bounds.yMin + margin + size.y * pivot.y;
        float maxY = bounds.yMax - margin - size.y * (1 - pivot.y);
        return new Vector2(minX > maxX ? bounds.center.x : Mathf.Clamp(position.x, minX, maxX),
            minY > maxY ? bounds.center.y : Mathf.Clamp(position.y, minY, maxY));
    }
    private void OnDestroy()
    {
        if (cameraImpulse == null) return;
        if (bubble != null)
        {
            bubble.TextPlayer.ShakeSpanRevealed -= cameraImpulse.PlayVerticalPulse;
            bubble.TextPlayer.MotionStopped -= cameraImpulse.StopImpulse;
        }
        cameraImpulse.StopImpulse();
    }

}
