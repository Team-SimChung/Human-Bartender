using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>제조 선택값에 포함되지 않는 도구의 월드 설명 영역.</summary>
public sealed class CraftPrepInfoHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private CraftPrepStageScreen owner;
    [SerializeField] private CraftPrepItemTooltip tooltip;
    [SerializeField] private CraftPrepStageScreen.Stage stage;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private bool isBottleOpener;
    [SerializeField] private SpriteRenderer selectionIcon;
    [SerializeField] private Color hoverColor = new Color(98f / 255f, 236f / 255f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 218f / 255f, 123f / 255f);
    private bool hovered;
    private readonly CraftPrepItemOutline outline = new CraftPrepItemOutline();
    public bool IsBottleOpener => isBottleOpener;
    public Sprite Icon => outline.SourceSprite(selectionIcon);

    private void LateUpdate()
    {
        if (hovered && (owner == null || !owner.CanInteract(stage)))
        {
            hovered = false;
            if (tooltip != null) tooltip.Hide();
        }
        RefreshOutline();
        outline.Tick(Time.unscaledDeltaTime);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isBottleOpener && eventData.button == PointerEventData.InputButton.Left &&
            owner != null && owner.CanInteract(stage))
            owner.ToggleBottleOpener();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || !owner.CanInteract(stage)) return;
        hovered = true;
        RefreshOutline();
        if (tooltip != null)
            tooltip.Show(displayName, description, owner.GuideCamera, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshOutline();
        if (tooltip != null) tooltip.Hide();
    }

    private void OnDisable()
    {
        if (hovered && tooltip != null) tooltip.Hide();
        hovered = false;
        outline.Clear();
    }

    private void RefreshOutline()
    {
        if (!isBottleOpener) return;
        outline.Set(selectionIcon, owner != null && owner.IsBottleOpenerSelected,
            hovered, selectedColor, hoverColor);
    }
}
