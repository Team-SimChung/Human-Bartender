// Editor/PourSceneSetup.cs
// 메뉴: Tools > Tycoon > Setup Pour Scene

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
/// Pour(따르기) 미니게임을 혼자 켜서 확인할 수 있는 독립 테스트 씬(Assets/00.Scenes/Pour.unity)을
/// 새로 만들어 저장한다. Shake.unity/Stur.unity와 같은 역할 — isTest=true로 바로 Play 가능하게 배선한다.
/// 병/잔을 가두는 벽은 콜라이더가 아니라 PourManager가 bottleInteriorHalfExtents / glassInteriorHalfExtents로
/// 직접 클램프한다. 그래서 여기서는 비주얼만 배치하고 콜라이더는 만들지 않는다.
/// 병은 BottleSilhouette가 메시로 그린다 — PourManager가 액체를 가두는 프로파일(몸통 → 어깨 → 통로)을
/// 그대로 넘겨받으므로 보이는 병목과 액체가 좁아지는 지점이 어긋날 수 없다. 네모 스프라이트를 쓰면
/// 병은 상자인데 액체만 목 모양이라 액체가 병 밖으로 삐져나와 보인다.
/// 잔 비주얼은 1유닛=1스케일짜리 흰 사각 스프라이트에 색만 입힌 placeholder이며, 실제 아트로 교체해야 한다.
/// 이미 Pour.unity가 있으면 덮어쓰지 않고 중단한다(재생성하려면 기존 씬을 지우고 다시 실행).
/// </summary>
public static class PourSceneSetup
{
    const string ScenePath = "Assets/00.Scenes/Pour.unity";
    const string FontAssetPath = "Assets/06.Fonts/NeoDunggeunmo SDF.asset";
    const string GradientMaterialPath = "Assets/03.Scripts/MiniGame/Shaker/New Material.mat";
    const string CraftStationDataPath = "Assets/03.Scripts/CraftLiquidData.asset";
    const string CategoryColorDataPath = "Assets/03.Scripts/MiniGame/Shaker/CategoryColorData.asset";
    const string NewCocktailDataSOPath = "Assets/03.Scripts/DataNew/DataNewSO/NewCocktailDataSO.asset";
    const string CraftServePath = "Assets/03.Scripts/Craft/CraftServe.asset";
    const string CraftRetryPath = "Assets/03.Scripts/Craft/CraftRetry.asset";

    [MenuItem("Tools/Tycoon/Setup Pour Scene")]
    public static void Run()
    {
        if (File.Exists(ScenePath))
        {
            Debug.LogWarning($"[PourSceneSetup] '{ScenePath}'가 이미 있습니다. 덮어쓰지 않고 중단합니다. " +
                              "다시 만들려면 기존 씬 파일을 지우고 실행하세요.");
            return;
        }

        Sprite placeholderSprite = CreateOrLoadPlaceholderSprite();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Material gradientMaterial = AssetDatabase.LoadAssetAtPath<Material>(GradientMaterialPath);
        CraftStationData craftStationData = AssetDatabase.LoadAssetAtPath<CraftStationData>(CraftStationDataPath);
        CategoryColorData categoryColorData = AssetDatabase.LoadAssetAtPath<CategoryColorData>(CategoryColorDataPath);
        NewCocktailDataSO cocktailDataSO = AssetDatabase.LoadAssetAtPath<NewCocktailDataSO>(NewCocktailDataSOPath);
        VoidEvent craftServe = AssetDatabase.LoadAssetAtPath<VoidEvent>(CraftServePath);
        VoidEvent craftRetry = AssetDatabase.LoadAssetAtPath<VoidEvent>(CraftRetryPath);

        if (font == null) Debug.LogWarning($"[PourSceneSetup] 폰트를 찾지 못했습니다: {FontAssetPath}");
        if (gradientMaterial == null) Debug.LogWarning($"[PourSceneSetup] 게이지 머티리얼을 찾지 못했습니다: {GradientMaterialPath}");
        if (craftStationData == null) Debug.LogWarning($"[PourSceneSetup] CraftStationData를 찾지 못했습니다: {CraftStationDataPath}");
        if (categoryColorData == null) Debug.LogWarning($"[PourSceneSetup] CategoryColorData를 찾지 못했습니다: {CategoryColorDataPath}");
        if (cocktailDataSO == null) Debug.LogWarning($"[PourSceneSetup] NewCocktailDataSO를 찾지 못했습니다: {NewCocktailDataSOPath}");
        if (craftServe == null) Debug.LogWarning($"[PourSceneSetup] CraftServe 이벤트를 찾지 못했습니다: {CraftServePath}");
        if (craftRetry == null) Debug.LogWarning($"[PourSceneSetup] CraftRetry 이벤트를 찾지 못했습니다: {CraftRetryPath}");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        SetupCamera();
        CreateEventSystem();

        Transform root = new GameObject("Pour Root").transform;

        (BottleTiltController bottle, BottleSilhouette bottleSilhouette) = CreateBottle(root);
        Transform glassCenter = CreateGlass(root, placeholderSprite);
        SphLiquidRenderer liquidRenderer = CreateLiquidRenderer(root);
        LiquidProfile liquidProfile = CreateOrLoadLiquidProfiles();
        CreateInputHandler(root, bottle);

        GradientRatioController gageBar = CreateGaugeCanvas(gradientMaterial);
        (Canvas buttonCanvas, Button serveButton, Button retryButton) = CreateButtonCanvas(font);

        PourManager manager = CreatePourManager(
            root, bottle, bottleSilhouette, glassCenter, liquidRenderer, liquidProfile, gageBar, buttonCanvas,
            craftStationData, categoryColorData, cocktailDataSO, craftServe, craftRetry);

        UnityEventTools.AddVoidPersistentListener(serveButton.onClick, manager.Serve);
        UnityEventTools.AddVoidPersistentListener(retryButton.onClick, manager.Retry);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log("[PourSceneSetup] 완료. Pour.unity가 생성되어 저장되었습니다. " +
                  "Play 전에 CraftLiquidData.asset의 targetCocktailId가 유효한 칵테일 id인지 확인하세요(Shake/Stur 테스트와 동일 조건). " +
                  "액체의 점도/질감은 Profiles 폴더의 LiquidProfile 에셋에서, 양은 PourManager의 " +
            "bottleParticleCount로 튜닝합니다. " +
                  "병/잔 비주얼과 배치는 placeholder이니 실제 아트로 교체해주세요.");
    }

