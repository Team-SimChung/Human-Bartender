using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>서비스 메뉴와 레시피 브라우저 중 현재 화면을 한 상태로 관리한다.</summary>
public class ServicePanelController : MonoBehaviour
{
    private enum PanelMode { Closed, Menu, Recipes }

    [Header("Service menu")]
    [SerializeField] private GameObject overlay;
    [Tooltip("ServicePanelOverlay 안의 ServicePanel. 레시피 화면에서는 메뉴를 숨긴다.")]
    [SerializeField] private GameObject serviceMenuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cocktailRecipeButton;

    [Header("Recipe browser")]
    [SerializeField] private GameObject leftSlideCanvas;
    [SerializeField] private CocktailRecipeBrowser recipeBrowser;
    [SerializeField] private Button recipeCloseButton;

    [Header("Availability")]
    [SerializeField] private PlayPhaseController playPhaseController;
    [SerializeField] private CraftFlowController craftFlow;

    private static ServicePanelController activePanel;
    private PanelMode mode;
    public event Action OpenChanged;

    public bool IsOpen
    {
        get { return mode != PanelMode.Closed; }
    }

    public static bool TryCloseForEscape()
    {
        if (activePanel == null || !activePanel.IsOpen) return false;
        activePanel.Close();
        return true;
    }

    void Awake()
    {
        if (overlay != null) overlay.SetActive(false);
        if (serviceMenuPanel != null) serviceMenuPanel.SetActive(false);
        if (recipeBrowser != null) recipeBrowser.Close();
        if (toggleButton != null) toggleButton.gameObject.SetActive(false);
        mode = PanelMode.Closed;
    }

    void OnEnable()
    {
        activePanel = this;
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.AddListener(OpenRecipes);
        if (recipeCloseButton != null) recipeCloseButton.onClick.AddListener(Close);
        if (recipeBrowser != null) recipeBrowser.CraftStarted += Close;
    }

    void OnDisable()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.RemoveListener(OpenRecipes);
        if (recipeCloseButton != null) recipeCloseButton.onClick.RemoveListener(Close);
        if (recipeBrowser != null) recipeBrowser.CraftStarted -= Close;
        Close();
        if (activePanel == this) activePanel = null;
    }

    void Update()
    {
        bool canOpen = CanOpen();
        if (IsOpen && !canOpen) Close();
        if (toggleButton != null)
        {
            toggleButton.gameObject.SetActive(mode != PanelMode.Recipes && (IsOpen || canOpen));
            toggleButton.interactable = IsOpen || canOpen;
        }
    }

    bool CanOpen()
    {
        if (mode != PanelMode.Recipes && leftSlideCanvas != null && leftSlideCanvas.activeInHierarchy) return false;
        if (playPhaseController == null ||
            (!playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) &&
             !playPhaseController.CanReceiveInput(EPlayPhase.Dialogue))) return false;
        if (craftFlow != null && (craftFlow.IsBusy || craftFlow.IsCraftFlowActive)) return false;
        return !UIDisplayOptions.IsOptionOpen;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!CanOpen() || overlay == null) return;
        SetMode(PanelMode.Menu);
    }

    public void OpenRecipes()
    {
        if (mode != PanelMode.Menu || !CanOpen()) return;
        if (recipeBrowser == null || leftSlideCanvas == null)
        {
            Debug.LogError("[ServicePanel] Left Slide Panel Canvas와 Recipe Browser 참조를 연결해 주세요.", this);
            return;
        }

        SetMode(PanelMode.Recipes);
    }

    public void Close()
    {
        SetMode(PanelMode.Closed);
    }

    private void SetMode(PanelMode next)
    {
        if (mode == next) return;

        PanelMode previous = mode;
        mode = next;
        if (overlay != null) overlay.SetActive(mode == PanelMode.Menu);
        if (serviceMenuPanel != null) serviceMenuPanel.SetActive(mode == PanelMode.Menu);
        if (leftSlideCanvas != null && (previous == PanelMode.Recipes || mode == PanelMode.Recipes))
            leftSlideCanvas.SetActive(mode == PanelMode.Recipes);
        if (recipeBrowser != null)
        {
            if (mode == PanelMode.Recipes) recipeBrowser.Open();
            else recipeBrowser.Close();
        }
        if (toggleButton != null) toggleButton.gameObject.SetActive(mode != PanelMode.Recipes && (IsOpen || CanOpen()));

        OpenChanged?.Invoke();
    }
}
