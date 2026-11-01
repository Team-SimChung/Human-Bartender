using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>타이틀의 uGUI 표현. 새 게임·불러오기는 기존 MainManager의 진행 서비스 경로를 사용한다.</summary>
[DisallowMultipleComponent]
public sealed class TitleMenuView : MonoBehaviour
{
    [SerializeField] MainManager manager;
    [SerializeField] Button newGameButton, continueButton, settingsButton, quitButton, closeSlotsButton;
    [SerializeField] GameObject slotsPanel;
    [SerializeField] Button[] slotButtons;
    [SerializeField] TMP_Text[] slotLabels;
    [SerializeField] TMP_Text statusLabel;
    bool wasBusy;
    static TitleMenuView activeView;
    void OnEnable() => activeView = this;
    void OnDisable() { if (activeView == this) activeView = null; }
    public static bool TryCloseForEscape()
    {
        if (activeView == null || activeView.manager.IsStarting || !activeView.slotsPanel.activeSelf) return false;
        activeView.CloseSlots();
        return true;
    }

    void Awake()
    {
        newGameButton.onClick.AddListener(manager.TempStart);
        continueButton.onClick.AddListener(OpenSlots);
        settingsButton.onClick.AddListener(UIDisplayOptions.ShowActive);
        quitButton.onClick.AddListener(Application.Quit);
        closeSlotsButton.onClick.AddListener(CloseSlots);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            slotButtons[i].onClick.AddListener(() => manager.LoadSlot(slot));
        }
        slotsPanel.SetActive(false);
    }

    void Start() { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(newGameButton.gameObject); }

    void Update()
    {
        bool busy = manager.IsStarting;
        if (busy != wasBusy)
        {
            wasBusy = busy;
            SetMenuInteractable(!busy && !slotsPanel.activeSelf);
            closeSlotsButton.interactable = !busy;
            if (!busy && slotsPanel.activeSelf) RefreshSlots();
            else if (busy) foreach (var button in slotButtons) button.interactable = false;
        }
        statusLabel.text = busy ? "불러오는 중…" : manager.StartError ?? "";

    }

    void SetMenuInteractable(bool enabled)
    {
        newGameButton.interactable = continueButton.interactable = settingsButton.interactable = quitButton.interactable = enabled;
    }

    void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            manager.DescribeSlot(i + 1, out string description, out bool canLoad);
            slotLabels[i].text = $"{i + 1:00}  {description}";
            slotButtons[i].interactable = canLoad && !manager.IsStarting;
        }
    }
    void OpenSlots()
    {
        RefreshSlots();
        slotsPanel.SetActive(true);
        SetMenuInteractable(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeSlotsButton.gameObject);
    }
    void CloseSlots()
    {
        slotsPanel.SetActive(false);
        SetMenuInteractable(!manager.IsStarting);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }
}
