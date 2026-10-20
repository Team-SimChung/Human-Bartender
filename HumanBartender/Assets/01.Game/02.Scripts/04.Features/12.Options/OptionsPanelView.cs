using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>공용 설정 창의 탭과 표시만 관리한다. 설정 저장·적용은 UIDisplayOptions가 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class OptionsPanelView : MonoBehaviour
{
    [SerializeField] UIDisplayOptions controller;
    [SerializeField] Button[] tabs;
    [SerializeField] GameObject[] pages;
    [SerializeField] GameObject[] tabIndicators;
    [SerializeField] Button closeButton, backButton, resetButton, applyButton;
    [SerializeField] Slider bgmSlider, seSlider;
    [SerializeField] TMP_Text bgmValue, seValue;
    int currentTab;
    readonly Color active = new Color32(70, 235, 241, 255);
    readonly Color inactive = new Color32(9, 21, 30, 255);

    void Awake()
    {
        for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].onClick.AddListener(() => SelectTab(index)); }
        closeButton.onClick.AddListener(controller.Hide);
        backButton.onClick.AddListener(controller.Hide);
        resetButton.onClick.AddListener(() => { if (currentTab == 0) controller.ResetSound(); else controller.ResetDisplayDraft(); });
        applyButton.onClick.AddListener(controller.OnApplyClicked);
        bgmSlider.onValueChanged.AddListener(_ => RefreshValues());
        seSlider.onValueChanged.AddListener(_ => RefreshValues());
    }
    void OnEnable() { SelectTab(0); RefreshValues(); }
    public void SelectTab(int index)
    {
        currentTab = Mathf.Clamp(index, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == currentTab);
            if (tabIndicators != null && i < tabIndicators.Length && tabIndicators[i] != null) tabIndicators[i].SetActive(i == currentTab);
            tabs[i].GetComponent<Image>().color = i == currentTab ? active : inactive;
            tabs[i].GetComponentInChildren<TMP_Text>().color = i == currentTab ? inactive : new Color32(157, 184, 195, 255);
        }
        applyButton.gameObject.SetActive(currentTab == 1);
        resetButton.gameObject.SetActive(currentTab != 2);
        if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(tabs[currentTab].gameObject);
        ConfigureNavigation();
        RefreshValues();
    }
    public void RefreshNavigation() => ConfigureNavigation();
    void ConfigureNavigation()
    {
        // 화면 밖의 게임 UI로 포커스가 빠지지 않도록 현재 탭의 활성 위젯 안에서 순환한다.
        var controls = GetComponentsInChildren<Selectable>().Where(s => s.IsInteractable()).ToArray();
        for (int i = 0; i < controls.Length; i++)
        {
            var navigation = controls[i].navigation; navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = controls[(i + controls.Length - 1) % controls.Length];
            navigation.selectOnDown = controls[(i + 1) % controls.Length];
            navigation.selectOnLeft = navigation.selectOnRight = null;
            controls[i].navigation = navigation;
        }
    }
    public void RefreshValues()
    {
        bgmValue.text = $"{Mathf.RoundToInt(bgmSlider.value * 100)}%";
        seValue.text = $"{Mathf.RoundToInt(seSlider.value * 100)}%";
    }
}
