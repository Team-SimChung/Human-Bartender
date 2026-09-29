using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>아코디언 안의 한 줄을 표현하는 데이터.</summary>
public struct SettlementLine
{
    public Sprite icon;     // 아이콘 (없으면 null)
    public string name;     // 표시 이름
    public int qty;         // 수량 (0 이하면 "×n" 숨김 — 팁 행 등)
    public string valueStr; // 우측 금액 문자열

    public SettlementLine(Sprite icon, string name, int qty, string valueStr)
    {
        this.icon = icon; this.name = name; this.qty = qty; this.valueStr = valueStr;
    }
}


/// <summary>정산 아코디언 안의 한 줄(아이콘/이름/수량/금액)을 표시하는 UI 컴포넌트.</summary>
public class SettlementRow : MonoBehaviour
{
    [SerializeField] private RectTransform rowRect;
    [SerializeField] private Vector2 rectSize;
    [SerializeField] private Image iconImage;   // 선택
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text qtyText;  // "×3" (선택)
    [SerializeField] private TMP_Text valueText;

    /// <summary>한 줄의 데이터를 UI에 반영한다. 아이콘/수량은 값이 없으면 오브젝트 자체를 숨긴다.</summary>
    public void Set(SettlementLine line)
    {
        rowRect.sizeDelta = new Vector2(rectSize.x, rectSize.y);

        if (iconImage != null)
        {
            bool has = line.icon != null;
            iconImage.gameObject.SetActive(has);
            if (has) iconImage.sprite = line.icon;
        }

        if (nameText != null) nameText.text = line.name;

        if (qtyText != null)
        {
            bool show = line.qty > 0;
            qtyText.gameObject.SetActive(show);
            if (show) qtyText.text = "×" + line.qty;
        }

        if (valueText != null) valueText.text = line.valueStr;
    }
}