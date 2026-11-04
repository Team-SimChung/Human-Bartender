using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기록 열람 화면의 수명과 일시정지를 소유한다. 수집기는 이 화면과 별도로 살아 있다.</summary>
public sealed class DialogueHistoryScreen : MonoBehaviour
{
    [SerializeField] GameObject screen;
    [SerializeField] BarDialogueHistory history;
    [SerializeField] ScrollRect scroll;
    [SerializeField] DialogueHistoryEntryView entryPrefab;
    [SerializeField] GameObject emptyMessage;
    [SerializeField] Button closeButton;
    [SerializeField] Button latestButton;

    readonly List<DialogueHistoryEntryView> rows = new();
    float previousTimeScale;
    public bool CanOpen => isActiveAndEnabled && history != null && history.CanUseHistory;
    public bool IsOpen { get; private set; }
    public event Action OpenChanged;

    void Awake() => screen.SetActive(false);
    void OnEnable()
    {
        closeButton.onClick.AddListener(Close);
        latestButton.onClick.AddListener(ScrollToLatest);
    }
    void OnDisable()
    {
        Close();
        closeButton.onClick.RemoveListener(Close);
        latestButton.onClick.RemoveListener(ScrollToLatest);
    }

    void Update()
    {
        if (IsOpen && !CanOpen) Close();
    }

    // 메뉴를 거치지 않는 호출도 같은 허용 조건을 확인한 뒤 목록을 갱신한다.
    public void Open()
    {
        if (IsOpen || !CanOpen) return;
        var entries = history.Snapshot();
        for (int i = 0; i < entries.Length; i++)
        {
            if (i == rows.Count) rows.Add(Instantiate(entryPrefab, scroll.content, false));
            rows[i].gameObject.SetActive(true);
            rows[i].Show(entries[i]);
        }
        for (int i = entries.Length; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        emptyMessage.SetActive(entries.Length == 0);
        latestButton.interactable = entries.Length > 0;
        previousTimeScale = Time.timeScale;
        IsOpen = true;
        // 단골·튜토리얼 대화 중에만 열 수 있다. 타이핑과 대화 연출을 함께 멈춘다.
        Time.timeScale = 0f;
        screen.SetActive(true);
        ScrollToLatest();
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        screen.SetActive(false);
        scroll.StopMovement();
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
    }

    public void ScrollToLatest()
    {
        if (!IsOpen) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 0f;
    }
}
