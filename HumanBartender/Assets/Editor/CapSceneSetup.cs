// Editor/CapSceneSetup.cs
// 메뉴: Tools > Tycoon > Setup Cap Scene

using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 병따기(Cap) 미니게임을 혼자 켜서 확인할 수 있는 독립 테스트 씬(Assets/00.Scenes/Cap.unity)을
/// 새로 만들어 저장한다. Pour.unity와 같은 역할 — isTest=true로 바로 Play 가능하게 배선한다.
/// 병/뚜껑 비주얼은 흰 사각 스프라이트에 색만 입힌 placeholder이며, 실제 아트로 교체해야 한다.
/// 이미 Cap.unity가 있으면 덮어쓰지 않고 중단한다(재생성하려면 기존 씬을 지우고 다시 실행).
/// </summary>
public static class CapSceneSetup
{
    const string ScenePath = "Assets/00.Scenes/Cap.unity";
    const string FontAssetPath = "Assets/06.Fonts/NeoDunggeunmo SDF.asset";
    const string CraftStationDataPath = "Assets/03.Scripts/CraftLiquidData.asset";
    const string NewCocktailDataSOPath = "Assets/03.Scripts/DataNew/DataNewSO/NewCocktailDataSO.asset";
    const string CraftServePath = "Assets/03.Scripts/Craft/CraftServe.asset";
    const string CraftRetryPath = "Assets/03.Scripts/Craft/CraftRetry.asset";
    const string PlaceholderPath = "Assets/03.Scripts/MiniGame/Cap/CapPlaceholder.png";

