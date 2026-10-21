using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>승인된 UI 갱신을 에셋에 저장하는 1회성 편집 도구. 런타임 생성/에디터 자동 실행은 하지 않는다.</summary>
public static class VisualUISetup
{
    const string Art = "Assets/01.Game/03.Content/01.ArtResources/05.UI/";
    const string Prefabs = "Assets/01.Game/03.Content/08.Prefabs/";
    const string Dialogue = Prefabs + "03.UI/02.Dialogue/Dialogue TextBox.prefab";
    const string Scope = Prefabs + "04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    static Color Ink => Hex("0b1720");
    static Color Cyan => Hex("43d6ee");
    static Color Line => Hex("2b4e5e");
    static Color Muted => Hex("88a6b3");
    static TMP_FontAsset BodyFont => Font("Medium_Paperlogy_SDF");
    static TMP_FontAsset PixelFont => Font("Regular_PFStardust_3_0_SDF");
    static TMP_FontAsset Font(string name) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/01.Game/03.Content/05.Fonts/" + name + ".asset");
    static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }
    static T Get<T>(Object owner, string key) where T : Object => (T)new SerializedObject(owner).FindProperty(key).objectReferenceValue;
    static void Set(Object owner, string key, Object value)
    {
        var so = new SerializedObject(owner); so.FindProperty(key).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetArray(Object owner, string key, Object[] values)
    {
        var so = new SerializedObject(owner); var p = so.FindProperty(key); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Sprite Sprite(string relative) => AssetDatabase.LoadAllAssetsAtPath(Art + relative).OfType<Sprite>()
        .OrderByDescending(s => s.name.EndsWith("__UIFull", StringComparison.Ordinal))
        .ThenByDescending(s => s.rect.width * s.rect.height).First();

    static void EnsureFullSprite(string relative)
    {
        string path = Art + relative;
        string metadataBefore = File.ReadAllText(path + ".meta");
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.spriteImportMode != SpriteImportMode.Multiple) return;
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var rects = provider.GetSpriteRects().ToList(); string name = Path.GetFileNameWithoutExtension(path) + "__UIFull";
        if (rects.Any(r => r.name == name)) return;
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        var rect = new SpriteRect { name = name, rect = new Rect(0, 0, width, height), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f), spriteID = GUID.Generate() };
        rects.Add(rect); provider.SetSpriteRects(rects.ToArray());
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = names.GetNameFileIdPairs().ToList(); pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID)); names.SetNameFileIdPairs(pairs);
        provider.Apply(); importer.SaveAndReimport();
        // Sprite Editor normalizes even untouched pivots. Keep the original entries byte-for-byte.
        string metadata = PreserveExistingSpriteEntries(metadataBefore, File.ReadAllText(path + ".meta"));
        File.WriteAllText(path + ".meta", metadata);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
    static string PreserveExistingSpriteEntries(string before, string after)
    {
        const string entry = @"    - serializedVersion: 2\r?\n      name: [^\r\n]+[\s\S]*?(?=\r?\n    - serializedVersion: 2|\r?\n    outline:)";
        var existing = Regex.Matches(before, entry).Cast<Match>()
            .ToDictionary(m => Regex.Match(m.Value, @"      name: ([^\r\n]+)").Groups[1].Value, m => m.Value);
        after = Regex.Replace(after, entry, m => existing.TryGetValue(Regex.Match(m.Value, @"      name: ([^\r\n]+)").Groups[1].Value, out var value) ? value : m.Value);
        const string rootId = @"(?m)^    spriteID:[^\r\n]*";
        var original = Regex.Match(before, rootId);
        return original.Success ? Regex.Replace(after, rootId, original.Value) : after;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.gameObject.layer = 5; r.SetParent(parent, false); Position(r, x, y, width, height); return r;
    }
    static void Position(RectTransform r, float x, float y, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); r.localScale = Vector3.one;
    }
    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
    }
    static Image Image(string name, Transform parent, float x, float y, float w, float h, Color color, Sprite sprite = null)
    {
        var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.color = color; image.sprite = sprite; image.raycastTarget = false; return image;
    }
    static TMP_Text Text(string name, Transform parent, string text, float x, float y, float w, float h, float size = 15, Color? color = null, TMP_FontAsset font = null)
    {
        var label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : BodyFont; label.text = text; label.fontSize = size;
        label.color = color ?? Hex("daedf0"); label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
        return label;
    }
    static Button Button(string name, Transform parent, string text, float x, float y, float w, float h, bool primary = false)
    {
        var image = Image(name, parent, x, y, w, h, primary ? Cyan : Hex("162f3b")); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Hex("b8f8ff"); colors.pressedColor = Hex("64aabb"); colors.selectedColor = colors.highlightedColor; button.colors = colors;
        var label = Text("Label", button.transform, text, 10, 0, w - 20, h, 14, primary ? Ink : Hex("cce4ec"));
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }
    static void ClearChildren(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }

    [MenuItem("Tools/Human Bartender/UI/Apply reviewed visual update")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Scope);
        if (existing != null && existing.GetComponentInChildren<OptionsPanelView>(true) != null)
        {
            Debug.Log("[VisualUISetup] 이미 적용된 프로젝트입니다. 이후 배치는 기존 씬/프리팹 Inspector에서 조정하세요.");
            return;
        }
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "02.Title/01.Sprites/title_street_full.png");
            importer.maxTextureSize = 4096; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            foreach (string name in new[] { "BTN_Disable", "BTN_Enable", "IMG_logo", "IMG_Deco_button", "PTN_Dot" })
                EnsureFullSprite("02.Title/01.Sprites/" + name + ".png");
            UpdateDialogue(); UpdateChoices(); UpdateRecipeRow(); UpdateSettings(); UpdateTitle(); UpdatePlayDialogue();
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualUISetup] 대화·선택지·타이틀·공용 설정을 저장했습니다.");
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [MenuItem("Tools/Human Bartender/UI/Fix dialogue name spacing")]
    public static void FixDialogueNameSpacing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            UpdateDialogue(); UpdatePlayDialogue(); AssetDatabase.SaveAssets();
            Debug.Log("[Dialogue spacing] smaller sky-blue names and independent frame/header spacing saved.");
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void StyleBubble(DynamicSpeechBubble bubble, bool player)
    {
        bubble.minSize = new Vector2(240, 112); bubble.maxSize = new Vector2(390, 168);
        bubble.paddingLeft = bubble.paddingRight = 28.5f;
        bubble.paddingTop = 56; bubble.paddingBottom = 24;
        bubble.baseFontSize = 13.5f; bubble.minFontSize = 11;
        bubble.textLabel.font = PixelFont; bubble.textLabel.fontSharedMaterial = PixelFont.material; bubble.textLabel.fontSize = 13.5f; bubble.textLabel.lineSpacing = 12;
        bubble.textLabel.alignment = TextAlignmentOptions.TopLeft;
        bubble.textLabel.raycastTarget = false;
        var name = bubble.nameLabel;
        name.font = Font("Bold_PFStardust_3_0_SDF"); name.fontSharedMaterial = name.font.material; name.enableAutoSizing = false; name.fontSize = 16; name.fontSizeMin = name.fontSizeMax = 16;
        bubble.speakerNameColor = new Color32(143, 220, 255, 255); name.color = bubble.speakerNameColor;
        name.raycastTarget = false; name.alignment = TextAlignmentOptions.TopLeft;
        var r = name.rectTransform; r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(28.5f, -22); r.sizeDelta = new Vector2(-57, 18);
        var image = bubble.GetComponent<Image>();
        image.sprite = Sprite("01.Shared/01.MainScreen/" + (player ? "GRD_textbox_luna.png" : "GRD_textbox.png"));
        image.type = UnityEngine.UI.Image.Type.Simple;
        // 루트 사각형은 실제 본문 박스다. 원본 PNG의 꼬리는 그 바깥에 배치하여 여백에 포함되지 않게 한다.
        // 기존 루트 Image/fileID와 클릭 영역은 유지한다.
        image.color = Color.clear;
        var frameTransform = bubble.transform.Find("Frame");
        var frame = frameTransform != null ? frameTransform.GetComponent<Image>() :
            Image("Frame", bubble.transform, 0, 0, 0, 0, Color.white);
        frame.transform.SetAsFirstSibling(); frame.sprite = image.sprite;
        frame.color = Color.white; frame.type = UnityEngine.UI.Image.Type.Simple; frame.raycastTarget = false;
        var frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
        var frameMesh = frame.GetComponent<DialogueFrameMesh>() ?? frame.gameObject.AddComponent<DialogueFrameMesh>();
        frameMesh.bodyInsets = player ? new Vector4(0, 66, 0, 4) : new Vector4(0, 5, 0, 71);
        // 원본 프레임에 꼬리가 포함되어 있다. 이전 꼬리/네임태그를 겹치지 않는다.
        foreach (Transform child in bubble.transform)
            if (child.name == "Image" || child.name == "Nametag") child.gameObject.SetActive(false);
        var indicator = bubble.transform.Find("Input Wait Indicator") as RectTransform;
        if (indicator != null)
        {
            indicator.anchorMin = indicator.anchorMax = new Vector2(1, 0);
            indicator.anchoredPosition = new Vector2(-20, 16); indicator.sizeDelta = new Vector2(10, 8);
        }
        bubble.bubble.sizeDelta = bubble.minSize;
    }
    static void UpdateDialogue()
    {
        var root = PrefabUtility.LoadPrefabContents(Dialogue);
        try { StyleBubble(root.GetComponent<DynamicSpeechBubble>(), false); PrefabUtility.SaveAsPrefabAsset(root, Dialogue); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void UpdatePlayDialogue()
    {
        const string path = "Assets/01.Game/01.Scenes/Play.unity";
        string original = File.ReadAllText(path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIDialogueTextView>(true)).Single();
        foreach (var b in new[] { view.customerSpeechBubble, view.lunaSpeechBubble })
        {
            StyleBubble(b, b == view.lunaSpeechBubble);
            if (b == view.lunaSpeechBubble)
                b.bubble.anchoredPosition = new Vector2(b.bubble.anchoredPosition.x, Mathf.Max(b.bubble.anchoredPosition.y, 44));
            foreach (var c in b.GetComponentsInChildren<Component>(true)) if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        SaveSceneKeepingCameraRects(scene, original);
    }
    static void UpdateChoices()
    {
        string path = Prefabs + "03.UI/02.Dialogue/ChoicePanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(450, 48.7793f);
            var hold = root.GetComponent<ChoiceHoldButton>();
            root.GetComponent<Image>().sprite = Sprite("01.Shared/01.MainScreen/IMG_choice_A.png");
            var states = hold.spriteState;
            states.highlightedSprite = states.pressedSprite = Sprite("01.Shared/01.MainScreen/IMG_choice_B.png");
            states.selectedSprite = null;
            hold.spriteState = states;
            var panel = root.GetComponent<ChoicePanel>();
            Get<UnityEngine.UI.Text>(panel, "panelText").gameObject.SetActive(false);
            var label = Get<TextMeshProUGUI>(panel, "panelTMPText"); label.gameObject.SetActive(true);
            label.font = PixelFont; label.fontSharedMaterial = PixelFont.material; label.fontSize = 13.5f; label.lineSpacing = 0; label.text = "선택지 예시";
            label.alignment = TextAlignmentOptions.Midline; label.enableAutoSizing = false;
            label.color = Hex("bfc0c1");
            label.raycastTarget = false;
            label.rectTransform.offsetMin = new Vector2(24, 4); label.rectTransform.offsetMax = new Vector2(-24, -4);
            if (root.GetComponent<ChoiceFrameMesh>() == null) root.AddComponent<ChoiceFrameMesh>();
            var fx = root.GetComponent<UIChoiceGlitch>() ?? root.AddComponent<UIChoiceGlitch>();
            Set(fx, "label", label); Set(fx, "holdButton", hold);
            var colors = new SerializedObject(fx);
            colors.FindProperty("restingColor").colorValue = Hex("bfc0c1");
            colors.FindProperty("focusedColor").colorValue = Hex("54dfea");
            colors.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Human Bartender/UI/Fix dialogue and choice frames")]
    public static void FixFrames()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            UpdateDialogue(); UpdateChoices(); UpdatePlayDialogue();
            const string play = "Assets/01.Game/01.Scenes/Play.unity";
            string original = File.ReadAllText(play);
            var scene = EditorSceneManager.OpenScene(play, OpenSceneMode.Single);
            foreach (var choice in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChoicePanel>(true)))
            {
                var label = Get<TextMeshProUGUI>(choice, "panelTMPText");
                var so = new SerializedObject(label);
                foreach (string field in new[] { "m_fontAsset", "m_sharedMaterial" })
                    if (so.FindProperty(field).prefabOverride) PrefabUtility.RevertPropertyOverride(so.FindProperty(field), InteractionMode.AutomatedAction);
            }
            SaveSceneKeepingCameraRects(scene, original); AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void SkinButton(Button button, string normal, string hover = null)
    {
        var image = button.GetComponent<Image>(); image.sprite = Sprite(normal); image.type = UnityEngine.UI.Image.Type.Simple; image.color = Color.white;
        if (hover != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState; state.highlightedSprite = state.pressedSprite = state.selectedSprite = Sprite(hover); button.spriteState = state;
        }
    }
    static void UpdatePlayAuxiliary(UnityEngine.SceneManagement.Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var service = roots.SelectMany(r => r.GetComponentsInChildren<ServicePanelController>(true)).Single();
        const string serviceArt = "01.Shared/01.MainScreen/service_panel/";
        var toggle = Get<Button>(service, "toggleButton"); SkinButton(toggle, serviceArt + "BTN_service_panel.png");
        // 최신 아이콘에는 문구가 없다. 기존 110×35 클릭 영역을 유지하고 아이콘만 정사각형으로 표시한다.
        toggle.GetComponent<Image>().preserveAspect = true;
        foreach (var label in toggle.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        SkinButton(Get<Button>(service, "closeButton"), serviceArt + "BTN_service_close.png");
        foreach (var button in Get<GameObject>(service, "serviceMenuPanel").GetComponentsInChildren<Button>(true))
            if (button != Get<Button>(service, "closeButton")) SkinButton(button, serviceArt + "BTN_service_A.png", serviceArt + "BTN_service_B.png");
        foreach (var label in Get<GameObject>(service, "serviceMenuPanel").GetComponentsInChildren<TMP_Text>(true)) label.font = PixelFont;
        var recipe = Get<RecipeBrowserScreen>(service, "recipeScreen");
        const string recipeArt = "01.Shared/01.MainScreen/recipe_choice/";
        SkinButton(Get<Button>(recipe, "closeButton"), recipeArt + "BTN_recipe_choice_close.png");
        var browser = Get<CocktailRecipeBrowser>(recipe, "browser");
        var scroll = Get<ScrollRect>(browser, "listScrollRect");
        if (scroll.verticalScrollbar != null)
        {
            var track = scroll.verticalScrollbar.GetComponent<Image>();
            if (track != null) { track.sprite = Sprite(recipeArt + "IMG_scroll_track.png"); track.type = UnityEngine.UI.Image.Type.Simple; }
            var thumb = scroll.verticalScrollbar.targetGraphic as Image;
            if (thumb != null) { thumb.sprite = Sprite(recipeArt + "IMG_scroll_thumb.png"); thumb.type = UnityEngine.UI.Image.Type.Simple; }
        }
    }
    static void UpdateRecipeRow()
    {
        const string path = Prefabs + "01.Crafting/04.Recipes/CocktailRecipeRow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            const string art = "01.Shared/01.MainScreen/recipe_choice/";
            SkinButton(root.GetComponent<Button>(), art + "BTN_recipe_choice_A.png", art + "BTN_recipe_choice_B.png");
            // 새 버튼 원본에 오른쪽 화살표가 포함되어 있다.
            var arrow = root.transform.Find("ArrowIcon"); if (arrow != null) arrow.gameObject.SetActive(false);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.font = PixelFont;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void UpdateTitle()
    {
        const string path = "Assets/01.Game/01.Scenes/Main.unity";
        string original = File.ReadAllText(path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single(c => c.name == "Canvas_Title");
        var manager = roots.SelectMany(r => r.GetComponentsInChildren<MainManager>(true)).Single();
        ClearChildren(canvas.transform);
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var bg = Rect("City backdrop", canvas.transform, 0, 0, 1280, 720).gameObject.AddComponent<RawImage>();
        bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "02.Title/01.Sprites/title_street_full.png");
        bg.uvRect = new Rect(5080f / 7680f, 40f / 4320f, 1f / 6f, 1f / 6f); bg.raycastTarget = false;
        Stretch(bg.rectTransform);
        var backgroundFit = bg.gameObject.AddComponent<AspectRatioFitter>();
        backgroundFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; backgroundFit.aspectRatio = 16f / 9f;
        var shade = Image("Backdrop shade", canvas.transform, 0, 0, 1280, 720, new Color(0, .02f, .04f, .18f)); Stretch(shade.rectTransform);
        var composition = Rect("Title composition", canvas.transform, 0, 0, 1280, 720);
        composition.anchorMin = composition.anchorMax = new Vector2(.5f, .5f); composition.pivot = new Vector2(.5f, .5f); composition.anchoredPosition = Vector2.zero;
        Image("Dot pattern", composition, 0, 0, 1280, 720, Color.white, Sprite("02.Title/01.Sprites/PTN_Dot.png"));
        var logo = Image("Project LUNA", composition, 46, 44, 447, 220, Color.white, Sprite("02.Title/01.Sprites/IMG_logo.png"));
        logo.preserveAspect = true; logo.gameObject.AddComponent<TitleLogoGlitch>();
        var view = composition.gameObject.AddComponent<TitleMenuView>(); Set(view, "manager", manager); Set(manager, "titleView", view);
        string[] names = { "새 게임", "이어하기", "설정", "종료" };
        string[] fields = { "newGameButton", "continueButton", "settingsButton", "quitButton" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = Button(names[i], composition, names[i], 83, 373 + i * 66, 318, 66);
            var image = button.GetComponent<Image>(); image.color = Color.white; image.sprite = Sprite("02.Title/01.Sprites/BTN_Disable.png");
            button.transition = Selectable.Transition.SpriteSwap;
            var states = button.spriteState; states.highlightedSprite = states.pressedSprite = states.selectedSprite = Sprite("02.Title/01.Sprites/BTN_Enable.png"); button.spriteState = states;
            var label = button.GetComponentInChildren<TMP_Text>(); Position(label.rectTransform, 36, 0, 258, 66); label.fontSize = 17; label.color = Hex("4d91a5"); label.alignment = TextAlignmentOptions.MidlineLeft;
            var decoration = Image("Focus marker", button.transform, -24, 4, 16, 59, Color.white, Sprite("02.Title/01.Sprites/IMG_Deco_button.png")); decoration.gameObject.SetActive(false);
            var fx = button.gameObject.AddComponent<UIChoiceGlitch>(); Set(fx, "label", label); Set(fx, "focusDecoration", decoration.gameObject);
            Set(view, fields[i], button);
        }
        Text("Menu index", composition, "004", 90, 628, 45, 16, 8, Hex("456b83"));
        Image("Menu rule", composition, 90, 645, 38, 2, Hex("456b83"));
        var modal = Image("Save slots modal", composition, 0, 0, 1280, 720, new Color(0, .02f, .04f, .92f)); modal.raycastTarget = true;
        var panel = Image("Save slots", modal.transform, 320, 130, 640, 460, Ink);
        Text("Heading", panel.transform, "이어하기  /  수동 저장 슬롯", 28, 20, 500, 42, 25, null, PixelFont);
        var close = Button("Close slots", panel.transform, "닫기  Esc", 480, 386, 132, 42);
        var slots = new Button[SaveManager.SlotCount]; var labels = new TMP_Text[SaveManager.SlotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = Button("Slot " + (i + 1), panel.transform, "저장 슬롯 " + (i + 1), 28, 82 + i * 56, 584, 44);
            labels[i] = slots[i].GetComponentInChildren<TMP_Text>(); labels[i].alignment = TextAlignmentOptions.MidlineLeft; labels[i].fontSize = 14;
        }
        Set(view, "slotsPanel", modal.gameObject); Set(view, "closeSlotsButton", close);
        SetArray(view, "slotButtons", slots); SetArray(view, "slotLabels", labels);
        var status = Text("Progress status", composition, "", 470, 640, 720, 52, 16, Hex("c0e8ef")); Set(view, "statusLabel", status);
        modal.gameObject.SetActive(false);
        SaveSceneKeepingCameraRects(scene, original);
    }

    internal static void SaveSceneKeepingCameraRects(UnityEngine.SceneManagement.Scene scene, string original)
    {
        // PixelPerfectCamera can rewrite its editor viewport on scene load. UI authoring must not save that side effect.
        const string pattern = @"  m_NormalizedViewPortRect:\r?\n    serializedVersion: 2\r?\n    x: [^\r\n]+\r?\n    y: [^\r\n]+\r?\n    width: [^\r\n]+\r?\n    height: [^\r\n]+";
        var rects = Regex.Matches(original, pattern).Cast<Match>().Select(m => m.Value).ToArray();
        EditorSceneManager.SaveScene(scene);
        int index = 0;
        string saved = File.ReadAllText(scene.path);
        if (Regex.Matches(saved, pattern).Count != rects.Length) throw new InvalidOperationException("Camera count changed during UI setup.");
        File.WriteAllText(scene.path, Regex.Replace(saved, pattern, _ => rects[index++]));
    }

    [MenuItem("Tools/Human Bartender/UI/Rebuild settings panel")]
    public static void RebuildSettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        UpdateSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[VisualUISetup] 확장된 설정 화면을 저장했습니다.");
    }

    static void UpdateSettings()
    {
        var root = PrefabUtility.LoadPrefabContents(Scope);
        try
        {
            var controller = root.GetComponentInChildren<UIDisplayOptions>(true);
            var overlay = Get<GameObject>(controller, "optionUI");
            var bgm = Get<Slider>(controller, "bgmSlider"); var se = Get<Slider>(controller, "seSlider");
            var toggle = Get<Toggle>(controller, "windowedToggle"); var dropdown = Get<TMP_Dropdown>(controller, "windowSizeDropdown");
            // 컴포넌트/fileID/UnityEvent를 유지한 채 기존 위젯을 새 레이아웃으로 옮긴다.
            foreach (var widget in new Component[] { bgm, se, toggle, dropdown }) widget.transform.SetParent(controller.transform, false);
            ClearChildren(overlay.transform);
            var canvas = controller.GetComponent<Canvas>(); canvas.sortingOrder = 105;
            var scaler = controller.GetComponent<CanvasScaler>(); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            controller.transform.localScale = Vector3.one;
            var overlayRect = (RectTransform)overlay.transform; Stretch(overlayRect);
            var overlayImage = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
            overlayImage.sprite = null; overlayImage.color = new Color(0, .02f, .04f, .9f); overlayImage.raycastTarget = true;
            // 기존 메뉴의 Layout/Animator가 새 화면을 재배치하지 않도록 제거한다.
            foreach (var component in overlay.GetComponents<Behaviour>())
                if (component is LayoutGroup || component is ContentSizeFitter || component is Animator) Object.DestroyImmediate(component);
            var frame = Image("Settings window", overlay.transform, 0, 0, 1000, 540, Hex("59899b"));
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(.5f, .5f); frame.rectTransform.pivot = new Vector2(.5f, .5f); frame.rectTransform.anchoredPosition = Vector2.zero;
            var inside = Image("Surface", frame.transform, 1, 1, 998, 538, Ink);
            var view = frame.gameObject.AddComponent<OptionsPanelView>(); Set(view, "controller", controller);
            var inputGroup = frame.gameObject.AddComponent<CanvasGroup>(); Set(view, "inputGroup", inputGroup);
            Image("Header", frame.transform, 1, 1, 998, 79, Hex("102731"));
            Image("Header accent", frame.transform, 0, 0, 52, 4, Hex("96e5eb"));
            Image("Header line", frame.transform, 1, 80, 998, 1, Line);
            Image("Menu icon", frame.transform, 28, 21, 40, 40, Color.white, Sprite("01.Shared/01.MainScreen/service_panel/BTN_service_panel.png"));
            Text("Heading", frame.transform, "설정", 88, 19, 400, 45, 30, null, Font("Bold_PFStardust_3_0_SDF"));
            Set(view, "statusMessage", Text("Settings status", frame.transform, "", 510, 19, 400, 45, 12, Hex("efb4a5")));
            var close = Button("Close settings", frame.transform, "", 934, 21, 40, 40);
            close.GetComponent<Image>().sprite = Sprite("01.Shared/01.MainScreen/service_panel/BTN_service_close.png"); close.GetComponent<Image>().color = Color.white;
            Image("Navigation background", frame.transform, 1, 81, 217, 394, Hex("09151e"));
            Image("Navigation rule", frame.transform, 218, 81, 1, 394, Line);
            Image("Footer", frame.transform, 1, 475, 998, 64, Hex("0f242e"));
            Image("Footer rule", frame.transform, 1, 475, 998, 1, Line);
            var pages = new GameObject[3]; var tabs = new Button[3]; var indicators = new GameObject[3]; string[] titles = { "사운드", "그래픽", "조작키" };
            for (int i = 0; i < 3; i++)
            {
                tabs[i] = Button(titles[i] + " tab", frame.transform, titles[i], 16, 106 + i * 60, 186, 50);
                tabs[i].GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
                tabs[i].GetComponentInChildren<TMP_Text>().fontSize = 16;
                indicators[i] = Text("Selected tab", tabs[i].transform, "›", 156, 0, 20, 50, 28, Ink).gameObject;
                indicators[i].SetActive(i == 0);
                pages[i] = Rect(titles[i] + " page", frame.transform, 248, 104, 722, 346).gameObject;
            }
            var sound = pages[0].transform;
            Text("BGM label", sound, "배경음", 0, 0, 400, 28, 16);
            Text("BGM help", sound, "음소거를 해제하면 설정한 음량으로 돌아옵니다.", 0, 28, 480, 25, 11, Muted);
            Text("SE label", sound, "효과음", 0, 118, 400, 28, 16);
            Text("SE help", sound, "UI · 상호작용 · 제조 효과음", 0, 146, 480, 25, 11, Muted);
            Image("BGM rule", sound, 0, 104, 722, 1, Line);
            Image("SE rule", sound, 0, 222, 722, 1, Line);
            StyleSlider(bgm, sound, 460, 15); StyleSlider(se, sound, 460, 133);
            var bgmValue = Text("BGM percentage", sound, "30%", 659, 9, 63, 30, 13, Hex("a6e7ed")); bgmValue.alignment = TextAlignmentOptions.MidlineRight;
            var seValue = Text("SE percentage", sound, "80%", 659, 127, 63, 30, 13, Hex("a6e7ed")); seValue.alignment = TextAlignmentOptions.MidlineRight;
            var bgmMute = SettingsToggle("BGM mute", sound, "배경음 음소거", 520, 52);
            var seMute = SettingsToggle("SE mute", sound, "효과음 음소거", 520, 170);
            UnityEventTools.AddPersistentListener(bgmMute.onValueChanged, controller.OnBgmMuted);
            UnityEventTools.AddPersistentListener(seMute.onValueChanged, controller.OnSeMuted);
            Set(controller, "bgmMuteToggle", bgmMute); Set(controller, "seMuteToggle", seMute);
            Text("Sound save hint", sound, "음량과 음소거는 즉시 적용되고 저장됩니다.", 0, 250, 700, 30, 12, Muted);
            var game = pages[1].transform;
            GraphicsRow(game, "화면 모드", "창모드를 끄면 전체화면으로 표시합니다.", 0);
            GraphicsRow(game, "창 크기", "창모드에서 사용할 해상도입니다.", 46);
            GraphicsRow(game, "화면 채우기", "끄면 원래 비율을 유지하고 여백을 표시합니다.", 92);
            GraphicsRow(game, "수직 동기화", "화면 주사율에 맞춰 화면 찢어짐을 줄입니다.", 138);
            GraphicsRow(game, "프레임 제한", "수직 동기화를 끈 상태에서 선택할 수 있습니다.", 184);
            GraphicsRow(game, "후처리 효과", "색 보정과 화면 후처리 효과를 사용합니다.", 230);
            toggle.transform.SetParent(game, false); Position((RectTransform)toggle.transform, 520, 3, 202, 36);
            StyleToggle(toggle);
            dropdown.transform.SetParent(game, false); Position((RectTransform)dropdown.transform, 520, 49, 202, 36);
            StyleDropdown(dropdown);
            var fill = SettingsToggle("Fill screen", game, "화면에 맞추기", 520, 95);
            var vsync = SettingsToggle("VSync", game, "사용", 520, 141);
            var post = SettingsToggle("Post processing", game, "사용", 520, 233);
            var frameRate = Object.Instantiate(dropdown, game); frameRate.name = "Frame rate";
            Position((RectTransform)frameRate.transform, 520, 187, 202, 36);
            frameRate.onValueChanged = new TMP_Dropdown.DropdownEvent();
            post.transform.SetAsLastSibling();
            frameRate.ClearOptions(); frameRate.AddOptions(new System.Collections.Generic.List<string> { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "제한 없음" });
            UnityEventTools.AddPersistentListener(vsync.onValueChanged, controller.OnVSyncToggled);
            Set(controller, "fillToggle", fill); Set(controller, "vSyncToggle", vsync);
            Set(controller, "postProcessingToggle", post); Set(controller, "frameRateDropdown", frameRate);
            Text("Display apply hint", game, "그래픽 변경은 ‘적용’을 눌러 저장합니다. 적용 전 닫으면 변경을 취소합니다.", 0, 294, 722, 40, 12, Muted);
            var controls = pages[2].transform;
            var hint = Text("Controls hint", controls, OptionsPanelView.DefaultControlsHint, 0, 0, 722, 44, 12, Muted); Set(view, "controlsHint", hint);
            var viewport = Image("Bindings viewport", controls, 0, 52, 702, 286, Hex("09151e"));
            viewport.raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.viewport = viewport.rectTransform; scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = Rect("Bindings", viewport.transform, 0, 0, 702, GameKeyBindings.Bindings.Length * 40);
            scroll.content = content; Set(view, "bindingsScroll", scroll);
            var scrollbarImage = Image("Bindings scrollbar", controls, 710, 52, 12, 286, Hex("162f3b")); scrollbarImage.raycastTarget = true;
            var scrollbar = scrollbarImage.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Image("Handle", scrollbar.transform, 0, 0, 12, 286, Hex("59899b")); handle.raycastTarget = true;
            Stretch(handle.rectTransform);
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle; scroll.verticalScrollbar = scrollbar;
            var bindingButtons = new Button[GameKeyBindings.Bindings.Length];
            for (int i = 0; i < bindingButtons.Length; i++)
            {
                var binding = GameKeyBindings.Bindings[i];
                Text(binding.Control + " label", content, binding.Label, 12, i * 40, 480, 36, 14);
                bindingButtons[i] = Button(binding.Control + " binding", content, binding.DefaultKey.ToString(), 520, i * 40 + 2, 170, 32);
                Image(binding.Control + " rule", content, 12, i * 40 + 39, 678, 1, Line);
            }
            SetArray(view, "bindingButtons", bindingButtons);
            var title = Button("Return to title", frame.transform, "타이틀로 돌아가기", 24, 491, 172, 36);
            Set(view, "titleButton", title);
            var reset = Button("Reset current tab", frame.transform, "이 항목 초기화", 640, 491, 118, 36);
            var apply = Button("Apply display", frame.transform, "적용", 770, 491, 80, 36);
            var back = Button("Back", frame.transform, "돌아가기  Esc", 862, 491, 112, 36, true);
            Set(view, "closeButton", close); Set(view, "backButton", back); Set(view, "resetButton", reset); Set(view, "applyButton", apply);
            Set(view, "bgmSlider", bgm); Set(view, "seSlider", se); Set(view, "bgmValue", bgmValue); Set(view, "seValue", seValue);
            SetArray(view, "tabs", tabs); SetArray(view, "pages", pages); SetArray(view, "tabIndicators", indicators);
            var confirmation = Image("Title confirmation", frame.transform, 0, 0, 1000, 540, new Color(0, .02f, .04f, .94f)); confirmation.raycastTarget = true;
            Image("Confirmation surface", confirmation.transform, 220, 165, 560, 210, Hex("162f3b"));
            Text("Confirmation heading", confirmation.transform, "타이틀로 돌아갈까요?", 252, 190, 496, 38, 23);
            Text("Confirmation help", confirmation.transform, "저장하지 않은 진행 상황은 사라집니다.", 252, 241, 496, 32, 14, Muted);
            var cancel = Button("Cancel return", confirmation.transform, "계속 플레이", 460, 310, 130, 40);
            var confirm = Button("Confirm return", confirmation.transform, "타이틀로 이동", 606, 310, 142, 40, true);
            Set(view, "titleConfirmation", confirmation.gameObject); Set(view, "cancelTitleButton", cancel); Set(view, "confirmTitleButton", confirm);
            confirmation.gameObject.SetActive(false);
            // 필드 연결을 끝낸 다음 활성화한다. OnEnable은 저장된 프리팹을 미리 보는데도 사용된다.
            pages[1].SetActive(false); pages[2].SetActive(false);
            overlay.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Scope);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void GraphicsRow(Transform parent, string label, string description, float y)
    {
        Text(label, parent, label, 0, y, 490, 23, 14);
        Text(label + " help", parent, description, 0, y + 22, 500, 19, 10, Muted);
        Image(label + " rule", parent, 0, y + 44, 722, 1, Line);
    }
    static Toggle SettingsToggle(string name, Transform parent, string label, float x, float y)
    {
        var root = Image(name, parent, x, y, 202, 36, Hex("162f3b"));
        var toggle = root.gameObject.AddComponent<Toggle>();
        StyleToggle(toggle, label); return toggle;
    }
    static void LabelRow(Transform parent, string label, string description, float y)
    {
        Text(label, parent, label, 0, y, 470, 28, 15);
        Text(label + " help", parent, description, 0, y + 28, 480, 25, 11, Muted);
        Image(label + " rule", parent, 0, y + 62, 722, 1, Line);
    }
    static void StyleSlider(Slider slider, Transform parent, float x, float y)
    {
        slider.transform.SetParent(parent, false); Position((RectTransform)slider.transform, x, y, 180, 28);
        slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
        // 기존 이벤트와 슬라이더 컴포넌트만 보존하고, 3개 시각 요소를 새로 연결한다.
        ClearChildren(slider.transform);
        var track = Image("Track", slider.transform, 0, 11, 180, 5, Hex("294653"));
        var fillArea = Rect("Fill area", slider.transform, 0, 11, 180, 5);
        var fill = Image("Fill", fillArea, 0, 0, 180, 5, Hex("5ccbdc")); Stretch(fill.rectTransform);
        var handleArea = Rect("Handle area", slider.transform, 5, 0, 170, 28);
        var handle = Image("Handle", handleArea, 0, 0, 10, -8, Hex("59899b")); handle.raycastTarget = true;
        handle.rectTransform.pivot = new Vector2(.5f, .5f); handle.rectTransform.anchoredPosition = Vector2.zero;
        var thumb = Image("Thumb surface", handle.transform, 0, 0, 0, 0, Hex("a1e6ed")); Stretch(thumb.rectTransform);
        thumb.rectTransform.offsetMin = Vector2.one; thumb.rectTransform.offsetMax = -Vector2.one;
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        var rootImage = slider.GetComponent<Image>() ?? slider.gameObject.AddComponent<Image>();
        rootImage.sprite = null; rootImage.color = Color.clear; rootImage.raycastTarget = true;
    }
    static void StyleToggle(Toggle toggle, string label = "창모드")
    {
        ClearChildren(toggle.transform);
        var bg = toggle.GetComponent<Image>() ?? toggle.gameObject.AddComponent<Image>(); bg.sprite = null; bg.color = Hex("162f3b"); bg.raycastTarget = true;
        var check = Image("Enabled", toggle.transform, 12, 11, 14, 14, Cyan);
        toggle.graphic = check; toggle.targetGraphic = bg;
        Text("Label", toggle.transform, label, 38, 0, 154, 36, 14);
    }
    static void StyleDropdown(TMP_Dropdown dropdown)
    {
        var bg = dropdown.GetComponent<Image>(); if (bg != null) { bg.sprite = null; bg.color = Hex("162f3b"); }
        dropdown.captionText.font = BodyFont; dropdown.captionText.fontSize = 14; dropdown.captionText.color = Hex("cce4ec");
        foreach (var label in dropdown.GetComponentsInChildren<TMP_Text>(true)) { label.font = BodyFont; label.fontSize = 14; label.color = Hex("cce4ec"); }
        // TMP 기본 템플릿의 토글/스크롤 및 참조를 유지한다.
    }
}
