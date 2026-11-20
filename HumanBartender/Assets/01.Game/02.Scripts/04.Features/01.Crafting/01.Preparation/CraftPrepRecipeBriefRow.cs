using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레시피 버튼 호버 목록의 한 줄: 체크 칸, 갈래(잔·도구·재료), 이름. 담은 항목은 체크 칸 그림이 바뀌고
/// 체크 표시와 민트색 이름으로 보인다. 웹 bar-views.css .prep-brief-row 기준이다.
/// 웹의 "담음/미선택" 글자는 화면 낭독기 전용이라 화면에 그리지 않는다.
/// </summary>
public sealed class CraftPrepRecipeBriefRow : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("체크 칸(테두리 포함). 색은 그림에 들어 있으므로 흰색으로 둔다.")]
    [SerializeField] private Image checkBox;
    [Tooltip("체크 표시(IMG_brief_check). 담은 줄에서만 켠다.")]
    [SerializeField] private Image checkMark;
    [SerializeField] private TextMeshProUGUI kindText;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Sprites")]
    [Tooltip("담지 않은 체크 칸(IMG_brief_check_box_A).")]
    [SerializeField] private Sprite emptyBoxSprite;
    [Tooltip("담은 체크 칸(IMG_brief_check_box_B).")]
    [SerializeField] private Sprite packedBoxSprite;

    [Header("Text colors")]
    [Tooltip("담지 않은 항목의 이름 색. 웹 #d9e7e9.")]
    [SerializeField] private Color nameColor = new Color32(0xD9, 0xE7, 0xE9, 0xFF);
    [Tooltip("담은 항목의 이름 색. 웹 #adf2e4.")]
    [SerializeField] private Color packedNameColor = new Color32(0xAD, 0xF2, 0xE4, 0xFF);
    [Tooltip("갈래(잔·도구·재료) 글자 색. 웹 #9eafb9.")]
    [SerializeField] private Color kindColor = new Color32(0x9E, 0xAF, 0xB9, 0xFF);

    public bool IsPacked { get; private set; }
    public string ItemId { get; private set; }
    public string DisplayName => nameText != null ? nameText.text : null;
    public bool IsCheckMarkVisible => checkMark != null && checkMark.enabled;
    public Sprite CheckBoxSprite => checkBox != null ? checkBox.sprite : null;
    public Color NameColor => nameText != null ? nameText.color : default;

    public void Bind(string itemId, string kind, string displayName, bool packed)
    {
        if (kindText == null || nameText == null)
        {
            Debug.LogError("[CraftPrep] 레시피 목록 줄의 Kind Text와 Name Text를 프리팹에 연결해 주세요.", this);
            return;
        }

        ItemId = itemId;
        IsPacked = packed;
        kindText.text = kind;
        kindText.color = kindColor;
        nameText.text = displayName;
        nameText.color = packed ? packedNameColor : nameColor;
        if (checkBox != null)
        {
            Sprite box = packed ? packedBoxSprite : emptyBoxSprite;
            if (box != null) checkBox.sprite = box;
        }
        if (checkMark != null) checkMark.enabled = packed;
    }
}
