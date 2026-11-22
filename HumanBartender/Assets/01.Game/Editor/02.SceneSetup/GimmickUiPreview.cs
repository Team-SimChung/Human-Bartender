using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>UI 프리팹의 실제 배치를 별도 프리뷰 씬에서 검증용 PNG로 저장한다.</summary>
public static class GimmickUiPreview
{
    public static void Export()
    {
        Capture("04.Cap/NewCapGame.prefab", "cap");
        Capture("02.Stir/NewStirGame.prefab", "stir");
    }

    public static void ExportPour() => Capture("03.Pour/NewPourGame.prefab", "pour-prefab-editor");
    public static void ExportCapReady() => Capture("04.Cap/NewCapGame.prefab", "cap-ready", startCap: false);

    static void Capture(string path, string name, bool startCap = true)
    {
        var root = PrefabUtility.LoadPrefabContents(GimmickUiPrefabSetup.Folder + "/" + path);
        var cameraObject = new GameObject("UI Verification Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = root.scene;
        camera.enabled = false;
        root.SetActive(true);
        camera.orthographic = true;
        bool pour = root.GetComponentInChildren<PourManager>(true) != null;
        camera.orthographicSize = pour ? 2.7f : 360f;
        if (pour) camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .035f, .055f);
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    canvas.worldCamera = camera;
                    continue;
                }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
            }
            var cap = root.GetComponentInChildren<CapOpenView>(true);
            if (cap != null)
            {
                var rules = new CapOpenRules();
                if (startCap) rules.Press();
                cap.Render(rules, "beer", 165f, 44f, false);
            }
            if (pour)
                foreach (var background in root.GetComponentsInChildren<MinigameBackground>(true)) background.Refresh();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs/UI-Previews");
            File.WriteAllBytes("Logs/UI-Previews/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
