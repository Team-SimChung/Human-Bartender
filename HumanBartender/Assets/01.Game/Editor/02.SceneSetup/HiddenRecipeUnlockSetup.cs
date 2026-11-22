using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play 씬에 3일차 히든 레시피 해금 창(Canvas_HiddenRecipeUnlock)을 만들고 StoryScriptRunner·PlayInputHandler에 연결한다.
/// 사용자가 실행하고 확인한 뒤 씬을 저장한다. 다시 실행하면 기존 배치는 두고 비어 있는 연결만 채운다.
/// </summary>
public static class HiddenRecipeUnlockSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    public const string CanvasName = "Canvas_HiddenRecipeUnlock";
    public const int SortOrder = 12;
    const string NeighbourCanvasName = "Canvas_DailyUnlocks";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/WebGenerated/Notifications/";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/";
    const string VisualsPath = "Assets/01.Game/04.Data/01.Definitions/UI/CocktailRecipeVisualCatalog.asset";
    const string UndoLabel = "Setup Hidden Recipe Unlock";
    const int RowCount = 3;

    [MenuItem("Tools/Human Bartender/UI/Setup Hidden Recipe Unlock")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[HiddenRecipeUnlockSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[HiddenRecipeUnlockSetup] Play.unity를 활성 씬으로 열어 주세요.");
            return;
        }
        var ui = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "05.UI");
        if (ui == null)
        {
            Debug.LogWarning("[HiddenRecipeUnlockSetup] Play 씬의 05.UI를 찾을 수 없습니다.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        var existing = ui.transform.Find(CanvasName);
        HiddenRecipeUnlockScreen screen;
        if (existing != null)
        {
            screen = existing.GetComponent<HiddenRecipeUnlockScreen>();
            Debug.Log("[HiddenRecipeUnlockSetup] 기존 " + CanvasName + "의 배치와 연결을 보존합니다.");
        }
        else
        {
            screen = CreateScreen(ui.transform);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        if (screen == null)
        {
            Debug.LogWarning("[HiddenRecipeUnlockSetup] 기존 Canvas에 HiddenRecipeUnlockScreen이 없습니다. Inspector에서 확인해 주세요.");
            return;
        }
        Connect(screen, "visuals", AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(VisualsPath));
        ConnectAll<StoryScriptRunner>(scene, screen);
        ConnectAll<PlayInputHandler>(scene, screen);
        Undo.CollapseUndoOperations(group);
        try { screen.ValidateReferences(); }
        catch (System.InvalidOperationException error) { Debug.LogWarning("[HiddenRecipeUnlockSetup] " + error.Message); }
        Debug.Log("[HiddenRecipeUnlockSetup] 설정을 확인했습니다. 이미지·폰트와 화면을 검토한 뒤 씬을 저장해 주세요.");
    }

    static void ConnectAll<T>(UnityEngine.SceneManagement.Scene scene, HiddenRecipeUnlockScreen screen) where T : Component
    {
        var targets = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
        if (targets.Length == 0) Debug.LogWarning($"[HiddenRecipeUnlockSetup] {typeof(T).Name}가 없어 화면을 연결하지 못했습니다.");
        foreach (var target in targets) Connect(target, "hiddenRecipeScreen", screen);
    }

    static HiddenRecipeUnlockScreen CreateScreen(Transform parent)
    {
        var canvasRect = Rect(CanvasName, parent);
        // Same sort order as DailyUnlocks; being the next sibling draws it on top.
        var neighbour = parent.Find(NeighbourCanvasName);
        if (neighbour != null) canvasRect.SetSiblingIndex(neighbour.GetSiblingIndex() + 1);
        else Debug.LogWarning("[HiddenRecipeUnlockSetup] " + NeighbourCanvasName + "가 없어 05.UI 맨 아래에 둡니다.");
        var canvas = Undo.AddComponent<Canvas>(canvasRect.gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;
        var scaler = Undo.AddComponent<CanvasScaler>(canvasRect.gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.matchWidthOrHeight = 0;
        Undo.AddComponent<GraphicRaycaster>(canvasRect.gameObject);
        var screen = Undo.AddComponent<HiddenRecipeUnlockScreen>(canvasRect.gameObject);

        var root = Rect("HiddenRecipeRoot", canvasRect);
        Stretch(root);
        var canvasGroup = Undo.AddComponent<CanvasGroup>(root.gameObject);
        var backdrop = Picture("BackgroundDimOverlay", root, "Web_GRD_modal_backdrop_daily_unlocks_backdrop_background", 0, true);
        Stretch(backdrop.rectTransform);
        if (backdrop.sprite == null) backdrop.color = ColorHex("050910BA");
        // Web art: panel 820×528, shadow 916×624 → ×0.75 on the 960×540 canvas.
        var shadow = Picture("PanelShadow", root, "Web_GRD_daily_unlocks_johnny_unlock_shadow", 0);
        Point(shadow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 687, 468);
        var panel = Picture("HiddenRecipePanel", root, "Web_GRD_daily_unlocks_johnny_unlock_background", 0);
        Point(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 615, 396);
        Stretch(Picture("PanelBorder", panel.transform, "Web_GRD_daily_unlocks_johnny_unlock_border", 0).rectTransform);

        var header = Rect("HeaderGroup", panel.transform);
        TopLeft(header, 24, -24, 567, 76);
        var badge = Picture("BadgeImage", header, "Web_GRD_unlock_day_background", 6);
        TopLeft(badge.rectTransform, 0, 0, 150, 18);
        Stretch(Picture("BadgeBorder", badge.transform, "Web_GRD_unlock_day_border", 6).rectTransform);
        Stretch(Label("BadgeText", badge.transform, "조니의 기억 · 히든 레시피", 10, "71DCE2", TextAlignmentOptions.Center).rectTransform);
        TopLeft(Label("TitleText", header, "새로운 칵테일이 해금되었습니다", 20, "EDF5F3", font: "Regular_PFStardust_3_0_SDF").rectTransform,
            0, -26, 567, 26);
        TopLeft(Label("DetailText", header, "기억 속 레시피와 모아 온 재료, 아일리의 각설탕이 한 잔으로 이어집니다.", 10, "AEBDC6").rectTransform,
            0, -58, 567, 14);
        Divider(panel.transform, "HeaderDivider", -103);

        var drinkGroup = Picture("DrinkGroup", panel.transform, "Web_GRD_johnny_unlock_drink_background", 0);
        TopLeft(drinkGroup.rectTransform, 24, -112, 253, 200);
        var drink = Undo.AddComponent<Image>(Rect("DrinkImage", drinkGroup.transform).gameObject);
        Point(drink.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), 0, -8, 120, 126);
        drink.preserveAspect = true;
        drink.raycastTarget = false;
        var drinkName = Label("DrinkNameText", drinkGroup.transform, "올드 패션드", 15, "D4B782", TextAlignmentOptions.Center);
        Point(drinkName.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), 0, -140, 237, 20);
        var tool = Label("DrinkToolText", drinkGroup.transform, "올드패션드 잔 · 믹싱 글라스 · 스터", 10, "AEBDC6", TextAlignmentOptions.Center);
        Point(tool.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), 0, -164, 237, 14);

        var ingredientGroup = Rect("IngredientGroup", panel.transform);
        TopLeft(ingredientGroup, 299, -112, 292, 200);
        TopLeft(Label("StockTitleText", ingredientGroup, "재료 선반에 추가되었습니다", 11, "ADEDEF").rectTransform, 0, 0, 292, 16);
        var list = Rect("IngredientList", ingredientGroup);
        TopLeft(list, 0, -22, 292, 151);
        var layout = Undo.AddComponent<VerticalLayoutGroup>(list.gameObject);
        layout.spacing = 5;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childScaleWidth = layout.childScaleHeight = false;
        TopLeft(Label("HintText", ingredientGroup, "각설탕은 리큐어 선반 맨 끝에 있습니다.", 10, "AEBDC6").rectTransform, 0, -180, 292, 14);

        var rows = new (GameObject root, Image icon, TMP_Text name, TMP_Text amount)[RowCount];
        for (int i = 0; i < RowCount; i++) rows[i] = Row(list, i);

        Divider(panel.transform, "FooterDivider", -333);
        var buttonImage = Picture("ConfirmButton", panel.transform, "Web_BTN_primary_background", 8, true);
        Point(buttonImage.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), 0, 15, 172, 30);
        Stretch(Picture("ButtonBorder", buttonImage.transform, "Web_GRD_primary_border", 8).rectTransform);
        var button = Undo.AddComponent<Button>(buttonImage.gameObject);
        button.targetGraphic = buttonImage;
        Stretch(Label("ConfirmText", button.transform, "확인 · 대화 계속하기", 12, "B8F8F4", TextAlignmentOptions.Center,
            "Bold_Paperlogy_SDF").rectTransform);

        Connect(screen, "root", root.gameObject);
        Connect(screen, "canvasGroup", canvasGroup);
        Connect(screen, "confirmButton", button);
        Connect(screen, "drinkImage", drink);
        Connect(screen, "drinkNameText", drinkName);
        ConnectRows(screen, rows);
        var visuals = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(VisualsPath);
        if (visuals == null) Debug.LogWarning("[HiddenRecipeUnlockSetup] 시각 카탈로그가 없습니다: " + VisualsPath);
        Debug.LogWarning("[HiddenRecipeUnlockSetup] Fallback Sprite는 Inspector에서 지정할 수 있습니다. 비어 있으면 누락된 아이콘은 숨깁니다.");
        root.gameObject.SetActive(false);
        return screen;
    }

    static (GameObject, Image, TMP_Text, TMP_Text) Row(Transform list, int index)
    {
        // Web row: 389×63 art, 36×48 icon → ×0.75.
        var background = Picture("IngredientRow" + index, list, "Web_GRD_johnny_unlock_item_background", 0);
        background.rectTransform.sizeDelta = new Vector2(292, 47);
        Stretch(Picture("RowBorder", background.transform, "Web_GRD_johnny_unlock_item_border", 0).rectTransform);
        var icon = Undo.AddComponent<Image>(Rect("IconImage", background.transform).gameObject);
        Point(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), 9, 0, 27, 36);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var name = Label("NameText", background.transform, "재료 이름", 11, "EDF5F3");
        Point(name.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), 46, 0, 170, 16);
        var amount = Label("AmountText", background.transform, "0ml", 10, "A5D4D6", TextAlignmentOptions.MidlineRight);
        Point(amount.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), -9, 0, 60, 16);
        return (background.gameObject, icon, name, amount);
    }

    static void ConnectRows(HiddenRecipeUnlockScreen screen, (GameObject root, Image icon, TMP_Text name, TMP_Text amount)[] rows)
    {
        var data = new SerializedObject(screen);
        var array = data.FindProperty("ingredientRows");
        if (array.arraySize > 0) return;
        Undo.RecordObject(screen, UndoLabel);
        array.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
            element.FindPropertyRelative("icon").objectReferenceValue = rows[i].icon;
            element.FindPropertyRelative("nameText").objectReferenceValue = rows[i].name;
            element.FindPropertyRelative("amountText").objectReferenceValue = rows[i].amount;
        }
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(screen);
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
            Debug.LogWarning("[HiddenRecipeUnlockSetup] 이미지가 없습니다: " + path);
            return null;
        }
        if (border > 0)
        {
            // Multiple sprites read border from each SpriteRect. Only empty borders are filled.
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
        if (sprite == null) Debug.LogWarning("[HiddenRecipeUnlockSetup] 스프라이트가 없습니다: " + path);
        return sprite;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string value, int size, string color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, string font = "Medium_Paperlogy_SDF")
    {
        var text = Undo.AddComponent<TextMeshProUGUI>(Rect(name, parent).gameObject);
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath + font + ".asset");
        if (fontAsset == null) Debug.LogWarning("[HiddenRecipeUnlockSetup] 폰트가 없습니다: " + font);
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
        image.rectTransform.sizeDelta = new Vector2(-48, 1);
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
        if (property == null)
        {
            Debug.LogWarning($"[HiddenRecipeUnlockSetup] {target.GetType().Name}에 '{field}' 칸이 없습니다.", target);
            return;
        }
        if (property.objectReferenceValue != null || value == null) return;
        Undo.RecordObject(target, UndoLabel);
        property.objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
