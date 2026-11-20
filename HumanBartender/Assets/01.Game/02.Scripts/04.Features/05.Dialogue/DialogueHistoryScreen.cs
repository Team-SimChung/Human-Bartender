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
    IDisposable pauseHandle;
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
        // 일시정지 전에 사본을 뜬다. 목록은 여는 순간의 기록이고, 현재 줄은 그때까지 보인 글자만 담는다.
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
        pauseHandle = GamePause.Acquire();
        IsOpen = true;
        // 단골·튜토리얼 대화 중이거나 영업 중에 열 수 있다. 게임 시간을 멈춰 타이핑·말풍선·손님 대기 시간을 세우고,
        // 현재 문장을 끝까지 공개하거나 다음 줄로 넘기지 않는다.
        screen.SetActive(true);
        ScrollToLatest();
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
    }

    public void Close()
    {
        pauseHandle?.Dispose();
        pauseHandle = null;
        if (!IsOpen) return;
        IsOpen = false;
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
