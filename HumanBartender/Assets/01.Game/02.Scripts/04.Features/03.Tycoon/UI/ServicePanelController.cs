using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>바 서비스 메뉴와 하위 화면을 관리한다.</summary>
public class ServicePanelController : MonoBehaviour
{
    [Header("Service menu")]
    [SerializeField] private GameObject overlay;
    [Tooltip("ServicePanelOverlay 안의 ServicePanel. 레시피 화면에서는 메뉴를 숨긴다.")]
    [SerializeField] private GameObject serviceMenuPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button cocktailRecipeButton;
    [SerializeField] private Button salesStatusButton;
    [SerializeField] private SalesStatusScreen salesStatusScreen;
    [SerializeField] private Button dialogueHistoryButton;
    [SerializeField] private DialogueHistoryScreen historyScreen;
    [SerializeField] private Button regularGuestInfoButton;
    [SerializeField] private ProfileScreen profileScreen;
    [Tooltip("지금은 열기 조건에 쓰지 않는다. 좌석을 옮기는 동안에도 메뉴 버튼과 좌석 표시를 유지하고 메뉴를 열 수 있다(엔진 수정 08, 웹 focusSeat과 같음). 씬 연결을 보존하려고 남겨 둔다.")]
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
        (recipeScreen != null && recipeScreen.IsOpen) || (profileScreen != null && profileScreen.IsOpen) || IsSalesStatusOpen;
    private bool IsSalesStatusOpen => salesStatusScreen != null && salesStatusScreen.IsOpen;
    public Transform ToggleControl => toggleButton != null ? toggleButton.transform : null;
    /// <summary>
    /// 영업 중 손님 쪽 조작(좌석 이동·메뉴 열기)과 좌석 표시가 가능한가. 카메라가 좌석을 옮기는 중에도 참이다.
    /// 이동 중에 막아야 하는 조작(코스터·잔 놓기, 되묻기)은 각자 PlayCamera.IsMoving을 따로 본다.
    /// </summary>
    public bool CanUseGuestActions => !menuOpen && playPhaseController != null &&
        playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) && CanOpen();

    public bool IsBlockingTycoonClock
    {
        get { return menuOpen || IsSalesStatusOpen || (profileScreen != null && profileScreen.IsOpen) || (historyScreen != null && historyScreen.IsOpen) || (recipeScreen != null && recipeScreen.IsOpen &&
                     playPhaseController != null && playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)); }
    }

    public static bool TryCloseForEscape()
    {
        if (activePanel == null || !activePanel.BlocksBarInput) return false;
        if (activePanel.profileScreen != null && activePanel.profileScreen.IsOpen)
            activePanel.profileScreen.Back();
        else if (activePanel.historyScreen != null && activePanel.historyScreen.IsOpen)
            activePanel.historyScreen.Close();
        else if (activePanel.IsSalesStatusOpen)
            activePanel.salesStatusScreen.Close();
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
        if (salesStatusButton != null) salesStatusButton.onClick.AddListener(OpenSalesStatus);
        if (dialogueHistoryButton != null) dialogueHistoryButton.onClick.AddListener(OpenHistory);
        if (regularGuestInfoButton != null) regularGuestInfoButton.onClick.AddListener(OpenProfiles);
        if (profileScreen != null) profileScreen.OpenChanged += OnScreenChanged;
        if (historyScreen != null) historyScreen.OpenChanged += OnScreenChanged;
        if (recipeScreen != null) recipeScreen.OpenChanged += OnScreenChanged;
        if (salesStatusScreen != null) salesStatusScreen.OpenChanged += OnScreenChanged;
    }

    void OnDisable()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (cocktailRecipeButton != null) cocktailRecipeButton.onClick.RemoveListener(OpenRecipes);
        if (salesStatusButton != null) salesStatusButton.onClick.RemoveListener(OpenSalesStatus);
        if (dialogueHistoryButton != null) dialogueHistoryButton.onClick.RemoveListener(OpenHistory);
        if (regularGuestInfoButton != null) regularGuestInfoButton.onClick.RemoveListener(OpenProfiles);
        if (profileScreen != null)
        {
            profileScreen.OpenChanged -= OnScreenChanged;
            profileScreen.Close();
        }
        if (historyScreen != null)
        {
            historyScreen.OpenChanged -= OnScreenChanged;
            historyScreen.Close();
        }
        if (recipeScreen != null) recipeScreen.OpenChanged -= OnScreenChanged;
        if (salesStatusScreen != null)
        {
            salesStatusScreen.OpenChanged -= OnScreenChanged;
            salesStatusScreen.Close();
        }
        Close();
        if (activePanel == this) activePanel = null;
    }

    void Update()
    {
        bool canOpen = CanOpen();
        if (cocktailRecipeButton != null) cocktailRecipeButton.interactable = CanOpenRecipes;
        if (salesStatusButton != null) salesStatusButton.interactable = salesStatusScreen != null && salesStatusScreen.CanOpen;
        if (dialogueHistoryButton != null) dialogueHistoryButton.interactable = canOpen && CanOpenHistory;
        if (regularGuestInfoButton != null) regularGuestInfoButton.interactable = canOpen && profileScreen != null && profileScreen.CanOpen;
        if (menuOpen && !canOpen) Close();
        if (toggleButton != null)
        {
            toggleButton.gameObject.SetActive(menuOpen || canOpen);
            toggleButton.interactable = menuOpen || canOpen;
        }
    }

    bool CanOpen()
    {
        if (profileScreen != null && profileScreen.IsOpen) return false;
        if (historyScreen != null && historyScreen.IsOpen) return false;
        if (IsSalesStatusOpen) return false;
        if (recipeScreen != null && recipeScreen.IsOpen) return false;
        if (playPhaseController == null) return false;
        bool storyMenu = playPhaseController.CanReceiveInput(EPlayPhase.Dialogue) && storyRunner != null &&
            (storyRunner.IsPresentingLine || storyRunner.CanStartCraft);
        if (!storyMenu && !playPhaseController.CanReceiveInput(EPlayPhase.Tycoon)) return false;
        if (craftFlow != null && (craftFlow.IsBusy || craftFlow.IsCraftFlowActive || craftFlow.IsAwaitingServeDecision)) return false;
        // 좌석 이동 중에는 막지 않는다(엔진 수정 08). 코스터·잔을 들고 있을 때만 막는다.
        if (CoasterDragItem.Active != null || DrinkDragItem.Active != null) return false;
        return !UIDisplayOptions.IsOptionOpen;
    }

    bool CanOpenRecipes => playPhaseController != null &&
        (playPhaseController.CanReceiveInput(EPlayPhase.Tycoon) ||
         (playPhaseController.CanReceiveInput(EPlayPhase.Dialogue) && storyRunner != null && storyRunner.CanStartCraft));

    bool CanOpenHistory => historyScreen != null && historyScreen.CanOpen;

    public void Toggle()
    {
        if (profileScreen != null && profileScreen.IsOpen) { profileScreen.Back(); return; }
        if (historyScreen != null && historyScreen.IsOpen) { historyScreen.Close(); return; }
        if (IsSalesStatusOpen) { salesStatusScreen.Close(); return; }
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

    private void OpenSalesStatus()
    {
        if (!menuOpen || !CanOpen() || salesStatusScreen == null) return;
        if (salesStatusScreen.TryOpen()) SetMenuOpen(false);
    }

    public void OpenProfiles()
    {
        if (!menuOpen || !CanOpen() || profileScreen == null || !profileScreen.CanOpen) return;
        profileScreen.Open();
        if (profileScreen.IsOpen) SetMenuOpen(false);
    }

    public void Close()
    {
        SetMenuOpen(false);
    }

    /// <summary>정산 등 바 조작이 끝나는 국면에서 열린 하위 화면까지 닫는다.</summary>
    public void CloseAllScreens()
    {
        if (profileScreen != null) profileScreen.Close();
        if (historyScreen != null) historyScreen.Close();
        if (salesStatusScreen != null) salesStatusScreen.Close();
        if (recipeScreen != null) recipeScreen.Close();
        Close();
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