    /// <summary>
    /// 4x4 흰색 텍스처를 PPU=4로 임포트한 스프라이트를 만들어(또는 이미 있으면 재사용) 반환한다.
    /// 내장 UISprite 리소스는 UI용으로 만들어져 있어 실제 PPU가 커서 월드 스페이스에 놓으면
    /// localScale을 아무리 키워도 매우 작게 보인다 — 그래서 1유닛=1스케일이 되는 스프라이트를 직접 만든다.
    /// PNG로 저장하는 실제 에셋이라 씬 저장 시에도 참조가 끊기지 않는다.
    /// </summary>
    static Sprite CreateOrLoadPlaceholderSprite()
    {
        const string path = "Assets/03.Scripts/MiniGame/Pour/PourPlaceholder.png";

        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 4f;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void SetupCamera()
    {
        Camera cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 3.5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    }

    static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    static (BottleTiltController, BottleSilhouette) CreateBottle(Transform parent)
    {
        var go = new GameObject("Bottle");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(-1.6f, 1.2f, 0f);
        go.transform.localScale = new Vector3(1.4f, 3.6f, 1f);

        var controller = go.AddComponent<BottleTiltController>();
        SetSerializedField(controller, "bottleVisual", go.transform);

        // 병 모양은 스프라이트가 아니라 메시로 그린다. PourManager가 액체를 가두는 내부 프로파일
        // (몸통 → 어깨 → 통로)을 그대로 넘겨 그리기 때문에, 보이는 병목과 액체가 좁아지는 지점이
        // 항상 일치한다. 네모 스프라이트를 두면 병은 상자인데 액체만 목 모양이라 어긋나 보인다.
        var silhouette = go.AddComponent<BottleSilhouette>();

        return (controller, silhouette);
    }

    static Transform CreateGlass(Transform parent, Sprite sprite)
    {
        var go = new GameObject("Glass", typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(1.4f, -1.0f, 0f);
        go.transform.localScale = new Vector3(1.6f, 2.7f, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.85f, 0.85f, 0.9f, 0.35f);
        sr.sortingOrder = 5;

        return go.transform;
    }

    /// <summary>
    /// 액체 프로파일 프리셋을 만든다(이미 있으면 그대로 둔다). 값 차이가 어떤 느낌으로 이어지는지
    /// 바로 비교해볼 수 있도록 서로 다른 성격 셋을 깔아두고, 기본(가벼운 술)을 씬에 꽂는다.
    ///
    /// 셋이 다른 건 점도와 흐를 때의 질감뿐이다 — 알갱이 굵기는 SetGrain()으로 통일한다.
    /// </summary>
    static LiquidProfile CreateOrLoadLiquidProfiles()
    {
        const string folder = "Assets/03.Scripts/MiniGame/Pour/Profiles";
        Directory.CreateDirectory(folder);

        // 가벼운 증류주 — 잘 흩어지고 산뜻하게 흐른다.
        LiquidProfile light = CreateProfileIfMissing($"{folder}/Liquid_Light.asset", p =>
        {
            SetGrain(p);
            p.physics.viscosity = 0.12f;
            p.physics.cohesion = 0.25f;
            p.physics.velocitySmoothing = 0.2f;
            p.stretchPerSpeed = 0.55f;
            p.maxStretch = 4.5f;
            p.streamThinning = 0.45f;
        });

        // 기본 — 리큐어 정도의 중간 질감.
        CreateProfileIfMissing($"{folder}/Liquid_Default.asset", p =>
        {
            SetGrain(p);
            p.physics.viscosity = 0.2f;
            p.physics.velocitySmoothing = 0.25f;
            p.stretchPerSpeed = 0.45f;
            p.maxStretch = 4f;
            p.streamThinning = 0.35f;
        });

        // 시럽 — 끈적하게 뭉쳐 늘어진다. 점도가 속도를 잡아주므로 늘임은 오히려 덜 필요하다.
        CreateProfileIfMissing($"{folder}/Liquid_Syrup.asset", p =>
        {
            SetGrain(p);
            p.physics.viscosity = 0.5f;
            p.physics.cohesion = 0.5f; // 끈적한 액체는 뭉치는 성질이 강해야 늘어지는 느낌이 난다
            p.physics.velocitySmoothing = 0.45f;
            p.stretchPerSpeed = 0.3f;
            p.maxStretch = 3f;
            p.streamThinning = 0.15f;
        });

        AssetDatabase.SaveAssets();
        return light;
    }

    /// <summary>
    /// 알갱이 굵기와 그 짝인 렌더 값. 술마다 다르게 두지 않고 전 프로파일이 같은 값을 쓴다.
    ///
    /// 이건 질감이 아니라 해상도 값이라서다 — spacing에는 병목 통로 폭(neckWidthInParticles)과
    /// 파티클 수가 물려 있어서, 술마다 다르면 술을 바꿀 때마다 액체가 얼마나 곱게 보이는지와
    /// 성능까지 같이 흔들린다. 세 값은 서로 짝이라 하나만 바꾸면 액체가 낱알로 흩어지거나 뭉툭해진다.
    /// </summary>
    static void SetGrain(LiquidProfile p)
    {
        p.physics.spacing = 0.16f;
        p.blobRadiusScale = 1.8f;
        p.threshold = 0.5f;
        p.edgeSmoothness = 0.08f;
    }

    static LiquidProfile CreateProfileIfMissing(string path, System.Action<LiquidProfile> configure)
    {
        var existing = AssetDatabase.LoadAssetAtPath<LiquidProfile>(path);
        if (existing != null) return existing;

        var profile = ScriptableObject.CreateInstance<LiquidProfile>();
        configure(profile);

        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    static SphLiquidRenderer CreateLiquidRenderer(Transform parent)
    {
        var go = new GameObject("Liquid Renderer");
        go.transform.SetParent(parent, false);

        return go.AddComponent<SphLiquidRenderer>();
    }

    static void CreateInputHandler(Transform parent, BottleTiltController bottle)
    {
        var go = new GameObject("Input Handler");
        go.transform.SetParent(parent, false);

        var handler = go.AddComponent<PourInputHandler>();
        SetSerializedField(handler, "bottle", bottle);
    }

    static GradientRatioController CreateGaugeCanvas(Material gradientMaterial)
    {
        var canvasGO = new GameObject("Gauge Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);

        var gaugeGO = new GameObject("Gauge", typeof(RectTransform), typeof(Image));
        gaugeGO.transform.SetParent(canvasGO.transform, false);

        var rect = gaugeGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(300f, 24f);
        rect.anchoredPosition = new Vector2(0f, -20f);

        var image = gaugeGO.GetComponent<Image>();
        if (gradientMaterial != null) image.material = gradientMaterial;

        var gageBar = gaugeGO.AddComponent<GradientRatioController>();
        SetSerializedField(gageBar, "targetImage", image);

        return gageBar;
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

        canvasGO.SetActive(false); // OnNextButton()이 호출될 때까지 숨김 (Shake/Stur의 buttonCanvas와 동일)

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

    static PourManager CreatePourManager(
        Transform parent, BottleTiltController bottle, BottleSilhouette bottleSilhouette, Transform glassCenter,
        SphLiquidRenderer liquidRenderer, LiquidProfile liquidProfile,
        GradientRatioController gageBar, Canvas buttonCanvas,
        CraftStationData craftStationData, CategoryColorData categoryColorData, NewCocktailDataSO cocktailDataSO,
        VoidEvent craftServe, VoidEvent craftRetry)
    {
        var go = new GameObject("Pour Manager");
        go.transform.SetParent(parent, false);

        var manager = go.AddComponent<PourManager>();

        SetSerializedField(manager, "isTest", true);
        SetSerializedField(manager, "data", craftStationData);
        SetSerializedField(manager, "colorData", categoryColorData);
        SetSerializedField(manager, "cocktailDataSO", cocktailDataSO);
        SetSerializedField(manager, "bottle", bottle);
        SetSerializedField(manager, "bottleSilhouette", bottleSilhouette);
        SetSerializedField(manager, "glassCenter", glassCenter);
        SetSerializedField(manager, "liquidRenderer", liquidRenderer);
        SetSerializedField(manager, "liquidProfile", liquidProfile);
        SetSerializedField(manager, "gageBar", gageBar);
        SetSerializedField(manager, "buttonCanvas", buttonCanvas);
        SetSerializedField(manager, "craftServe", craftServe);
        SetSerializedField(manager, "craftRetry", craftRetry);

        return manager;
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
}
