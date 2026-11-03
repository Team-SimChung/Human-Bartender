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
    [SerializeField] RectTransform closeupCharacter;
    [SerializeField] RectTransform arena;
    [SerializeField] RectTransform resultGauge;
    [SerializeField] RectTransform elapsedIcon;
    [SerializeField] RectTransform elapsedText;
    [SerializeField] RectTransform startHint;
    [SerializeField] StirGaugeView gauge;

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

        // stir-resources.css의 1280×720 무대를 균등 축소한다. 원본 배경이 두 패널을 모두 포함한다.
        float scale = Mathf.Min(size.x / 1280f, size.y / 720f);
        PlaceWeb(leftFrame, 0, 0, 1280, 720, scale);
        if (rightFrame != null) rightFrame.gameObject.SetActive(false);
        if (grid != null) grid.gameObject.SetActive(false);
        PlaceWeb(characterViewport, 34, 29, 388, 664, scale);
        float motionScale = 664f / 550f * scale;
        Place(character, new Vector2(-19f, 22f) * motionScale,
            new Vector2(417f, 507f) * motionScale);
        Place(barBackground, Vector2.zero, new Vector2(970f, 550f) * motionScale);

        PlaceWeb(closeupFrame, 350, 0, 187, 325, scale);
        PlaceWeb(closeupViewport, 351, 1, 185, 323, scale);
        float closeupScale = 323f / 181f * scale;
        Place(closeupCharacter, new Vector2(-4f, 79.5f) * closeupScale,
            new Vector2(417f, 507f) * closeupScale);
        Place(arena, new Vector2(210f, 14f) * scale, new Vector2(310f, 310f));
        if (arena != null) arena.localScale = Vector3.one * (464f / 310f * scale);
        PlaceWeb(resultGauge, 496, 632, 708, 30, scale);
        PlaceWeb(startHint, 500, 583, 700, 28, scale);
        PlaceWeb(elapsedIcon, 1069, 50, 43, 48, scale);
        PlaceWeb(elapsedText, 1117, 54, 90, 40, scale);
        gauge?.RefreshLayout();
    }

    static void PlaceWeb(RectTransform target, float x, float y, float width, float height, float scale)
        => Place(target, new Vector2(x + width * .5f - 640f, 360f - y - height * .5f) * scale,
            new Vector2(width, height) * scale);

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
