using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 빗물 합성을 타이틀의 배경 카메라에 연결한다. 전경 메뉴와 로고는 Overlay에 남긴다.</summary>
public static class TitleRainSetup
{
    const string ScenePath = "Assets/01.Game/01.Scenes/Main.unity";
    const string ShaderFolder = "Assets/01.Game/03.Content/03.Vfx/03.Shaders/";

    [MenuItem("Tools/Human Bartender/Window Rain/Connect Title Scene")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행하세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            WindowRainSetup.AddFeature();
            string original = File.ReadAllText(ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var camera = objects.Select(t => t.GetComponent<Camera>()).First(c => c != null && c.name == "Main Camera");
            var title = objects.Select(t => t.GetComponent<Canvas>()).First(c => c != null && c.name == "Canvas_Title");
            var background = objects.First(t => t.name == "City backdrop");
            var shade = objects.First(t => t.name == "Backdrop shade");
            var canvas = objects.Select(t => t.GetComponent<Canvas>()).FirstOrDefault(c => c != null && c.name == "Canvas_TitleBackground");
            if (canvas == null)
            {
                var go = new GameObject("Canvas_TitleBackground", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.layer = 5; go.transform.SetParent(title.transform.parent, false);
                canvas = go.GetComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 1; canvas.sortingOrder = -100;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            foreach (var child in new[] { background, shade })
            {
                child.SetParent(canvas.transform, false); var rect = (RectTransform)child;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
            }
            var layer = camera.GetComponent<WindowRainLayer>() ?? camera.gameObject.AddComponent<WindowRainLayer>();
            var settings = new SerializedObject(layer);
            settings.FindProperty("ambientRain").boolValue = true;
            settings.FindProperty("strength").floatValue = .4f;
            settings.FindProperty("rainSource").objectReferenceValue = null;
            settings.FindProperty("dropShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "WindowRainDrops.shader");
            settings.FindProperty("compositeShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "WindowRainComposite.shader");
            settings.ApplyModifiedPropertiesWithoutUndo();
            VisualUISetup.SaveSceneKeepingCameraRects(scene, original); AssetDatabase.SaveAssets();
            Debug.Log("[Title rain] City background uses WindowRain; logo and menu stay in Overlay.");
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
