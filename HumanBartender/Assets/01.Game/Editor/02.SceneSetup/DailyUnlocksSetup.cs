using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Play 씬에서 사용자가 실행하고 확인한 뒤 저장하는 일차 시작 화면 셋업.</summary>
public static class DailyUnlocksSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    public const string CardPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/06.DayStart/DailyUnlockCard.prefab";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/WebGenerated/Notifications/";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/";
    const string VisualsPath = "Assets/01.Game/04.Data/01.Definitions/UI/CocktailRecipeVisualCatalog.asset";
    const string UndoLabel = "Setup Daily Unlocks";

    [MenuItem("Tools/Human Bartender/UI/Setup Daily Unlocks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DailyUnlocksSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[DailyUnlocksSetup] Play.unity를 활성 씬으로 열어 주세요.");
            return;
        }
        var ui = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "05.UI");
        if (ui == null)
        {
            Debug.LogWarning("[DailyUnlocksSetup] Play 씬의 05.UI를 찾을 수 없습니다.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        var existing = ui.transform.Find("Canvas_DailyUnlocks");
        DailyUnlocksScreen screen;
        if (existing != null)
        {
            screen = existing.GetComponent<DailyUnlocksScreen>();
            Debug.Log("[DailyUnlocksSetup] 기존 Canvas_DailyUnlocks의 배치와 연결을 보존합니다.");
        }
        else
        {
            var card = LoadOrCreateCard();
            screen = CreateScreen(ui.transform, card);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        if (screen == null)
        {
            Debug.LogWarning("[DailyUnlocksSetup] 기존 Canvas에 DailyUnlocksScreen이 없습니다. Inspector에서 확인해 주세요.");
            return;
        }
        var controller = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayPhaseController>(true)).FirstOrDefault();
        if (controller != null) Connect(controller, "dailyUnlocksScreen", screen);
        else Debug.LogWarning("[DailyUnlocksSetup] PlayPhaseController가 없어 화면을 연결하지 못했습니다.");
        Undo.CollapseUndoOperations(group);
        try { screen.ValidateReferences(); }
        catch (System.InvalidOperationException error) { Debug.LogWarning("[DailyUnlocksSetup] " + error.Message); }
        Debug.Log("[DailyUnlocksSetup] 설정을 확인했습니다. 이미지·폰트와 화면을 검토한 뒤 씬을 저장해 주세요.");
    }

    static DailyUnlocksScreen CreateScreen(Transform parent, DailyUnlockCard card)
    {
        var canvasRect = Rect("Canvas_DailyUnlocks", parent);
        var canvas = Undo.AddComponent<Canvas>(canvasRect.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        var scaler = Undo.AddComponent<CanvasScaler>(canvasRect.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.matchWidthOrHeight = 0;
        Undo.AddComponent<GraphicRaycaster>(canvasRect.gameObject);
        var screen = Undo.AddComponent<DailyUnlocksScreen>(canvasRect.gameObject);
        var root = Rect("DailyUnlocksRoot", canvasRect);
        Stretch(root);
        var canvasGroup = Undo.AddComponent<CanvasGroup>(root.gameObject);
        var backdrop = Picture("BackgroundDimOverlay", root, "Web_GRD_modal_backdrop_daily_unlocks_backdrop_background", 0, true);
        Stretch(backdrop.rectTransform);
        if (backdrop.sprite == null) backdrop.color = ColorHex("050910BA");
        var panel = Picture("DailyUnlocksPanel", root, "Web_GRD_daily_unlocks_background", 24);
        Point(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 810, 400);
        Stretch(Picture("PanelBorder", panel.transform, "Web_GRD_daily_unlocks_border", 24).rectTransform);
        var header = Rect("Header", panel.transform);
        TopLeft(header, 22, -20, 766, 72);
        var badge = Picture("DayBadge", header, "Web_GRD_unlock_day_background", 6);
        TopLeft(badge.rectTransform, 0, 0, 46, 18);
        Stretch(Picture("DayBorder", badge.transform, "Web_GRD_unlock_day_border", 6).rectTransform);
        var day = Label("DayText", badge.transform, "1일차", 10, "71DCE2", TextAlignmentOptions.Center);
        Stretch(day.rectTransform);
        var title = Label("TitleText", header, "오늘의 새로운 재료와 레시피", 18, "EDF5F3", font: "Regular_PFStardust_3_0_SDF");
        TopLeft(title.rectTransform, 0, -26, 766, 24);
        var subtitle = Label("SubtitleText", header, "새로 도착한 재료로 더 다양한 한 잔을 만들어 보세요.", 10, "9DADB7");
        TopLeft(subtitle.rectTransform, 0, -56, 766, 14);
        Divider(panel.transform, "HeaderDivider", -103);
        Section(screen, panel.transform, "IngredientSection", "새로 들어온 재료", "ingredient", 22);
        Section(screen, panel.transform, "RecipeSection", "해금된 레시피", "cocktail", 415);
        var detail = Label("DetailText", panel.transform, "카드에 마우스를 올리면 설명이 보입니다.", 10, "9DADB7");
        TopLeft(detail.rectTransform, 22, -308, 766, 16);
        Divider(panel.transform, "FooterDivider", -333);
        var buttonImage = Picture("ConfirmButton", panel.transform, "Web_BTN_primary_background", 8, true);
        Point(buttonImage.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 15, 172, 30);
        Stretch(Picture("ButtonBorder", buttonImage.transform, "Web_GRD_primary_border", 8).rectTransform);
        var button = Undo.AddComponent<Button>(buttonImage.gameObject);
        button.targetGraphic = buttonImage;
        var text = Label("Text", button.transform, "확인 · 영업 준비하기", 11, "B8F8F4", TextAlignmentOptions.Center, "Bold_Paperlogy_SDF");
        Stretch(text.rectTransform);
        Connect(screen, "root", root.gameObject);
        Connect(screen, "canvasGroup", canvasGroup);
        Connect(screen, "dayText", day);
        Connect(screen, "detailText", detail);
        Connect(screen, "confirmButton", button);
        Connect(screen, "cardPrefab", card);
        var visuals = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(VisualsPath);
        if (visuals == null) Debug.LogWarning("[DailyUnlocksSetup] 시각 카탈로그가 없습니다: " + VisualsPath);
        Connect(screen, "visuals", visuals);
        Debug.LogWarning("[DailyUnlocksSetup] Fallback Sprite는 Inspector에서 지정할 수 있습니다. 비어 있으면 누락된 아이콘은 숨기고 이름을 표시합니다.");
        root.gameObject.SetActive(false);
        return screen;
    }

    static void Section(DailyUnlocksScreen screen, Transform parent, string name, string title, string prefix, int x)
    {
        var section = Rect(name, parent);
        TopLeft(section, x, -117, 373, 184);
        var heading = Label("SectionTitleText", section, title, 12, "A8E8E9");
        TopLeft(heading.rectTransform, 0, 0, 128, 18);
        var badge = Picture("CountBadge", section, "Web_GRD_unlock_day_background", 6);
        TopLeft(badge.rectTransform, 135, 0, 24, 18);
        var count = Label("CountText", badge.transform, "0", 9, "8BA9B5", TextAlignmentOptions.Center);
        Stretch(count.rectTransform);
        var scrollRect = Rect("ScrollView", section);
        TopLeft(scrollRect, 0, -27, 373, 157);
        var scroll = Undo.AddComponent<ScrollRect>(scrollRect.gameObject);
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport", scrollRect);
        Stretch(viewport);
        Undo.AddComponent<RectMask2D>(viewport.gameObject);
        // A transparent hit surface also receives the wheel between cards and in empty sections.
        var hitArea = Undo.AddComponent<Image>(viewport.gameObject);
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var grid = Undo.AddComponent<GridLayoutGroup>(content.gameObject);
        grid.cellSize = new Vector2(86, 75);
        grid.spacing = new Vector2(7, 7);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        var fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        var empty = Label("EmptyText", section, "오늘 새로 추가된 항목이 없습니다.", 10, "9DADB7", TextAlignmentOptions.Center);
        TopLeft(empty.rectTransform, 0, -27, 373, 157);
        empty.gameObject.SetActive(false);
        Connect(screen, prefix + "CountText", count);
        Connect(screen, prefix + "Content", content);
        Connect(screen, prefix + "Empty", empty.gameObject);
        Connect(screen, prefix + "Scroll", scroll);
    }

    static DailyUnlockCard LoadOrCreateCard()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (existing != null)
        {
            Debug.Log("[DailyUnlocksSetup] 기존 카드 프리팹을 보존합니다.");
            return existing.GetComponent<DailyUnlockCard>();
        }
        EnsureFolder(System.IO.Path.GetDirectoryName(CardPath).Replace('\\', '/'));
        var background = Picture("DailyUnlockCard", null, "Web_GRD_unlock_card_background", 10, true);
        try
        {
            background.rectTransform.sizeDelta = new Vector2(86, 75);
            var selectable = Undo.AddComponent<Selectable>(background.gameObject);
            var card = Undo.AddComponent<DailyUnlockCard>(background.gameObject);
            var border = Picture("CardBorder", background.transform, "Web_GRD_unlock_card_border", 10);
            Stretch(border.rectTransform);
            selectable.targetGraphic = border;
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;
            colors.normalColor = new Color(1, 1, 1, .4f);
            colors.highlightedColor = ColorHex("8CECF1");
            colors.selectedColor = colors.highlightedColor;
            selectable.colors = colors;
            var art = Picture("ArtBackground", background.transform, "Web_GRD_unlock_art_background", 0);
            Point(art.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), 0, -7, 80, 44);
            var icon = Undo.AddComponent<Image>(Rect("Icon", art.transform).gameObject);
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(6, 2);
            icon.rectTransform.offsetMax = new Vector2(-6, -2);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var name = Label("NameText", background.transform, "항목 이름", 9, "DAE4E6", TextAlignmentOptions.Center);
            name.rectTransform.anchorMin = Vector2.zero;
            name.rectTransform.anchorMax = new Vector2(1, 0);
            name.rectTransform.pivot = new Vector2(.5f, 0);
            name.rectTransform.anchoredPosition = new Vector2(0, 7);
            name.rectTransform.sizeDelta = new Vector2(-4, 13);
            Connect(card, "icon", icon);
            Connect(card, "nameText", name);
            return PrefabUtility.SaveAsPrefabAsset(background.gameObject, CardPath).GetComponent<DailyUnlockCard>();
        }
        finally { Object.DestroyImmediate(background.gameObject); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Point(RectTransform rect, Vector2 anchor, Vector2 pivot, int x, int y, int w, int h)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }

    static void TopLeft(RectTransform rect, int x, int y, int w, int h) =>
        Point(rect, new Vector2(0, 1), new Vector2(0, 1), x, y, w, h);

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static Image Picture(string name, Transform parent, string resource, int border, bool raycast = false)
    {
        var image = Undo.AddComponent<Image>(Rect(name, parent).gameObject);
        image.sprite = Sprite(resource, border);
        image.type = border > 0 ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = raycast;
        return image;
    }

    static Sprite Sprite(string resource, int border)
    {
        string path = ArtPath + resource + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[DailyUnlocksSetup] 이미지가 없습니다: " + path);
            return null;
        }
        if (border > 0)
        {
            // Multiple sprites read border from each SpriteRect, not TextureImporter.spriteBorder.
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            bool changed = false;
            foreach (var rect in rects)
                if (rect.border == Vector4.zero)
                {
                    rect.border = new Vector4(border, border, border, border);
                    changed = true;
                }
            if (changed)
            {
                Undo.RecordObject(importer, UndoLabel);
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
            }
        }
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) Debug.LogWarning("[DailyUnlocksSetup] 스프라이트가 없습니다: " + path);
        return sprite;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string value, int size, string color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, string font = "Medium_Paperlogy_SDF")
    {
        var text = Undo.AddComponent<TextMeshProUGUI>(Rect(name, parent).gameObject);
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath + font + ".asset");
        if (fontAsset == null) Debug.LogWarning("[DailyUnlocksSetup] 폰트가 없습니다: " + font);
        text.font = fontAsset;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = alignment;
        text.color = ColorHex(color);
        text.raycastTarget = false;
        return text;
    }

    static void Divider(Transform parent, string name, int y)
    {
        var image = Undo.AddComponent<Image>(Rect(name, parent).gameObject);
        image.color = ColorHex("80DFE324");
        image.raycastTarget = false;
        image.rectTransform.anchorMin = new Vector2(0, 1);
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.pivot = new Vector2(.5f, 1);
        image.rectTransform.sizeDelta = new Vector2(-44, 1);
        image.rectTransform.anchoredPosition = new Vector2(0, y);
    }

    static Color ColorHex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    static void Connect(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        if (property.objectReferenceValue != null || value == null) return;
        Undo.RecordObject(target, UndoLabel);
        property.objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
