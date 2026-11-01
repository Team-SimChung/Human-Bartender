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
    [SerializeField, Min(20)] private float minimumItemWidth = 72;
    [SerializeField, Min(20)] private float maximumItemWidth = 138;
    [SerializeField, Min(0)] private float cursorGutter = 22;
    [SerializeField] private bool showMoney = true;
    [SerializeField, Min(0)] private float moneyHeaderHeight = 13;
    [Header("Animation")]
    [Tooltip("위에서 아래로 펼쳐지는 시간(초). 0이면 즉시 표시합니다.")]
    [SerializeField, Min(0)] private float revealDuration = .22f;
    [SerializeField] private AnimationCurve revealEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, Min(0)] private float cursorMoveDuration = .08f;
    private readonly List<WorldInteractionChoiceItem> rows = new();
    [NonSerialized] private NewStreetOptionData[] options;
    private string[] labels;
    private string[] lockReasons;
    private float[] widths;
    private Action<NewStreetOptionData> selected;
    private int selectedRow;
    private int visibleRows;
    private int openedFrame;
    private int revision;
    private bool english;
    private bool rightSide = true;
    private bool collectedRows;
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
    internal RectTransform RevealViewport => revealViewport;
    internal RectTransform SelectionCursor => selectionCursor;
    internal TMP_Text MoneyLabel => moneyLabel;
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
            return moneyLabel == null ? $"{name}의 Money Label을 연결하세요." : null;
        }
    }

    public void Show(NewStreetOptionData[] values, Action<NewStreetOptionData> callback, ELanguage language, int? money = null,
        bool fitWidthToText = false)
    {
        Hide();
        if (values == null || values.Length == 0 || !values.Any(option => option.Selectable != false))
            throw new InvalidOperationException("표시할 선택 가능한 항목이 없습니다.");
        if (values.Length > MaximumChoices)
            throw new InvalidOperationException($"외부·홈 상호작용 선택지는 최대 {MaximumChoices}개입니다. 현재 {values.Length}개입니다.");
        if (!IsConfigured) throw new InvalidOperationException(ConfigurationError);
        options = values.ToArray();
        selected = callback ?? throw new ArgumentNullException(nameof(callback));
        english = language == ELanguage.En;
        MeasureRows(fitWidthToText);
        moneyLabel.gameObject.SetActive(showMoney && money.HasValue);
        moneyLabel.text = money.HasValue ? (english ? $"Money {money.Value:N0}" : $"소지금 {money.Value:N0}원") : "";
        headerHeight = moneyLabel.gameObject.activeSelf ? moneyHeaderHeight : 0;
        gameObject.SetActive(true);
        BuildRows();
    }

    private void MeasureRows(bool fitWidthToText)
    {
        labels = new string[options.Length];
        lockReasons = new string[options.Length];
        widths = new float[options.Length];
        listWidth = 0;
        for (int i = 0; i < options.Length; i++)
        {
            labels[i] = WorldInteractionChoiceItem.SingleLine(Text(options[i].Text));
            lockReasons[i] = LockReason(options[i]);
            widths[i] = Mathf.Clamp(itemPrefab.MeasureWidth(labels[i], lockReasons[i]),
                fitWidthToText ? 0 : minimumItemWidth, Mathf.Max(minimumItemWidth, maximumItemWidth));
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
            // 항목 내부 레이아웃은 잠금 사유를, 목록 레이아웃은 항목 위치를 담당함.
            row.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widths[index]);
            row.Bind(labels[index], options[index].Selectable != false,
                () => SelectChoice(index, version), () => Focus(index), lockReasons[index]);
            // 목록에서 높이를 읽기 전에 잠금 사유의 레이아웃을 계산함.
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.Rect);
        }
        itemsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, listWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
        Rect.sizeDelta = new Vector2(listWidth + cursorGutter, headerHeight + itemsRoot.rect.height);
        content.sizeDelta = Rect.rect.size;
        moneyLabel.rectTransform.sizeDelta = new Vector2(listWidth, moneyHeaderHeight);
        selectedRow = -1;
        ApplySide();
        Move(1);
        revealElapsed = 0;
        UpdateReveal();
    }

    private void SelectChoice(int index, int version)
    {
        if (!IsOpen || IsRevealing || revision != version || Time.frameCount == openedFrame) return;
        var option = options[index];
        if (option.Selectable == false) return;
        var callback = selected;
        // 다음 선택창이 열리기 전에 이전 콜백을 무효화함.
        Hide();
        callback(option);
    }

    public void MoveSelection(int direction)
    {
        if (!IsOpen || IsRevealing || direction == 0) return;
        Move(direction > 0 ? 1 : -1);
    }

    public void Confirm()
    {
        if (IsOpen && !IsRevealing && selectedRow >= 0 && Time.frameCount != openedFrame)
            rows[selectedRow].Confirm();
    }

    private void Focus(int index)
    {
        if (!IsOpen || index < 0 || index >= visibleRows || !rows[index].CanSelect) return;
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
        moneyLabel.rectTransform.anchoredPosition = new Vector2(rightSide ? cursorGutter : 0, 0);
        itemsLayout.childAlignment = rightSide ? TextAnchor.UpperLeft : TextAnchor.UpperRight;
        for (int i = 0; i < visibleRows; i++)
        {
            var row = rows[i];
            row.SetSide(rightSide);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsRoot);
        keyIcon.rectTransform.anchoredPosition = new Vector2(rightSide ? 0 : 2, 0);
        bool mirrorPointer = !rightSide && leftPointer == null;
        pointer.rectTransform.anchoredPosition = new Vector2(rightSide ? 19 : mirrorPointer ? 3 : 0, 0);
        pointer.rectTransform.localScale = new Vector3(mirrorPointer ? -1 : 1, 1, 1);
        pointer.sprite = rightSide || leftPointer == null ? rightPointer : leftPointer;
        PositionCursor(true);
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
