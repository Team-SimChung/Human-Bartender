using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>바 서비스 메뉴를 관리하고 레시피·대화 기록 화면으로 이동한다.</summary>
public class ServicePanelController : MonoBehaviour
{
    [Header("Service menu")]
    [SerializeField] private GameObject overlay;
    [Tooltip("ServicePanelOverlay 안의 ServicePanel. 레시피 화면에서는 메뉴를 숨긴다.")]
    [SerializeField] private GameObject serviceMenuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cocktailRecipeButton;
    [SerializeField] private Button dialogueHistoryButton;
    [SerializeField] private DialogueHistoryScreen historyScreen;
    [SerializeField] private PlayCamera playCamera;

    [Header("Recipe browser")]
    [SerializeField] private RecipeBrowserScreen recipeScreen;

    [Header("Availability")]
    [SerializeField] private PlayPhaseController playPhaseController;
    [SerializeField] private CraftFlowController craftFlow;
    [SerializeField] private StoryScriptRunner storyRunner;

    private static ServicePanelController activePanel;
    private bool menuOpen;
    public event Action OpenChanged;

    public bool IsMenuOpen { get { return menuOpen; } }
    public bool BlocksBarInput => menuOpen || (historyScreen != null && historyScreen.IsOpen) ||
        (recipeScreen != null && recipeScreen.IsOpen);
    public Transform ToggleControl => toggleButton != null ? toggleButton.transform : null;
    public bool CanUseGuestActions => !menuOpen && playPhaseController != null &&
        playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) && CanOpen();

    public bool IsBlockingTycoonClock
    {
        get { return menuOpen || (historyScreen != null && historyScreen.IsOpen) || (recipeScreen != null && recipeScreen.IsOpen &&
                     playPhaseController != null && playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)); }
    }

    public static bool TryCloseForEscape()
    {
        if (activePanel == null || !activePanel.BlocksBarInput) return false;
        if (activePanel.historyScreen != null && activePanel.historyScreen.IsOpen)
            activePanel.historyScreen.Close();
        else if (activePanel.recipeScreen != null && activePanel.recipeScreen.IsOpen)
            activePanel.recipeScreen.Close();
        else
            activePanel.Close();
        return true;
    }

    void Awake()
    {
        if (storyRunner == null)
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                storyRunner = root.GetComponentInChildren<StoryScriptRunner>(true);
                if (storyRunner != null) break;
            }
        if (overlay != null) overlay.SetActive(false);
        if (serviceMenuPanel != null) serviceMenuPanel.SetActive(false);
        if (toggleButton != null) toggleButton.gameObject.SetActive(false);
        if (dialogueHistoryButton != null) dialogueHistoryButton.interactable = false;
        menuOpen = false;
    }

    void OnEnable()
    {
        activePanel = this;
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.AddListener(OpenRecipes);
        if (dialogueHistoryButton != null) dialogueHistoryButton.onClick.AddListener(OpenHistory);
        if (historyScreen != null) historyScreen.OpenChanged += OnScreenChanged;
        if (recipeScreen != null) recipeScreen.OpenChanged += OnScreenChanged;
    }

    void OnDisable()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.RemoveListener(OpenRecipes);
        if (dialogueHistoryButton != null) dialogueHistoryButton.onClick.RemoveListener(OpenHistory);
        if (historyScreen != null)
        {
            historyScreen.OpenChanged -= OnScreenChanged;
            historyScreen.Close();
        }
        if (recipeScreen != null) recipeScreen.OpenChanged -= OnScreenChanged;
        Close();
        if (activePanel == this) activePanel = null;
    }

    void Update()
    {
        bool canOpen = CanOpen();
        if (cocktailRecipeButton != null) cocktailRecipeButton.interactable = CanOpenRecipes;
        if (dialogueHistoryButton != null) dialogueHistoryButton.interactable = canOpen && CanOpenHistory;
        if (menuOpen && !canOpen) Close();
        if (toggleButton != null)
        {
            toggleButton.gameObject.SetActive(menuOpen || canOpen);
            toggleButton.interactable = menuOpen || canOpen;
        }
    }

    bool CanOpen()
    {
        if (historyScreen != null && historyScreen.IsOpen) return false;
        if (recipeScreen != null && recipeScreen.IsOpen) return false;
        if (playPhaseController == null) return false;
        bool storyMenu = playPhaseController.CanReceiveInput(EPlayPhase.Dialogue) && storyRunner != null &&
            (storyRunner.IsPresentingLine || storyRunner.CanStartCraft);
        if (!storyMenu && !playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)) return false;
        if (craftFlow != null && (craftFlow.IsBusy || craftFlow.IsCraftFlowActive || craftFlow.IsAwaitingServeDecision)) return false;
        if ((playCamera != null && playCamera.IsMoving) || CoasterDragItem.Active != null || DrinkDragItem.Active != null) return false;
        return !UIDisplayOptions.IsOptionOpen;
    }

    bool CanOpenRecipes => playPhaseController != null &&
        (playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) ||
         (playPhaseController.CanReceiveInput(EPlayPhase.Dialogue) && storyRunner != null && storyRunner.CanStartCraft));

    bool CanOpenHistory => historyScreen != null && historyScreen.CanOpen;

    public void Toggle()
    {
        if (historyScreen != null && historyScreen.IsOpen) { historyScreen.Close(); return; }
        if (recipeScreen != null && recipeScreen.IsOpen) { recipeScreen.Close(); return; }
        if (menuOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (!CanOpen()) return;
        if (overlay != null) SetMenuOpen(true);
    }

    public void OpenRecipes()
    {
        if (!menuOpen || !CanOpen() || !CanOpenRecipes) return;
        if (recipeScreen == null)
        {
            Debug.LogError("[ServicePanel] Recipe Browser Screen 참조를 연결해 주세요.", this);
            return;
        }

        SetMenuOpen(false);
        if (playPhaseController.CanReceiveInput(EPlayPhase.Dialogue)) storyRunner.TryOpenCraftMenu();
        else recipeScreen.OpenForTycoon();
    }

    public void OpenHistory()
    {
        if (!menuOpen || !CanOpen() || !CanOpenHistory) return;
        historyScreen.Open();
        SetMenuOpen(false);
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
        if (dialogueHistoryButton != null) dialogueHistoryButton.interactable = CanOpenHistory;
        if (toggleButton != null) toggleButton.gameObject.SetActive(menuOpen || CanOpen());

        OpenChanged?.Invoke();
    }

    private void OnScreenChanged()
    {
        OpenChanged?.Invoke();
    }
}
