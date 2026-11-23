using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>CSV 선택지를 프리팹으로 표시함. 대화 분기와 조건 실행은 DialogueRunner가 담당함.</summary>
public sealed class WorldInteractionChoiceView : MonoBehaviour
{
    [SerializeField] private WorldInteractionChoiceItem itemPrefab;
    public const int MaximumChoices = 4;
    [Header("Prefab parts")]
    [SerializeField] private RectTransform revealViewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup contentVisibility;
    [SerializeField] private RectTransform itemsRoot;
    [SerializeField] private VerticalLayoutGroup itemsLayout;
    [SerializeField] private RectTransform selectionCursor;
    [SerializeField] private Image keyIcon;
    [SerializeField] private Image pointer;
    [SerializeField] private Sprite rightPointer;
    [Tooltip("비워 두면 Right Pointer를 좌우 반전하여 사용합니다.")]
    [SerializeField] private Sprite leftPointer;
    [SerializeField] private TMP_Text moneyLabel;
    [Header("Layout")]
    [Tooltip("항목 폭의 기준은 Item Prefab 루트의 Width입니다. 문구(잠금 사유 포함)가 그보다 길 때만 이 값까지 늘어나고, " +
        "이 값을 넘는 문구는 글자 크기를 줄여 맞춥니다.")]
    [SerializeField, Min(20)] private float maximumItemWidth = 138;
    [Tooltip("커서 칸의 폭. Items Root와 Money Text의 X가 됩니다. 선택창이 캐릭터 왼쪽에 뜰 때는 " +
        "Key Icon·Pointer의 프리팹 배치(오른쪽에 뜰 때의 모양)를 이 칸 안에서 좌우로 뒤집습니다.")]
    [SerializeField, Min(0)] private float cursorGutter = 22;
    [SerializeField] private bool showMoney = true;
    [SerializeField, Min(0)] private float moneyHeaderHeight = 13;
    [Header("Animation")]
    [Tooltip("위에서 아래로 펼쳐지는 시간(초). 0이면 즉시 표시합니다.")]
    [SerializeField, Min(0)] private float revealDuration = .22f;
    [SerializeField] private AnimationCurve revealEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, Min(0)] private float cursorMoveDuration = .08f;
    [Header("Input")]
    [Tooltip("선택지가 나타난 뒤 확정(E·Enter·Space·클릭)을 받지 않는 시간(초). 대사를 넘기던 입력이 첫 선택지를 고르지 않게 합니다. " +
        "이 동안에도 선택 이동은 되고, 이 동안 누른 확정은 버립니다. 호출한 쪽이 따로 값을 주면(테라스) 그 값을 씁니다.")]
    [SerializeField, Min(0)] private float confirmGuardDuration = .5f;
    [Tooltip("마우스 휠로 한 칸 옮긴 뒤 다음 휠 입력을 받기까지의 시간(초, 실제 시간). 한 번 굴린 휠이 여러 칸을 건너뛰지 않게 합니다. " +
        "웹은 0.14초입니다. 키보드·패드 이동에는 쓰지 않습니다.")]
    [SerializeField, Min(0)] private float wheelStepInterval = .14f;
    private readonly List<WorldInteractionChoiceItem> rows = new();
    [NonSerialized] private NewStreetOptionData[] options;
    private string[] labels;
    private string[] lockReasons;
    private float[] widths;
    private Action<NewStreetOptionData> selected;
    private int selectedRow;
    private int visibleRows;
    private int openedFrame;
    private double confirmReadyAt;
    private double wheelReadyAt;
    // 마지막 선택 이동이 키보드·패드·휠이었는지. 멈춘 포인터 아래로 줄이 온 진입이 그 선택을 되돌리지 않게 한다.
    private bool movedByKeys;
    private int revision;
    private bool english;
    private bool rightSide = true;
    private bool collectedRows;
    // 코드가 덮어쓰기 전의 프리팹 배치. 오른쪽 배치는 그대로 쓰고 왼쪽 배치는 뒤집어 계산한다.
    private bool rememberedLayout;
    private Vector2 authoredKeyIconPosition;
    private Vector2 authoredPointerPosition;
    private float authoredMoneyWidth;
    private HorizontalAlignmentOptions authoredMoneyAlignment;
    private float listWidth;
    private float headerHeight;
    private float revealElapsed;
    private float cursorElapsed;
    private Vector2 cursorStart;
    private Vector2 cursorDestination;

    public RectTransform Rect => (RectTransform)transform;
    internal IReadOnlyList<WorldInteractionChoiceItem> Rows => rows;
    public bool IsOpen => selected != null && isActiveAndEnabled;
    public bool IsRevealing => IsOpen && revealElapsed < revealDuration;
    /// <summary>등장 직후 확정 보호 중인지. 펼침 연출과 별개의 시간이다.</summary>
    public bool IsConfirmGuarded => IsOpen && Time.realtimeSinceStartupAsDouble < confirmReadyAt;
    private bool CanConfirm => IsOpen && !IsRevealing && !IsConfirmGuarded && Time.frameCount != openedFrame;
    // 펼침/열린 프레임도 입력을 소비한다. 선택 콜백 전에 컨트롤러가 소비 프레임을 기록한다.
    public bool HandlesCancelInput => IsOpen && (IsRevealing || Time.frameCount == openedFrame ||
        options.Any(option => option.IsCancel == true));
    internal RectTransform RevealViewport => revealViewport;
    internal RectTransform SelectionCursor => selectionCursor;
    internal TMP_Text MoneyLabel => moneyLabel;
    internal Image KeyIcon => keyIcon;
    internal Image Pointer => pointer;
    /// <summary>열린 선택창 첫 항목의 높이(선택창 루트 단위, 루트 Scale 미포함). 닫혀 있으면 0.</summary>
    internal float FirstRowHeight
    {
        get
        {
            if (!IsOpen || visibleRows <= 0) return 0;
            var row = rows[0].Rect;
            return Mathf.Abs(Rect.InverseTransformVector(row.TransformVector(0, row.rect.height, 0)).y);
        }
    }
    public bool IsConfigured => ConfigurationError == null;
    public string ConfigurationError
    {
        get
        {
            if (itemPrefab == null) return $"{name}의 Item Prefab을 연결하세요.";
            if (!itemPrefab.IsConfigured) return itemPrefab.ConfigurationError;
            if (revealViewport == null || revealViewport.GetComponent<RectMask2D>() == null ||
                content == null || contentVisibility == null || itemsRoot == null)
                return $"{name}의 Reveal Viewport(Rect Mask 2D), Content, Content Visibility, Items Root를 연결하세요.";
            if (itemsLayout == null || itemsLayout.transform != itemsRoot || itemsRoot.GetComponent<ContentSizeFitter>() == null)
                return $"{name}의 Items Root에 Vertical Layout Group과 Content Size Fitter를 연결하세요.";
            if (selectionCursor == null || keyIcon == null || pointer == null || rightPointer == null)
                return $"{name}의 Selection Cursor와 Key Icon, Pointer, Right Pointer Sprite를 연결하세요.";
            if (keyIcon.transform.parent != selectionCursor || pointer.transform.parent != selectionCursor)
                return $"{name}의 Key Icon과 Pointer를 Selection Cursor 바로 아래에 두세요. 좌우 배치를 커서 칸 기준으로 뒤집습니다.";
            return moneyLabel == null ? $"{name}의 Money Label을 연결하세요." : null;
        }
    }

    /// <param name="confirmGuard">확정 보호 시간(초). 비우면 Confirm Guard Duration을 쓴다.</param>
    public void Show(NewStreetOptionData[] values, Action<NewStreetOptionData> callback, ELanguage language, int? money = null,
        float? confirmGuard = null)
    {
        Hide();
        if (values == null || values.Length == 0)
            throw new InvalidOperationException("표시할 선택지가 없습니다.");
        if (values.Length > MaximumChoices)
            throw new InvalidOperationException($"외부·홈 상호작용 선택지는 최대 {MaximumChoices}개입니다. 현재 {values.Length}개입니다.");
        if (values.Count(option => option.IsCancel == true) > 1)
            throw new InvalidOperationException("[WorldInteraction] 한 선택지 묶음에 취소 표시는 하나만 지정할 수 있습니다.");
        if (!IsConfigured) throw new InvalidOperationException(ConfigurationError);
        RememberAuthoredLayout();
        options = values.ToArray();
        selected = callback ?? throw new ArgumentNullException(nameof(callback));
        english = language == ELanguage.En;
        // 새 선택창마다 한 번만 시작한다. 거부 표시·좌우 전환은 보호 시간을 다시 시작하지 않는다.
        confirmReadyAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0, confirmGuard ?? confirmGuardDuration);
        // 웹처럼 새 선택창은 휠 간격을 새로 센다.
        wheelReadyAt = 0;
        MeasureRows();
        moneyLabel.gameObject.SetActive(showMoney && money.HasValue);
        moneyLabel.text = money.HasValue ? (english ? $"Money {money.Value:N0}" : $"소지금 {money.Value:N0}원") : "";
        headerHeight = moneyLabel.gameObject.activeSelf ? moneyHeaderHeight : 0;
        gameObject.SetActive(true);
        BuildRows();
    }

    // 코드가 배치를 바꾸기 전에 한 번만 읽는다. 프리팹(또는 씬 인스턴스)에서 고친 값이 실행 결과가 된다.
    private void RememberAuthoredLayout()
    {
        if (rememberedLayout) return;
        authoredKeyIconPosition = keyIcon.rectTransform.anchoredPosition;
        authoredPointerPosition = pointer.rectTransform.anchoredPosition;
        authoredMoneyWidth = moneyLabel.rectTransform.rect.width;
        authoredMoneyAlignment = moneyLabel.horizontalAlignment;
        rememberedLayout = true;
    }

    private void MeasureRows()
    {
        labels = new string[options.Length];
        lockReasons = new string[options.Length];
        widths = new float[options.Length];
        listWidth = 0;
        // 대화·동작 메뉴 모두 같은 규칙: 기준 폭은 줄 프리팹의 Width, 문구가 더 길 때만 최대 폭까지 늘린다.
        float authoredWidth = itemPrefab.Rect.rect.width;
        float widest = Mathf.Max(authoredWidth, maximumItemWidth);
        for (int i = 0; i < options.Length; i++)
        {
            labels[i] = WorldInteractionChoiceItem.SingleLine(Text(options[i].Text));
            lockReasons[i] = LockReason(options[i]);
            widths[i] = Mathf.Clamp(itemPrefab.MeasureWidth(labels[i], lockReasons[i]), authoredWidth, widest);
            listWidth = Mathf.Max(listWidth, widths[i]);
        }
        listWidth += itemsLayout.padding.horizontal;
    }

    private string LockReason(NewStreetOptionData option)
    {
        if (option.Selectable != false) return "";
        string reason = Text(option.LockReason);
        if (!string.IsNullOrWhiteSpace(reason)) return reason;
        return english ? "Unavailable." : "선택할 수 없습니다.";
    }

    private string Text(Texts value) => english && !string.IsNullOrEmpty(value?.En) ? value.En : value?.Ko ?? "";

    private void CollectRows()
    {
        if (collectedRows || itemsRoot == null) return;
        // 프리팹에 배치한 미리보기 항목을 재사용해 실행 중 중복 생성을 막음.
        foreach (Transform child in itemsRoot)
            if (child.TryGetComponent<WorldInteractionChoiceItem>(out var row)) rows.Add(row);
        collectedRows = true;
    }

    private void BuildRows()
    {
        ClearSelection();
        foreach (var row in rows) row.Clear();
        int version = ++revision;
        openedFrame = Time.frameCount;
        visibleRows = options.Length;
        while (rows.Count < visibleRows) rows.Add(Instantiate(itemPrefab, itemsRoot));
        for (int i = 0; i < visibleRows; i++)
        {
            int index = i;
            var row = rows[i];
            if (!row.IsConfigured) throw new InvalidOperationException(row.ConfigurationError);
            row.name = "Choice_" + options[index].Id;
            // 원문과 사유 중 긴 문구를 기준으로 같은 항목 폭을 확보한다.
            row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widths[index]);
            row.Bind(labels[index], options[index].Selectable != false,
                () => ConfirmChoice(index, version), pointerMoved => Hover(index, pointerMoved), lockReasons[index]);
            // 목록에서 높이를 읽기 전에 글자 칸의 레이아웃을 계산한다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.Rect);
        }
        itemsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, listWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
        Rect.sizeDelta = new Vector2(listWidth + cursorGutter, headerHeight + itemsRoot.rect.height);
        content.sizeDelta = Rect.rect.size;
        // 목록이 좁아도 소지금은 프리팹의 Money Text 폭 아래로 줄이지 않는다(한 줄 유지).
        moneyLabel.rectTransform.sizeDelta = new Vector2(Mathf.Max(listWidth, authoredMoneyWidth), moneyHeaderHeight);
        selectedRow = -1;
        movedByKeys = false;
        ApplySide();
        Move(1);
        revealElapsed = 0;
        UpdateReveal();
    }

    // E·Enter·Space·클릭 확정. 보호 시간 중 입력은 거부 연출 없이 버린다.
    private void ConfirmChoice(int index, int version)
    {
        if (CanConfirm) SelectChoice(index, version);
    }

    private void SelectChoice(int index, int version)
    {
        if (!IsOpen || IsRevealing || revision != version || Time.frameCount == openedFrame) return;
        var option = options[index];
        Focus(index);
        if (option.Selectable == false)
        {
            rows[index].Reject();
            return;
        }
        var callback = selected;
        // 다음 선택창이 열리기 전에 이전 콜백을 무효화함.
        Hide();
        callback(option);
    }

    // 펼침·확정 보호 중에도 이동은 받는다.
    public void MoveSelection(int direction)
    {
        if (!IsOpen || direction == 0) return;
        Move(direction > 0 ? 1 : -1);
        movedByKeys = true;
    }

    // 마우스 휠 이동. 한 번에 한 칸이고, Wheel Step Interval 안에 들어온 휠은 버린다(웹 world-choices.js wheel).
    public void ScrollSelection(int direction)
    {
        if (!IsOpen || direction == 0) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < wheelReadyAt) return;
        wheelReadyAt = now + wheelStepInterval;
        MoveSelection(direction);
    }

    // 펼침이 끝나거나 선택창이 움직여 멈춘 포인터 아래로 줄이 온 경우에는 키보드·휠로 고른 줄을 되돌리지 않는다
    // (웹 world-choices.js: 멈춘 포인터 아래의 다시 그리기가 키보드 선택을 되돌리면 안 됨).
    private void Hover(int index, bool pointerMoved)
    {
        if (!pointerMoved && movedByKeys) return;
        movedByKeys = false;
        Focus(index);
    }

    public void Confirm()
    {
        if (CanConfirm && selectedRow >= 0) rows[selectedRow].Confirm();
    }

    // 취소(Esc·패드 B)는 확정 보호 대상이 아니다. 펼침 중과 열린 프레임만 막는다.
    public void SelectCancelChoice()
    {
        if (!IsOpen) return;
        int index = Array.FindIndex(options, option => option.IsCancel == true);
        if (index >= 0) SelectChoice(index, revision);
    }

    private void Focus(int index)
    {
        if (!IsOpen || index < 0 || index >= visibleRows || !rows[index].CanSelect) return;
        // 같은 행의 마우스·키보드 포커스 알림이 거부 표시를 초기화하지 않게 한다.
        if (selectedRow == index) return;
        bool first = selectedRow < 0;
        if (!first) rows[selectedRow].SetFocused(false);
        selectedRow = index;
        rows[index].SetFocused(true);
        PositionCursor(first);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rows[index].gameObject);
    }

    private void Move(int direction)
    {
        for (int offset = 1; offset <= visibleRows; offset++)
        {
            int index = (selectedRow + direction * offset + visibleRows) % visibleRows;
            if (rows[index].CanSelect) { Focus(index); return; }
        }
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (IsRevealing)
        {
            revealElapsed += Time.unscaledDeltaTime;
            UpdateReveal();
        }
        if (cursorElapsed < cursorMoveDuration)
        {
            cursorElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(cursorElapsed / cursorMoveDuration);
            selectionCursor.anchoredPosition = Vector2.Lerp(cursorStart, cursorDestination, t * t * (3 - 2 * t));
        }

    }

    private void UpdateReveal()
    {
        float t = revealDuration <= 0 ? 1 : Mathf.Clamp01(revealElapsed / revealDuration);
        float amount = t >= 1 ? 1 : Mathf.Clamp01(revealEase.Evaluate(t));
        // 내용 크기는 유지하고 위쪽에 고정된 마스크의 높이만 늘림.
        revealViewport.sizeDelta = new Vector2(Rect.rect.width, Rect.rect.height * amount);
        contentVisibility.interactable = contentVisibility.blocksRaycasts = t >= 1;
    }

    public void SetSide(bool right)
    {
        if (rightSide == right) return;
        rightSide = right;
        if (IsOpen) ApplySide();
    }

    private void ApplySide()
    {
        itemsRoot.anchoredPosition = new Vector2(rightSide ? cursorGutter : 0, -headerHeight);
        // 웹(world-choices.css [data-side="left"] .bd-balance)처럼 왼쪽 배치에서는 소지금을 루나 쪽(줄 묶음 오른쪽 끝)에 붙인다.
        moneyLabel.rectTransform.anchoredPosition = new Vector2(
            rightSide ? cursorGutter : listWidth - moneyLabel.rectTransform.rect.width, 0);
        moneyLabel.horizontalAlignment = rightSide ? authoredMoneyAlignment : Mirror(authoredMoneyAlignment);
        itemsLayout.childAlignment = rightSide ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
        for (int i = 0; i < visibleRows; i++)
        {
            var row = rows[i];
            row.SetSide(rightSide);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
        // 키 그림·화살표: 오른쪽 배치는 프리팹 값 그대로, 왼쪽 배치는 커서 칸 안에서 좌우로 뒤집은 값.
        bool mirrorPointer = !rightSide && leftPointer == null;
        keyIcon.rectTransform.anchoredPosition = SidePosition(keyIcon.rectTransform, authoredKeyIconPosition, false);
        pointer.rectTransform.anchoredPosition = SidePosition(pointer.rectTransform, authoredPointerPosition, mirrorPointer);
        pointer.rectTransform.localScale = new Vector3(mirrorPointer ? -1 : 1, 1, 1);
        pointer.sprite = rightSide || leftPointer == null ? rightPointer : leftPointer;
        PositionCursor(true);
    }

    private static HorizontalAlignmentOptions Mirror(HorizontalAlignmentOptions value) => value switch
    {
        HorizontalAlignmentOptions.Left => HorizontalAlignmentOptions.Right,
        HorizontalAlignmentOptions.Right => HorizontalAlignmentOptions.Left,
        _ => value
    };

    /// <summary>
    /// 커서 칸(커서 기준점에서 0 ~ Cursor Gutter) 안에서 프리팹 배치를 좌우로 뒤집은 위치.
    /// 커서 기준점은 오른쪽 배치에서 선택창 바깥 끝, 왼쪽 배치에서 줄 묶음 끝에 놓이므로(PositionCursor)
    /// 결과는 선택창 전체를 좌우 반전한 모양이 되고, 줄과의 간격도 프리팹과 같다.
    /// </summary>
    /// <param name="flipped">Scale X -1로 뒤집어 그리는지. 이때는 피벗 반대쪽으로 펼쳐진다.</param>
    private Vector2 SidePosition(RectTransform part, Vector2 authored, bool flipped)
    {
        if (rightSide) return authored;
        // 커서 기준점에서 본 이 요소의 앵커 기준점(앵커·피벗 반영).
        float anchorX = (Mathf.Lerp(part.anchorMin.x, part.anchorMax.x, part.pivot.x) - selectionCursor.pivot.x)
            * selectionCursor.rect.width;
        float width = part.rect.width;
        float left = anchorX + authored.x - part.pivot.x * width;
        float mirroredLeft = cursorGutter - left - width;
        float pivotFromLeft = (flipped ? 1 - part.pivot.x : part.pivot.x) * width;
        return new Vector2(mirroredLeft + pivotFromLeft - anchorX, authored.y);
    }

    private void PositionCursor(bool immediate)
    {
        if (selectedRow < 0 || selectedRow >= visibleRows) return;
        var row = rows[selectedRow].Rect;
        cursorStart = selectionCursor.anchoredPosition;
        float centerY = content.InverseTransformPoint(row.TransformPoint(row.rect.center)).y;
        cursorDestination = new Vector2(rightSide ? 0 : listWidth,
            centerY);
        cursorElapsed = immediate ? cursorMoveDuration : 0;
        if (immediate || cursorMoveDuration <= 0) selectionCursor.anchoredPosition = cursorDestination;
    }

    private void ClearSelection()
    {
        var events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(transform))
            events.SetSelectedGameObject(null);
    }

    public void Hide()
    {
        CollectRows();
        revision++;
        selected = null;
        options = null;
        revealElapsed = revealDuration;
        cursorElapsed = cursorMoveDuration;
        ClearSelection();
        foreach (var row in rows) if (row != null) row.Clear();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        selected = null;
        revision++;
        revealElapsed = revealDuration;
        cursorElapsed = cursorMoveDuration;
        ClearSelection();
    }
}
