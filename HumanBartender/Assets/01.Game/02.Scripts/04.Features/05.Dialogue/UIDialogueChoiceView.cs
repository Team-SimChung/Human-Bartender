using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 선택지 UI 패널들을 관리한다. 조건 판정은 부르는 쪽이 끝내고 넘긴다 —
/// 2부는 when DSL(IConditionUtil)로, 실외는 스텝의 options를 그대로 그린다.
/// </summary>
public class UIDialogueChoiceView : MonoBehaviour
{
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private List<ChoicePanel> choicePanels = new();
    [Inject] private ISoundEffectPlayer sound;

    private void ConfigureHover(ChoicePanel panel)
    {
        var button = panel.GetButton();
        if (!button.TryGetComponent<UISelectionAudio>(out var feedback))
            feedback = button.gameObject.AddComponent<UISelectionAudio>();
        feedback.Configure(sound, DialoguePresentationSettings.Shared?.ChoiceHoverSoundKey);
    }

    private void PlayConfirm()
    {
        string key = DialoguePresentationSettings.Shared?.ChoiceConfirmSoundKey;
        if (!string.IsNullOrWhiteSpace(key)) sound?.PlaySE(key);
    }


    // 실행 중 선택 콜백만 쓰는 임시 상태다. 도메인 리로드 때 Unity가 이 재귀적인
    // result_steps/options 그래프를 백업하면 직렬화 깊이 초과와 오브젝트 로드 경고가 난다.
    [NonSerialized] private NewStreetOptionData[] curOutsideOptions;

    // GameStateManager의 언어 설정을 실시간 참조
    public ELanguage CurrentLanguage => GameStateManager.Instance != null
        ? GameStateManager.Instance.Language
        : ELanguage.Ko;

    /// <summary>
    /// [신규] Step 기반 아웃사이드 선택지(NewStreetOptionData[]) 패널 표시 및 이벤트 등록
    /// </summary>
    public void ShowOutsideChoice(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected)
    {
        if (options == null || options.Length == 0) return;

        Debug.Log("아웃사이드 선택지 UI 표시 중...");

        choicesPanel.SetActive(true);
        curOutsideOptions = options;

        for (int i = 0; i < choicePanels.Count; i++)
        {
            choicePanels[i].ResetPanel();
            choicePanels[i].GetButton().interactable = true;
            choicePanels[i].SetActive(false);
        }

        int visibleIndex = 0;
        for (int i = 0; i < curOutsideOptions.Length; i++)
        {
            if (visibleIndex >= choicePanels.Count)
            {
                Debug.LogWarning("[UIDialogueChoiceView] 표시할 선택지 수가 choicePanels 슬롯 수를 초과했습니다.");
                break;
            }

            var option = curOutsideOptions[i];

            // LocalizedText? 또는 Texts? 타입 파싱 처리
            string optionText = GetText(option.Text);

            var choicePanel = choicePanels[visibleIndex];
            ConfigureHover(choicePanel);
            choicePanel.Bind(optionText, option.Selectable != false, Reason(GetText(option.LockReason)),
                () => SelectOutsideChoice(option, onSelected), Focus);
            choicePanel.SetActive(true);
            visibleIndex++;
        }
    }

    /// <summary>
    /// 원문과 조건 판정 결과를 표시한다. 불가 항목도 가리킬 수 있으며, 확정하면 사유만 표시한다.
    /// </summary>
    /// <param name="texts">조건 충족 여부와 관계없이 원래 선택지 문구를 받는다.</param>
    /// <param name="selectable">실제 결과를 실행할 수 있는지. texts와 같은 길이여야 한다.</param>
    /// <param name="onSelected">고른 칸의 자리 번호를 받는다.</param>
    /// <param name="lockReasons">CSV에서 읽은 실행 불가 사유. 비어 있으면 공통 문구를 쓴다.</param>
    public void ShowChoice(IReadOnlyList<string> texts, IReadOnlyList<bool> selectable, Action<int> onSelected,
        IReadOnlyList<string> lockReasons = null)
    {
        if (texts == null || selectable == null || texts.Count != selectable.Count ||
            (lockReasons != null && lockReasons.Count != texts.Count))
        {
            Debug.LogError("[ChoiceView] 문구와 선택 가능 여부의 개수가 다릅니다.");
            return;
        }

        if (texts.Count > choicePanels.Count && choicePanels.Count > 1)
        {
            ShowChoicePage(texts, selectable, lockReasons, onSelected, 0);
            return;
        }

        // 재사용하는 항목의 거부 상태·홀드 진행·콜백을 함께 정리한다.
        for (int i = 0; i < choicePanels.Count; i++)
        {
            choicePanels[i].ResetPanel();
            choicePanels[i].GetButton().interactable = true;
        }

        if (texts.Count > choicePanels.Count)
            Debug.LogError($"[ChoiceView] 선택지가 {texts.Count}개인데 칸은 {choicePanels.Count}개뿐입니다.");

        int shown = Mathf.Min(texts.Count, choicePanels.Count);

        for (int i = 0; i < shown; i++)
        {
            ChoicePanel panel = choicePanels[i];

            ConfigureHover(panel);
            int index = i;
            panel.Bind(texts[i], selectable[i], Reason(lockReasons?[i]),
                () => SelectChoice(index, onSelected), Focus);
            panel.SetActive(true);
        }

        choicesPanel.SetActive(true);
    }

