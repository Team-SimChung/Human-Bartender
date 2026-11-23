using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>월드 선반에 배치한 물건 하나의 포인터 입력과 표시를 담당한다.</summary>
public sealed class CraftPrepShelfSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private string itemId;
    [SerializeField] private CraftPrepStageScreen.Stage stage;
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private Color hoverColor = new Color(98f / 255f, 236f / 255f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 218f / 255f, 123f / 255f);
    [SerializeField] private Color guideColor = new Color(0.75f, 0.95f, 1f);

    private CraftPrepStageScreen owner;
    private NewShelfItemData item;
    private bool available;
    private bool selected;
    private bool guide;
    private bool hovered;
    private readonly CraftPrepItemOutline outline = new CraftPrepItemOutline();

    public string ItemId { get { return itemId; } }
    public CraftPrepStageScreen.Stage ItemStage { get { return stage; } }
    public Sprite Icon { get { return outline.SourceSprite(icon); } }

    public void Configure(CraftPrepStageScreen screen, NewShelfItemData data, bool isSelected, bool isGuide)
    {
        owner = screen;
        item = data;
        available = true;
        selected = isSelected;
        guide = isGuide;
        hovered &= owner != null && owner.CanInteract(stage);
        gameObject.SetActive(true);
        RefreshVisual();
    }

    public void SetUnavailable()
    {
        available = false;
        hovered = false;
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hovered || !available || owner == null || !owner.CanInteract(stage)) return;
        hovered = true;
        RefreshVisual();
        owner.ShowItemInfo(this, item, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshVisual();
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

    private void LateUpdate()
    {
        if (hovered && (!available || owner == null || !owner.CanInteract(stage)))
        {
            hovered = false;
            RefreshVisual();
            if (owner != null) owner.HideItemInfo(this);
        }
        outline.Tick(Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        hovered = false;
        outline.Clear();
        if (icon != null) icon.color = Color.white;
        if (owner != null) owner.HideItemInfo(this);
    }

    private void RefreshVisual()
    {
        if (icon == null) return;
        // Keep the recipe hint tint; hover and selection use only the outline.
        icon.color = guide ? guideColor : Color.white;
        outline.Set(icon, available && selected, available && hovered, selectedColor, hoverColor);
    }
}
