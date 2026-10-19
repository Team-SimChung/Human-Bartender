using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>Canvas_Dialogue/InteractionLayer 전용 UI. 공용 대사창의 위치/타이핑 상태를 건드리지 않음.</summary>
[DefaultExecutionOrder(10000)]
public sealed class WorldInteractionView : MonoBehaviour, IDialoguePresenter, IDialogueInputPrompt, IDialogueInputFeedback
{
    [SerializeField] private DynamicSpeechBubble bubblePrefab;
    [SerializeField] private WorldInteractionImageView imageViewerPrefab;
    [SerializeField] private WorldInteractionHintView interactionHintPrefab;
    [SerializeField] private WorldInteractionChoiceView choicesPrefab;
    [SerializeField] private RectTransform ambientLayer;
    [SerializeField, Min(0)] private float choiceSideGap = 14;
    [Tooltip("소파 같은 동작 메뉴에만 적용하는 선택창 배율. 일반 대화 선택창에는 적용하지 않음.")]
    [SerializeField, Range(.1f, 1)] private float menuScale = .7f;
    [SerializeField] private NewTextTagDataSO textTags;
    [SerializeField, Min(0)] private float typingDelay = .035f;
    [Inject] private ICameraImpulse cameraImpulse;
    [Inject] private ISoundEffectPlayer sound;
    private Vector2 authoredMaximumSize;
    private Vector3 authoredChoiceScale;
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
    private bool imageMode;
    private bool built;
    private bool hasDialogue;
    private bool menuMode;
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
            presenter.Initialize(instance, ambientLayer, textTags, typingDelay, sound);
            return presenter;
        }
        catch { Destroy(instance.gameObject); throw; }
    }

    public void Initialize(Action onAdvance, Action onClose, Func<int> moneyReader = null)
    {
        if (built) return;
        cancelSession = onClose;
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
        authoredChoiceScale = choices.Rect.localScale;
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

    public void Begin(WorldInteractionTarget value, Transform playerTransform, Transform anchor = null, bool autoAdvance = false)
    {
        if (!built) throw new InvalidOperationException("상호작용 UI가 초기화되지 않았습니다.");
        EndScene();
        target = value;
        player = playerTransform;
        speakerOverride = anchor;
        automatic = autoAdvance;
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
        choices.Hide();
        hasDialogue = true;
        speaker = actor == "luna" ? player : speakerOverride != null ? speakerOverride : target.ResolveSpeaker(actor, player);
        bubble.gameObject.SetActive(true);
        bubble.centerArrow.SetActive(!imageMode);
        bubble.leftArrow.SetActive(false);
        bubble.rightArrow.SetActive(false);
        float scale = Mathf.Max(.001f, Mathf.Abs(bubble.bubble.localScale.x));
        bubble.maxSize = new Vector2(Mathf.Min(authoredMaximumSize.x, (root.rect.width - 20) / scale), authoredMaximumSize.y);
        Follow();
        await bubble.TextPlayer.PlayAsync(new TypingData(text, "", Vector3.zero, Color.white, actor == "luna"), textTags, typingDelay, token);
        Follow();
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
        if (IsChoosing || automatic) hint.Hide(); // 선택지의 E 표시는 커서가 담당하며 방송은 자동으로 진행함.
        else if (imageMode) hint.ShowExit();
        else hint.ShowDialogue(waiting);
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
        speakerOverride = null;
        automatic = false;
        hasDialogue = false;
        menuMode = false;
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

    public async UniTask<NewStreetOptionData> ChooseMenuAsync(NewStreetOptionData[] options, string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        menuMode = true;
        try
        {
            if (!string.IsNullOrEmpty(message))
            {
                // 저장 결과와 확인 문구는 타이핑 대기 없이 표시함.
                var typing = ShowDialogueAsync("luna", message, null, token);
                bubble.TextPlayer.Skip();
                await typing;
            }
            var completion = new UniTaskCompletionSource<NewStreetOptionData>();
            ShowChoices(options, option => completion.TrySetResult(option), null);
            return await completion.Task.AttachExternalCancellation(token);
        }
        finally
        {
            if (choices != null) choices.Hide();
            HideDialogue();
            menuMode = false;
        }
    }

    private void ShowChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected, int? money)
    {
        if (target == null) throw new InvalidOperationException("상호작용 대상이 사라졌습니다.");
        // 선택 중에는 직전 질문을 다시 표시함.
        bubble.gameObject.SetActive(hasDialogue);
        if (prompt != null) prompt.SetWaiting(false);
        choices.Rect.localScale = authoredChoiceScale * (menuMode ? menuScale : 1);
        choices.Show(options, onSelected, GameStateManager.Instance.Language, money, fitWidthToText: menuMode);
        hint.Hide();
        Follow();
    }

    public void ConfirmChoice() => choices?.Confirm();
    public void MoveChoice(int direction) => choices?.MoveSelection(direction);

    private void LateUpdate() => Follow();
    private void Follow()
    {
        if (!built) return;
        var camera = Camera.main;
        if (target == null) return;
        if (imageMode)
        {
            PlaceBubbleAt(new Vector2(0, root.rect.yMin + 33));
        }
        else if (speaker != null && camera != null && bubble.gameObject.activeSelf)
        {
            Vector3 position = speaker.position + (speaker == player ? Vector3.up * .4f : Vector3.zero);
            Place(position, target.BubbleOffset, camera);
            // 화면 가장자리에서도 꼬리가 화자의 위치를 가리키도록 보정함.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, camera.WorldToScreenPoint(position), null, out var anchor);
            var tail = (RectTransform)bubble.centerArrow.transform;
            Vector3 localAnchor = bubble.bubble.InverseTransformPoint(root.TransformPoint(anchor));
            Rect bounds = bubble.bubble.rect;
            Vector3 tailPosition = tail.localPosition;
            tailPosition.x = Mathf.Clamp(localAnchor.x, bounds.xMin + 12, bounds.xMax - 12);
            tail.localPosition = tailPosition;
        }
        if (IsChoosing && player != null && camera != null)
        {
            Vector3 screen = camera.WorldToScreenPoint(player.position + (menuMode ? Vector3.zero : Vector3.up * .2f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var playerPoint);
            float side = menuMode ? -1 : screen.x >= camera.WorldToScreenPoint(target.SpeechAnchor.position).x ? 1 : -1;
            choices.SetSide(side > 0);
            var size = ScaledSize(choices.Rect);
            var position = playerPoint + new Vector2(side * (size.x * .5f + choiceSideGap), 0);
            if (hasDialogue && bubble.gameObject.activeSelf)
            {
                float bubbleBottom = root.InverseTransformPoint(bubble.bubble.TransformPoint(bubble.bubble.rect.min)).y;
                position.y = Mathf.Min(position.y, bubbleBottom - size.y * .5f - 8);
            }
            choices.Rect.anchoredPosition = ClampToCanvas(root.rect, size, choices.Rect.pivot, position, 8);
            choicesVisibility.alpha = screen.z > 0 ? 1 : 0;
            choicesVisibility.blocksRaycasts = screen.z > 0;
        }
    }

    private void Place(Vector3 worldPosition, Vector2 offset, Camera camera)
    {
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var point);
        PlaceBubbleAt(point + offset);
        // 타이핑 작업을 끄지 않고 카메라 뒤에 있는 말풍선만 숨김.
        bubbleVisibility.alpha = screen.z > 0 ? 1 : 0;
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
