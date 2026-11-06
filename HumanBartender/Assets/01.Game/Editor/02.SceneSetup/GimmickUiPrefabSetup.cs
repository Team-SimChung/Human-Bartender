using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실행 중 생성하던 UI를 프리팹에 한 번 저장한다. 재실행해도 편집된 배치를 유지한다.</summary>
public static class GimmickUiPrefabSetup
{
    public const string Folder = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames";
    public const string ArtFolder = "Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/EditableUI";

    [MenuItem("Tools/Crafting/Save Editable Gimmick UI")]
    public static void Run()
    {
        System.IO.Directory.CreateDirectory(ArtFolder);
        AssetDatabase.Refresh();
        Edit("04.Cap/NewCapGame.prefab", root => new CapUiAuthoring(root.GetComponentInChildren<CapOpenView>(true)).Build());
        Edit("02.Stir/NewStirGame.prefab", BakeStir);
        Edit("03.Pour/NewPourGame.prefab", BakePour);
        Edit("01.Shake/NewShakingMinigame.prefab", BakeShake);
        UpdateStirScene("Assets/02.Dev/01.Scenes/01.Minigames/Stir.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("[EditableGimmickUI] 프리팹 배치와 참조 저장 완료.");
    }

    [MenuItem("Tools/Crafting/Update Saved Shake Gauge")]
    public static void UpdateShakeGauge() => Edit("01.Shake/NewShakingMinigame.prefab", BakeShake);

    internal static void UpdateStirScene(string path)
    {
        if (!System.IO.File.Exists(path)) return;
        var previousActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects()) BakeStir(root);
            // 이미 열려 있던 씬은 미저장 사용자 편집과 함께 유지한다.
            if (openedHere) EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    static void Edit(string relative, Action<GameObject> edit)
    {
        string path = Folder + "/" + relative;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    internal static Object Read(Object target, string field) => new SerializedObject(target).FindProperty(field).objectReferenceValue;
    internal static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    internal static void Set(Object target, string field, Object[] values)
    {
        var data = new SerializedObject(target);
        var array = data.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BakeStir(GameObject root)
    {
        var layout = root.GetComponentInChildren<StirLayoutView>(true);
        if (layout != null && !new SerializedObject(layout).FindProperty("authoredLayout").boolValue)
        {
            foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            layout.RefreshLayout();
            var data = new SerializedObject(layout);
            data.FindProperty("authoredLayout").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var previousMarker = root.transform.Find("Editable UI Layout");
        if (previousMarker != null) Object.DestroyImmediate(previousMarker.gameObject);
        foreach (var gauge in root.GetComponentsInChildren<StirGaugeView>(true))
        {
            var data = new SerializedObject(gauge);
            var segments = data.FindProperty("segments");
            var previous = data.FindProperty("nextOutlines");
            var outlines = new Object[segments.arraySize];
            bool changed = previous.arraySize != segments.arraySize;
            for (int i = 0; i < outlines.Length; i++)
            {
                var segment = (Image)segments.GetArrayElementAtIndex(i).objectReferenceValue;
                if (i < previous.arraySize && previous.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    outlines[i] = previous.GetArrayElementAtIndex(i).objectReferenceValue;
                    continue;
                }
                changed = true;
                var existing = segment.GetComponentInChildren<StirGaugeOutline>(true);
                if (existing != null) { outlines[i] = existing; continue; }
                var go = new GameObject("Next Segment Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(StirGaugeOutline));
                go.transform.SetParent(segment.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var outline = go.GetComponent<StirGaugeOutline>();
                outline.color = new Color(.25f, .72f, 1f, .95f);
                outline.raycastTarget = false;
                outlines[i] = outline;
                go.SetActive(false);
            }
            if (changed) Set(gauge, "nextOutlines", outlines);
        }
    }

    static void BakePour(GameObject root)
    {
        PourHudPrefabSetup.Attach(root);
        var manager = root.GetComponentInChildren<PourManager>(true);
        var data = new SerializedObject(manager);
        if (Read(manager, "targetHeightMarker") == null)
        {
            var glass = (Transform)Read(manager, "glassCenter");
            var driver = new GameObject("Target Height (Recipe Driven)").transform;
            driver.SetParent(glass, false);
            var layout = new GameObject("Marker Layout").transform;
            layout.SetParent(driver, false);
            var half = data.FindProperty("glassInteriorHalfExtents").vector2Value;
            float gap = data.FindProperty("targetMarkerGap").floatValue;
            float size = data.FindProperty("targetMarkerSize").floatValue;
            var material = SaveMaterial("PourGuide", new Color(1f, .94f, .15f));
            var arrow = new GameObject("Arrow", typeof(MeshFilter), typeof(MeshRenderer));
            arrow.transform.SetParent(layout, false);
            arrow.transform.localPosition = new Vector3(half.x + gap, 0f);
            arrow.transform.localScale = new Vector3(size, size, 1f);
            string meshPath = ArtFolder + "/GuideArrow.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Guide Arrow" };
                mesh.vertices = new[] { Vector3.zero, new Vector3(1f, .5f), new Vector3(1f, -.5f) };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.colors = new[] { Color.white, Color.white, Color.white };
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            arrow.GetComponent<MeshFilter>().sharedMesh = mesh;
            arrow.GetComponent<MeshRenderer>().sharedMaterial = material;
            arrow.GetComponent<MeshRenderer>().sortingOrder = 20;
            if (data.FindProperty("showTargetHeightDashedLine").boolValue)
            {
                var dashes = new GameObject("Guide Dashes").transform;
                dashes.SetParent(layout, false);
                float length = data.FindProperty("targetDashLength").floatValue;
                float step = length + data.FindProperty("targetDashGap").floatValue;
                float thickness = data.FindProperty("targetDashThickness").floatValue;
                var square = SaveSquare();
                int i = 0;
                for (float x = -half.x; x < half.x; x += Mathf.Max(.002f, step))
                {
                    var go = new GameObject($"Dash {++i:00}", typeof(SpriteRenderer));
                    go.transform.SetParent(dashes, false);
                    float width = Mathf.Min(length, half.x - x);
                    go.transform.localPosition = new Vector3(x + width * .5f, 0f);
                    go.transform.localScale = new Vector3(width, thickness, 1f);
                    var renderer = go.GetComponent<SpriteRenderer>();
                    renderer.sprite = square;
                    renderer.color = new Color(1f, .94f, .15f);
                    renderer.sortingOrder = 20;
                }
            }
            Set(manager, "targetHeightMarker", driver);
        }
        if (Read(manager, "ingredientBottle") == null)
        {
            var bottle = (BottleTiltController)Read(manager, "bottle");
            var layout = new GameObject("Bottle Art Layout").transform;
            layout.SetParent(bottle.BottleVisual, false);
            var go = new GameObject("Ingredient Bottle", typeof(SpriteRenderer));
            go.transform.SetParent(layout, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            var catalog = (CocktailRecipeVisualCatalog)Read(manager, "bottleVisuals");
            renderer.sprite = catalog != null ? catalog.GetIngredientSprite("beer") : null;
            Set(manager, "ingredientBottle", renderer);
            if (renderer.sprite != null)
            {
                var half = data.FindProperty("bottleInteriorHalfExtents").vector2Value;
                var bounds = renderer.sprite.bounds;
                Vector3 parentScale = bottle.BottleVisual.lossyScale;
                float sx = Mathf.Max(.0001f, Mathf.Abs(parentScale.x));
                float sy = Mathf.Max(.0001f, Mathf.Abs(parentScale.y));
                float scale = Mathf.Min(half.x * 2f * sx / bounds.size.x, half.y * 2f * sy / bounds.size.y);
                go.transform.localScale = new Vector3(scale / sx, scale / sy, 1f);
                go.transform.localPosition = new Vector3(-bounds.center.x * scale / sx, half.y - bounds.max.y * scale / sy);
            }
        }
        var silhouette = root.GetComponentInChildren<BottleSilhouette>(true);
        if (silhouette != null && Read(silhouette, "silhouetteFilter") == null)
        {
            var go = new GameObject("Silhouette", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(silhouette.transform, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = SaveMaterial("BottleSilhouette", Color.white);
            go.GetComponent<MeshRenderer>().sortingOrder = 10;
            Set(silhouette, "silhouetteFilter", go.GetComponent<MeshFilter>());
        }
    }

    static void BakeShake(GameObject root)
    {
        var manager = root.GetComponentInChildren<ShakingManagerNew>(true);
        if (manager != null)
        {
            var balance = (NewBalanceDataSO)Read(manager, "balanceData");
            int count = Mathf.Max(20, balance?.balanceData?.Config.ShakeTargetStacks ?? 20);
            var gauge = (StirGaugeView)Read(manager, "attemptGauge");
            if (gauge != null) EnsureShakeSegments(gauge, count);
        }
        BakeStir(root);
        foreach (var line in root.GetComponentsInChildren<ShakeLineCreator>(true))
        {
            if (Read(line, "dotLine") != null) continue;
            var go = new GameObject("Shake Path", typeof(LineRenderer));
            go.transform.SetParent(line.transform, false);
            var renderer = go.GetComponent<LineRenderer>();
            var data = new SerializedObject(line);
            renderer.useWorldSpace = false;
            renderer.sharedMaterial = SaveMaterial("ShakePath", Color.white);
            renderer.sortingOrder = data.FindProperty("sortingOrder").intValue - 2;
            renderer.startColor = data.FindProperty("startColor").colorValue;
            renderer.endColor = data.FindProperty("endColor").colorValue;
            renderer.startWidth = renderer.endWidth = data.FindProperty("pathWidth").floatValue;
            renderer.positionCount = data.FindProperty("pointsPerSegment").intValue;
            Set(line, "dotLine", renderer);
        }
    }

    static void EnsureShakeSegments(StirGaugeView gauge, int count)
    {
        var data = new SerializedObject(gauge);
        var saved = data.FindProperty("segments");
        if (saved.arraySize >= count) return;
        if (saved.arraySize == 0) throw new InvalidOperationException("셰이크 진행 칸의 원본 이미지가 없습니다.");
        var segments = new Image[count];
        for (int i = 0; i < saved.arraySize; i++)
            segments[i] = (Image)saved.GetArrayElementAtIndex(i).objectReferenceValue;
        var template = segments[saved.arraySize - 1];
        for (int i = saved.arraySize; i < count; i++)
        {
            var copy = Object.Instantiate(template.gameObject, gauge.transform, false);
            copy.name = "Segment " + i;
            segments[i] = copy.GetComponent<Image>();
        }
        // 기존 가로 게이지(부모가 -90도 회전)의 오른쪽부터 채우는 순서를 유지한다.
        var area = (RectTransform)gauge.transform;
        const float padding = 4f, gap = 3f;
        float width = (area.rect.width - padding * 2f - gap * (count - 1)) / count;
        var references = new Object[count];
        for (int i = 0; i < count; i++)
        {
            var rect = segments[i].rectTransform;
            rect.anchoredPosition = new Vector2(area.rect.width - padding - width * .5f - i * (width + gap), rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            references[i] = segments[i];
        }
        Set(gauge, "segments", references);
    }

    internal static Material SaveMaterial(string name, Color color)
    {
        string path = ArtFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default")) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
    static Sprite SaveSquare()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        return CapUiAuthoring.SaveSprite("GuideDash", sprite);
    }
}

internal sealed class CapUiAuthoring
{
    const float Cx = 640f, Cy = 310f, WebWidth = 1280f, WebHeight = 720f;
    readonly CapOpenView view;
    readonly Transform transform;
    Texture2D bottleTexture, openerTexture, haloDefault, haloSuccess;
    TMP_FontAsset font;
    int canvasSortingOrder = 20;
    Canvas canvas;
    RectTransform webRoot, lessonArrow, powerFocus, rhythmFocus;
    CapCanvasLayer halo, body, carbonationLayer, mouth, cap, perfectBurst, opener, overlay, powerGauge, rhythmRings;
    TMP_Text phaseLabel, feedback, timingHint, gaugeLabel, hitLabel, finishLabel, lessonText;
    Button hitButton, finishButton;
    CanvasGroup hitGroup, finishGroup;
    GameObject hitKey, lessonRoot;
    CapLessonShade lessonShade;
    static string L(string ko, string en) => ko;
    static void Set(Object target, string field, Object[] values) => GimmickUiPrefabSetup.Set(target, field, values);

    public CapUiAuthoring(CapOpenView target)
    {
        view = target;
        transform = view.transform;
        bottleTexture = (Texture2D)GimmickUiPrefabSetup.Read(view, "bottleTexture");
        openerTexture = (Texture2D)GimmickUiPrefabSetup.Read(view, "openerTexture");
        font = (TMP_FontAsset)GimmickUiPrefabSetup.Read(view, "font");
    }

    public void Build()
    {
        if (GimmickUiPrefabSetup.Read(view, "hitButton") != null) return;
        BuildUi();
        lessonRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(webRoot);
        foreach (var fitter in webRoot.GetComponentsInChildren<ContentSizeFitter>(true)) Object.DestroyImmediate(fitter);
        foreach (var layout in webRoot.GetComponentsInChildren<LayoutGroup>(true)) Object.DestroyImmediate(layout);
        foreach (var element in webRoot.GetComponentsInChildren<LayoutElement>(true)) Object.DestroyImmediate(element);
        hitButton.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 190f);
        lessonRoot.SetActive(false);
        foreach (string name in new[] { "halo", "body", "carbonationLayer", "mouth", "cap", "perfectBurst", "opener", "overlay", "powerGauge", "rhythmRings", "haloDefault", "haloSuccess", "phaseLabel", "feedback", "timingHint", "gaugeLabel", "hitLabel", "finishLabel", "hitButton", "finishButton", "hitGroup", "finishGroup", "hitKey", "lessonRoot", "lessonShade", "lessonText", "lessonArrow", "powerFocus", "rhythmFocus" })
            GimmickUiPrefabSetup.Set(view, name, (Object)GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));
    }

    void BuildWorld()
    {
        var bottle = NewRect("Bottle Assembly", webRoot);
        Place(bottle, 0, 0, 1280, 720);
        halo = Layer(bottle, "Halo", new Rect(190, -140, 900, 900), null);
        body = Layer(bottle, "Bottle", new Rect(551.8f, 322.6f, 180.6f, 630), bottleTexture);
        carbonationLayer = Layer(bottle, "Carbonation", new Rect(551.8f, 322.6f, 180.6f, 630), null);
        mouth = Layer(bottle, "Bottle Mouth", new Rect(606.4f, 316.3f, 67.2f, 12.6f), null);
        cap = Layer(bottle, "Cap", new Rect(598, 297.4f, 88.2f, 25.2f), bottleTexture);
        perfectBurst = Layer(bottle, "Perfect Burst", new Rect(450, 120, 380, 380), null);
        opener = Layer(bottle, "Opener", new Rect(620, 240, 210, 210), openerTexture);
        overlay = Layer(bottle, "Cracks And Fragments", new Rect(0, 0, 1280, 720), null);
        powerGauge = Layer(webRoot, "Power Gauge", new Rect(391, 240, 48, 250), null);
        rhythmRings = Layer(webRoot, "Timing Rings", new Rect(450, 120, 380, 380), null);
        powerFocus = powerGauge.rectTransform;
        rhythmFocus = rhythmRings.rectTransform;
        haloDefault = SaveTexture("CapHalo", CreateHalo(Hex("#19364926")));
        haloSuccess = SaveTexture("CapHaloSuccess", CreateHalo(Hex("#124e4740")));
        halo.SetTexture(haloDefault);
    }

    static CapCanvasLayer Layer(Transform parent, string name, Rect area, Texture texture)
    {
        var rect = NewRect(name, parent);
        Place(rect, area.x, area.y, area.width, area.height);
        var layer = rect.gameObject.AddComponent<CapCanvasLayer>();
        layer.raycastTarget = false;
        var data = new SerializedObject(layer);
        data.FindProperty("texture").objectReferenceValue = texture;
        data.FindProperty("drawingOrigin").vector2Value = area.position;
        data.FindProperty("referenceSize").vector2Value = area.size;
        data.ApplyModifiedPropertiesWithoutUndo();
        return layer;
    }

    static Texture2D SaveTexture(string name, Texture2D texture)
    {
        string path = GimmickUiPrefabSetup.ArtFolder + "/" + name + ".asset";
        var previous = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (previous != null) { Object.DestroyImmediate(texture); return previous; }
        texture.name = name; texture.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }
    internal static Sprite SaveSprite(string name, Sprite sprite)
    {
        string path = GimmickUiPrefabSetup.ArtFolder + "/" + name + ".asset";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite previous) { Object.DestroyImmediate(sprite.texture); Object.DestroyImmediate(sprite); return previous; }
        var texture = sprite.texture;
        texture.name = name; texture.hideFlags = HideFlags.None;
        sprite.name = name; sprite.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(texture, path);
        AssetDatabase.AddObjectToAsset(sprite, texture);
        return sprite;
    }
    static Texture2D CreateHalo(Color inner)
    {
        const int size = 256;
        var outer = Hex("#080e1700");
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "CapHalo", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 텍스처 한 변이 지름 900px(반지름 450)이다.
                float dx = (x + .5f) / size * 900f - 450f, dy = (y + .5f) / size * 900f - 450f;
                float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 25f) / 425f);
                pixels[y * size + x] = Color.Lerp(inner, outer, t);
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }

    void BuildUi()
    {
        var canvasObject = new GameObject("Cap Web Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WebWidth, WebHeight);
        // 월드 무대도 높이에 맞춰 줄이므로, 글자도 높이를 따라야 둘이 어긋나지 않는다.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();

        webRoot = NewRect("Web Root", canvasObject.transform);
        webRoot.anchorMin = webRoot.anchorMax = new Vector2(.5f, .5f);
        webRoot.pivot = new Vector2(0f, 1f);
        webRoot.sizeDelta = new Vector2(WebWidth, WebHeight);
        webRoot.anchoredPosition = new Vector2(-WebWidth * .5f, WebHeight * .5f);

        BuildWorld();

        // .opening-timing-hint: top 200, 가운데, 24px
        timingHint = Text("Timing Hint", webRoot, "Space", 24f, Hex("#d4fff0"), TextAlignmentOptions.Top);
        Place(timingHint.rectTransform, 0f, 200f, WebWidth, 40f);

        // .opening-feedback: left 780, top 272, 42px
        feedback = Text("Feedback", webRoot, "PERFECT!", 42f, Hex("#8affd1"), TextAlignmentOptions.TopLeft);
        Place(feedback.rectTransform, 780f, 272f, 400f, 60f);

        // .opening-phase: top 45, 가운데, 20px
        phaseLabel = Text("Phase", webRoot, "Space / 클릭으로 시작", 20f, Hex("#e7c88e"), TextAlignmentOptions.Top);
        Place(phaseLabel.rectTransform, 0f, 45f, WebWidth, 32f);

        // 캔버스 fillText('힘', 400, 222): 기준선이 222라서 글자 윗변을 그만큼 올린다.
        gaugeLabel = Text("Gauge Label", webRoot, "힘", 17f, Hex("#e8f6ee"), TextAlignmentOptions.TopLeft);
        Place(gaugeLabel.rectTransform, 400f, 222f - 17f, 120f, 24f);

        BuildHitButton();
        BuildFinishButton();
        BuildLesson();
    }

    void BuildHitButton()
    {
        // .opening-hit: 아래 40, 가운데, 최소 190, 배경 #0b202cf0, 글자 #dffaf5. 테두리는 .primary(#e1c389).
        var rect = NewRect("Opening Hit", webRoot);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 40f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = SaveSprite("CapHit", RoundedSprite(4, 1, Hex("#0b202cf0"), Hex("#e1c389")));
        image.type = Image.Type.Sliced;
        hitButton = rect.gameObject.AddComponent<Button>();
        hitButton.transition = Selectable.Transition.None;
        // 누르기는 입력 라우터가 이미 받는다(클릭·Space). 버튼이 또 누르면 한 번에 두 번 판정한다.
        hitButton.navigation = new Navigation { mode = Navigation.Mode.None };
        hitGroup = rect.gameObject.AddComponent<CanvasGroup>();

        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 11, 11);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rect.gameObject.AddComponent<LayoutElement>().minWidth = 190f;

        hitKey = Kbd(rect, "Space", 12f, Hex("#e9dcb9")).gameObject;
        hitLabel = Text("Label", rect, "시작", 15f, Hex("#dffaf5"), TextAlignmentOptions.Center);
        hitLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void BuildFinishButton()
    {
        // .gimmick-finish: 오른쪽 22, 아래 18, 14px, 안쪽 여백 12/24. .primary 색.
        var rect = NewRect("Gimmick Finish", webRoot);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-22f, 18f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = SaveSprite("CapFinish", RoundedSprite(4, 1, Hex("#e1c389"), Hex("#e1c389")));
        image.type = Image.Type.Sliced;
        finishButton = rect.gameObject.AddComponent<Button>();
        finishButton.transition = Selectable.Transition.None;
        finishGroup = rect.gameObject.AddComponent<CanvasGroup>();

        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 12);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        finishLabel = Text("Label", rect, L("다음 →", "Next →"), 14f, Hex("#1e2227"), TextAlignmentOptions.Center);
        finishLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void BuildLesson()
    {
        // .opening-lesson: 화면 전체를 #020611(0.55)로 덮고 볼 곳만 뚫는다. 입력은 막지 않는다.
        var root = NewRect("Opening Lesson", webRoot);
        Place(root, 0f, 0f, WebWidth, WebHeight);
        lessonRoot = root.gameObject;

        var shade = NewRect("Shade", root);
        Place(shade, 0f, 0f, WebWidth, WebHeight);
        lessonShade = shade.gameObject.AddComponent<CapLessonShade>();
        lessonShade.color = WithAlpha(Hex("#020611"), .55f);
        lessonShade.raycastTarget = false;
        Set(lessonShade, "buttons", new UnityEngine.Object[] { hitButton.transform, finishButton.transform });
        lessonShade.SetFocus(powerFocus);

        // .opening-lesson-copy: left 70, top 230, 너비 290, 안쪽 20/24, 배경 #0a1522f5, 테두리 #68ced7, 둥글기 12.
        var copy = NewRect("Copy", root);
        copy.anchorMin = copy.anchorMax = new Vector2(0f, 1f);
        copy.pivot = new Vector2(0f, 1f);
        copy.anchoredPosition = new Vector2(70f, -230f);
        var background = copy.gameObject.AddComponent<Image>();
        background.sprite = SaveSprite("CapLesson", RoundedSprite(12, 1, Hex("#0a1522f5"), Hex("#68ced7")));
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        var layout = copy.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 20, 20);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = copy.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        copy.sizeDelta = new Vector2(290f, 0f);

        Kbd(copy, "SPACE", 17f, Hex("#9ef5f0"));
        lessonText = Text("Text", copy, "초록 구간에서 Space를 눌러 힘을 조절하세요.", 20f, Hex("#e5ffff"), TextAlignmentOptions.TopLeft);
        // 웹 line-height 1.65
        lessonText.lineSpacing = 40f;
        var textLayout = lessonText.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 290f - 48f;

        var arrowText = Text("Arrow", copy, "→", 36f, Hex("#9ef5f0"), TextAlignmentOptions.Left);
        arrowText.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        lessonArrow = arrowText.rectTransform;
        lessonArrow.anchorMin = lessonArrow.anchorMax = new Vector2(1f, .5f);
        lessonArrow.pivot = new Vector2(0f, 1f);
        lessonArrow.sizeDelta = new Vector2(44f, 48f);

        lessonRoot.SetActive(false);
    }

    /// <summary>둥근 사각형 바깥까지의 거리. 안쪽이면 음수다.</summary>
    static float RoundedRectDistance(float x, float y, float left, float top, float width, float height, float radius)
    {
        float cx = left + width * .5f, cy = top + height * .5f;
        float qx = Mathf.Abs(x - cx) - (width * .5f - radius), qy = Mathf.Abs(y - cy) - (height * .5f - radius);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    // ── 도우미 ───────────────────────────────────────────────────────

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>CSS의 left·top·width·height를 그대로 쓴다(웹 루트 왼쪽 위 기준).</summary>
    static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    TMP_Text Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        var rect = NewRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>웹 kbd: 테두리 #6d7477, 아래 테두리 3px, 배경 #17212a, 둥글기 3, 안쪽 3/6.</summary>
    RectTransform Kbd(Transform parent, string label, float size, Color color)
    {
        var rect = NewRect("Kbd", parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = SaveSprite("CapKey", RoundedSprite(3, 1, Hex("#17212a"), Hex("#6d7477"), 3));
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 5);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var text = Text("Label", rect, label, size, color, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return rect;
    }

    /// <summary>둥근 모서리 9분할 스프라이트를 만든다. bottom을 주면 아래 테두리만 그만큼 굵다.</summary>
    static Sprite RoundedSprite(int radius, int border, Color fill, Color stroke, int bottom = -1)
    {
        if (bottom < 0) bottom = border;
        int size = radius * 2 + 4 + bottom;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "CapRounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f, py = y + .5f;
                float outer = RoundedRectDistance(px, py, 0f, 0f, size, size, radius);
                float coverage = Mathf.Clamp01(.5f - outer);
                if (coverage <= 0f) { pixels[y * size + x] = Color.clear; continue; }
                // 텍스처 y는 아래에서 위로 간다. 아래 테두리(웹 border-bottom)는 y가 작은 쪽이다.
                float inner = RoundedRectDistance(px, py, border, bottom, size - border * 2, size - border - bottom, Mathf.Max(0, radius - border));
                float fillAmount = Mathf.Clamp01(.5f - inner);
                Color color = Color.Lerp(stroke, fill, fillAmount);
                color.a *= coverage;
                pixels[y * size + x] = color;
            }
        texture.SetPixels(pixels);
        texture.Apply(false);
        int edge = radius + Mathf.Max(border, bottom) + 1;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var color);
        return color;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

}
