using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using VContainer;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

/// <summary>
/// 공용 설정의 음량·음소거, 그래픽 초안 적용, 타이틀 복귀를 제어한다.
/// 음향은 즉시 저장하고 그래픽은 Apply 버튼으로 확정한다.
/// </summary>
public class UIDisplayOptions : MonoBehaviour
{
    [Header("Screen UI References")]
    [SerializeField] private GameObject optionUI;
    [FormerlySerializedAs("fullscreenToggle")]
    [SerializeField] private Toggle windowedToggle;   // ON = 창모드
    [SerializeField] private TMP_Dropdown windowSizeDropdown;
    [SerializeField] private Toggle fillToggle, vSyncToggle, postProcessingToggle;
    [SerializeField] private TMP_Dropdown frameRateDropdown;

    [Header("Effect UI References")]
    [SerializeField] private TMP_Dropdown bloomDropdown;
    [SerializeField] private Toggle lightShaftToggle, characterLightingToggle, puddleReflectionToggle;

    [Header("Sound UI References")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Toggle bgmMuteToggle, seMuteToggle;

    [Inject] private IDisplaySettings _display;
    [Inject] private IAudioVolumeSettings _sound;
    [Inject] private ISceneTransitionService _transitions;

    public bool CanReturnToTitle => SceneManager.GetActiveScene().name != "Main" &&
        _transitions != null && !_transitions.IsBusy;

    static UIDisplayOptions activeOptions;
    GameObject previousSelection;
    int lastEscapeFrame = -1;
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
        _sound.SetBGMMuted(false);
        _sound.SetSEMuted(false);
        SyncAudioSliders();
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshValues();
    }
    public void ResetDisplayDraft()
    {
        windowedToggle.isOn = false;
        windowSizeDropdown.value = 0;
        windowSizeDropdown.interactable = false;
        fillToggle.SetIsOnWithoutNotify(true);
        vSyncToggle.SetIsOnWithoutNotify(true);
        postProcessingToggle.SetIsOnWithoutNotify(true);
        frameRateDropdown.SetValueWithoutNotify(1);
        frameRateDropdown.interactable = false;
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }
    public void ResetEffectsDraft()
    {
        ShowEffects(GraphicsEffectOptions.Values.Default);
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }
    void ShowEffects(GraphicsEffectOptions.Values values)
    {
        if (bloomDropdown) bloomDropdown.SetValueWithoutNotify((int)values.Bloom);
        if (lightShaftToggle) lightShaftToggle.SetIsOnWithoutNotify(values.LightShafts);
        if (characterLightingToggle) characterLightingToggle.SetIsOnWithoutNotify(values.CharacterLighting);
        if (puddleReflectionToggle) puddleReflectionToggle.SetIsOnWithoutNotify(values.PuddleReflections);
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
        if (bgmMuteToggle) bgmMuteToggle.SetIsOnWithoutNotify(_sound.IsBGMMuted);
        if (seMuteToggle) seMuteToggle.SetIsOnWithoutNotify(_sound.IsSEMuted);
    }

    private void OnDisable()
    {
        if (activeOptions == this) activeOptions = null;
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        HandleEscape();
    }

    public void HandleEscape()
    {
        // 패널과 컨트롤러가 같은 프레임의 Esc를 읽어도 한 번만 처리한다.
        if (lastEscapeFrame == Time.frameCount) return;
        lastEscapeFrame = Time.frameCount;
        var view = optionUI != null ? optionUI.GetComponentInChildren<OptionsPanelView>(true) : null;
        if (IsOptionOpen && view != null && view.TryCancelForEscape()) return;
        if (IsOptionOpen) { Hide(); return; }
        if (ServicePanelController.TryCloseForEscape()) return;
        if (WorldInteractionController.TryCancelForEscape()) return;
        if (!TitleMenuView.TryCloseForEscape()) Show();
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
        if (frameRateDropdown != null)
        {
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(new List<string> { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "제한 없음" });
        }
        if (bloomDropdown != null)
        {
            bloomDropdown.ClearOptions();
            bloomDropdown.AddOptions(new List<string>(GraphicsEffectOptions.BloomLabels));
        }
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
        if (fillToggle) fillToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("FillStretch", 1) == 1);
        bool vsync = PlayerPrefs.GetInt("VSync", 1) == 1;
        if (vSyncToggle) vSyncToggle.SetIsOnWithoutNotify(vsync);
        if (postProcessingToggle) postProcessingToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("PostProcessing", 1) == 1);
        if (frameRateDropdown)
        {
            frameRateDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt("FrameRateIndex", 1), 0, DisplaySettings.FrameRates.Length - 1));
            frameRateDropdown.interactable = !vsync;
        }
        ShowEffects(GraphicsEffectOptions.Current);
    }

    // 창모드 토글 OnValueChanged 에 연결
    public void OnWindowedToggled(bool windowedOn)
    {
        windowSizeDropdown.interactable = windowedOn;
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }

    public void OnApplyClicked()
    {
        if (_display == null) return;
        bool windowed = windowedToggle.isOn;
        int winIndex = windowSizeDropdown.value;

        _display.SetWindowedSize(winIndex);
        _display.SetFullscreen(!windowed);   // 토글 ON(창모드) = 전체화면 아님
        _display.SetFillMode(fillToggle.isOn);
        _display.SetVSync(vSyncToggle.isOn);
        _display.SetFrameRate(frameRateDropdown.value);
        _display.SetPostProcessing(postProcessingToggle.isOn);
        if (bloomDropdown == null) return;
        _display.SetEffects(new GraphicsEffectOptions.Values
        {
            Bloom = (GraphicsEffectOptions.BloomLevel)bloomDropdown.value,
            LightShafts = lightShaftToggle.isOn,
            CharacterLighting = characterLightingToggle.isOn,
            PuddleReflections = puddleReflectionToggle.isOn,
        });
    }

    public void OnVSyncToggled(bool enabled)
    {
        frameRateDropdown.interactable = !enabled;
        optionUI.GetComponentInChildren<OptionsPanelView>(true)?.RefreshNavigation();
    }

    public void OnBgmMuted(bool muted) => _sound?.SetBGMMuted(muted);
    public void OnSeMuted(bool muted) => _sound?.SetSEMuted(muted);

    public void ReturnToTitle() => ReturnToTitleAsync().Forget(Debug.LogException);

    async UniTask ReturnToTitleAsync()
    {
        if (!CanReturnToTitle) return;
        var request = _transitions.RequestLoadScene("Main", cancellationToken: this.GetCancellationTokenOnDestroy());
        if (!request.Accepted)
        {
            optionUI.GetComponentInChildren<OptionsPanelView>(true)?.ShowError("타이틀로 이동하지 못했습니다. 다시 시도해 주세요.");
            return;
        }
        Hide();
        var result = await request.Completion;
        if (!result.Succeeded)
        {
            Show();
            optionUI.GetComponentInChildren<OptionsPanelView>(true)?.ShowError("타이틀로 이동하지 못했습니다. 다시 시도해 주세요.");
        }
    }

    public void OnBgmSliderChanged(float value01) => _sound.SetBGMVolume(value01);
    public void OnSeSliderChanged(float value01) => _sound.SetSEVolume(value01);
}
