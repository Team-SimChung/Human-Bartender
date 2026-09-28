using TMPro;
using UnityEngine;

/// <summary>월드 좌표에 놓이는 공용 물건 설명 말풍선.</summary>
public sealed class CraftPrepItemTooltip : MonoBehaviour
{
    public enum Direction { Above, Below }

    [SerializeField] private SpriteRenderer frame;
    [SerializeField] private TextMeshPro nameText;
    [SerializeField] private TextMeshPro descriptionText;

    public void Show(NewShelfItemData item, Transform anchor, Direction direction)
    {
        string displayName = string.IsNullOrWhiteSpace(item.Name.Ko) ? item.Id : item.Name.Ko;
        Show(displayName, item.Desc.Ko, anchor, direction);
    }

    public void Show(string displayName, string description, Transform anchor, Direction direction)
    {
        if (anchor == null) return;
        float verticalOffset = direction == Direction.Above ? 0.46f : -0.46f;
        transform.position = anchor.position + Vector3.up * verticalOffset;
        if (frame != null) frame.flipY = direction == Direction.Below;
        if (nameText != null) nameText.text = displayName;
        if (descriptionText != null) descriptionText.text = description;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
