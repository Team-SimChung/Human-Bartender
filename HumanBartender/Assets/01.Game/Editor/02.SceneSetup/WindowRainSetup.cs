using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 외부 씬 빗물 연결. Renderer2D에 합성 피처를 한 번만 등록하고, OutSide의 Main Camera에
/// 레이어를 붙여 기존 FX_Rain과 연결한다. 다시 실행해도 중복으로 추가하지 않는다.
/// </summary>
public static class WindowRainSetup
{
    const string RendererPath = "Assets/01.Game/05.Settings/02.Rendering/Renderer2D.asset";
    const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    const string ShaderFolder = "Assets/01.Game/03.Content/03.Vfx/03.Shaders/";

    [MenuItem("Tools/Human Bartender/Window Rain/Connect Outside Scene")]
    public static void Run()
    {
        AddFeature();
        ConnectScene();
    }

    public static void AddFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
        if (data == null) throw new System.InvalidOperationException("Renderer2D asset is missing: " + RendererPath);
        if (data.rendererFeatures.Any(f => f is WindowRainFeature)) return;
        var feature = ScriptableObject.CreateInstance<WindowRainFeature>();
        feature.name = "WindowRain";
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
        var serialized = new SerializedObject(data);
        var features = serialized.FindProperty("m_RendererFeatures");
        var map = serialized.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    public static void ConnectScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var cameraObject = objects.Select(t => t.gameObject).FirstOrDefault(g => g.name == "Main Camera" && g.GetComponent<Camera>() != null);
        var rain = objects.Select(t => t.gameObject).FirstOrDefault(g => g.name == "FX_Rain");
        var movementSource = objects.FirstOrDefault(t => t.name == "VCam:TrackedTarget")?.parent;
        if (cameraObject == null || rain == null)
            throw new System.InvalidOperationException("OutSide Main Camera or FX_Rain was not found.");
        var layer = cameraObject.GetComponent<WindowRainLayer>();
        if (layer == null) layer = cameraObject.AddComponent<WindowRainLayer>();
        var serialized = new SerializedObject(layer);
        var drops = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "WindowRainDrops.shader");
        var composite = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "WindowRainComposite.shader");
        if (serialized.FindProperty("rainSource").objectReferenceValue == rain &&
            serialized.FindProperty("movementSource").objectReferenceValue == movementSource &&
            serialized.FindProperty("dropShader").objectReferenceValue == drops &&
            serialized.FindProperty("compositeShader").objectReferenceValue == composite)
            return; // 이미 연결된 씬을 다시 저장하며 PPC 뷰포트까지 바꾸지 않는다.
        serialized.FindProperty("rainSource").objectReferenceValue = rain;
        serialized.FindProperty("movementSource").objectReferenceValue = movementSource;
        serialized.FindProperty("dropShader").objectReferenceValue = drops;
        serialized.FindProperty("compositeShader").objectReferenceValue = composite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
