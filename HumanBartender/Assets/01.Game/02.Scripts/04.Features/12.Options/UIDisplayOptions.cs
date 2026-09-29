using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
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
    [Inject] private ISoundManager _sound;

    static UIDisplayOptions activeOptions;
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

    }

    private void OnEnable()
    {
        activeOptions = this;
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
        if (optionUI != null) optionUI.SetActive(!optionUI.activeSelf);
    }

    public void QuitGame()
    {
        Application.Quit();
    }


    private void BuildDropdownOptions()
    {
        windowSizeDropdown.ClearOptions();

        var labels = new List<string>();
        foreach (var s in _display.WindowedSizes)
            labels.Add($"{s.x} x {s.y}");

        windowSizeDropdown.AddOptions(labels);
    }

    private void SyncUIFromCurrentSettings()
    {
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
