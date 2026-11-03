// 메뉴: Tools > Story > Set Up Bar Seat HUD

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play 씬 05.UI 아래에 바 좌석 표시(Canvas_BarSeatHud / Bar Seat HUD)를 배치하고 참조를 연결한다.
///
/// 좌석 표시는 예전에 튜토리얼 오버레이가 런타임에 만들었다. 오버레이가 없는 날(1부 영업만 있는 날)에도
/// 보여야 해서 바 UI의 하나로 옮겼다. 다른 바 캔버스와 같이 960×540, match 0을 쓰고, 예전 1280×720 기준
/// 크기는 ×0.75로 환산했다. 다시 실행하면 캔버스는 그대로 두고 좌석 표시만 새로 만든다.
/// 배치 모드: -executeMethod BarSeatHudSetup.Run
/// </summary>
public static class BarSeatHudSetup
{
    const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    const string OverlayPrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";
    const string CanvasName = "Canvas_BarSeatHud";
    const string HudName = "Bar Seat HUD";
    /// <summary>대화(8)·코스터(9)·서비스(10)·레시피(11) 위, 기믹 HUD(100)·서빙(103)·메뉴(105)·튜토리얼(4000) 아래.</summary>
    const int SortingOrder = 12;
    const float Scale = .75f;

    static readonly string[] SeatNames = { "Left", "Middle", "Right" };

