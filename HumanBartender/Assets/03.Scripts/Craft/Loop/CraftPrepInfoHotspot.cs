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
    public bool IsBottleOpener => isBottleOpener;
    public Sprite Icon => selectionIcon != null ? selectionIcon.sprite : null;

    private void LateUpdate()
    {
        if (isBottleOpener && selectionIcon != null)
            selectionIcon.color = owner != null && owner.IsBottleOpenerSelected
                ? new Color(1f, 0.85f, 0.45f) : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isBottleOpener && eventData.button == PointerEventData.InputButton.Left &&
            owner != null && owner.CanInteract(stage))
            owner.ToggleBottleOpener();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null && owner.CanInteract(stage) && tooltip != null)
            tooltip.Show(displayName, description, infoAnchor, infoDirection);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }
}
