// Editor/CoasterUISetup.cs
// 메뉴: Tools > Tycoon > Setup Coaster UI

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 씬의 화면 코스터 공급함과 대화 클릭 캐처를 연결한다. 월드 서빙 자리는 런타임에 생성한다.
/// </summary>
public static class CoasterUISetup
{
    const string TrayCanvasName = "Coaster Tray Canvas";
    const string SupplyTrayPrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/CoasterSupplyTray.prefab";
    public const string ServeSpotPrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/TableServeSpot.prefab";
    const string PlayScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    // 웹 tutorial-view.js coaster: 놓을 자리 상자의 아래 35%만 강조한다(target.y += h * .65, h *= .35).
    const float GuideAreaHeightRatio = .35f;

    /// <summary>
    /// 코스터 튜토리얼(웹 tutorial-view.js coaster)이 강조할 두 자리를 연결한다.
    /// TableServeSpot 프리팹 원본에 아래 35% 영역(GuideArea)을 만들고, 열린 씬의 CraftServingView에 공급함을 연결한다.
    /// 이미 연결된 값은 두고 비어 있는 것만 채운다. Play 씬을 연 상태에서 실행한다.
    /// </summary>
    [MenuItem("Tools/Tycoon/Connect Coaster Tutorial Guide")]
    public static void ConnectTutorialGuide()
    {
        ConnectServeSpotGuideArea();
        var scene = EditorSceneManager.GetActiveScene();
        if (ConnectSupplyTray(scene)) EditorSceneManager.MarkSceneDirty(scene);
    }

    /// <summary>batchmode용: Play 씬을 열어 연결하고 저장한 뒤 종료 코드로 결과를 알린다.</summary>
    public static void ConnectTutorialGuideBatch()
    {
        try
        {
            ConnectServeSpotGuideArea();
            var scene = EditorSceneManager.OpenScene(PlayScenePath, OpenSceneMode.Single);
            if (ConnectSupplyTray(scene)) EditorSceneManager.SaveScene(scene);
            Debug.Log("COASTER_TUTORIAL_GUIDE_OK");
            EditorApplication.Exit(0);
        }
        catch (System.Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static bool ConnectServeSpotGuideArea()
    {
        var root = PrefabUtility.LoadPrefabContents(ServeSpotPrefabPath);
        try
        {
            var spot = root.GetComponent<TableServeSpot>();
            using var data = new SerializedObject(spot);
            var field = data.FindProperty("guideArea");
            if (field.objectReferenceValue != null) return false;
            var area = new GameObject("GuideArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root.transform, false);
            area.SetAsFirstSibling();
            area.anchorMin = Vector2.zero;
            area.anchorMax = new Vector2(1, 0);
            area.pivot = new Vector2(.5f, 0);
            area.anchoredPosition = Vector2.zero;
            area.sizeDelta = new Vector2(0, Mathf.Round(((RectTransform)root.transform).sizeDelta.y * GuideAreaHeightRatio));
            field.objectReferenceValue = area;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ServeSpotPrefabPath);
            Debug.Log("[CoasterUISetup] TableServeSpot에 튜토리얼 강조 영역(GuideArea)을 만들었습니다.");
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static bool ConnectSupplyTray(UnityEngine.SceneManagement.Scene scene)
    {
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var serving = objects.Select(t => t.GetComponent<CraftServingView>()).FirstOrDefault(s => s != null);
        if (serving == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에 CraftServingView가 없습니다. Play 씬을 열고 실행하세요.");
            return false;
        }
        var data = new SerializedObject(serving);
        var field = data.FindProperty("supplyTray");
        if (field.objectReferenceValue != null) return false;
        var tray = objects.FirstOrDefault(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) == SupplyTrayPrefabPath);
        if (tray == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에 코스터 공급함(CoasterSupplyTray)이 없습니다.");
            return false;
        }
        field.objectReferenceValue = tray;
        data.ApplyModifiedProperties();
        Debug.Log("[CoasterUISetup] CraftServingView에 코스터 공급함을 연결했습니다.");
        return true;
    }

    [MenuItem("Tools/Tycoon/Setup Coaster UI")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(canvas => canvas.gameObject.scene == scene && canvas.name == TrayCanvasName))
        {
            Debug.LogWarning($"[CoasterUISetup] '{TrayCanvasName}'가 이미 있습니다. 중복 생성을 막기 위해 트레이 생성을 건너뜁니다.");
        }
        else
        {
            CreateTray();
        }

        WireDialogueClickCatcher();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[CoasterUISetup] 코스터 공급함과 대화 클릭 영역을 확인했습니다.");
    }

    /// <summary>
    /// TycoonFlow.dialogueClickCatcher를 화면 전체를 덮는 대화(2부)용 클릭 캐처("OnClickimage")에 연결한다.
    /// Screen Space Overlay는 World Space UI(코스터 드롭존)보다 항상 레이캐스트 우선순위가 높아서,
    /// 이 오브젝트가 켜져 있으면 타이쿤 국면 동안 코스터를 포함한 월드 스페이스 UI가 아예 드롭을 받지 못한다.
    /// </summary>
    static void WireDialogueClickCatcher()
    {
        var tycoonFlow = Object.FindFirstObjectByType<TycoonFlow>();
        if (tycoonFlow == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에서 TycoonFlow를 찾지 못해 dialogueClickCatcher 연결을 건너뜁니다.");
            return;
        }

        var so = new SerializedObject(tycoonFlow);
        var prop = so.FindProperty("dialogueClickCatcher");
        if (prop.objectReferenceValue != null) return; // 이미 연결되어 있으면 건드리지 않음

        var clickCatcher = GameObject.Find("OnClickimage");
        if (clickCatcher == null)
        {
            Debug.LogWarning("[CoasterUISetup] 씬에서 'OnClickimage'를 찾지 못해 dialogueClickCatcher 연결을 건너뜁니다.");
            return;
        }

        prop.objectReferenceValue = clickCatcher;
        so.ApplyModifiedProperties();
        Debug.Log("[CoasterUISetup] TycoonFlow.dialogueClickCatcher를 'OnClickimage'에 연결했습니다.");
    }

    static void CreateTray()
    {
        var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SupplyTrayPrefabPath);
        if (trayPrefab == null)
        {
            Debug.LogError($"[CoasterUISetup] 코스터 트레이 프리팹이 없습니다: {SupplyTrayPrefabPath}");
            return;
        }

        var canvasGO = new GameObject(TrayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        var tray = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, canvasGO.transform);
        foreach (CoasterDragItem coaster in tray.GetComponentsInChildren<CoasterDragItem>(true))
        {
            SetSerializedField(coaster, "rootCanvas", canvas);
        }
        var serving = Object.FindFirstObjectByType<CraftServingView>(FindObjectsInactive.Include);
        if (serving != null)
        {
            SetSerializedField(serving, "serveCanvas", canvas);
            SetSerializedField(serving, "supplyArrow", tray.transform.Find("SupplyArrow").gameObject);
            SetSerializedField(serving, "supplyTray", tray.transform);
        }
    }

    static void SetSerializedField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
