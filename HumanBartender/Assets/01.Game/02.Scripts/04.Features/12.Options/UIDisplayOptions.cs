using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using VContainer;

/// <summary>
/// 화면 옵션(창모드 토글, 해상도 드롭다운) 및 사운드 옵션(BGM/SE 슬라이더) UI 컴포넌트.
/// Esc 키로 옵션 패널 토글, Apply 버튼으로 설정을 적용한다.
/// </summary>
public class UIDisplayOptions : MonoBehaviour
{
    [Header("Screen UI References")]
    [SerializeField] private GameObject optionUI;
    [FormerlySerializedAs("fullscreenToggle")]
    [SerializeField] private Toggle windowedToggle;   // ON = 창모드
    [SerializeField] private TMP_Dropdown windowSizeDropdown;

    [Header("Sound UI References")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;

    [Inject] private IDisplaySettings _display;
    [Inject] private IAudioVolumeSettings _sound;

    static UIDisplayOptions activeOptions;
    GameObject previousSelection;
    public static void ShowActive() { if (activeOptions != null) activeOptions.Show(); }
    public void Show()
    {
        if (optionUI == null || optionUI.activeSelf) return;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        SyncUIFromCurrentSettings();
        SyncAudioSliders();
        optionUI.SetActive(true);
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshValues();
    }
    public void Hide()
    {
        if (optionUI != null) optionUI.SetActive(false);
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
    }
    public void ResetSound()
    {
        if (_sound == null) return;
        _sound.SetBGMVolume(.3f);
        _sound.SetSEVolume(.8f);
        SyncAudioSliders();
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshValues();
    }
    public void ResetDisplayDraft()
    {
        windowedToggle.isOn = false;
        windowSizeDropdown.value = 0;
        windowSizeDropdown.interactable = false;
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }
    public static bool IsOptionOpen
    {
        get
        {
            return activeOptions != null && activeOptions.optionUI != null && activeOptions.optionUI.activeInHierarchy;
        }
    }

    private void Start()
    {
        BuildDropdownOptions();
        SyncUIFromCurrentSettings();
        SyncAudioSliders();

    }

    private void OnEnable()
    {
        activeOptions = this;
        SyncAudioSliders();
    }

    void SyncAudioSliders()
    {
        if (_sound == null) return;
        if (bgmSlider) bgmSlider.SetValueWithoutNotify(_sound.GetBGMVolume());
        if (seSlider) seSlider.SetValueWithoutNotify(_sound.GetSEVolume());
    }

    private void OnDisable()
    {
        if (activeOptions == this) activeOptions = null;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (ServicePanelController.TryCloseForEscape()) return;
        if (WorldInteractionController.TryCancelForEscape()) return;
        if (IsOptionOpen) Hide();
        else if (!TitleMenuView.TryCloseForEscape()) Show();
    }

    public void QuitGame()
    {
        Application.Quit();
    }


    private void BuildDropdownOptions()
    {
        if (_display == null || windowSizeDropdown == null) return;
        windowSizeDropdown.ClearOptions();

        var labels = new List<string>();
        foreach (var s in _display.WindowedSizes)
            labels.Add($"{s.x} x {s.y}");

        windowSizeDropdown.AddOptions(labels);
    }

    private void SyncUIFromCurrentSettings()
    {
        if (_display == null || windowSizeDropdown == null || windowedToggle == null) return;
        // 기본값 전체화면(1) → 창모드 토글은 off
        bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        bool windowed = !fullscreen;
        int winIndex = PlayerPrefs.GetInt("WinIndex", 0);

        windowedToggle.SetIsOnWithoutNotify(windowed);
        windowSizeDropdown.SetValueWithoutNotify(Mathf.Clamp(winIndex, 0, _display.WindowedSizes.Length - 1));
        windowSizeDropdown.interactable = windowed;   // 창모드일 때만 크기 선택
    }

    // 창모드 토글 OnValueChanged 에 연결
    public void OnWindowedToggled(bool windowedOn)
    {
        windowSizeDropdown.interactable = windowedOn;
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }

    public void OnApplyClicked()
    {
        bool windowed = windowedToggle.isOn;
        int winIndex = windowSizeDropdown.value;

        _display.SetWindowedSize(winIndex);
        _display.SetFullscreen(!windowed);   // 토글 ON(창모드) = 전체화면 아님
    }

    public void OnBgmSliderChanged(float value01) => _sound.SetBGMVolume(value01);
    public void OnSeSliderChanged(float value01) => _sound.SetSEVolume(value01);
}
