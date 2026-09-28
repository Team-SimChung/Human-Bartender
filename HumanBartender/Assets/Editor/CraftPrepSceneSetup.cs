// Editor/CraftPrepSceneSetup.cs
// 메뉴: Tools > Tycoon > Setup Craft Prep Scene

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 제조 준비 화면을 혼자 켜서 확인할 수 있는 독립 테스트 씬(Assets/00.Scenes/CraftPrep.unity)과,
/// 그 화면을 그대로 담은 프리팹(Assets/04.Prefabs/Game/CraftPrepScreen.prefab)을 만든다.
///
/// 미니게임 씬들과 같은 방식이다 — 화면을 코드로 조립해 두면 아트가 없어도 흐름을 돌려 볼 수 있고,
/// 나중에 프리팹만 Play 씬에 얹으면 된다.
///
/// 화면은 한 장씩 넘어간다: 잔 → 도구 → 술 선반 → 냉장고. 위쪽에 칵테일 이름과 지금 단계,
/// 가운데 선반 격자, 아래에 이전·다음 버튼과 지금까지 고른 것이 쌓이는 트레이가 있다.
/// 탭은 두지 않는다 — 순서대로만 오가는 것이 이 화면의 규칙이다.
/// </summary>
public static class CraftPrepSceneSetup
{
    const string ScenePath = "Assets/00.Scenes/CraftPrep.unity";
    const string PrefabPath = "Assets/04.Prefabs/Game/CraftPrepScreen.prefab";
    const string FontAssetPath = "Assets/06.Fonts/NeoDunggeunmo SDF.asset";
    const string ShelfDataPath = "Assets/03.Scripts/DataNew/DataNewSO/NewShelfItemDataSO.asset";
    const string CocktailDataPath = "Assets/03.Scripts/DataNew/DataNewSO/NewCocktailDataSO.asset";
    const string BalanceDataPath = "Assets/03.Scripts/DataNew/DataNewSO/NewBalanceDataSO.asset";
    const string ShelfCellPrefabPath = "Assets/04.Prefabs/UI/CraftShelfCell.prefab";
    const string TrayChipPrefabPath = "Assets/04.Prefabs/UI/CraftTrayChip.prefab";

    // 기믹 프리팹. Play 씬의 GimmickRunner가 물고 있는 것과 같은 것들이다.
    const string CapPrefabPath = "Assets/04.Prefabs/Game/NewCapGame.prefab";
    const string PourPrefabPath = "Assets/04.Prefabs/Game/NewPourGame.prefab";
    const string ShakePrefabPath = "Assets/04.Prefabs/Game/NewShakingMinigame.prefab";
    const string StirPrefabPath = "Assets/04.Prefabs/Game/NewStirGame.prefab";

    const float CanvasW = 960f;
    const float CanvasH = 540f;

    /// <summary>바깥 여백. 네 변을 같은 값으로 둬야 격자가 가운데로 읽힌다.</summary>
    const float Pad = 24f;
    const float TopbarH = 40f;
    const float FooterH = 64f;

    static readonly Color Bg = new(0.07f, 0.07f, 0.09f, 1f);
    static readonly Color Panel = new(1f, 1f, 1f, 0.04f);
    static readonly Color Accent = new(0.83f, 0.55f, 0.18f, 0.9f);

    static TMP_FontAsset font;

