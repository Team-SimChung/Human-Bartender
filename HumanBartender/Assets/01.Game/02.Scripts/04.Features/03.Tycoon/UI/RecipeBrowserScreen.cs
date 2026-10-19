using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>레시피 화면의 표시 상태를 소유한다. 타이쿤 메뉴와 스토리 대본이 같은 화면을 사용한다.</summary>
public sealed class RecipeBrowserScreen : MonoBehaviour
{
    [SerializeField] private GameObject leftSlideCanvas;
    [SerializeField] private CocktailRecipeBrowser browser;
    [SerializeField] private Button closeButton;
    [SerializeField] private PlayPhaseController playPhaseController;

    private bool storyOrderRequired;
    private EPlayPhase openedPhase;

    public bool IsOpen { get; private set; }
    public event Action OpenChanged;

    private void Awake()
    {
        if (leftSlideCanvas != null) leftSlideCanvas.SetActive(false);
    }

    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (browser != null) browser.CraftStarted += CloseAfterCraftStarted;
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (browser != null) browser.CraftStarted -= CloseAfterCraftStarted;
        ForceClose();
    }

    private void Update()
    {
        if (IsOpen && (playPhaseController == null ||
                       !playPhaseController.CanReceiveInput(openedPhase)))
            ForceClose();
    }

    public void OpenForTycoon()
    {
        Open(false);
    }

    public void OpenForStoryOrder()
    {
        Open(true);
    }

    private void Open(bool required)
    {
        EPlayPhase phase = required ? EPlayPhase.Dialogue : EPlayPhase.Tycoon;
        if (playPhaseController == null || !playPhaseController.CanReceiveInput(phase)) return;
        if (leftSlideCanvas == null || browser == null || closeButton == null)
        {
            Debug.LogError("[RecipeBrowserScreen] 레시피 캔버스, 브라우저, 닫기 버튼을 연결해 주세요.", this);
            return;
        }

        storyOrderRequired = required;
        openedPhase = phase;
        IsOpen = true;
        closeButton.interactable = !required;
        leftSlideCanvas.SetActive(true);
        browser.Open();
        OpenChanged?.Invoke();
    }

    public void Close()
    {
        if (storyOrderRequired) return;
        ForceClose();
    }

    public void CloseAfterCraftStarted()
    {
        ForceClose();
    }

    public void ForceClose()
    {
        bool wasOpen = IsOpen;
        IsOpen = false;
        storyOrderRequired = false;
        openedPhase = EPlayPhase.None;
        if (closeButton != null) closeButton.interactable = true;
        if (browser != null) browser.Close();
        if (leftSlideCanvas != null) leftSlideCanvas.SetActive(false);
        if (wasOpen) OpenChanged?.Invoke();
    }
}
