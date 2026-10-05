using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대화 선택지 항목 하나를 나타내는 UI 패널.
/// 선택지 텍스트 설정, 패널 활성화/비활성화, 버튼 이벤트 초기화 기능을 제공한다.
/// </summary>
public class ChoicePanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI panelTMPText;
    [SerializeField] Text panelText;
    [SerializeField] Button button;

    public void SetPanelText(string text)
    {
        panelText.text = text;
    }

    public void SetActive(bool isOn)
    {
        panel.SetActive(isOn);
    }

    public void ResetPanel()
    {
        if (panelText != null) panelText.text = "";
        if (panelTMPText != null) panelTMPText.text = "";
        if (panel != null) panel.SetActive(false);
        if (button != null) button.onClick.RemoveAllListeners();
    }
    public Button GetButton()
    {
        return button;
    }
}
