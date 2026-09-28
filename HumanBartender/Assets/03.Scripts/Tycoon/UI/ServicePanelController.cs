using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>타이쿤 서비스 메뉴를 관리하고 레시피 화면으로 이동한다.</summary>
public class ServicePanelController : MonoBehaviour
{
    [Header("Service menu")]
    [SerializeField] private GameObject overlay;
    [Tooltip("ServicePanelOverlay 안의 ServicePanel. 레시피 화면에서는 메뉴를 숨긴다.")]
    [SerializeField] private GameObject serviceMenuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cocktailRecipeButton;

    [Header("Recipe browser")]
    [SerializeField] private RecipeBrowserScreen recipeScreen;

    [Header("Availability")]
    [SerializeField] private PlayPhaseController playPhaseController;
    [SerializeField] private CraftFlowController craftFlow;

    private static ServicePanelController activePanel;
    private bool menuOpen;
    public event Action OpenChanged;

    public bool IsMenuOpen { get { return menuOpen; } }

    public bool IsBlockingTycoonClock
    {
        get { return menuOpen || (recipeScreen != null && recipeScreen.IsOpen &&
                     playPhaseController != null && playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)); }
    }

    public static bool TryCloseForEscape()
    {
        if (activePanel == null || !activePanel.IsBlockingTycoonClock) return false;
        if (activePanel.recipeScreen != null && activePanel.recipeScreen.IsOpen)
            activePanel.recipeScreen.Close();
        else
            activePanel.Close();
        return true;
    }

    void Awake()
    {
        if (overlay != null) overlay.SetActive(false);
        if (serviceMenuPanel != null) serviceMenuPanel.SetActive(false);
        if (toggleButton != null) toggleButton.gameObject.SetActive(false);
        menuOpen = false;
    }

    void OnEnable()
    {
        activePanel = this;
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.AddListener(OpenRecipes);
        if (recipeScreen != null) recipeScreen.OpenChanged += OnRecipeScreenChanged;
    }

    void OnDisable()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.RemoveListener(OpenRecipes);
        if (recipeScreen != null) recipeScreen.OpenChanged -= OnRecipeScreenChanged;
        Close();
        if (activePanel == this) activePanel = null;
    }

    void Update()
    {
        bool canOpen = CanOpen();
        if (menuOpen && !canOpen) Close();
        if (toggleButton != null)
        {
            toggleButton.gameObject.SetActive(menuOpen || canOpen);
            toggleButton.interactable = menuOpen || canOpen;
        }
    }

    bool CanOpen()
    {
        if (recipeScreen != null && recipeScreen.IsOpen) return false;
        if (playPhaseController == null || !playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)) return false;
        if (craftFlow != null && (craftFlow.IsBusy || craftFlow.IsCraftFlowActive)) return false;
        return !UIDisplayOptions.IsOptionOpen;
    }

    public void Toggle()
    {
        if (menuOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!CanOpen() || overlay == null) return;
        SetMenuOpen(true);
    }

    public void OpenRecipes()
    {
        if (!menuOpen || !CanOpen()) return;
        if (recipeScreen == null)
        {
            Debug.LogError("[ServicePanel] Recipe Browser Screen 참조를 연결해 주세요.", this);
            return;
        }

        SetMenuOpen(false);
        recipeScreen.OpenForTycoon();
    }

    public void Close()
    {
        SetMenuOpen(false);
    }

    private void SetMenuOpen(bool next)
    {
        if (menuOpen == next) return;
        menuOpen = next;
        if (overlay != null) overlay.SetActive(menuOpen);
        if (serviceMenuPanel != null) serviceMenuPanel.SetActive(menuOpen);
        if (toggleButton != null) toggleButton.gameObject.SetActive(menuOpen || CanOpen());

        OpenChanged?.Invoke();
    }

    private void OnRecipeScreenChanged()
    {
        OpenChanged?.Invoke();
    }
}
