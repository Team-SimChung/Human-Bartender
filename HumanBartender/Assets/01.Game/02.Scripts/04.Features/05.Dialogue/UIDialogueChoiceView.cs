using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택지 UI 패널들을 관리한다. 조건 판정은 부르는 쪽이 끝내고 넘긴다 —
/// 2부는 when DSL(IConditionUtil)로, 실외는 스텝의 options를 그대로 그린다.
/// </summary>
public class UIDialogueChoiceView : MonoBehaviour
{
    [SerializeField] private GameObject choicesPanel;
    [SerializeField] private List<ChoicePanel> choicePanels = new();


    // 실행 중 선택 콜백만 쓰는 임시 상태다. 도메인 리로드 때 Unity가 이 재귀적인
    // result_steps/options 그래프를 백업하면 직렬화 깊이 초과와 오브젝트 로드 경고가 난다.
    [NonSerialized] private NewStreetOptionData[] curOutsideOptions;

    // GameStateManager의 언어 설정을 실시간 참조
    public ELanguage CurrentLanguage => GameStateManager.Instance != null
        ? GameStateManager.Instance.Language
        : ELanguage.Ko;

    void Start()
    {

    }

    void Update()
    {

    }

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

            int num = i;
            choicePanels[visibleIndex].SetPanelText(optionText);

            var button = choicePanels[visibleIndex].GetButton();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                var selected = option;
                CloseChoices();
                onSelected?.Invoke(selected);
            });

            choicePanels[visibleIndex].SetActive(true);
            visibleIndex++;
        }
    }

    /// <summary>
    /// 선택지를 띄우되 고를 수 없는 항목을 감추지 않고 비활성으로 남긴다.
    ///
    /// 조건에 걸린 항목을 감추지 않고 회색으로 남긴다. 2부 대본은 그것을
    /// 회색으로 두고 왜 못 고르는지를 대신 보여 준다(2부 운영 명세 §12.4). 무엇을 놓쳤는지 보이지
    /// 않으면 조건이 없는 것과 같기 때문이다.
    ///
    /// 조건 판정은 하지 않는다. 부르는 쪽이 이미 끝낸 결과(selectable)와 대신 적을 문구(texts)만 받는다 —
    /// 판정 방식이 서로 다르고(등급 기반 vs when DSL) 그 차이를 이 뷰가 알 이유가 없다.
    /// </summary>
    /// <param name="texts">칸에 적을 문구. 고를 수 없는 칸에는 그 이유를 넣어 보낸다.</param>
    /// <param name="selectable">칸을 누를 수 있는지. texts와 같은 길이여야 한다.</param>
    /// <param name="onSelected">고른 칸의 자리 번호를 받는다.</param>
    public void ShowChoice(IReadOnlyList<string> texts, IReadOnlyList<bool> selectable, Action<int> onSelected)
    {
        if (texts == null || selectable == null || texts.Count != selectable.Count)
        {
            Debug.LogError("[ChoiceView] 문구와 선택 가능 여부의 개수가 다릅니다.");
            return;
        }

        // ResetPanel은 문구와 리스너만 지운다. interactable은 그대로 남아서, 여기서 회색으로 둔 칸이
        // 다음에 다른 경로(ShowChoice/ShowOutsideChoice)로 열릴 때까지 눌리지 않는 채로 남는다.
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

            panel.SetPanelText(texts[i]);
            panel.SetActive(true);

            // 비활성 칸은 보이되 눌리지 않는다. 회색 처리는 Button이 하는 색 전이에 맡긴다.
            panel.GetButton().interactable = selectable[i];

            if (!selectable[i]) continue;

            int index = i;
            panel.GetButton().onClick.AddListener(() =>
            {
                CloseChoices();
                onSelected?.Invoke(index);
            });
        }

        choicesPanel.SetActive(true);
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