    private void ShowChoicePage(IReadOnlyList<string> texts, IReadOnlyList<bool> selectable,
        IReadOnlyList<string> lockReasons, Action<int> onSelected, int page)
    {
        int size = choicePanels.Count - 1;
        int pageCount = (texts.Count + size - 1) / size;
        int start = page * size;
        int count = Mathf.Min(size, texts.Count - start);
        var labels = new List<string>();
        var enabled = new List<bool>();
        var reasons = new List<string>();
        for (int i = 0; i < count; i++)
        {
            labels.Add(texts[start + i]);
            enabled.Add(selectable[start + i]);
            reasons.Add(lockReasons?[start + i]);
        }
        labels.Add($"다음 선택지 ▶ ({(page + 1) % pageCount + 1}/{pageCount})");
        enabled.Add(true);
        reasons.Add(null);
        var selection = new PageSelection(this, texts, selectable, lockReasons, onSelected, page, start, count, pageCount);
        ShowChoice(labels, enabled, selection.Select, reasons);
    }

    private void Focus(ChoicePanel selectedPanel)
    {
        foreach (var item in choicePanels)
            if (item != selectedPanel) item.RestoreRejection();
    }

    private string Reason(string reason) => !string.IsNullOrWhiteSpace(reason) ? reason
        : CurrentLanguage == ELanguage.En ? "Unavailable." : "선택할 수 없습니다.";

    private void SelectChoice(int index, Action<int> onSelected)
    {
        PlayConfirm();
        CloseChoices();
        onSelected?.Invoke(index);
    }

    private void SelectOutsideChoice(NewStreetOptionData option, Action<NewStreetOptionData> onSelected)
    {
        PlayConfirm();
        CloseChoices();
        onSelected?.Invoke(option);
    }

    /// <summary>페이지 이동용 항목은 선택 결과 번호에 섞지 않고 원래 선택지 번호를 전달한다.</summary>
    private sealed class PageSelection
    {
        private readonly UIDialogueChoiceView owner;
        private readonly IReadOnlyList<string> texts;
        private readonly IReadOnlyList<bool> selectable;
        private readonly IReadOnlyList<string> reasons;
        private readonly Action<int> selected;
        private readonly int page;
        private readonly int start;
        private readonly int count;
        private readonly int pageCount;

        public PageSelection(UIDialogueChoiceView owner, IReadOnlyList<string> texts, IReadOnlyList<bool> selectable,
            IReadOnlyList<string> reasons, Action<int> selected, int page, int start, int count, int pageCount)
        {
            this.owner = owner;
            this.texts = texts;
            this.selectable = selectable;
            this.reasons = reasons;
            this.selected = selected;
            this.page = page;
            this.start = start;
            this.count = count;
            this.pageCount = pageCount;
        }

        public void Select(int index)
        {
            if (index == count) owner.ShowChoicePage(texts, selectable, reasons, selected, (page + 1) % pageCount);
            else selected?.Invoke(start + index);
        }
    }

    /// <summary>선택지를 닫고 칸을 비운다.</summary>
    public void CloseChoices()
    {
        if (choicesPanel != null) choicesPanel.SetActive(false);
        curOutsideOptions = null;
        foreach (var panel in choicePanels)
        {
            if (panel == null) continue;
            panel.ResetPanel();
            if (panel.GetButton() != null) panel.GetButton().interactable = true;
        }
    }

    public void ChoiceSelect(int num)
    {
        choicesPanel.SetActive(false);

        for (int i = 0; i < choicePanels.Count; i++)
            choicePanels[i].ResetPanel();
    }

    private void OutsideChoiceSelect()
    {
        choicesPanel.SetActive(false);

        for (int i = 0; i < choicePanels.Count; i++)
            choicePanels[i].ResetPanel();
    }

    // LocalizedText? 지원
    private string GetText(LocalizedText? textData)
    {
        if (!textData.HasValue) return string.Empty;

        var text = textData.Value;
        return CurrentLanguage switch
        {
            ELanguage.En => !string.IsNullOrEmpty(text.En) ? text.En : text.Ko,
            _ => text.Ko
        };
    }

    // Texts? 지원 오버로드
    private string GetText(Texts textData)
    {
        if (textData == null) return string.Empty;

        return CurrentLanguage switch
        {
            ELanguage.En => !string.IsNullOrEmpty(textData.En) ? textData.En : textData.Ko,
            _ => textData.Ko
        };
    }
}
