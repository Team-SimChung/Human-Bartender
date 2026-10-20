using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class OutlineHighlightTests
{
    const string ShaderPath = "Assets/01.Game/03.Content/06.Shared/03.Shaders/SpriteOutline.shader";
    const string NpcTexturePath = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/npc_w1_idle.png";
    static readonly int Intensity = Shader.PropertyToID("_OutlineIntensity");

    [Test]
    public void DisabledOutlinePreservesBilinearNpcEdges()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        Assert.NotNull(shader);
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NpcTexturePath);
        Assert.AreEqual(FilterMode.Bilinear, texture.filterMode);
        var material = new Material(shader);
        try
        {
            var darkOff = Render(material, texture, Color.black, 0);
            var cyanOff = Render(material, texture, Color.cyan, 0);
            var cyanOn = Render(material, texture, Color.cyan, 1);
            Assert.Greater(cyanOff.Count(c => c.a > .001f && c.a < .99f), 100, "Exercise filtered, semi-transparent edges on the real NPC texture");
            Assert.Less(MaxDifference(darkOff, cyanOff), .0001f, "At zero intensity the outline color must not tint sprite edges");
            Assert.Greater(MaxDifference(cyanOff, cyanOn), .1f, "An enabled outline must still be visible");

            // Independent RGBA check: a half-transparent sprite must retain its original color.
            var solid = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
            try
            {
                solid.SetPixels(Enumerable.Repeat(new Color(.25f, .25f, .25f, .5f), 4).ToArray());
                solid.Apply();
                var pixels = Render(material, solid, Color.cyan, 0);
                var center = pixels[pixels.Length / 2 + 128];
                // SrcAlpha blending over clear: RGB = .25 * .5; alpha = .5 * .5.
                Assert.That(center.r, Is.EqualTo(.125f).Within(.003f));
                Assert.That(center.g, Is.EqualTo(.125f).Within(.003f));
                Assert.That(center.b, Is.EqualTo(.125f).Within(.003f));
                Assert.That(center.a, Is.EqualTo(.25f).Within(.003f));
            }
            finally { Object.DestroyImmediate(solid); }
        }
        finally { Object.DestroyImmediate(material); }
    }

    static Color[] Render(Material material, Texture texture, Color outlineColor, float intensity)
    {
        var target = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) },
            uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
            colors = Enumerable.Repeat(Color.white, 4).ToArray(),
            triangles = new[] { 0, 1, 2, 0, 2, 3 }
        };
        var previous = RenderTexture.active;
        using var commands = new CommandBuffer { name = "Outline regression render" };
        try
        {
            target.Create();
            material.SetTexture("_MainTex", texture);
            material.SetColor("_OutlineColor", outlineColor);
            material.SetFloat(Intensity, intensity);
            material.SetFloat("_OutlineThickness", 1);
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1), true));
            // Test the unlit variant so scene lights cannot conceal a residual cyan border.
            for (int i = 0; i < 4; i++) commands.DisableShaderKeyword("USE_SHAPE_LIGHT_TYPE_" + i);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
            for (int i = 0; i < 4; i++)
                if (Shader.IsKeywordEnabled("USE_SHAPE_LIGHT_TYPE_" + i)) commands.EnableShaderKeyword("USE_SHAPE_LIGHT_TYPE_" + i);
            Graphics.ExecuteCommandBuffer(commands);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            readback.Apply();
            return readback.GetPixels();
        }
        finally
        {
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(mesh);
        }
    }

    static float MaxDifference(Color[] a, Color[] b) => a.Zip(b, (x, y) =>
        Mathf.Max(Mathf.Abs(x.r - y.r), Mathf.Abs(x.g - y.g), Mathf.Abs(x.b - y.b), Mathf.Abs(x.a - y.a))).Max();

    [UnityTest, Explicit("Run alone: exercises actual distance focus and opts into the disabled development NPC.")]
    public IEnumerator NearbyOnlyOutlineIncludingAnimatedNpc()
    {
        EditorSceneManager.OpenScene(WorldInteractionCutsceneTests.OutsidePath);
        yield return new EnterPlayMode();
        yield return ExerciseRuntime();
        yield return new ExitPlayMode();
    }

    public static IEnumerator ExerciseRuntime()
    {
        var controller = Object.FindFirstObjectByType<WorldInteractionController>();
        float deadline = Time.realtimeSinceStartup + 45;
        while (controller == null || !controller.IsReady)
        {
            Assert.Less(Time.realtimeSinceStartup, deadline, "OutSide interaction initialization timed out");
            yield return null;
            controller = Object.FindFirstObjectByType<WorldInteractionController>();
        }

        var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
        material.SetFloat(Intensity, .7f);
        var root = new GameObject("Outline lifecycle regression");
        root.SetActive(false);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;
        var block = new MaterialPropertyBlock();
        int otherProperty = Shader.PropertyToID("_OutlineTestUnrelatedProperty");
        block.SetFloat(otherProperty, 42);
        renderer.SetPropertyBlock(block);
        var highlight = root.AddComponent<OutlineHighlight>();
        try
        {
            root.SetActive(true);
            Assert.AreEqual(0, ReadIntensity(renderer), "First enable must override the material immediately");
            highlight.SetHighlight(true);
            yield return Settle();
            Assert.AreEqual(1, ReadIntensity(renderer));
            highlight.SetHighlight(false);
            yield return Settle();
            Assert.AreEqual(0, ReadIntensity(renderer));
            highlight.SetHighlight(true);
            yield return Settle();
            highlight.enabled = false;
            Assert.AreEqual(0, ReadIntensity(renderer), "Disabled component cannot leave a lit renderer");
            highlight.enabled = true;
            Assert.AreEqual(0, ReadIntensity(renderer), "Re-enable must start hidden");
            highlight.SetHighlight(true);
            yield return Settle();
            root.SetActive(false);
            Assert.AreEqual(0, ReadIntensity(renderer));
            root.SetActive(true);
            Assert.AreEqual(0, ReadIntensity(renderer));
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(42, block.GetFloat(otherProperty), "Keep unrelated renderer overrides");
            Assert.AreEqual(.7f, material.GetFloat(Intensity), "Never mutate the shared material");
        }
        finally { Object.Destroy(root); Object.Destroy(material); }

        var npc = WorldInteractionTarget.InScene(controller.gameObject.scene)
            .Single(t => t.StorySceneId == "outside_interaction_money_example");
        npc.gameObject.SetActive(true);
        var targets = WorldInteractionTarget.InScene(controller.gameObject.scene).Where(t => t.isActiveAndEnabled).ToArray();
        foreach (var target in targets.Where(t => t != controller.FocusedTarget))
            if (target.TryGetComponent<SpriteRenderer>(out var targetRenderer))
                Assert.AreEqual(0, ReadIntensity(targetRenderer), "Unvisited target must start hidden: " + target.name);

        var player = Object.FindFirstObjectByType<Player>();
        var originalPosition = player.transform.position;
        try
        {
            player.transform.position = npc.transform.position + Vector3.right * .2f;
            yield return Settle();
            Assert.AreEqual(npc, controller.FocusedTarget);
            var npcRenderer = npc.GetComponent<SpriteRenderer>();
            Assert.AreEqual(1, ReadIntensity(npcRenderer));
            yield return Capture("npc-near");
            // Stay outside its 1.5-unit radius while keeping the NPC visible in the camera.
            player.transform.position = npc.transform.position + Vector3.right * 2;
            yield return Settle();
            Assert.AreNotEqual(npc, controller.FocusedTarget);
            var frames = new HashSet<Sprite>();
            float until = Time.realtimeSinceStartup + 1.3f;
            do
            {
                Assert.AreEqual(0, ReadIntensity(npcRenderer), "Outline must stay fully off while Idle changes sprites");
                frames.Add(npcRenderer.sprite);
                yield return null;
            } while (Time.realtimeSinceStartup < until);
            Assert.AreEqual(6, frames.Count, "Check all six animated NPC frames");
            yield return Capture("npc-far");
        }
        finally { player.transform.position = originalPosition; }
    }

    static float ReadIntensity(SpriteRenderer renderer)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Assert.IsTrue(block.HasFloat(Intensity), "Renderer must explicitly override the shared material");
        return block.GetFloat(Intensity);
    }

    static IEnumerator Settle()
    {
        float until = Time.time + .3f;
        do { yield return null; } while (Time.time < until);
    }

    static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory("Library/WorldInteractionValidation/outline-fix");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/WorldInteractionValidation/outline-fix/" + name + ".png"));
        yield return null;
        yield return null;
    }
}
