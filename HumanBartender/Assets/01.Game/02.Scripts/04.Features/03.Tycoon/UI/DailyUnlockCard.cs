using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class DailyUnlockCard : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [Header("카드 표시")]
    [Tooltip("비율을 유지하는 항목 이미지.")]
    [SerializeField] Image icon;
    [Tooltip("카드 아래의 한국어 이름.")]
    [SerializeField] TMP_Text nameText;

    Action<DailyUnlockCard> onFocus;
    public string DisplayName { get; private set; }
    public string Description { get; private set; }

    public void Bind(Sprite sprite, string displayName, string description, Action<DailyUnlockCard> focus)
    {
        if (icon == null || nameText == null)
            throw new InvalidOperationException("[DailyUnlocks] 카드의 Icon과 NameText를 연결해 주세요.");
        icon.sprite = sprite;
        // 대체 이미지까지 비어 있으면 흰 사각형을 그리지 않는다.
        icon.enabled = sprite != null;
        nameText.text = displayName;
        DisplayName = displayName;
        Description = description;
        onFocus = focus;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!UIDisplayOptions.IsOptionOpen) onFocus?.Invoke(this);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!UIDisplayOptions.IsOptionOpen) onFocus?.Invoke(this);
    }
}
