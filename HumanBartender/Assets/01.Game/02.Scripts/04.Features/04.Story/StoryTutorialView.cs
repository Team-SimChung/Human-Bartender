using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared C# UI for the missing intro and the clickable bartending lessons.</summary>
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

    BarSeatStatusView seatHud;
    StoryTutorialGuideGraphic guide;
    GameObject legend;
    readonly List<(StoryTutorialShape dot, EBarSeatSignal signal)> legendDots = new();
    Transform focusTarget, dropTarget;
    bool dragGuide;
    bool nearby;

    public BarSeatStatusView SeatHud => seatHud;
    public StoryTutorialGuideGraphic Guide => guide;
    public bool IsSeatLegendVisible => legend != null && legend.activeInHierarchy;
    public RectTransform InstructionPanel => panel;

    public bool IsIntroNoticeVisible => fullScreenClick != null && fullScreenClick.gameObject.activeSelf;
    public Button IntroClickButton => fullScreenClick;
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

    public async UniTask WaitForIntroClickAsync(CancellationToken token)
    {
        ShowInstruction("인트로 컷신", "현재 컷신 리소스가 적용되지 않았습니다.\n아무 곳이나 클릭하면 0일차 튜토리얼로 넘어갑니다.");
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        fullScreenClick.gameObject.SetActive(true);
        seatHud.Suppressed = true;
        var clicked = new UniTaskCompletionSource();
        void Continue() => clicked.TrySetResult();
        fullScreenClick.onClick.AddListener(Continue);
        // The notice text must also let clicks reach the full-screen button.
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        try { await clicked.Task.AttachExternalCancellation(token); }
        finally { fullScreenClick.onClick.RemoveListener(Continue); Hide(); }
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
        confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -86);
        nearby = false;
        focusTarget = dropTarget = null;
        if (guide != null) guide.gameObject.SetActive(false);
        if (legend != null) legend.SetActive(false);
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
        var image = panel.GetComponent<Image>();
        if (image != null) { image.color = Color.clear; image.raycastTarget = false; }
        var fillRect = MakeRect("Instruction Fill", panel, Vector2.zero, Vector2.zero);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        var fill = fillRect.gameObject.AddComponent<StoryTutorialShape>();
        fill.Style(new Color(.035f, .07f, .10f, .96f), 7);
        var edge = MakeRect("Instruction Border", panel, Vector2.zero, Vector2.zero);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.gameObject.AddComponent<StoryTutorialShape>().Style(new Color(.32f, .54f, .58f), 7, 1);
        edge.SetAsFirstSibling();
        fillRect.SetAsFirstSibling();
        seatHud = BarSeatStatusView.Create(transform, heading.font);
        seatHud.transform.SetSiblingIndex(1);
        var art = MakeRect("Tutorial Spotlight and Arrows", transform, Vector2.zero, Vector2.zero);
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        guide = art.gameObject.AddComponent<StoryTutorialGuideGraphic>();
        guide.raycastTarget = false;
        panel.SetAsLastSibling();
        guide.gameObject.SetActive(false);
        BuildLegend();
    }

    public void ConfigureHud(PlayPhaseController phases, DialogueCharacterManager characters, GuestManager guests,
        CraftFlowController craft, CraftPrepStageHost preparation, RecipeBrowserScreen recipes) =>
        seatHud.Configure(phases, characters, guests, craft, preparation, recipes);

    public void ShowGuide(string title, string text, Transform focus, Transform destination = null,
        bool drag = false, Action onConfirm = null)
    {
        ShowInstruction(title, text, onConfirm);
        nearby = true;
        focusTarget = focus;
        dropTarget = destination;
        dragGuide = drag;
        heading.gameObject.SetActive(false);
        instruction.fontSize = 18;
        float height = Mathf.Clamp(instruction.GetPreferredValues(text, 292, 600).y + (onConfirm != null ? 64 : 30), 72, 230);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.sizeDelta = new Vector2(320, height);
        instruction.rectTransform.sizeDelta = new Vector2(292, height - (onConfirm != null ? 60 : 24));
        instruction.rectTransform.anchoredPosition = new Vector2(0, onConfirm != null ? 20 : 0);
        confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -height * .5f + 23);
        guide.gameObject.SetActive(true);
        UpdateGuide();
    }

    public void SetGuideTargets(Transform focus, Transform destination = null, bool drag = false)
    {
        focusTarget = focus;
        dropTarget = destination;
        dragGuide = drag;
    }

    public void ShowSeatIndicator(Action completed) => ShowGuide("좌석 알림",
        "여기에서 좌석 상태를 확인할 수 있어요.\n색과 깜빡임의 의미를 알아볼까요?", seatHud.Indicators,
        onConfirm: () => ShowSeatLegend(completed));

    void ShowSeatLegend(Action completed)
    {
        ShowInstruction("좌석 알림", "", completed);
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(700, 480);
        heading.rectTransform.anchoredPosition = new Vector2(0, 205);
        instruction.gameObject.SetActive(false);
        confirm.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -210);
        guide.gameObject.SetActive(true);
        guide.SetGuide(null, null, null, null, false);
        legend.SetActive(true);
    }

    void BuildLegend()
    {
        legend = MakeRect("Seat Signal Legend", panel, Vector2.zero, Vector2.zero).gameObject;
        var states = new[] { EBarSeatSignal.Empty, EBarSeatSignal.Arrival, EBarSeatSignal.Occupied,
            EBarSeatSignal.Waiting, EBarSeatSignal.LeavingSoon, EBarSeatSignal.Departing };
        var titles = new[] { "회색 · 빈 좌석", "하양/민트색 깜빡임 · 새 손님", "하양/민트색 · 손님 있음",
            "주황색 · 기다리는 중", "빨간색 · 곧 떠나요", "빨간색 깜빡임 · 퇴장 중" };
        var descriptions = new[] { "아직 손님이 없는 자리예요.", "손님이 막 들어왔어요.\n코스터를 준비하세요.", "손님이 앉아 있는 자리예요.",
            "인내심이 절반 이하로 줄었어요.", "기다릴 수 있는 시간이\n10초 이하예요.", "손님이 자리를 떠나고 있어요." };
        for (int i = 0; i < states.Length; i++)
        {
            var row = MakeRect("Signal " + states[i], legend.transform, new Vector2(320, 94),
                new Vector2(i % 2 == 0 ? -164 : 164, 124 - i / 2 * 104));
            row.gameObject.AddComponent<StoryTutorialShape>().Style(new Color(.06f, .12f, .165f), 7);
            var dot = MakeRect("Sample Dot", row, new Vector2(20, 20), new Vector2(-136, 0))
                .gameObject.AddComponent<StoryTutorialShape>();
            dot.Style(BarSeatStatusView.SignalColor(states[i]), 10);
            legendDots.Add((dot, states[i]));
            var title = MakeText("State", row, heading.font, new Vector2(246, 30), new Vector2(20, 23), 16);
            title.alignment = TextAlignmentOptions.Left;
            title.text = titles[i];
            var description = MakeText("Meaning", row, heading.font, new Vector2(246, 48), new Vector2(20, -15), 14);
            description.alignment = TextAlignmentOptions.Left;
            description.color = new Color(.71f, .80f, .82f);
            description.text = descriptions[i];
        }
        var selected = MakeRect("Current Seat Ring", legend.transform, new Vector2(30, 30), new Vector2(-268, -158));
        selected.gameObject.AddComponent<StoryTutorialShape>().Style(BarSeatStatusView.SelectedColor, 15, 1.5f);
        MakeRect("Current Seat Dot", selected, new Vector2(14, 14), Vector2.zero).gameObject.AddComponent<StoryTutorialShape>()
            .Style(BarSeatStatusView.SelectedColor, 7);
        MakeText("Current Seat Meaning", legend.transform, heading.font, new Vector2(520, 40), new Vector2(22, -158), 16)
            .text = "테두리로 둘러싸인 점이 지금 보고 있는 좌석이에요.";
        legend.SetActive(false);
    }

    void LateUpdate()
    {
        if (nearby && panel.gameObject.activeSelf) UpdateGuide();
        if (IsSeatLegendVisible)
        foreach (var sample in legendDots)
        {
            var tint = BarSeatStatusView.SignalColor(sample.signal);
            if ((sample.signal == EBarSeatSignal.Arrival || sample.signal == EBarSeatSignal.Departing) &&
                (int)(Time.unscaledTime / .4f) % 2 == 1) tint.a = .24f;
            sample.dot.color = tint;
        }
    }

    void UpdateGuide()
    {
        Rect? source = TargetRect(focusTarget), target = TargetRect(dropTarget);
        Rect area = ((RectTransform)transform).rect;
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
        if (source.HasValue && dragGuide && target.HasValue)
        {
            from = new Vector2(source.Value.xMin - 10, source.Value.center.y);
            to = new Vector2(target.Value.xMax + 10, target.Value.center.y);
        }
        else if (source.HasValue)
        {
            Rect box = source.Value;
            Vector2 direction = (box.center - panel.anchoredPosition).normalized;
            to = box.center - new Vector2(direction.x * (box.width * .5f + 16), direction.y * (box.height * .5f + 16));
            from = to - direction * 30;
        }
        guide.gameObject.SetActive(source.HasValue || target.HasValue);
        guide.SetGuide(source, target, from, to, dragGuide);
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
            camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }
        else
        {
            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer == null) return null;
            var bounds = renderer.bounds;
            corners[0] = bounds.min;
            corners[2] = bounds.max;
            camera = target.GetComponentInParent<CraftPrepStageScreen>()?.GuideCamera ?? Camera.main;
        }
        var root = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[0]), null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, RectTransformUtility.WorldToScreenPoint(camera, corners[2]), null, out var max);
        return Rect.MinMaxRect(min.x - 7, min.y - 7, max.x + 7, max.y + 7);
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
        if (legend != null) legend.SetActive(false);
        if (seatHud != null) seatHud.Suppressed = false;
        nearby = false;
        focusTarget = dropTarget = null;
    }
}
