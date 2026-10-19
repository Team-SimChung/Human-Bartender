using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>월드 선반에 배치한 물건 하나의 포인터 입력과 표시를 담당한다.</summary>
public sealed class CraftPrepShelfSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private string itemId;
    [SerializeField] private CraftPrepStageScreen.Stage stage;
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private Transform infoAnchor;
    [SerializeField] private CraftPrepItemTooltip.Direction infoDirection;
    [SerializeField] private Color hoverColor = new Color(0.35f, 0.9f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color guideColor = new Color(0.75f, 0.95f, 1f);

    private CraftPrepStageScreen owner;
    private NewShelfItemData item;
    private bool available;
    private bool selected;
    private bool guide;
    private bool hovered;

    public string ItemId { get { return itemId; } }
    public CraftPrepStageScreen.Stage ItemStage { get { return stage; } }
    public Sprite Icon { get { return icon != null ? icon.sprite : null; } }
    public Transform InfoAnchor { get { return infoAnchor; } }
    public CraftPrepItemTooltip.Direction InfoDirection { get { return infoDirection; } }

    public void Configure(CraftPrepStageScreen screen, NewShelfItemData data, bool isSelected, bool isGuide)
    {
        owner = screen;
        item = data;
        available = true;
        selected = isSelected;
        guide = isGuide;
        gameObject.SetActive(true);
        RefreshColor();
    }

    public void SetUnavailable()
    {
        available = false;
        hovered = false;
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!available || owner == null || !owner.CanInteract(stage)) return;
        hovered = true;
        RefreshColor();
        owner.ShowItemInfo(this, item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshColor();
        if (owner != null) owner.HideItemInfo(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (available && eventData.button == PointerEventData.InputButton.Left &&
            owner != null && owner.CanInteract(stage))
        {
            owner.ToggleItem(itemId, stage);
        }
    }

    private void RefreshColor()
    {
        if (icon == null) return;
        icon.color = hovered ? hoverColor : selected ? selectedColor : guide ? guideColor : Color.white;
    }
}
