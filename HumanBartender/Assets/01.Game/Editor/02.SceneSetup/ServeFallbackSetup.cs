using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play 씬의 서빙 화면에 전용 클립이 없는 칵테일용 정지 그림(FallbackDrinkImage)을 만들고 ServeScreen에 연결한다.
/// 다시 실행하면 기존 배치는 두고 비어 있는 연결만 채운다. 씬은 사용자가 확인한 뒤 저장한다.
/// </summary>
public static class ServeFallbackSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/Play.unity";
    public const string ImageName = "FallbackDrinkImage";
    // 웹 .serve-fallback 260×370(1280×720) × 0.75.
    public static readonly Vector2Int Size = new(195, 278);
    const string VisualsPath = "Assets/01.Game/04.Data/01.Definitions/UI/CocktailRecipeVisualCatalog.asset";
    const string UndoLabel = "Setup Serve Fallback Drink";

    [MenuItem("Tools/Human Bartender/UI/Setup Serve Fallback Drink")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[ServeFallbackSetup] Play 모드를 끄고 실행해 주세요.");
            return;
        }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("[ServeFallbackSetup] Play.unity를 활성 씬으로 열어 주세요.");
            return;
        }
        var screen = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ServeScreen>(true)).FirstOrDefault();
        if (screen == null)
        {
            Debug.LogWarning("[ServeFallbackSetup] Play 씬에서 ServeScreen을 찾을 수 없습니다.");
            return;
        }
        var parent = screen.transform.parent;
        if (parent == null)
        {
            Debug.LogWarning("[ServeFallbackSetup] ServeScreen(drink)의 부모(BG)가 없습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoLabel);
        var image = parent.Find(ImageName)?.GetComponent<Image>();
        if (image != null) Debug.Log("[ServeFallbackSetup] 기존 " + ImageName + "의 배치를 보존합니다.");
        else
        {
            image = Create(parent, screen.transform.GetSiblingIndex() + 1);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Connect(screen, "fallbackDrinkImage", image);
        var animated = screen.GetComponent<Image>();
        if (animated == null) Debug.LogWarning("[ServeFallbackSetup] drink 오브젝트에 Image가 없어 Animated Drink Image를 비워 둡니다.");
        Connect(screen, "animatedDrinkImage", animated);
        var visuals = AssetDatabase.LoadAssetAtPath<CocktailRecipeVisualCatalog>(VisualsPath);
        if (visuals == null) Debug.LogWarning("[ServeFallbackSetup] 시각 카탈로그가 없습니다: " + VisualsPath);
        Connect(screen, "visuals", visuals);
        Undo.CollapseUndoOperations(group);
        Debug.Log("[ServeFallbackSetup] 설정을 확인했습니다. 화면을 검토한 뒤 씬을 저장해 주세요.");
    }

    static Image Create(Transform parent, int siblingIndex)
    {
        var go = new GameObject(ImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(siblingIndex);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Size;
        rect.localScale = Vector3.one;
        var image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        go.SetActive(false);
        return image;
    }

    static void Connect(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        var property = data.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[ServeFallbackSetup] {target.GetType().Name}에 '{field}' 칸이 없습니다.", target);
            return;
        }
        if (property.objectReferenceValue != null || value == null) return;
        Undo.RecordObject(target, UndoLabel);
        property.objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
