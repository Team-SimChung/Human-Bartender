using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared C# UI for the home guide pages and the clickable bartending lessons.</summary>
public sealed class StoryTutorialView : MonoBehaviour
{
    [SerializeField] Button fullScreenClick;
    [SerializeField] RectTransform panel;
    [SerializeField] TMP_Text heading;
    [SerializeField] TMP_Text instruction;
    [SerializeField] Button confirm;
    [SerializeField] TMP_Text confirmLabel;
    [SerializeField] GameObject seats;
    [SerializeField] Button[] seatButtons;
    [SerializeField] TMP_Text[] seatLabels;
    [SerializeField] TMP_FontAsset seatKeyFont;

    [Header("Guide Box (web .day0-guide-copy)")]
    [Tooltip("대상 옆에 뜨는 안내 상자의 글꼴. 웹은 PF스타더스트 Regular(LunaPixel 400)다.")]
    [SerializeField] TMP_FontAsset guideFont;
    [Tooltip("안내 상자 글자 크기(1280×720 기준 px). 웹 16.")]
    [SerializeField, Min(1)] float guideFontSize = 16;
    [Tooltip("안내 상자 글자 색. 웹 #E2FCFF.")]
    [SerializeField] Color guideTextColor = new Color32(0xE2, 0xFC, 0xFF, 0xFF);
    [Tooltip("안내 상자 폭(테두리 포함). 웹 320.")]
    [SerializeField, Min(1)] float guideWidth = 320;
    [Tooltip("테두리 안쪽 여백. X는 좌우 각각, Y는 위아래 각각. 웹은 좌우 15, 위아래 12.")]
    [SerializeField, Min(0)] Vector2 guidePadding = new Vector2(15, 12);
    [Tooltip("안내 상자 배경색. 웹 #09121A, 불투명도 0.93(ee).")]
    [SerializeField] Color guideFillColor = new Color32(0x09, 0x12, 0x1A, 0xEE);
    [Tooltip("안내 상자 테두리 색. 웹 #538995.")]
    [SerializeField] Color guideBorderColor = new Color32(0x53, 0x89, 0x95, 0xFF);
    [Tooltip("안내 상자 테두리 두께. 웹 1.")]
    [SerializeField, Min(0)] float guideBorderWidth = 1;
    [Tooltip("안내 상자 모서리 반지름. 웹 6.")]
    [SerializeField, Min(0)] float guideCornerRadius = 6;
    [Tooltip("문구와 확인 버튼 사이 간격. 웹 12. 버튼 크기·색은 프리팹의 Confirm이 갖는다.")]
    [SerializeField, Min(0)] float guideConfirmSpacing = 12;

    [Header("Focus Outline")]
    [Tooltip("대상 강조 테두리를 화면 가장자리에서 이만큼 안쪽에 그린다(1280×720 기준). 대상이 가장자리에 붙어 있으면 그 변의 여유(7)를 줄여 맞추고, " +
             "떨어져 있으면 모양은 그대로다. 테두리 바깥 후광(4)까지 다 보이도록 기본 4. 딤 구멍은 줄이지 않아 대상이 가려지지 않는다.")]
    [SerializeField, Min(0)] float focusScreenMargin = 4;

    [Header("Drag Route (web .day0-drag-route)")]
    [Tooltip("화살표 시작점. X는 끄는 대상(공급함) 왼쪽 변에서 바깥으로 떨어진 거리, Y는 대상 위쪽에서 내려온 높이 비율. 웹 (12, 0.4).")]
    [SerializeField] Vector2 routeStart = new Vector2(12, .4f);
    [Tooltip("화살표 끝점. X는 놓을 자리 오른쪽 변에서 바깥으로 떨어진 거리, Y는 자리 위쪽에서 내려온 높이 비율. 웹 (10, 0.75).")]
    [SerializeField] Vector2 routeEnd = new Vector2(10, .75f);
    [Tooltip("시작점에서 첫 곡선 조절점까지(1280×720 기준, 위가 +). 웹 (-120, 75): 공급함 왼쪽 위로 솟는다.")]
    [SerializeField] Vector2 routeStartBend = new Vector2(-120, 75);
    [Tooltip("끝점에서 둘째 곡선 조절점까지(위가 +). 웹 (125, -50): 자리 오른쪽 아래에서 들어와 화살촉이 왼쪽 위를 향한다.")]
    [SerializeField] Vector2 routeEndBend = new Vector2(125, -50);
    [Tooltip("점선 한 칸 길이와 빈칸 길이. 웹 12, 9.")]
    [SerializeField] Vector2 routeDash = new Vector2(12, 9);
    [Tooltip("점선이 놓을 자리 쪽으로 흐르는 속도(초당 px). 웹은 1.4초에 42 → 30.")]
    [SerializeField, Min(0)] float routeDashSpeed = 30;

