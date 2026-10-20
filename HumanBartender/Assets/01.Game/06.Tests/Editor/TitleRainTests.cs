using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TitleRainTests
{
    [Test]
    public void TitleRainOnlyTargetsTheCameraBackgroundAndKeepsMenuInOverlay()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var layer = roots.SelectMany(r => r.GetComponentsInChildren<WindowRainLayer>(true)).Single();
            var title = roots.SelectMany(r => r.GetComponentsInChildren<TitleMenuView>(true)).Single();
            var city = roots.SelectMany(r => r.GetComponentsInChildren<RawImage>(true)).Single(r => r.name == "City backdrop");
            Assert.That(title.GetComponentInParent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(city.GetComponentInParent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(city.GetComponentInParent<Canvas>().worldCamera, Is.EqualTo(layer.GetComponent<Camera>()));
            Assert.That(new UnityEditor.SerializedObject(layer).FindProperty("ambientRain").boolValue, Is.True);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator TitleActuallyRendersMovingWaterAndRestoresItsResources()
    {
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return RenderRain();
        yield return new ExitPlayMode();
    }

    static IEnumerator RenderRain()
    {
        yield return null; yield return null;
        var layer = Object.FindFirstObjectByType<WindowRainLayer>();
        Assert.That(layer, Is.Not.Null); Assert.That(layer.IsRaining, Is.True);
        Assert.That(layer.HasVisibleWater, Is.True); Assert.That(layer.Simulation.Drops.Count, Is.GreaterThan(20));
        var camera = layer.GetComponent<Camera>(); var title = Object.FindFirstObjectByType<TitleMenuView>().GetComponentInParent<Canvas>();
        var wet = new RenderTexture(1280, 720, 24); var dry = new RenderTexture(1280, 720, 24);
        var previous = camera.targetTexture; float strength = layer.Strength;
        try
        {
            camera.targetTexture = wet; Canvas.ForceUpdateCanvases(); camera.Render();
            layer.Strength = 0; camera.targetTexture = dry; camera.Render();
            var a = Read(wet); var b = Read(dry);
            try
            {
                var pixelsA = a.GetPixels32(); var pixelsB = b.GetPixels32(); int different = 0;
                for (int i = 0; i < pixelsA.Length; i++)
                    if (Mathf.Abs(pixelsA[i].r - pixelsB[i].r) + Mathf.Abs(pixelsA[i].g - pixelsB[i].g) + Mathf.Abs(pixelsA[i].b - pixelsB[i].b) > 8) different++;
                Assert.That(different, Is.GreaterThan(500), "registered title camera must really composite the existing rain shader");
                TestContext.WriteLine("Title wet/dry changed pixels: " + different);
                Save(a, "title-rain-background");
            }
            finally { Object.Destroy(a); Object.Destroy(b); }
            CaptureMenu(wet, title, camera);
            layer.Strength = strength; camera.targetTexture = previous;
            yield return null;
            Assert.That(layer.HasVisibleWater, Is.True);
            layer.enabled = false;
            Assert.That(WindowRainLayer.TryGet(camera, out _), Is.False); Assert.That(layer.WaterTexture, Is.Null);
            layer.enabled = true; yield return null;
            Assert.That(WindowRainLayer.TryGet(camera, out var registered), Is.True); Assert.That(registered, Is.SameAs(layer));
        }
        finally
        {
            camera.targetTexture = previous; layer.Strength = strength;
            Object.Destroy(wet); Object.Destroy(dry);
        }
    }

    static void CaptureMenu(RenderTexture wetBackground, Canvas title, Camera originalCamera)
    {
        // Render the already-composited camera background below clean foreground UI, as in the real Overlay composition.
        var cameraGO = new GameObject("Title review camera", typeof(Camera)); var camera = cameraGO.GetComponent<Camera>();
        camera.CopyFrom(originalCamera); camera.transform.SetPositionAndRotation(originalCamera.transform.position, originalCamera.transform.rotation); camera.enabled = false;
        var output = new RenderTexture(1280, 720, 24); camera.targetTexture = output;
        var backgroundGO = new GameObject("Title review background", typeof(RectTransform), typeof(Canvas), typeof(RawImage));
        var background = backgroundGO.GetComponent<Canvas>(); background.renderMode = RenderMode.ScreenSpaceCamera; background.worldCamera = camera;
        background.planeDistance = camera.nearClipPlane + 1; background.sortingOrder = -100;
        backgroundGO.GetComponent<RawImage>().texture = wetBackground;
        var previousMode = title.renderMode; var previousCamera = title.worldCamera; float previousDistance = title.planeDistance;
        try
        {
            title.renderMode = RenderMode.ScreenSpaceCamera; title.worldCamera = camera; title.planeDistance = camera.nearClipPlane + .5f;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var texture = Read(output); Save(texture, "title-rain-menu"); Object.Destroy(texture);
        }
        finally
        {
            title.renderMode = previousMode; title.worldCamera = previousCamera; title.planeDistance = previousDistance;
            Object.Destroy(backgroundGO); Object.Destroy(cameraGO); Object.Destroy(output);
        }
    }

    static Texture2D Read(RenderTexture target)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); RenderTexture.active = previous;
        return texture;
    }
    static void Save(Texture2D texture, string name)
    {
        string directory = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR"); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
    }
}
