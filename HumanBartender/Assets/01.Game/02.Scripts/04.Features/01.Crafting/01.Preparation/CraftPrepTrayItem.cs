using UnityEngine;
using UnityEngine.UI;

/// <summary>하단 선택 트레이에 놓이는 프리팹 한 칸.</summary>
public sealed class CraftPrepTrayItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Button removeButton;

    private CraftPrepStageScreen owner;
    private string itemId;
    private CraftPrepStageScreen.Stage stage;

    public string ItemId => itemId;
    public Transform SelectionControl => removeButton != null ? removeButton.transform : transform;

    private void OnEnable()
    {
        if (removeButton != null) removeButton.onClick.AddListener(Remove);
    }

    private void OnDisable()
    {
        if (removeButton != null) removeButton.onClick.RemoveListener(Remove);
    }

    public void Bind(CraftPrepStageScreen screen, string id, CraftPrepStageScreen.Stage from, Sprite sprite)
    {
        owner = screen;
        itemId = id;
        stage = from;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
    }

    private void Remove()
    {
        if (owner != null) owner.ToggleItem(itemId, stage);
    }
}