    [MenuItem("Tools/Tycoon/Setup Cap Scene")]
    public static void Run()
    {
        if (File.Exists(ScenePath))
        {
            Debug.LogWarning($"[CapSceneSetup] '{ScenePath}'가 이미 있습니다. 덮어쓰지 않고 중단합니다. " +
                              "다시 만들려면 기존 씬 파일을 지우고 실행하세요.");
            return;
        }

        Sprite placeholder = CreateOrLoadPlaceholderSprite();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        CraftStationData craftStationData = AssetDatabase.LoadAssetAtPath<CraftStationData>(CraftStationDataPath);
        NewCocktailDataSO cocktailDataSO = AssetDatabase.LoadAssetAtPath<NewCocktailDataSO>(NewCocktailDataSOPath);
        VoidEvent craftServe = AssetDatabase.LoadAssetAtPath<VoidEvent>(CraftServePath);
        VoidEvent craftRetry = AssetDatabase.LoadAssetAtPath<VoidEvent>(CraftRetryPath);

        if (font == null) Debug.LogWarning($"[CapSceneSetup] 폰트를 찾지 못했습니다: {FontAssetPath}");
        if (craftStationData == null) Debug.LogWarning($"[CapSceneSetup] CraftStationData를 찾지 못했습니다: {CraftStationDataPath}");
        if (cocktailDataSO == null) Debug.LogWarning($"[CapSceneSetup] NewCocktailDataSO를 찾지 못했습니다: {NewCocktailDataSOPath}");
        if (craftServe == null) Debug.LogWarning($"[CapSceneSetup] CraftServe 이벤트를 찾지 못했습니다: {CraftServePath}");
        if (craftRetry == null) Debug.LogWarning($"[CapSceneSetup] CraftRetry 이벤트를 찾지 못했습니다: {CraftRetryPath}");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        SetupCamera();
        CreateEventSystem();

        Transform root = new GameObject("Cap Root").transform;

        // 고리 중심. 캡처 시안처럼 화면 중앙보다 살짝 위에 둬서 아래쪽에 설명 텍스트 자리를 남긴다.
        var ringCenter = new GameObject("Ring Center").transform;
        ringCenter.SetParent(root, false);
        ringCenter.position = new Vector3(0f, 0.9f, 0f);

        CreateBottle(ringCenter, placeholder);
        Transform capPiece = CreateCapPiece(ringCenter, placeholder);

        CapRing targetRing = CreateRing(ringCenter, "Target Ring", 0.03f, 21);
        CapRing incomingRing = CreateRing(ringCenter, "Incoming Ring", 0.045f, 22);

        // HUD 캔버스 자체는 매니저가 참조할 일이 없다(항상 켜져 있음) — 텍스트만 받는다.
        (_, TMP_Text nameText, TMP_Text attemptText, TMP_Text timeText, TMP_Text judgeText) =
            CreateHudCanvas(font);

        (Canvas buttonCanvas, Button serveButton, Button retryButton) = CreateButtonCanvas(font);

        CapManager manager = CreateCapManager(
            root, incomingRing, targetRing, capPiece, buttonCanvas,
            nameText, attemptText, timeText, judgeText,
            craftStationData, cocktailDataSO, craftServe, craftRetry);

        CreateInputHandler(root, manager);

        // AddListener는 런타임 등록이라 씬에 저장되지 않는다. 에디터에서 만드는 씬은 반드시 영속 리스너로 걸어야 한다.
        UnityEventTools.AddVoidPersistentListener(serveButton.onClick, manager.Serve);
        UnityEventTools.AddVoidPersistentListener(retryButton.onClick, manager.Retry);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[CapSceneSetup] '{ScenePath}' 생성 완료. 바로 Play하면 병따기를 테스트할 수 있습니다.\n" +
                  "SPACE 또는 클릭으로 판정합니다. 난이도는 CapManager의 approachSeconds(조여드는 시간) / " +
                  "judgeWindow(판정 폭) / timeLimit(제한 시간)으로 조절하세요.");
    }

    /// <summary>
    /// 1유닛=1스케일이 되는 흰 사각 스프라이트. Unity 내장 Sprite는 PPU가 100이라
    /// localScale을 키워도 매우 작게 보여서, placeholder를 직접 만들어 쓴다.
    /// </summary>
    static Sprite CreateOrLoadPlaceholderSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
        if (existing != null) return existing;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderPath));
        File.WriteAllBytes(PlaceholderPath, png);
        AssetDatabase.ImportAsset(PlaceholderPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 4f;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPath);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 3.5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
    }

    static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    /// <summary>병 몸통. 고리 중심 아래로 길게 내려오게 둔다(뚜껑이 중심에 오도록).</summary>
    static void CreateBottle(Transform parent, Sprite sprite)
    {
        var go = new GameObject("Bottle", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -1.5f, 0f);
        go.transform.localScale = new Vector3(0.9f, 3f, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.42f, 0.24f, 0.12f, 1f);
        sr.sortingOrder = 10;
    }

    /// <summary>병뚜껑. CapManager가 성공/실패 때 이 Transform을 움직인다.</summary>
    static Transform CreateCapPiece(Transform parent, Sprite sprite)
    {
        var go = new GameObject("Cap Piece", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = new Vector3(0.55f, 0.3f, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        sr.sortingOrder = 12;

        return go.transform;
    }

    static CapRing CreateRing(Transform parent, string name, float width, int sortingOrder)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var ring = go.AddComponent<CapRing>();

        SetSerializedField(ring, "lineWidth", width);
        SetSerializedField(ring, "sortingOrder", sortingOrder);

        return ring;
    }

    static (Canvas, TMP_Text, TMP_Text, TMP_Text, TMP_Text) CreateHudCanvas(TMP_FontAsset font)
    {
        var canvasGO = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // 고리보다 위 레이어 — 음료 명이 고리에 가리면 안 된다

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        TMP_Text name = CreateText(canvasGO.transform, "Drink Name", "Bottled Beer", font, 30f,
            new Vector2(0.5f, 1f), new Vector2(0f, -46f), TextAlignmentOptions.Center, new Vector2(420f, 44f));
        TMP_Text attempt = CreateText(canvasGO.transform, "Attempt", "시도 1회째", font, 20f,
            new Vector2(0f, 1f), new Vector2(130f, -34f), TextAlignmentOptions.Left, new Vector2(240f, 32f));
        TMP_Text time = CreateText(canvasGO.transform, "Time", "TIME 0.0 / 25.0", font, 20f,
            new Vector2(1f, 1f), new Vector2(-130f, -34f), TextAlignmentOptions.Right, new Vector2(240f, 32f));
        TMP_Text judge = CreateText(canvasGO.transform, "Judge", string.Empty, font, 34f,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), TextAlignmentOptions.Center, new Vector2(400f, 48f));

        CreateText(canvasGO.transform, "Hint", "고리가 겹치는 순간 SPACE 또는 클릭", font, 18f,
            new Vector2(0.5f, 0f), new Vector2(0f, 44f), TextAlignmentOptions.Center, new Vector2(520f, 30f));

        return (canvas, name, attempt, time, judge);
    }

    static TMP_Text CreateText(Transform parent, string name, string content, TMP_FontAsset font, float size,
                               Vector2 anchor, Vector2 anchoredPosition, TextAlignmentOptions align, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        if (font != null) text.font = font;

        return text;
    }

    static (Canvas, Button, Button) CreateButtonCanvas(TMP_FontAsset font)
    {
        var canvasGO = new GameObject("Button Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        Button serveButton = CreateButton(canvasGO.transform, "Serve Button", "서빙", font, new Vector2(-70f, 40f));
        Button retryButton = CreateButton(canvasGO.transform, "Retry Button", "재시도", font, new Vector2(70f, 40f));

        canvasGO.SetActive(false); // OnNextButton()이 호출될 때까지 숨김 (Pour/Shake/Stur와 동일)

        return (canvas, serveButton, retryButton);
    }

    static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(120f, 48f);
        rect.anchoredPosition = anchoredPosition;

        go.GetComponent<Image>().color = new Color(0.82f, 0.62f, 0.38f, 1f);

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(go.transform, false);

        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        var text = labelGO.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        if (font != null) text.font = font;

        return go.GetComponent<Button>();
    }

    static CapManager CreateCapManager(
        Transform parent, CapRing incomingRing, CapRing targetRing, Transform capPiece, Canvas buttonCanvas,
        TMP_Text nameText, TMP_Text attemptText, TMP_Text timeText, TMP_Text judgeText,
        CraftStationData craftStationData, NewCocktailDataSO cocktailDataSO,
        VoidEvent craftServe, VoidEvent craftRetry)
    {
        var go = new GameObject("Cap Manager");
        go.transform.SetParent(parent, false);

        var manager = go.AddComponent<CapManager>();

        SetSerializedField(manager, "isTest", true);
        SetSerializedField(manager, "data", craftStationData);
        SetSerializedField(manager, "cocktailDataSO", cocktailDataSO);
        SetSerializedField(manager, "incomingRing", incomingRing);
        SetSerializedField(manager, "targetRing", targetRing);
        SetSerializedField(manager, "capPiece", capPiece);
        SetSerializedField(manager, "buttonCanvas", buttonCanvas);
        SetSerializedField(manager, "drinkNameText", nameText);
        SetSerializedField(manager, "attemptText", attemptText);
        SetSerializedField(manager, "timeText", timeText);
        SetSerializedField(manager, "judgeText", judgeText);
        SetSerializedField(manager, "craftServe", craftServe);
        SetSerializedField(manager, "craftRetry", craftRetry);

        return manager;
    }

    static void CreateInputHandler(Transform parent, CapManager manager)
    {
        var go = new GameObject("Cap Input");
        go.transform.SetParent(parent, false);

        var handler = go.AddComponent<CapInputHandler>();
        SetSerializedField(handler, "cap", manager);
    }

    static void SetSerializedField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetSerializedField(Object target, string fieldName, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).boolValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetSerializedField(Object target, string fieldName, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).floatValue = value;
        so.ApplyModifiedProperties();
    }

    static void SetSerializedField(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(fieldName).intValue = value;
        so.ApplyModifiedProperties();
    }
}