    BarSeatStatusView seatHud;
    StoryTutorialGuideGraphic guide;
    Transform focusTarget, dropTarget;
    bool dragGuide;
    bool nearby;
    // 참이면 안내 상자만 숨기고 딤·강조·화살표는 그대로 둔다(웹 data-dragging: 코스터를 끄는 동안).
    Func<bool> copyHiddenWhile;
    // 화면 위쪽 큰 안내(ShowInstruction)의 글꼴과 색. 대상 옆 안내 상자가 바꾼 뒤 되돌리는 데 쓴다.
    TMP_FontAsset instructionFont;
    Color instructionColor;

    public BarSeatStatusView SeatHud => seatHud;
    /// <summary>좌석 키 안내 글꼴. 씬의 좌석 표시를 배치하는 도구가 같은 글꼴을 쓰려고 읽는다.</summary>
    public TMP_FontAsset SeatKeyFont => seatKeyFont;
    public StoryTutorialGuideGraphic Guide => guide;
    public RectTransform InstructionPanel => panel;

    public bool IsPageVisible => fullScreenClick != null && fullScreenClick.gameObject.activeSelf;
    public string Heading => heading != null ? heading.text : null;
    public string Instruction => instruction != null ? instruction.text : null;

    public static StoryTutorialView Create(StoryTutorialView prefab, TMP_FontAsset font)
    {
        if (prefab != null && prefab.IsConfigured)
        {
            var view = Instantiate(prefab);
            view.CreateGuidance();
            return view;
        }
        return CreateFallback(font);
    }

    bool IsConfigured => fullScreenClick != null && panel != null && heading != null && instruction != null &&
        confirm != null && confirmLabel != null && seats != null && seatButtons?.Length == 3 && seatLabels?.Length == 3 &&
        Array.TrueForAll(seatButtons, button => button != null) && Array.TrueForAll(seatLabels, label => label != null);

    /// <summary>Scene references are optional: the notice and lesson controls remain usable without a prefab.</summary>
    public static StoryTutorialView CreateFallback(TMP_FontAsset font, bool createGuidance = true)
    {
        var root = new GameObject("Story Tutorial Overlay", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(StoryTutorialView));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        var view = root.GetComponent<StoryTutorialView>();
        var background = MakeRect("Intro Full Screen Click", root.transform, Vector2.zero, Vector2.zero);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(.025f, .035f, .05f, 1f);
        view.fullScreenClick = background.gameObject.AddComponent<Button>();
        view.fullScreenClick.targetGraphic = backgroundImage;
        view.fullScreenClick.transition = Selectable.Transition.None;
        view.panel = MakeRect("Instruction", root.transform, new Vector2(700, 230), new Vector2(0, -125));
        view.panel.anchorMin = view.panel.anchorMax = new Vector2(.5f, 1f);
        var panelImage = view.panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(.035f, .055f, .075f, .94f);
        panelImage.raycastTarget = false;
        view.heading = MakeText("Heading", view.panel, font, new Vector2(660, 38), new Vector2(0, 84), 28);
        view.heading.color = new Color(.65f, .96f, .97f);
        view.instruction = MakeText("Instruction Text", view.panel, font, new Vector2(660, 124), new Vector2(0, 5), 22);
        view.confirm = MakeButton("Confirm", view.panel, font, new Vector2(145, 38), new Vector2(0, -86), out view.confirmLabel);
        view.seats = MakeRect("Seat Buttons", view.panel, new Vector2(660, 40), new Vector2(0, -86)).gameObject;
        view.seatButtons = new Button[3];
        view.seatLabels = new TMP_Text[3];
        for (int i=0; i<3; i++) view.seatButtons[i] = MakeButton("Seat " + i, view.seats.transform, font,
            new Vector2(200, 38), new Vector2((i-1)*215, 0), out view.seatLabels[i]);
        if (createGuidance) view.CreateGuidance();
        view.Hide();
        return view;
    }

