using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>제조 선택값에 포함되지 않는 도구의 월드 설명 영역.</summary>
public sealed class CraftPrepInfoHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CraftPrepStageScreen owner;
    [SerializeField] private CraftPrepItemTooltip tooltip;
    [SerializeField] private Transform infoAnchor;
    [SerializeField] private CraftPrepStageScreen.Stage stage;
    [SerializeField] private CraftPrepItemTooltip.Direction infoDirection;
    [SerializeField] private string displayName;
    [SerializeField] private string description;

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
