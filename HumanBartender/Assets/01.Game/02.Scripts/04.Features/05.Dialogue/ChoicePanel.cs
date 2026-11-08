using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 대화 선택지 항목 하나를 나타내는 UI 패널.
/// 선택지 텍스트 설정, 패널 활성화/비활성화, 버튼 이벤트 초기화 기능을 제공한다.
/// </summary>
public class ChoicePanel : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI panelTMPText;
    [SerializeField] private Text panelText;
    [SerializeField] private Button button;
    [Header("선택 거부 표시")]
    [SerializeField, InspectorName("배경")] private Image background;
    [SerializeField, InspectorName("기본 배경색")] private Color normalBackgroundColor = Color.white;
    [SerializeField, InspectorName("거부 배경색")] private Color rejectedBackgroundColor = new Color32(239, 51, 64, 255);
    [SerializeField, InspectorName("거부 글자색")] private Color rejectedTextColor = new Color32(38, 8, 12, 255);
    [SerializeField, InspectorName("거부 글리치 설정")] private UIChoiceGlitchSettings rejectionGlitchSettings;

    private readonly ChoiceTextGlitch rejectionGlitch = new ChoiceTextGlitch();
    private Action selected;
    private Action<ChoicePanel> focused;
    private string originalText;
    private string rejectionReason;
    private bool canExecute;
    private bool rejected;
    private UIChoiceGlitch normalGlitch;
    private bool ownsExternalFeedback;
    private Color originalTextColor;

    internal bool IsRejected => rejected;
    internal TMP_Text Label => panelTMPText;
    internal Image Background => background;

    public void Bind(string text, bool executable, string reason, Action onSelected, Action<ChoicePanel> onFocused)
    {
        RestoreRejection();
        originalText = text;
        canExecute = executable;
        rejectionReason = reason;
        selected = onSelected;
        focused = onFocused;
        SetPanelText(text);
        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Confirm);
        if (button is ChoiceHoldButton hold) hold.SetHoldRequired(executable);
        if (background != null) background.color = normalBackgroundColor;
    }

    private void Confirm()
    {
        if (!isActiveAndEnabled || selected == null) return;
        focused?.Invoke(this);
        if (!canExecute)
        {
            Reject();
            return;
        }
        selected.Invoke();
    }

    private void Reject()
    {
        if (!rejected)
        {
            originalTextColor = panelTMPText != null ? panelTMPText.color : panelText != null ? panelText.color : Color.white;
            normalGlitch = GetComponent<UIChoiceGlitch>();
            ownsExternalFeedback = normalGlitch != null && normalGlitch.isActiveAndEnabled;
            // 화살표와 입력 포커스는 유지하고, 글자 색·재질만 거부 표시가 잠시 맡는다.
            if (ownsExternalFeedback) normalGlitch.SetExternalFeedback(true);
        }
        rejected = true;
        SetPanelText(rejectionReason);
        if (background != null) background.color = rejectedBackgroundColor;
        if (panelTMPText != null)
        {
            panelTMPText.color = rejectedTextColor;
            rejectionGlitch.Play(panelTMPText, rejectionGlitchSettings);
        }
        else if (panelText != null) panelText.color = rejectedTextColor;
    }

    public void RestoreRejection()
    {
        if (!rejected) return;
        rejected = false;
        rejectionGlitch.Stop();
        SetPanelText(originalText);
        if (background != null) background.color = normalBackgroundColor;
        if (panelTMPText != null) panelTMPText.color = originalTextColor;
        else if (panelText != null) panelText.color = originalTextColor;
        // 기존 글리치가 현재 포커스를 기준으로 정상 글자색과 폰트를 다시 적용한다.
        if (normalGlitch != null && ownsExternalFeedback) normalGlitch.SetExternalFeedback(false);
        ownsExternalFeedback = false;
    }

    public void OnPointerEnter(PointerEventData eventData) => focused?.Invoke(this);
    public void OnSelect(BaseEventData eventData) => focused?.Invoke(this);

    private void LateUpdate() => rejectionGlitch.Tick();
    private void OnDisable() => RestoreRejection();
    private void OnDestroy() => rejectionGlitch.Dispose();

    public void SetPanelText(string text)
    {
        if (panelTMPText != null && panelTMPText.gameObject.activeSelf) panelTMPText.text = text;
        else if (panelText != null) panelText.text = text;
    }

    public void SetActive(bool isOn)
    {
        panel.SetActive(isOn);
    }

    public void ResetPanel()
    {
        RestoreRejection();
        selected = null;
        focused = null;
        originalText = rejectionReason = null;
        if (panelText != null) panelText.text = "";
        if (panelTMPText != null) panelTMPText.text = "";
        if (panel != null) panel.SetActive(false);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (button is ChoiceHoldButton hold) hold.SetHoldRequired(true);
        }
    }
    public Button GetButton()
    {
        return button;
    }
}