    internal static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    internal static TMP_Text MakeText(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position, float fontSize)
    {
        var text = MakeRect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static Button MakeButton(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position, out TMP_Text label)
    {
        var rect = MakeRect(name, parent, size, position);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.12f, .28f, .32f, .98f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        label = MakeText("Label", rect, font, size - new Vector2(8, 4), Vector2.zero, 22);
        return button;
    }

    /// <summary>
    /// 화면을 가리는 안내 한 장을 가운데에 띄우고 진행 입력을 기다린다. 처음 inputLockSeconds 동안은 입력을 받지 않아
    /// 직전 입력으로 바로 넘어가지 않는다. 플레이 안내·이동·상호작용처럼 멈추고 읽게 하는 안내에 쓴다.
    /// </summary>
    /// <param name="shadeAlpha">뒤 화면을 가리는 정도. 1이면 완전히 가린다.</param>
    public async UniTask ShowPageAsync(string title, string body, string footer, float shadeAlpha,
        float inputLockSeconds, Func<bool> advancePressed, CancellationToken token)
    {
        if (advancePressed == null) throw new ArgumentNullException(nameof(advancePressed));
        string text = string.IsNullOrEmpty(footer) ? body : body + "\n\n<size=70%><color=#8FA9AD>" + footer + "</color></size>";
        ShowInstruction(title, text);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        fullScreenClick.gameObject.SetActive(true);
        // 배경은 가리기만 하고 클릭으로 넘기지 않는다. 진행은 advancePressed만 따른다.
        fullScreenClick.interactable = false;
        if (fullScreenClick.targetGraphic != null)
        {
            var shade = fullScreenClick.targetGraphic.color;
            shade.a = Mathf.Clamp01(shadeAlpha);
            fullScreenClick.targetGraphic.color = shade;
        }
        if (seatHud != null) seatHud.Suppressed = true;
        float unlockAt = Time.unscaledTime + Mathf.Max(0, inputLockSeconds);
        try
        {
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (Time.unscaledTime >= unlockAt && advancePressed()) break;
            }
        }
        finally
        {
            // 씬이 바뀌어 취소된 경우에는 이미 파괴되었으므로 정리할 것이 없다.
            if (this != null)
            {
                if (fullScreenClick != null) fullScreenClick.interactable = true;
                Hide();
            }
        }
    }

    /// <summary>다음 화면이 준비되는 동안 기존 안내 배경을 검게 유지한다.</summary>
    internal async UniTask KeepScreenCoveredAsync(int sortingOrder, Func<UniTask> present)
    {
        var canvas = GetComponent<Canvas>();
        var shade = fullScreenClick != null ? fullScreenClick.targetGraphic : null;
        if (canvas == null || shade == null)
        {
            Debug.LogWarning("[StoryTutorial] 화면을 가릴 안내 배경이 연결되지 않았습니다.", this);
            await present();
            return;
        }

        int originalOrder = canvas.sortingOrder;
        Color originalColor = shade.color;
        bool wasInteractable = fullScreenClick.interactable;
        Hide();
        canvas.sortingOrder = sortingOrder;
        shade.color = Color.black;
        fullScreenClick.interactable = false;
        fullScreenClick.gameObject.SetActive(true);
        try
        {
            await present();
        }
        finally
        {
            if (this != null)
            {
                fullScreenClick.gameObject.SetActive(false);
                fullScreenClick.interactable = wasInteractable;
                shade.color = originalColor;
                canvas.sortingOrder = originalOrder;
            }
        }
    }