    [MenuItem("Tools/Tycoon/Setup Craft Prep Scene")]
    public static void Run()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog(
                "CraftPrep 씬 다시 만들기",
                $"'{ScenePath}'가 이미 있습니다.\n덮어쓰면 씬에 직접 준 수정은 모두 사라집니다.\n계속할까요?",
                "덮어쓰기", "취소"))
        {
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        var shelfData = AssetDatabase.LoadAssetAtPath<NewShelfItemDataSO>(ShelfDataPath);
        var cocktailData = AssetDatabase.LoadAssetAtPath<NewCocktailDataSO>(CocktailDataPath);
        var balanceData = AssetDatabase.LoadAssetAtPath<NewBalanceDataSO>(BalanceDataPath);

        if (font == null) Debug.LogWarning($"[CraftPrepSceneSetup] 폰트를 찾지 못했습니다: {FontAssetPath}");
        if (shelfData == null) Debug.LogWarning($"[CraftPrepSceneSetup] 선반 데이터를 찾지 못했습니다: {ShelfDataPath}");
        if (cocktailData == null) Debug.LogWarning($"[CraftPrepSceneSetup] 칵테일 데이터를 찾지 못했습니다: {CocktailDataPath}");
        if (balanceData == null) Debug.LogWarning($"[CraftPrepSceneSetup] 밸런스 데이터를 찾지 못했습니다: {BalanceDataPath}");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        SetupCamera();
        CreateEventSystem();

        // json 로더(ProjectLifeScope)는 VContainerSettings의 루트 스코프라 씬마다 저절로 만들어진다.
        // 여기서 또 넣으면 로더와 사운드 매니저가 두 벌이 된다.

        CraftFlowController craftFlow = BuildCraftLoop(shelfData, cocktailData, balanceData);

        GameObject screen = BuildScreen(shelfData, cocktailData, craftFlow);

        // 기믹을 띄우고 주입을 이어 주려면 실행기와 흐름이 스코프에 올라 있어야 한다.
        new GameObject("CraftPrep LifetimeScope", typeof(CraftPrepLifetimeScope));

        SaveAsPrefab(screen);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log($"[CraftPrepSceneSetup] 씬 '{ScenePath}'과 프리팹 '{PrefabPath}'을 만들었습니다.");
    }

    /// <summary>
    /// 화면 전체를 만든다. 반환한 오브젝트가 그대로 프리팹이 되므로 씬에만 있는 것(카메라·EventSystem·
    /// 데이터 로더)은 이 아래에 넣지 않는다.
    /// </summary>
    /// <summary>
    /// 기믹을 돌리는 쪽을 만든다. 준비 화면과 달리 프리팹으로 저장하지 않는다 — 카메라를 들고 있어서
    /// Play 씬에 그대로 얹으면 그쪽 카메라와 겹친다.
    ///
    /// 실행기와 흐름을 한 오브젝트에 얹고 나머지를 그 아래에 둔다. GimmickRunner가 기믹을 띄우면서
    /// "자기 아래에 있지 않은 겹쳐 그리는 캔버스"를 전부 끄기 때문에, HUD가 그 아래에 있어야 살아남는다.
    /// </summary>
    static CraftFlowController BuildCraftLoop(NewShelfItemDataSO shelfData, NewCocktailDataSO cocktailData,
                                              NewBalanceDataSO balanceData)
    {
        var rootGo = new GameObject("Craft Loop", typeof(GimmickRunner), typeof(CraftFlowController));
        Transform root = rootGo.transform;

        // 기믹이 놓이는 자리. 바에서 멀리 떼어 두는 Play 씬과 달리 여기서는 볼 것이 이것뿐이라 원점에 둔다.
        var gimmickRoot = new GameObject("Gimmick Root").transform;
        gimmickRoot.SetParent(root, false);

        var cameraGo = new GameObject("Gimmick Camera", typeof(Camera));
        cameraGo.transform.SetParent(root, false);
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var gimmickCamera = cameraGo.GetComponent<Camera>();
        gimmickCamera.orthographic = true;
        gimmickCamera.orthographicSize = 5f;
        gimmickCamera.clearFlags = CameraClearFlags.SolidColor;
        gimmickCamera.backgroundColor = Bg;
        cameraGo.SetActive(false); // 기믹이 돌 때만 켜진다.

        CraftGimmickHud hud = BuildGimmickHud(root);

        var runner = rootGo.GetComponent<GimmickRunner>();
        SetField(runner, "gimmickRoot", gimmickRoot);
        SetField(runner, "gimmickCamera", gimmickCamera);
        SetField(runner, "hud", hud);
        SetGimmickPrefabs(runner);

        var craftFlow = rootGo.GetComponent<CraftFlowController>();
        SetField(craftFlow, "runner", runner);
        SetField(craftFlow, "cocktailData", cocktailData);
        SetField(craftFlow, "shelfData", shelfData);
        SetField(craftFlow, "balanceData", balanceData);

        // 준비 화면이 잔·도구·재료를 고르게 할 것이므로 정답 자동 선택은 끈다.
        SetBool(craftFlow, "autoPrepareForTest", false);

        return craftFlow;
    }

    /// <summary>
    /// 기믹이 도는 동안 위에 뜨는 공통 표시(진행·재료·시간·O.K·다음).
    ///
    /// 특히 다음 버튼이 없으면 안 된다 — 따르기·필업처럼 플레이어가 "이만하면 됐다"고 끝내는 기믹은
    /// 이 버튼이 유일한 종료 수단이라, 없으면 그 기믹에서 큐가 멈춘 채 돌아오지 않는다.
    /// </summary>
    static CraftGimmickHud BuildGimmickHud(Transform parent)
    {
        var hudGo = new GameObject("Gimmick Hud",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CraftGimmickHud));
        hudGo.transform.SetParent(parent, false);

        var canvas = hudGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;

        var scaler = hudGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CanvasW, CanvasH);
        scaler.matchWidthOrHeight = 0.5f;

        // 기믹이 돌 때만 켜지는 묶음. 꺼진 채로 시작하는 것은 HudAwake가 알아서 한다.
        RectTransform root = CreateRect(hudGo.transform, "Root",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI progress = CreateText(root, "Progress", "", 15f, TextAlignmentOptions.TopLeft);
        Place(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0.34f, 1f), new Vector2(Pad, -60f), new Vector2(0f, -Pad));

        TextMeshProUGUI subject = CreateText(root, "Subject", "", 17f, TextAlignmentOptions.Top);
        Place(subject.rectTransform, new Vector2(0.34f, 1f), new Vector2(0.66f, 1f), new Vector2(0f, -60f), new Vector2(0f, -Pad));

        TextMeshProUGUI time = CreateText(root, "Time", "", 15f, TextAlignmentOptions.TopRight);
        Place(time.rectTransform, new Vector2(0.66f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(-Pad, -Pad));

        TextMeshProUGUI ok = CreateText(root, "OK Mark", "O.K", 44f, TextAlignmentOptions.Center);
        ok.color = Accent;
        Place(ok.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90f, -34f), new Vector2(90f, 34f));

        Button next = CreateButton(root, "Next", "다음", 128f, 44f);
        var nextRect = next.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0f);
        nextRect.anchorMax = new Vector2(1f, 0f);
        nextRect.pivot = new Vector2(1f, 0f);
        nextRect.anchoredPosition = new Vector2(-Pad, Pad);
        next.GetComponent<Image>().color = Accent;

        var hud = hudGo.GetComponent<CraftGimmickHud>();
        SetField(hud, "root", root.gameObject);
        SetField(hud, "progressText", progress);
        SetField(hud, "subjectText", subject);
        SetField(hud, "timeText", time);
        SetField(hud, "okMark", ok.gameObject);
        SetField(hud, "nextButton", next);

        return hud;
    }

    /// <summary>앵커와 오프셋을 한 번에 준다.</summary>
    static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>기믹 종류와 프리팹 짝을 채운다. 배열이라 SerializedProperty로 한 칸씩 넣는다.</summary>
    static void SetGimmickPrefabs(GimmickRunner runner)
    {
        (ECraftGimmick type, string path)[] entries =
        {
            (ECraftGimmick.Open, CapPrefabPath),
            (ECraftGimmick.Pour, PourPrefabPath),
            (ECraftGimmick.Shake, ShakePrefabPath),
            (ECraftGimmick.Stir, StirPrefabPath),
        };

        var so = new SerializedObject(runner);
        SerializedProperty list = so.FindProperty("prefabs");

        if (list == null)
        {
            Debug.LogWarning("[CraftPrepSceneSetup] GimmickRunner의 prefabs 필드를 찾지 못했습니다.");
            return;
        }

        list.arraySize = entries.Length;

        for (int i = 0; i < entries.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entries[i].path);
            if (prefab == null) Debug.LogWarning($"[CraftPrepSceneSetup] 기믹 프리팹이 없습니다: {entries[i].path}");

            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("type").enumValueIndex = (int)entries[i].type;
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject BuildScreen(NewShelfItemDataSO shelfData, NewCocktailDataSO cocktailData,
                                  CraftFlowController craftFlow)
    {
        var canvasGo = new GameObject("Craft Prep Screen",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CanvasW, CanvasH);
        scaler.matchWidthOrHeight = 0.5f;

        // 준비 중에만 켜지는 묶음. 캔버스 자체는 계속 켜 둬야 신호 구독이 유지된다.
        RectTransform root = CreateRect(canvasGo.transform, "Content",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        CreateImage(root, "Shell", Bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 상단: 칵테일 이름 + 레시피 노트
        RectTransform topbar = CreateRect(root, "Topbar",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Pad, -Pad - TopbarH), new Vector2(-Pad, -Pad));

        TextMeshProUGUI title = CreateText(topbar, "Title", "칵테일", 20f, TextAlignmentOptions.BottomLeft);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(0.55f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // 탭이 없으니 지금 몇 번째 선반인지는 이 글자가 알려 준다.
        TextMeshProUGUI stageLabel = CreateText(topbar, "Stage", "잔  1/4", 13f, TextAlignmentOptions.BottomLeft);
        stageLabel.color = new Color(1f, 1f, 1f, 0.62f);
        var stageRect = stageLabel.rectTransform;
        stageRect.anchorMin = new Vector2(0.55f, 0f);
        stageRect.anchorMax = new Vector2(1f, 1f);
        stageRect.offsetMin = new Vector2(0f, 0f);
        stageRect.offsetMax = new Vector2(-140f, 0f);

        Button noteButton = CreateButton(topbar, "Recipe Note", "레시피 노트", 130f, TopbarH);
        AnchorRight(noteButton.GetComponent<RectTransform>(), 0f);

        // 선반 격자
        float shelfTop = Pad + TopbarH + 12f;
        RectTransform shelfPanel = CreateRect(root, "Shelf Panel",
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(Pad, Pad + FooterH + 8f), new Vector2(-Pad, -shelfTop));
        AddImage(shelfPanel.gameObject, Panel);

        RectTransform shelfContent = CreateRect(shelfPanel, "Shelf Content",
            Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));

        var grid = shelfContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(104f, 116f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(2, 2, 2, 2);
        grid.childAlignment = TextAnchor.UpperLeft;

        // 하단: 고른 것 + 다음
        RectTransform footer = CreateRect(root, "Footer",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(Pad, Pad), new Vector2(-Pad, Pad + FooterH));
        AddImage(footer.gameObject, Panel);

        Button prev = CreateButton(footer, "Prev", "이전", 96f, 40f);
        var prevRect = prev.GetComponent<RectTransform>();
        prevRect.anchorMin = new Vector2(0f, 0.5f);
        prevRect.anchorMax = new Vector2(0f, 0.5f);
        prevRect.pivot = new Vector2(0f, 0.5f);
        prevRect.anchoredPosition = new Vector2(12f, 0f);

        RectTransform trayContent = CreateRect(footer, "Tray Content",
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(118f, 8f), new Vector2(-160f, -26f));

        var trayLayout = trayContent.gameObject.AddComponent<HorizontalLayoutGroup>();
        trayLayout.spacing = 6f;
        trayLayout.childAlignment = TextAnchor.MiddleLeft;
        trayLayout.childForceExpandWidth = false;
        trayLayout.childForceExpandHeight = false;
        trayLayout.childControlWidth = false;
        trayLayout.childControlHeight = false;

        TextMeshProUGUI notice = CreateText(footer, "Notice", "", 11.5f, TextAlignmentOptions.Left);
        notice.color = new Color(1f, 1f, 1f, 0.6f);
        var noticeRect = notice.rectTransform;
        noticeRect.anchorMin = new Vector2(0f, 0f);
        noticeRect.anchorMax = new Vector2(1f, 0f);
        noticeRect.offsetMin = new Vector2(118f, 4f);
        noticeRect.offsetMax = new Vector2(-160f, 22f);

        Button next = CreateButton(footer, "Next", "다음", 128f, 40f);
        TextMeshProUGUI nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
        var nextRect = next.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(1f, 0.5f);
        nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot = new Vector2(1f, 0.5f);
        nextRect.anchoredPosition = new Vector2(-12f, 0f);
        next.GetComponent<Image>().color = Accent;

        // 컴포넌트를 붙이고 방금 만든 자리들을 꽂는다.
        var prep = canvasGo.AddComponent<CraftPrepScreen>();
        SetField(prep, "content", root.gameObject);
        SetField(prep, "shelfData", shelfData);
        SetField(prep, "cocktailData", cocktailData);
        SetField(prep, "shelfContent", shelfContent);
        SetField(prep, "trayContent", trayContent);
        SetField(prep, "titleText", title);
        SetField(prep, "stageText", stageLabel);
        SetField(prep, "noticeText", notice);
        SetField(prep, "prevButton", prev);
        SetField(prep, "nextButton", next);
        SetField(prep, "nextLabel", nextLabel);
        SetField(prep, "recipeNoteButton", noteButton);
        SetField(prep, "craftFlow", craftFlow);
        var shelfCell = AssetDatabase.LoadAssetAtPath<GameObject>(ShelfCellPrefabPath);
        var trayChip = AssetDatabase.LoadAssetAtPath<GameObject>(TrayChipPrefabPath);
        SetField(prep, "shelfCellPrefab", shelfCell != null ? shelfCell.GetComponent<CraftPrepItemView>() : null);
        SetField(prep, "trayChipPrefab", trayChip != null ? trayChip.GetComponent<CraftPrepItemView>() : null);
        SetBool(prep, "openOnStartForTest", true);

        return canvasGo;
    }

    // ── 조각 만들기 ─────────────────────────────────────────────────────

    static RectTransform CreateRect(Transform parent, string name,
                                    Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        return rect;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    static void CreateImage(Transform parent, string name, Color color,
                            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
        AddImage(rect.gameObject, color);
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
                                      TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;

        if (font != null) label.font = font;

        return label;
    }

    static Button CreateButton(Transform parent, string name, string label, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(width, height);

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

        TextMeshProUGUI text = CreateText(go.transform, "Label", label, 13f, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    static void AnchorRight(RectTransform rect, float x)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-x, 0f);
    }

    static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Bg;
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }

    static void CreateEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    static void SaveAsPrefab(GameObject screen)
    {
        string dir = Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        PrefabUtility.SaveAsPrefabAssetAndConnect(screen, PrefabPath, InteractionMode.AutomatedAction);
    }

    // ── private 필드 채우기 ─────────────────────────────────────────────

    static void SetField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[CraftPrepSceneSetup] '{fieldName}' 필드를 찾지 못했습니다.");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string fieldName, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[CraftPrepSceneSetup] '{fieldName}' 필드를 찾지 못했습니다.");
            return;
        }

        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