    [MenuItem("Tools/Story/Set Up Bar Seat HUD")]
    public static void Run()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("[BarSeatHud] 플레이 모드를 끝낸 뒤 실행하세요.");
        // 에디터에서 메뉴로 실행할 때 열려 있던 씬의 저장 안 된 변경을 버리지 않는다.
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        Transform uiRoot = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "05.UI") { uiRoot = child; break; }
        if (uiRoot == null) throw new System.InvalidOperationException("[BarSeatHud] Play 씬에 05.UI가 없습니다.");

        var servicePanel = Require<ServicePanelController>(scene);
        Canvas canvas = EnsureCanvas(uiRoot, servicePanel.GetComponentInParent<Canvas>(true));

        var old = canvas.transform.Find(HudName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var font = LoadSeatKeyFont();
        var hud = BuildHud(canvas.transform, font, out var fields);

        var view = new SerializedObject(hud);
        SetArray(view.FindProperty("dots"), fields, "dot");
        SetArray(view.FindProperty("rings"), fields, "ring");
        foreach (var (name, value) in fields) view.FindProperty(name).objectReferenceValue = value;
        view.FindProperty("phases").objectReferenceValue = Require<PlayPhaseController>(scene);
        view.FindProperty("characters").objectReferenceValue = Require<DialogueCharacterManager>(scene);
        view.FindProperty("guests").objectReferenceValue = Require<GuestManager>(scene);
        view.FindProperty("craft").objectReferenceValue = Require<CraftFlowController>(scene);
        view.FindProperty("preparation").objectReferenceValue = Require<CraftPrepStageHost>(scene);
        view.FindProperty("recipes").objectReferenceValue = Require<RecipeBrowserScreen>(scene);
        view.FindProperty("playCamera").objectReferenceValue = Require<PlayCamera>(scene);
        view.FindProperty("servicePanel").objectReferenceValue = servicePanel;
        view.ApplyModifiedPropertiesWithoutUndo();

        // 기믹·준비 화면 동안 바 UI를 끄는 두 목록에 같이 넣는다.
        AddBarCanvas(Require<GimmickRunner>(scene), canvas);
        AddBarCanvas(Require<CraftPrepStageHost>(scene), canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BarSeatHud] Play 씬에 바 좌석 표시를 배치했습니다.");
    }

    static Canvas EnsureCanvas(Transform uiRoot, Canvas reference)
    {
        var existing = uiRoot.Find(CanvasName);
        GameObject go = existing != null ? existing.gameObject
            : new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(uiRoot, false);
        if (reference != null) go.layer = reference.gameObject.layer;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        return canvas;
    }

    /// <summary>예전 BarSeatStatusView.Create와 같은 모양을 960×540 기준으로 만든다.</summary>
    static BarSeatStatusView BuildHud(Transform parent, TMP_FontAsset font, out List<(string, Object)> fields)
    {
        fields = new List<(string, Object)>();
        var root = Rect(HudName, parent, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.gameObject.layer = parent.gameObject.layer;
        var hud = root.gameObject.AddComponent<BarSeatStatusView>();

        var indicators = Rect("Seat Indicators", root, new Vector2(104, 38) * Scale, new Vector2(66, 25) * Scale);
        indicators.anchorMin = indicators.anchorMax = Vector2.zero;
        Shape(indicators, new Color(.035f, .06f, .085f, .94f), 18 * Scale);
        Shape(Rect("HUD Border", indicators, new Vector2(104, 38) * Scale, Vector2.zero), new Color(.31f, .52f, .57f, .8f), 18 * Scale, 1 * Scale);
        fields.Add(("indicators", indicators));
        for (int i = 0; i < 3; i++)
        {
            Vector2 position = new Vector2((i - 1) * 32, 0) * Scale;
            var ring = Shape(Rect("Selected Seat " + SeatNames[i], indicators, new Vector2(30, 30) * Scale, position),
                BarSeatStatusView.SelectedColor, 15 * Scale, 1.5f * Scale);
            var dot = Shape(Rect("Seat " + SeatNames[i], indicators, new Vector2(14, 14) * Scale, position),
                BarSeatStatusView.EmptyColor, 7 * Scale);
            fields.Add(("ring" + i, ring));
            fields.Add(("dot" + i, dot));
        }
        fields.Add(("leftKey", Key(root, font, "Left Seat Key", new Vector2(0, .5f), new Vector2(29, 0) * Scale)));
        fields.Add(("rightKey", Key(root, font, "Right Seat Key", new Vector2(1, .5f), new Vector2(-29, 0) * Scale)));
        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true)) rect.gameObject.layer = parent.gameObject.layer;
        return hud;
    }

    static RectTransform Key(Transform parent, TMP_FontAsset font, string name, Vector2 anchor, Vector2 position)
    {
        var key = Rect(name, parent, new Vector2(48, 44) * Scale, position);
        key.anchorMin = key.anchorMax = anchor;
        var text = Rect("Key", key, new Vector2(48, 44) * Scale, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = 17 * Scale;
        text.alignment = TextAlignmentOptions.Center;
        text.color = BarSeatStatusView.SelectedColor;
        text.raycastTarget = false;
        key.gameObject.SetActive(false);
        return key;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    static StoryTutorialShape Shape(RectTransform rect, Color color, float radius, float border = 0f)
    {
        var shape = rect.gameObject.AddComponent<StoryTutorialShape>();
        shape.Style(color, radius, border);
        return shape;
    }

    static void SetArray(SerializedProperty array, List<(string name, Object value)> fields, string prefix)
    {
        array.arraySize = 3;
        for (int i = 0; i < 3; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = fields.Find(field => field.name == prefix + i).value;
        fields.RemoveAll(field => field.name.StartsWith(prefix, System.StringComparison.Ordinal) && field.name.Length == prefix.Length + 1);
    }

    static TMP_FontAsset LoadSeatKeyFont()
    {
        var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPrefabPath);
        var view = overlay != null ? overlay.GetComponent<StoryTutorialView>() : null;
        if (view == null || view.SeatKeyFont == null)
            throw new System.InvalidOperationException("[BarSeatHud] 튜토리얼 오버레이의 좌석 키 글꼴을 찾지 못했습니다: " + OverlayPrefabPath);
        return view.SeatKeyFont;
    }

    static void AddBarCanvas(Object owner, Canvas canvas)
    {
        var serialized = new SerializedObject(owner);
        var list = serialized.FindProperty("barCanvases");
        if (list == null) throw new System.InvalidOperationException($"[BarSeatHud] {owner.GetType().Name}에 barCanvases가 없습니다.");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == canvas) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = canvas;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static T Require<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        throw new System.InvalidOperationException($"[BarSeatHud] Play 씬에 {typeof(T).Name}가 없습니다.");
    }
}
