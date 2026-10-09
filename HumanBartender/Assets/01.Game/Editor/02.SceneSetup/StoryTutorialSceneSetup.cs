using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates the tutorial UI asset and connects it to the existing Play scene resources.</summary>
public static class StoryTutorialSceneSetup
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/04.Tutorial/StoryTutorialOverlay.prefab";

    [MenuItem("Tools/Story/Connect Tutorial UI")]
    public static void Apply()
    {
        var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabObject == null)
        {
            CreatePrefab();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
            prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        var play = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity");
        var flow = Find<StoryFlow>(play);
        var tutorial = flow.GetComponent<StoryTutorialController>();
        if (tutorial == null) tutorial = flow.gameObject.AddComponent<StoryTutorialController>();
        Set(tutorial, "viewPrefab", LoadView());
        Set(tutorial, "slotCamera", Find<PlayCamera>(play));
        Set(tutorial, "characters", Find<DialogueCharacterManager>(play));
        Set(tutorial, "dialogue", Find<UIDialogueTextView>(play));
        Set(tutorial, "serving", Find<CraftServingView>(play));
        Set(tutorial, "craft", Find<CraftFlowController>(play));
        Set(tutorial, "preparationHost", Find<CraftPrepStageHost>(play));
        Set(tutorial, "recipes", Find<RecipeBrowserScreen>(play));
        Set(Find<StoryScriptRunner>(play), "tutorial", tutorial);
        var tabletop = play.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "Tabletop Serve Anchor");
        if (tabletop == null)
        {
            tabletop = new GameObject("Tabletop Serve Anchor").transform;
            SceneManager.MoveGameObjectToScene(tabletop.gameObject, play);
            tabletop.position = new Vector3(0, -1.5f, 0);
        }
        Set(Find<CraftServingView>(play), "tabletopAnchor", tabletop);
        EditorSceneManager.MarkSceneDirty(play);
        EditorSceneManager.SaveScene(play);
        AssetDatabase.SaveAssets();
        Debug.Log("[StoryTutorial] Play scene intro notice and tutorial resource connections saved.");
    }

    static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

    static StoryTutorialView LoadView() =>
        AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<StoryTutorialView>();

    static void Set(Object target, string field, Object value)
    {
        if (target == null || value == null)
            throw new System.InvalidOperationException("튜토리얼 리소스 연결이 없습니다: " + field);
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
    }

    static StoryTutorialView CreatePrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/01.Game/03.Content/05.Fonts/NeoDunggeunmo SDF.asset");
        if (font == null) throw new System.InvalidOperationException("튜토리얼 한글 폰트를 찾을 수 없습니다.");
        var view = StoryTutorialView.CreateFallback(font, false);
        try { return PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath).GetComponent<StoryTutorialView>(); }
        finally { Object.DestroyImmediate(view.gameObject); }
    }
}
