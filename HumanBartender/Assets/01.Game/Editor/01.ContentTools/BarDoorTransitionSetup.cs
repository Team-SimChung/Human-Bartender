using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이미 들여온 웹 문 그림 레이어(Web_GRD_bar_door_*)로 바 문 전환 화면 프리팹을 조립하고 SceneTransitionManager에 연결한다.
/// 좌표는 1280×720 화면의 왼쪽 위 기준 픽셀이다. 조립 뒤의 위치·색 조정은 프리팹에서 하며, 다시 실행하면 프리팹을 새로 만든다.
/// </summary>
public static class BarDoorTransitionSetup
{
    const string ArtFolder = "Assets/01.Game/03.Content/01.ArtResources/05.UI/06.Ect/WebGenerated/Cutscenes/";
    const string PrefabFolder = "Assets/01.Game/03.Content/08.Prefabs/03.UI/05.Transition";
    const string PrefabPath = PrefabFolder + "/BarDoorTransition.prefab";
    const string ProjectScopePath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    const string TutorialOverlayPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";
    // 그림자 레이어는 본체보다 사방 48px 크게 만들어져 있다.
    const float ShadowPad = 48;
    static readonly Vector2 Stage = new(1280, 720);

    [MenuItem("Tools/Story/Build Bar Door Transition")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/01.Game/03.Content/08.Prefabs/03.UI", "05.Transition");

        var root = new GameObject("BarDoorTransition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));
        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 32000; // 페이드 캔버스보다 위
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Stage;
            scaler.matchWidthOrHeight = .5f;

            var backdrop = Stretch(Child("Backdrop", root.transform));
            backdrop.gameObject.AddComponent<Image>().color = Color.black;

            var stage = Child("Stage", root.transform);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
            stage.sizeDelta = Stage;

            Layer(stage, "wall_halo_background", 335, 113, 610, 494);
            // 문턱 하단 바로 아래에서 UI 하단으로 퍼지도록 둔다.
            var floorLight = Layer(stage, "light_floor_background", 393, 579, 494, 123).gameObject.AddComponent<CanvasGroup>();
            floorLight.alpha = 0;
            floorLight.blocksRaycasts = false;
            floorLight.interactable = false;
            Layer(stage, "ground_background", 315, 575, 650, 2);

            Shadowed(stage, "sign", 519, 153, 242, 74);
            var font = LoadFont();
            var sign = Text(stage, "Sign Text", "언노운", font, 30, new Color(.93f, .85f, .58f), 519, 153, 242, 74);
            sign.characterSpacing = 10;

            Layer(stage, "rail_shadow", 524 - ShadowPad, 241 - ShadowPad, 232 + ShadowPad * 2, 5 + ShadowPad * 2);
            Layer(stage, "frame_shadow", 532 - ShadowPad, 246 - ShadowPad, 216 + ShadowPad * 2, 328 + ShadowPad * 2);
            Layer(stage, "frame_background", 532, 246, 216, 328);

            // 문틀 안쪽. 열린 문짝이 문틀 밖으로 보이지 않게 가린다.
            var aperture = Place(Child("Aperture", stage), 539, 253, 202, 314);
            aperture.gameObject.AddComponent<RectMask2D>();
            Layer(aperture, "aperture_background", 0, 0, 202, 314);
            Layer(aperture, "interior_background", 0, 0, 202, 314);
            var leaf = Place(Child("Leaf", aperture), 0, 0, 202, 314);
            Layer(leaf, "leaf_background", 0, 0, 202, 314);
            Layer(leaf, "leaf_border", 0, 0, 202, 314);
            Layer(leaf, "window_background", 24, 42, 149, 49);
            Layer(leaf, "window_border", 24, 42, 149, 49);
            Layer(leaf, "window_after_background", 36, 51, 125, 31);
            Layer(leaf, "grip_background", 178, 143, 6, 48);
            Layer(leaf, "grip_border", 178, 143, 6, 48);

            // 문짝보다 문틀·상단 레일이 앞에 오도록 그린다.
            Layer(stage, "frame_border", 532, 246, 216, 328);
            Layer(stage, "rail_background", 524, 241, 232, 5);
            Layer(stage, "sill_background", 526, 571, 228, 8);
            Layer(stage, "sill_border", 526, 571, 228, 8);

            Layer(stage, "prop_bin_shadow", 462 - ShadowPad, 494 - ShadowPad, 51 + ShadowPad * 2, 81 + ShadowPad * 2);
            Layer(stage, "prop_left_bag_background", 418, 523, 41, 52);
            Layer(stage, "prop_bags_background", 772, 502, 58, 73);
            Layer(stage, "prop_bin_background", 462, 494, 51, 81);
            Layer(stage, "prop_bin_border", 462, 494, 51, 81);
            Layer(stage, "prop_bin_after_border", 475, 483, 24, 8);
            Layer(stage, "prop_bin_before_background", 459, 489, 57, 13);
            Layer(stage, "prop_bin_before_border", 459, 489, 57, 13);

            var caption = Text(stage, "Caption", "영업을 준비하고 있습니다", font, 13, new Color(.55f, .6f, .62f), 0, 628, 1280, 24);
            caption.characterSpacing = 4;
            var track = Layer(stage, "load_track_background", 610, 671, 60, 2);
            var slider = Place(Child("Slider", track.rectTransform), 0, 0, 24, 2);
            slider.gameObject.AddComponent<Image>().color = new Color(.93f, .85f, .58f);

            // 그림 전체를 한 번에 가려야 페이드 중 내부 빛이 닫힌 문짝을 뚫고 보이지 않는다.
            var curtainRect = Stretch(Child("Fade Curtain", root.transform));
            var curtainImage = curtainRect.gameObject.AddComponent<Image>();
            curtainImage.color = Color.black;
            curtainImage.raycastTarget = false;
            var curtain = curtainRect.gameObject.AddComponent<CanvasGroup>();
            curtain.alpha = 0;
            curtain.interactable = false;
            curtain.blocksRaycasts = false;

            var view = root.AddComponent<BarDoorTransitionView>();
            using (var serialized = new SerializedObject(view))
            {
                serialized.FindProperty("leaf").objectReferenceValue = leaf;
                serialized.FindProperty("caption").objectReferenceValue = caption;
                serialized.FindProperty("loadSlider").objectReferenceValue = slider;
                serialized.FindProperty("loadTrack").objectReferenceValue = track.gameObject;
                serialized.FindProperty("floorLight").objectReferenceValue = floorLight;
                serialized.FindProperty("fadeCurtain").objectReferenceValue = curtain;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var group = root.GetComponent<CanvasGroup>();
            // 편집 중에는 완성된 UI를 보여 주고, 실행 시 Awake가 숨김 상태를 초기화한다.
            group.alpha = 1;
            group.blocksRaycasts = false;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        ConnectToTransitionManager();
    }

    /// <summary>프로젝트 스코프의 SceneTransitionManager 아래에 문 화면을 하나만 두고 연결한다.</summary>
    static void ConnectToTransitionManager()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<BarDoorTransitionView>(PrefabPath);
        var scope = PrefabUtility.LoadPrefabContents(ProjectScopePath);
        try
        {
            var manager = scope.GetComponentsInChildren<SceneTransitionManager>(true).Single();
            foreach (var old in manager.GetComponentsInChildren<BarDoorTransitionView>(true))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var instance = (BarDoorTransitionView)PrefabUtility.InstantiatePrefab(prefab, manager.transform);
            using (var serialized = new SerializedObject(manager))
            {
                serialized.FindProperty("barDoorOverlay").objectReferenceValue = instance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(scope, ProjectScopePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(scope); }
    }

    static RectTransform Child(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // 부모의 왼쪽 위를 기준으로 (x, y) 픽셀에 w×h 크기로 둔다.
    static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    static Image Layer(RectTransform parent, string part, float x, float y, float w, float h)
    {
        string path = ArtFolder + "Web_GRD_bar_door_" + part + ".png";
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
            ?? throw new InvalidOperationException("문 그림 Sprite가 없습니다: " + path);
        var image = Place(Child(part, parent), x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    static void Shadowed(RectTransform parent, string part, float x, float y, float w, float h, bool border = true)
    {
        Layer(parent, part + "_shadow", x - ShadowPad, y - ShadowPad, w + ShadowPad * 2, h + ShadowPad * 2);
        Layer(parent, part + "_background", x, y, w, h);
        if (border) Layer(parent, part + "_border", x, y, w, h);
    }

    static TMP_Text Text(RectTransform parent, string name, string value, TMP_FontAsset font, float size, Color color,
        float x, float y, float w, float h)
    {
        var text = Place(Child(name, parent), x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    // 튜토리얼 오버레이와 같은 글꼴을 쓴다. 따로 글꼴 경로를 두지 않기 위함이다.
    static TMP_FontAsset LoadFont()
    {
        var overlay = AssetDatabase.LoadAssetAtPath<StoryTutorialView>(TutorialOverlayPath);
        if (overlay == null) return null;
        using var serialized = new SerializedObject(overlay);
        return (serialized.FindProperty("heading").objectReferenceValue as TMP_Text)?.font;
    }

    /// <summary>batchmode용: 프리팹 조립과 연결을 실행하고 종료 코드로 결과를 알린다.</summary>
    public static void BuildBatch()
    {
        try { Build(); Debug.Log("BAR_DOOR_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
