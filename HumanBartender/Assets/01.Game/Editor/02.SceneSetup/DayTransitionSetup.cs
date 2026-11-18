using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ProjectLifeScope 프리팹 원본에 취침 뒤 명언·Day 전환 화면(Canvas_DayTransition)을 만들고 SceneTransitionManager에 연결한다.
/// 다시 실행하면 기존 배치는 두고 비어 있는 연결만 채운다. 값은 웹 day-transition.js·campaign.css를 960×540(×0.75)으로 옮긴 것이다.
/// </summary>
public static class DayTransitionSetup
{
    public const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    public const string CanvasName = "Canvas_DayTransition";
    // 전역 캔버스 Cash 205 위, 바 출입 문(BarDoorTransition) 32000 아래.
    public const int SortOrder = 30000;
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/Regular_PFStardust_3_0_SDF.asset";
    const int QuoteSize = 17, AuthorSize = 13, TitleSize = 42;

    [MenuItem("Tools/Human Bartender/UI/Setup Day Transition")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[DayTransitionSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("[DayTransitionSetup] 편집 중인 프리팹을 저장하고 Prefab 모드를 닫아 주세요.");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (Apply(root)) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>프리팹 내용에 전환 화면을 만들고 연결한다. 바뀐 것이 있으면 true.</summary>
    public static bool Apply(GameObject prefabRoot)
    {
        var manager = prefabRoot.GetComponentInChildren<SceneTransitionManager>(true);
        if (manager == null)
        {
            Debug.LogWarning("[DayTransitionSetup] SceneTransitionManager가 없습니다.");
            return false;
        }
        bool changed = false;
        var existing = prefabRoot.transform.Find(CanvasName);
        DayTransitionView view;
        if (existing != null)
        {
            view = existing.GetComponent<DayTransitionView>();
            Debug.Log("[DayTransitionSetup] 기존 " + CanvasName + "의 배치와 연결을 보존합니다.");
        }
        else
        {
            view = Create(prefabRoot.transform);
            changed = true;
        }
        if (view == null)
        {
            Debug.LogWarning("[DayTransitionSetup] 기존 캔버스에 DayTransitionView가 없습니다. Inspector에서 확인해 주세요.");
            return changed;
        }
        changed |= Connect(manager, "dayTransition", view);
        try { view.ValidateReferences(); }
        catch (System.InvalidOperationException error) { Debug.LogWarning("[DayTransitionSetup] " + error.Message); }
        Debug.Log("[DayTransitionSetup] 설정을 확인했습니다.");
        return changed;
    }

    static DayTransitionView Create(Transform parent)
    {
        var canvasRect = Rect(CanvasName, parent);
        var canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;
        var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        scaler.matchWidthOrHeight = 0;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        var view = canvasRect.gameObject.AddComponent<DayTransitionView>();

        var root = Rect("DayTransitionRoot", canvasRect);
        Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        var shade = Rect("ShadeImage", root).gameObject.AddComponent<Image>();
        Stretch(shade.rectTransform);
        shade.color = new Color(0, 0, 0, 0);
        shade.raycastTarget = true;

        var quoteGroup = Rect("QuoteGroup", root);
        Point(quoteGroup, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 840, 200);
        var quote = Label("QuoteText", quoteGroup, "", QuoteSize, new Color32(229, 238, 239, 0), TextAlignmentOptions.Bottom);
        // 명언 마지막 줄과 저자 사이 18px(웹 24px × 0.75).
        Point(quote.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, 0), 0, 16, 840, 120);
        // 웹 line-height 1.65에 가깝게 줄 간격을 넓힌다(TMP 단위: 0.01em). 화면에서 확인 후 조절.
        quote.lineSpacing = 30;
        var author = Label("AuthorText", quoteGroup, "", AuthorSize, new Color32(185, 197, 200, 0), TextAlignmentOptions.Top);
        Point(author.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, 1), 0, -2, 840, 24);

        var titleRect = Rect("DayTitleGroup", root);
        Point(titleRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), 0, 0, 240, 60);
        var title = titleRect.gameObject.AddComponent<CanvasGroup>();
        title.alpha = 0;
        title.blocksRaycasts = false;
        var label = Label("DayLabelText", titleRect, "Day", TitleSize, new Color32(229, 238, 239, 255), TextAlignmentOptions.Right);
        Point(label.rectTransform, new Vector2(.5f, .5f), new Vector2(1, .5f), 10, 0, 110, 56);
        var mask = Rect("DigitsMask", titleRect);
        // 웹 숫자 칸 1.45em × 1.3em.
        Point(mask, new Vector2(.5f, .5f), new Vector2(0, .5f), 16, 0, 61, 55);
        mask.gameObject.AddComponent<RectMask2D>();
        var oldDigit = Label("OldDigitText", mask, "0", TitleSize, new Color32(229, 238, 239, 255), TextAlignmentOptions.Center);
        Stretch(oldDigit.rectTransform);
        var newDigit = Label("NewDigitText", mask, "1", TitleSize, new Color32(229, 238, 239, 0), TextAlignmentOptions.Center);
        Stretch(newDigit.rectTransform);

        var data = new SerializedObject(view);
        data.FindProperty("root").objectReferenceValue = root.gameObject;
        data.FindProperty("shade").objectReferenceValue = shade;
        data.FindProperty("quoteText").objectReferenceValue = quote;
        data.FindProperty("authorText").objectReferenceValue = author;
        data.FindProperty("dayTitle").objectReferenceValue = title;
        data.FindProperty("oldDigit").objectReferenceValue = oldDigit;
        data.FindProperty("newDigit").objectReferenceValue = newDigit;
        data.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        return view;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
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

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string value, int size, Color32 color, TextAlignmentOptions alignment)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) Debug.LogWarning("[DayTransitionSetup] 폰트가 없습니다: " + FontPath);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    static bool Connect(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[DayTransitionSetup] {target.GetType().Name}에 '{field}' 칸이 없습니다.", target);
            return false;
        }
        if (property.objectReferenceValue != null || value == null) return false;
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
