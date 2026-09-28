using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>제조 준비 화면의 선반 칸 또는 선택 트레이 칩.</summary>
public sealed class CraftPrepItemView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image swatch;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color selectedColor = new Color(0.83f, 0.55f, 0.18f, 0.28f);
    [SerializeField] private Color guideColor = new Color(0.36f, 0.76f, 0.72f, 0.30f);

    private CraftPrepScreen owner;
    private string itemId;
    private CraftPrepScreen.EStage stage;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    public void BindShelf(NewShelfItemData item, string displayName, bool selected, bool guided,
        CraftPrepScreen screen, CraftPrepScreen.EStage from)
    {
        Bind(item.Id, displayName, screen, from);
        if (background != null) background.color = selected ? selectedColor : guided ? guideColor : normalColor;
        if (swatch != null)
            swatch.color = item.TryGetLiquidColor(out Color32 color) ? color : new Color(1f, 1f, 1f, 0.35f);
    }

    public void BindTray(string id, string displayName, CraftPrepScreen screen, CraftPrepScreen.EStage from)
    {
        Bind(id, displayName, screen, from);
        if (background != null) background.color = selectedColor;
    }

    private void Bind(string id, string displayName, CraftPrepScreen screen, CraftPrepScreen.EStage from)
    {
        itemId = id;
        owner = screen;
        stage = from;
        if (label != null) label.text = displayName;
    }

    private void OnClick()
    {
        if (owner != null) owner.ToggleItem(itemId, stage);
    }
}
