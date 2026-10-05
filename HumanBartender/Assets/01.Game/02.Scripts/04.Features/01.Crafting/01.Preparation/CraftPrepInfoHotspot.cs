using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>제조 선택값에 포함되지 않는 도구의 월드 설명 영역.</summary>
public sealed class CraftPrepInfoHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private CraftPrepStageScreen owner;
    [SerializeField] private CraftPrepItemTooltip tooltip;
    [SerializeField] private Transform infoAnchor;
    [SerializeField] private CraftPrepStageScreen.Stage stage;
    [SerializeField] private CraftPrepItemTooltip.Direction infoDirection;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private bool isBottleOpener;
    [SerializeField] private SpriteRenderer selectionIcon;
    [SerializeField] private Color hoverColor = new Color(0.35f, 0.9f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.45f);
    private bool hovered;
    public bool IsBottleOpener => isBottleOpener;
    public Sprite Icon => selectionIcon != null ? selectionIcon.sprite : null;

    private void LateUpdate()
    {
        if (hovered && (owner == null || !owner.CanInteract(stage)))
        {
            hovered = false;
            if (tooltip != null) tooltip.Hide();
        }
        if (isBottleOpener && selectionIcon != null)
            selectionIcon.color = hovered ? hoverColor
                : owner != null && owner.IsBottleOpenerSelected ? selectedColor : Color.white;
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
        if (tooltip != null)
            tooltip.Show(displayName, description, infoAnchor, infoDirection);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (tooltip != null) tooltip.Hide();
    }

    private void OnDisable()
    {
        hovered = false;
        if (isBottleOpener && selectionIcon != null) selectionIcon.color = Color.white;
    }
}
