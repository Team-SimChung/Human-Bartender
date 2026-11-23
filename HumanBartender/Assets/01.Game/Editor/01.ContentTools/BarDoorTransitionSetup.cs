using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 웹 bar-door-transition.js와 같은 세 장(배경·문짝·빛)으로 바 문 전환 화면 프리팹을 조립하고 SceneTransitionManager에 연결한다.
/// 좌표는 960×540 화면의 왼쪽 위 기준 픽셀이다(1920×1080 그림의 절반). 조립 뒤의 위치·색 조정은 프리팹에서 하며,
/// 다시 실행하면 프리팹을 새로 만든다.
/// </summary>
public static class BarDoorTransitionSetup
{
    const string ArtFolder = "Assets/01.Game/03.Content/01.ArtResources/05.UI/06.Ect/BarDoor/";
    const string PrefabFolder = "Assets/01.Game/03.Content/08.Prefabs/03.UI/05.Transition";
    const string PrefabPath = PrefabFolder + "/BarDoorTransition.prefab";
    const string ProjectScopePath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    static readonly Vector2 Stage = new(960, 540);

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
            scaler.matchWidthOrHeight = 0;

            var backdrop = Stretch(Child("Backdrop", root.transform));
            backdrop.gameObject.AddComponent<Image>().color = Color.black;

            var stage = Child("Stage", root.transform);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
            stage.sizeDelta = Stage;

            // 배경 그림의 문 자리는 투명하게 뚫려 있다(1920×1080 그림의 (864,428) 192×296).
            // 그 구멍 안에 빛과 문짝을 두고, 열린 문짝이 구멍 밖으로 보이지 않게 가린다.
            var aperture = Place(Child("Aperture", stage), 432, 214, 96, 148);
            aperture.gameObject.AddComponent<RectMask2D>();
            Layer(aperture, "Light", "Door_Transition_Light", 0, 0, 96, 148);
            var leaf = Layer(aperture, "Leaf", "Door_Transition_Door", 0, 0, 96, 148).rectTransform;
            Layer(stage, "Background", "Door_Transition_BG", 0, 0, 960, 540);

            var track = Place(Child("LoadTrack", stage), 458, 503, 45, 2);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color32(0x26, 0x31, 0x2f, 255);
            trackImage.raycastTarget = false;
            var slider = Place(Child("LoadSlider", track), 0, 0, 18, 2);
            var sliderImage = slider.gameObject.AddComponent<Image>();
            sliderImage.color = new Color32(0xbd, 0xb3, 0x89, 255);
            sliderImage.raycastTarget = false;

            // 그림 전체를 한 번에 가려야 페이드 중 빛이 닫힌 문짝을 뚫고 보이지 않는다.
            var curtainRect = Stretch(Child("FadeCurtain", root.transform));
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
                serialized.FindProperty("loadSlider").objectReferenceValue = slider;
                serialized.FindProperty("loadTrack").objectReferenceValue = track.gameObject;
                serialized.FindProperty("fadeCurtain").objectReferenceValue = curtain;
                // 웹: 문짝이 자기 폭만큼(-100%) 밀려 열린다.
                serialized.FindProperty("openTravel").floatValue = 1;
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

    static Image Layer(RectTransform parent, string name, string file, float x, float y, float w, float h)
    {
        string path = ArtFolder + file + ".png";
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault()
            ?? throw new InvalidOperationException("[BarDoorTransitionSetup] 문 그림 Sprite가 없습니다: " + path);
        var image = Place(Child(name, parent), x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>batchmode용: 프리팹 조립과 연결을 실행하고 종료 코드로 결과를 알린다.</summary>
    public static void BuildBatch()
    {
        try { Build(); Debug.Log("BAR_DOOR_SETUP_OK"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
