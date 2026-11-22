using UnityEngine;

/// <summary>명시적으로 실행할 때만 기본 배치를 복구하는 에디터 도구. 실행 중 배치를 변경하지 않는다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
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
    [SerializeField, HideInInspector] bool authoredLayout;

#if UNITY_EDITOR
    [ContextMenu("Reset To Web Layout (Editor Only)")]
    public void RefreshLayout()
    {
        if (playfield != null)
        {
            playfield.anchorMin = Vector2.zero;
            playfield.anchorMax = Vector2.one;
            playfield.offsetMin = playfield.offsetMax = Vector2.zero;
        }

        // stir-resources.css의 1280×720 무대를 균등 축소한다. 원본 배경이 두 패널을 모두 포함한다.
        const float scale = 1f;
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
#endif
}
