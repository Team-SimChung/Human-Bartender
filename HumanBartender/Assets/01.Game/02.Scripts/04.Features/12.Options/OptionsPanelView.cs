using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    [SerializeField] Button titleButton, confirmTitleButton, cancelTitleButton;
    [SerializeField] GameObject titleConfirmation;
    [SerializeField] Button[] bindingButtons;
    [SerializeField] TMP_Text controlsHint;
    [SerializeField] TMP_Text statusMessage;
    [SerializeField] ScrollRect bindingsScroll;
    [SerializeField] CanvasGroup inputGroup;
    public const int SoundTab = 0, GraphicsTab = 1, EffectsTab = 2, ControlsTab = 3;
    int currentTab;
    int rebindingIndex = -1, rebindStartedFrame;
    GameObject lastSelected;
    GameObject rebindSelection;
    int selectionAfterFrame;
    public const string DefaultControlsHint = "버튼을 누른 뒤 새 키를 입력하세요. Esc: 취소 · 방향키 / Enter / 마우스 / 패드는 기존 조작 유지";
    readonly Color active = new Color32(70, 235, 241, 255);
    readonly Color inactive = new Color32(9, 21, 30, 255);

    void Awake()
    {
        for (int i = 0; i < tabs.Length; i++) { int index = i; tabs[i].onClick.AddListener(() => SelectTab(index)); }
        closeButton.onClick.AddListener(controller.Hide);
        backButton.onClick.AddListener(controller.Hide);
        resetButton.onClick.AddListener(() =>
        {
            if (currentTab == SoundTab) controller.ResetSound();
            else if (currentTab == GraphicsTab) controller.ResetDisplayDraft();
            else if (currentTab == EffectsTab) controller.ResetEffectsDraft();
            else { GameKeyBindings.Reset(); RefreshBindings(); ShowMessage("조작키를 기본값으로 되돌렸습니다."); }
        });
        applyButton.onClick.AddListener(controller.OnApplyClicked);
        bgmSlider.onValueChanged.AddListener(_ => RefreshValues());
        seSlider.onValueChanged.AddListener(_ => RefreshValues());
        titleButton.onClick.AddListener(() =>
        {
            if (!controller.CanReturnToTitle) return;
            titleConfirmation.SetActive(true);
            ConfigureNavigation();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancelTitleButton.gameObject);
        });
        cancelTitleButton.onClick.AddListener(CloseTitleConfirmation);
        confirmTitleButton.onClick.AddListener(() => { CloseTitleConfirmation(); controller.ReturnToTitle(); });
        for (int i = 0; i < bindingButtons.Length; i++)
        {
            int index = i;
            bindingButtons[i].onClick.AddListener(() => BeginRebind(index));
        }
    }
    void OnEnable()
    {
        titleConfirmation.SetActive(false);
        if (statusMessage != null) statusMessage.text = "";
        if (titleButton.interactable != controller.CanReturnToTitle)
        {
            titleButton.interactable = controller.CanReturnToTitle;
            ConfigureNavigation();
        }
        ShowMessage(DefaultControlsHint);
        SelectTab(0);
        RefreshValues();
        RefreshBindings();
    }
    void OnDisable() { CancelRebind(); rebindSelection = null; }
    void OnApplicationFocus(bool focused)
    {
        if (!focused && rebindingIndex >= 0) { CancelRebind(); ShowMessage("키 변경을 취소했습니다."); }
    }
    void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { controller.HandleEscape(); return; }
        if (titleButton.interactable != controller.CanReturnToTitle)
        {
            titleButton.interactable = controller.CanReturnToTitle;
            ConfigureNavigation();
        }
        if (rebindSelection != null && Time.frameCount > selectionAfterFrame)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(rebindSelection);
            rebindSelection = null;
        }
        if (rebindingIndex < 0 || Time.frameCount == rebindStartedFrame || Keyboard.current == null) return;
        foreach (var key in Keyboard.current.allKeys)
        {
            if (!key.wasPressedThisFrame || key.keyCode == Key.Escape) continue;
            if (GameKeyBindings.TrySetKey(GameKeyBindings.Bindings[rebindingIndex].Control, key.keyCode, out string message))
            {
                CancelRebind();
                RefreshBindings();
            }
            ShowMessage(message);
            break;
        }
    }
    void LateUpdate()
    {
        if (bindingsScroll == null || currentTab != ControlsTab || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == lastSelected) return;
        lastSelected = selected;
        int index = System.Array.FindIndex(bindingButtons, button => button.gameObject == selected);
        if (index < 0) return;
        Canvas.ForceUpdateCanvases();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bindingsScroll.viewport, bindingButtons[index].transform);
        var position = bindingsScroll.content.anchoredPosition;
        if (bounds.min.y < bindingsScroll.viewport.rect.yMin) position.y += bindingsScroll.viewport.rect.yMin - bounds.min.y;
        else if (bounds.max.y > bindingsScroll.viewport.rect.yMax) position.y -= bounds.max.y - bindingsScroll.viewport.rect.yMax;
        bindingsScroll.content.anchoredPosition = position;
    }
    void BeginRebind(int index)
    {
        rebindingIndex = index;
        rebindStartedFrame = Time.frameCount;
        inputGroup.interactable = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        bindingButtons[index].GetComponentInChildren<TMP_Text>().text = "키 입력 대기…";
        ShowMessage($"‘{GameKeyBindings.Bindings[index].Label}’의 새 키를 누르세요. Esc로 취소합니다.");
    }
    void CancelRebind()
    {
        if (rebindingIndex >= 0)
        {
            rebindSelection = bindingButtons[rebindingIndex].gameObject;
            selectionAfterFrame = Time.frameCount;
        }
        rebindingIndex = -1;
        if (inputGroup != null) inputGroup.interactable = true;
        RefreshBindings();
    }
    void RefreshBindings()
    {
        if (bindingButtons == null) return;
        for (int i = 0; i < bindingButtons.Length; i++)
            bindingButtons[i].GetComponentInChildren<TMP_Text>().text = GameKeyBindings.DisplayName(GameKeyBindings.Bindings[i].Control);
    }
    public void ShowMessage(string message) { if (controlsHint != null) controlsHint.text = message; }
    public void ShowError(string message) { if (statusMessage != null) statusMessage.text = message; }
    public bool TryCancelForEscape()
    {
        if (rebindingIndex >= 0) { CancelRebind(); ShowMessage("키 변경을 취소했습니다."); return true; }
        if (titleConfirmation.activeSelf) { CloseTitleConfirmation(); return true; }
        return false;
    }
    void CloseTitleConfirmation()
    {
        titleConfirmation.SetActive(false);
        ConfigureNavigation();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(titleButton.gameObject);
    }
    public void SelectTab(int index)
    {
        CancelRebind();
        currentTab = Mathf.Clamp(index, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == currentTab);
            if (tabIndicators != null && i < tabIndicators.Length && tabIndicators[i] != null) tabIndicators[i].SetActive(i == currentTab);
            tabs[i].GetComponent<Image>().color = i == currentTab ? active : inactive;
            tabs[i].GetComponentInChildren<TMP_Text>().color = i == currentTab ? inactive : new Color32(157, 184, 195, 255);
        }
        // 그래픽과 효과 탭은 같은 초안을 공유하고 '적용'으로 함께 저장한다.
        applyButton.gameObject.SetActive(currentTab == GraphicsTab || currentTab == EffectsTab);
        resetButton.gameObject.SetActive(true);
        if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(tabs[currentTab].gameObject);
        ConfigureNavigation();
        RefreshValues();
    }
    public void RefreshNavigation() => ConfigureNavigation();
    void ConfigureNavigation()
    {
        // 화면 밖의 게임 UI로 포커스가 빠지지 않도록 현재 탭의 활성 위젯 안에서 순환한다.
        var root = titleConfirmation != null && titleConfirmation.activeSelf ? titleConfirmation.transform : transform;
        var controls = root.GetComponentsInChildren<Selectable>().Where(s => s.IsInteractable()).ToArray();
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