    public void ShowInstruction(string title, string text, Action onConfirm = null)
    {
        gameObject.SetActive(true);
        fullScreenClick.gameObject.SetActive(false);
        panel.gameObject.SetActive(true);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
        panel.anchoredPosition = new Vector2(0, -110);
        panel.sizeDelta = new Vector2(700, 230);
        heading.gameObject.SetActive(true);
        heading.rectTransform.anchoredPosition = new Vector2(0, 84);
        heading.rectTransform.sizeDelta = new Vector2(660, 38);
        instruction.gameObject.SetActive(true);
        instruction.rectTransform.anchoredPosition = new Vector2(0, 5);
        instruction.rectTransform.sizeDelta = new Vector2(660, 124);
        instruction.fontSize = 22;
        if (instructionFont != null) instruction.font = instructionFont;
        instruction.color = instructionColor;
        confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -86);
        nearby = false;
        focusTarget = dropTarget = null;
        copyHiddenWhile = null;
        if (guide != null) guide.gameObject.SetActive(false);
        heading.text = title;
        instruction.text = text;
        seats.SetActive(false);
        confirm.onClick.RemoveAllListeners();
        confirm.gameObject.SetActive(onConfirm != null);
        if (onConfirm != null)
        {
            confirmLabel.text = "확인";
            confirm.onClick.AddListener(() => onConfirm());
        }
    }

    void CreateGuidance()
    {
        if (guide != null) return;
        instructionFont = instruction.font;
        instructionColor = instruction.color;
        var image = panel.GetComponent<Image>();
        if (image != null) { image.color = Color.clear; image.raycastTarget = false; }
        var fillRect = MakeRect("Instruction Fill", panel, Vector2.zero, Vector2.zero);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        var fill = fillRect.gameObject.AddComponent<StoryTutorialShape>();
        fill.Style(guideFillColor, guideCornerRadius);
        var edge = MakeRect("Instruction Border", panel, Vector2.zero, Vector2.zero);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.gameObject.AddComponent<StoryTutorialShape>().Style(guideBorderColor, guideCornerRadius, guideBorderWidth);
        edge.SetAsFirstSibling();
        fillRect.SetAsFirstSibling();
        var art = MakeRect("Tutorial Spotlight and Arrows", transform, Vector2.zero, Vector2.zero);
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        guide = art.gameObject.AddComponent<StoryTutorialGuideGraphic>();
        guide.raycastTarget = false;
        panel.SetAsLastSibling();
        guide.gameObject.SetActive(false);
    }

    /// <summary>
    /// 씬의 바 좌석 표시를 빌려 온다. 좌석 표시는 바 UI 캔버스가 소유하고, 오버레이는 좌석 수업에서
    /// 강조 대상과 키 안내로 쓰고 전면 안내 동안 숨기기만 한다.
    /// </summary>
    public void UseSeatHud(BarSeatStatusView hud) => seatHud = hud;

    /// <param name="drag">참이면 focus에서 destination으로 웹 코스터 수업과 같은 곡선 점선 화살표를 그린다.</param>
    /// <param name="hideCopyWhile">참을 돌려주는 동안 안내 상자만 숨긴다(예: 대상을 끄는 중).</param>
    public void ShowGuide(string title, string text, Transform focus, Transform destination = null,
        bool drag = false, Action onConfirm = null, Func<bool> hideCopyWhile = null)
    {
        ShowInstruction(title, text, onConfirm);
        nearby = true;
        focusTarget = focus;
        dropTarget = destination;
        dragGuide = drag;
        copyHiddenWhile = hideCopyWhile;
        heading.gameObject.SetActive(false);
        // 웹 .day0-guide[data-nearby] .day0-guide-copy: 테두리 포함 폭, 안쪽 여백, 글꼴·크기·색. 정렬은 가운데 그대로다.
        if (guideFont != null) instruction.font = guideFont;
        instruction.fontSize = guideFontSize;
        instruction.color = guideTextColor;
        float textWidth = guideWidth - (guideBorderWidth + guidePadding.x) * 2;
        float textHeight = instruction.GetPreferredValues(text, textWidth, 600).y;
        var confirmRect = (RectTransform)confirm.transform;
        float confirmHeight = onConfirm != null ? guideConfirmSpacing + confirmRect.sizeDelta.y : 0;
        float height = Mathf.Ceil((guideBorderWidth + guidePadding.y) * 2 + textHeight + confirmHeight);
        float textTop = height * .5f - guideBorderWidth - guidePadding.y;
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(guideWidth, height);
        instruction.rectTransform.sizeDelta = new Vector2(textWidth, textHeight);
        instruction.rectTransform.anchoredPosition = new Vector2(0, textTop - textHeight * .5f);
        confirmRect.anchoredPosition = new Vector2(0, textTop - textHeight - guideConfirmSpacing - confirmRect.sizeDelta.y * .5f);
        guide.gameObject.SetActive(true);
        UpdateGuide();
    }

    public void SetGuideTargets(Transform focus, Transform destination = null, bool drag = false)
    {
        focusTarget = focus;
        dropTarget = destination;
        dragGuide = drag;
    }

    /// <summary>좌석 알림 수업의 첫 안내. 좌석 표시를 가리키고, 확인하면 completed를 부른다(웹 tutorial-view.js seatIndicator).</summary>
    public void ShowSeatIndicator(Action completed) => ShowGuide("좌석 알림",
        "여기에서 좌석 상태를 확인할 수 있어요.", seatHud.Indicators, onConfirm: completed);

    /// <summary>확인 버튼이 보이는 동안 웹처럼 E·Space·Enter로도 확인한다. 쓴 키는 이 프레임의 진행 입력으로 차지해 대사 진행 등으로 새지 않게 한다.</summary>
    void Update()
    {
        if (confirm == null || !confirm.gameObject.activeInHierarchy || !confirm.IsInteractable()) return;
        if (UIDisplayOptions.IsOptionOpen || GamePause.IsPaused) return;
        if (TutorialPageView.ConfirmKeyPressed()) confirm.onClick.Invoke();
    }

    void LateUpdate()
    {
        if (nearby) UpdateGuide();
    }

    internal void UpdateGuide()
    {
        bool showCopy = copyHiddenWhile == null || !copyHiddenWhile();
        if (panel.gameObject.activeSelf != showCopy) panel.gameObject.SetActive(showCopy);
        Rect area = ((RectTransform)transform).rect;
        // 딤 구멍은 대상 + 여유 그대로 두어 대상이 가려지지 않게 하고, 그리는 테두리와 그 테두리를 기준으로 하는
        // 화살표·안내 상자 자리는 화면 안으로 당긴 사각형을 쓴다. 가장자리에 붙은 대상도 테두리 네 변이 다 보인다.
        Rect? sourceHole = TargetRect(focusTarget), targetHole = TargetRect(dropTarget);
        Rect? source = FitOnScreen(sourceHole, area), target = FitOnScreen(targetHole, area);
        Vector2 size = panel.sizeDelta;
        if (source.HasValue)
        {
            Rect box = source.Value;
            float x = box.xMax + 46 + size.x * .5f;
            if (x + size.x * .5f > area.xMax - 16) x = box.xMin - 46 - size.x * .5f;
            float y = box.center.y;
            if (x - size.x * .5f < area.xMin + 16)
            {
                x = box.center.x;
                y = box.yMax + 45 + size.y * .5f;
            }
            x = Mathf.Clamp(x, area.xMin + 16 + size.x * .5f, area.xMax - 16 - size.x * .5f);
            y = Mathf.Clamp(y, area.yMin + 16 + size.y * .5f, area.yMax - 16 - size.y * .5f);
            Rect copy = new Rect(new Vector2(x, y) - size * .5f, size);
            if (target.HasValue && copy.Overlaps(target.Value))
                y = Mathf.Clamp(target.Value.yMax + 32 + size.y * .5f, area.yMin + 16 + size.y * .5f, area.yMax - 16 - size.y * .5f);
            panel.anchoredPosition = new Vector2(x, y);
        }
        else panel.anchoredPosition = new Vector2(area.xMax - size.x * .5f - 24, area.yMax - size.y * .5f - 28);
        Vector2? from = null, to = null;
        Vector2 fromBend = Vector2.zero, toBend = Vector2.zero;
        if (source.HasValue && dragGuide && target.HasValue)
        {
            // 강조 영역은 매 프레임 다시 재므로 카메라가 좌석을 옮겨도 화살촉이 자리를 따라간다.
            var route = DragRoute(source.Value, target.Value);
            from = route.from;
            to = route.to;
            fromBend = route.fromBend;
            toBend = route.toBend;
        }
        else if (source.HasValue)
        {
            Rect box = source.Value;
            to = RectEdge(box, panel.anchoredPosition);
            from = RectEdge(new Rect(panel.anchoredPosition - size * .5f, size), box.center);
        }
        guide.gameObject.SetActive(source.HasValue || target.HasValue);
        guide.SetRoute(fromBend, toBend, routeDash, routeDashSpeed);
        guide.SetGuide(source, target, from, to, dragGuide, sourceHole, targetHole);
    }

    /// <summary>
    /// 웹 tutorial-view.js coaster 화살표: 끄는 대상(공급함) 왼쪽에서 출발해 위로 솟았다가 놓을 자리 오른쪽으로 들어오는 3차 곡선.
    /// 시작·끝점과 두 조절점을 돌려준다(오버레이 1280×720 좌표, 위가 +).
    /// </summary>
    internal (Vector2 from, Vector2 to, Vector2 fromBend, Vector2 toBend) DragRoute(Rect source, Rect target)
    {
        var from = new Vector2(source.xMin - routeStart.x, source.yMax - source.height * routeStart.y);
        var to = new Vector2(target.xMax + routeEnd.x, target.yMax - target.height * routeEnd.y);
        return (from, to, from + routeStartBend, to + routeEndBend);
    }

    Rect? FitOnScreen(Rect? box, Rect area) => box.HasValue ? FitInside(box.Value, area, focusScreenMargin) : null;

    /// <summary>
    /// 강조 사각형을 화면 영역(area)에서 margin만큼 안쪽으로 들인다. 넘치는 변만 안으로 당기고 나머지 변은 그대로다.
    /// 화면이 margin보다 작으면 그대로 돌려준다.
    /// </summary>
    internal static Rect FitInside(Rect box, Rect area, float margin)
    {
        Rect inner = Rect.MinMaxRect(area.xMin + margin, area.yMin + margin, area.xMax - margin, area.yMax - margin);
        if (inner.width <= 0 || inner.height <= 0) return box;
        return Rect.MinMaxRect(
            Mathf.Clamp(box.xMin, inner.xMin, inner.xMax), Mathf.Clamp(box.yMin, inner.yMin, inner.yMax),
            Mathf.Clamp(box.xMax, inner.xMin, inner.xMax), Mathf.Clamp(box.yMax, inner.yMin, inner.yMax));
    }

    Rect? TargetRect(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return null;
        var corners = new Vector3[4];
        Camera camera;
        if (target is RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            var canvas = target.GetComponentInParent<Canvas>();
            camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera ?? Camera.main : null;
        }
        else
        {
            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer == null) return null;
            var bounds = renderer.bounds;
            corners[0] = bounds.min;
            corners[1] = new Vector3(bounds.min.x, bounds.max.y, bounds.center.z);
            corners[2] = bounds.max;
            corners[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.center.z);
            camera = target.GetComponentInParent<CraftPrepStageScreen>()?.GuideCamera ?? Camera.main;
        }
        var root = (RectTransform)transform;
        // 게임에서는 오버레이라 카메라가 없다. 미리보기처럼 카메라 캔버스로 그릴 때만 그 카메라로 되돌린다.
        var rootCanvas = GetComponent<Canvas>();
        Camera rootCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corner), rootCamera, out var point);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x - 7, min.y - 7, max.x + 7, max.y + 7);
    }

    static Vector2 RectEdge(Rect rect, Vector2 towards)
    {
        Vector2 direction = towards - rect.center;
        if (direction.sqrMagnitude < .001f) return rect.center;
        float x = Mathf.Abs(direction.x) > .001f ? rect.width * .5f / Mathf.Abs(direction.x) : float.PositiveInfinity;
        float y = Mathf.Abs(direction.y) > .001f ? rect.height * .5f / Mathf.Abs(direction.y) : float.PositiveInfinity;
        return rect.center + direction * Mathf.Min(x, y);
    }

    public void ShowSeats(ESlotType current, Action<ESlotType> selected)
    {
        seats.SetActive(true);
        var slots = new[] { ESlotType.Left, ESlotType.Middle, ESlotType.Right };
        var labels = new[] { "왼쪽 좌석", "가운데 좌석", "오른쪽 좌석" };
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            seatLabels[i].text = (slot == current ? "● " : "○ ") + labels[i];
            seatButtons[i].onClick.RemoveAllListeners();
            seatButtons[i].onClick.AddListener(() => selected(slot));
        }
    }

    public void SetSeatsInteractable(bool value)
    {
        foreach (var button in seatButtons) button.interactable = value;
    }

    public void Hide()
    {
        if (fullScreenClick != null) fullScreenClick.gameObject.SetActive(false);
        if (panel != null) panel.gameObject.SetActive(false);
        if (confirm != null) confirm.onClick.RemoveAllListeners();
        if (seatButtons != null) foreach (var button in seatButtons) if (button != null) button.onClick.RemoveAllListeners();
        if (guide != null) guide.gameObject.SetActive(false);
        if (seatHud != null) seatHud.Suppressed = false;
        nearby = false;
        focusTarget = dropTarget = null;
        copyHiddenWhile = null;
    }
}
