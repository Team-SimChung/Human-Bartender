using UnityEngine;

/// <summary>화면 크기에 맞춰 두 패널과 조작 영역을 배치한다. 개별 아트의 종횡비는 유지한다.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
public class StirLayoutView : MonoBehaviour
{
    [SerializeField] RectTransform playfield;
    [SerializeField] RectTransform leftFrame;
    [SerializeField] RectTransform rightFrame;
    [SerializeField] RectTransform grid;
    [SerializeField] RectTransform characterViewport;
    [SerializeField] RectTransform barBackground;
    [SerializeField] RectTransform character;
    [SerializeField] RectTransform closeupViewport;
    [SerializeField] RectTransform closeupFrame;
    [SerializeField] RectTransform arena;
    [SerializeField] RectTransform resultGauge;
    [SerializeField] RectTransform elapsedIcon;
    [SerializeField] RectTransform elapsedText;
    [SerializeField] RectTransform startHint;
    [SerializeField] StirGaugeView gauge;

    [SerializeField] float outerMargin = 18f;
    [SerializeField] float panelGap = 16f;
    Vector2 previousSize;

    void OnEnable() => RefreshLayout();
    void LateUpdate()
    {
        if (((RectTransform)transform).rect.size != previousSize) RefreshLayout();
    }

    public void RefreshLayout()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        previousSize = size;
        if (playfield != null)
        {
            playfield.anchorMin = Vector2.zero;
            playfield.anchorMax = Vector2.one;
            playfield.offsetMin = playfield.offsetMax = Vector2.zero;
        }

        float height = Mathf.Max(1f, size.y - outerMargin * 2f);
        float leftWidth = Mathf.Min(304f, size.x * 0.32f);
        float leftX = -size.x * 0.5f + outerMargin + leftWidth * 0.5f;
        float rightStart = leftX + leftWidth * 0.5f + panelGap;
        float rightWidth = Mathf.Max(1f, size.x * 0.5f - outerMargin - rightStart);
        float rightX = rightStart + rightWidth * 0.5f;

        Place(leftFrame, new Vector2(leftX, 0f), new Vector2(leftWidth, height));
        Place(rightFrame, new Vector2(rightX, 0f), new Vector2(rightWidth, height));
        Place(grid, new Vector2(rightX, 0f), new Vector2(rightWidth - 16f, height - 16f));
        Place(characterViewport, new Vector2(leftX, 0f), new Vector2(leftWidth - 16f, height - 16f));
        float characterHeight = (height - 16f) * 0.9f;
        Place(character, new Vector2(0f, (height - 16f) * 0.06f),
            new Vector2(characterHeight * 417f / 507f, characterHeight));
        Place(barBackground, Vector2.zero, new Vector2((height - 16f) * 970f / 550f, height - 16f));

        float closeupHeight = Mathf.Min(243f, height * 0.48f);
        float closeupWidth = closeupHeight * 1205f / 2029f;
        Vector2 closeupPosition = new Vector2(leftX + leftWidth * 0.5f + 6f,
            height * 0.5f - closeupHeight * 0.5f);
        Place(closeupFrame, closeupPosition, new Vector2(closeupWidth, closeupHeight));
        Place(closeupViewport, closeupPosition, new Vector2(closeupWidth - 8f, closeupHeight - 8f));

        float arenaSize = Mathf.Min(310f, Mathf.Min(rightWidth - 120f, height - 160f));
        arenaSize = Mathf.Max(1f, arenaSize);
        Place(arena, new Vector2(rightX, 18f), new Vector2(310f, 310f));
        if (arena != null) arena.localScale = Vector3.one * (arenaSize / 310f);

        float gaugeY = -height * 0.5f + 30f;
        Place(resultGauge, new Vector2(rightX, gaugeY), new Vector2(Mathf.Max(1f, rightWidth - 70f), 22.25f));
        Place(startHint, new Vector2(rightX, gaugeY + 27f), new Vector2(260f, 20f));
        float timeY = height * 0.5f - 36f;
        float timeX = rightStart + rightWidth - 65f;
        Place(elapsedText, new Vector2(timeX, timeY), new Vector2(65f, 30f));
        Place(elapsedIcon, new Vector2(timeX - 47f, timeY), new Vector2(22.3f, 25f));
        gauge?.RefreshLayout();
    }

    static void Place(RectTransform target, Vector2 position, Vector2 size)
    {
        if (target == null) return;
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0.5f, 0.5f);
        target.anchoredPosition = position;
        target.sizeDelta = size;
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }
}
