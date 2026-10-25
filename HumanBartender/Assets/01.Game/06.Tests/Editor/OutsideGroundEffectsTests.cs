using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OutsideGroundEffectsTests
{
    readonly List<Object> owned = new List<Object>();
    GameObject Make(string name)
    {
        var go = new GameObject(name);
        owned.Add(go);
        return go;
    }
    [TearDown]
    public void Cleanup()
    {
        foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
        owned.Clear();
    }

    [Test]
    public void RainStopsNewRipplesButLetsExistingRipplesExpire()
    {
        var puddle = Make("puddle").AddComponent<OutsidePuddleSurface>();
        for (int i = 0; i < 60; i++) puddle.Advance(1f / 60f, true);
        Assert.That(puddle.LivingRipples, Is.GreaterThan(0));
        int previous = puddle.LivingRipples;
        for (int i = 0; i < 90; i++)
        {
            puddle.Advance(1f / 60f, false);
            Assert.That(puddle.LivingRipples, Is.LessThanOrEqualTo(previous));
            previous = puddle.LivingRipples;
        }
        Assert.That(puddle.LivingRipples, Is.Zero);
        puddle.enabled = false; puddle.enabled = true;
        Assert.That(puddle.LivingRipples, Is.Zero);
    }

    [Test]
    public void RainSourceRequiresActiveLoopingEmission()
    {
        var rain = Make("rain");
        var system = rain.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main; main.loop = true;
        var systems = new List<ParticleSystem>();
        ParticleRainSource.Collect(rain, systems);
        Assert.That(ParticleRainSource.IsEmitting(rain, systems), Is.False);
        system.Play();
        Assert.That(ParticleRainSource.IsEmitting(rain, systems), Is.True);
        var emission = system.emission; emission.enabled = false;
        Assert.That(ParticleRainSource.IsEmitting(rain, systems), Is.False);
        emission.enabled = true; rain.SetActive(false);
        Assert.That(ParticleRainSource.IsEmitting(rain, systems), Is.False);
    }

    [Test]
    public void ProjectionKeepsFeetFixedAndCapsLengthNearLightHeight()
    {
        Vector3 foot = new Vector3(2, 3, 0), light = new Vector3(-100, 3, 0);
        Assert.That(OutsideCharacterShadow.ProjectVertex(foot, foot, light, 0.2f, 0.8f, 1), Is.EqualTo(foot));
        Vector3 head = foot + Vector3.up;
        Vector3 shadow = OutsideCharacterShadow.ProjectVertex(head, foot, light, 0.2f, 0.8f, 1);
        Assert.That(Vector3.Distance(shadow, foot), Is.LessThanOrEqualTo(0.801f));
        Assert.That(shadow.x, Is.GreaterThan(foot.x));
        Assert.That(shadow.y, Is.LessThan(foot.y));
        Vector3 opposite = OutsideCharacterShadow.ProjectVertex(head, foot, new Vector3(100, 4), 0.2f, 0.8f, 1);
        Assert.That(opposite.x, Is.LessThan(foot.x));
    }

    [Test]
    public void ReceiverFollowsPlatformAndRejectsAnotherFloorOrDisabledReceiver()
    {
        var ground = Make("platform").AddComponent<OutsideGroundReceiver>();
        ground.Configure(new Rect(-1, -0.3f, 2, 0.4f), 0, 0.2f);
        var scene = ground.gameObject.scene;
        Assert.That(OutsideGroundReceiver.Find(Vector3.zero, scene), Is.SameAs(ground));
        Assert.That(OutsideGroundReceiver.Find(new Vector3(2, 0), scene), Is.Not.SameAs(ground));
        ground.transform.position = new Vector3(0, 10);
        Assert.That(OutsideGroundReceiver.Find(new Vector3(0, 10), scene), Is.SameAs(ground));
        Assert.That(OutsideGroundReceiver.Find(Vector3.zero, scene), Is.Not.SameAs(ground));
        ground.enabled = false;
        Assert.That(OutsideGroundReceiver.Find(new Vector3(0, 10), scene), Is.Not.SameAs(ground));
        ground.enabled = true;
        Assert.That(OutsideGroundReceiver.Find(new Vector3(0, 10), scene), Is.SameAs(ground));
    }

    [Test]
    public void FacingChangeMirrorsAnOffCenterFootAndDisablingReleasesMeshes()
    {
        var go = Make("asymmetric caster");
        var sprite = go.AddComponent<SpriteRenderer>();
        var shadow = go.AddComponent<OutsideCharacterShadow>();
        shadow.Configure(AssetDatabase.LoadAssetAtPath<Material>("Assets/01.Game/03.Content/03.Vfx/02.Materials/M_OutsideCharacterShadow.mat"), new Vector2(0.2f, -0.4f));
        Assert.That(shadow.FootPosition, Is.EqualTo(new Vector3(0.2f, -0.4f, 0)));
        sprite.flipX = true;
        Assert.That(shadow.FootPosition, Is.EqualTo(new Vector3(-0.2f, -0.4f, 0)));
        var meshes = go.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
        Assert.That(meshes.Length, Is.EqualTo(3));
        shadow.enabled = false;
        Assert.That(meshes.All(m => m == null), Is.True);
        shadow.enabled = true;
        Assert.That(go.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(3), "Reenable must not duplicate projections.");
    }

    [Test]
    public void RimLightFollowsNearbyLightAndFadesOutsideItsRange()
    {
        var caster = Make("lit caster").AddComponent<SpriteRenderer>();
        caster.transform.position = new Vector3(-1000, 1000);
        var lighting = caster.gameObject.AddComponent<CharacterSpriteLighting>();
        var lamp = Make("rim light"); lamp.transform.position = caster.transform.position + new Vector3(1, 1);
        var light = lamp.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.intensity = 1;
        light.color = Color.red;
        light.targetSortingLayers = new[] { caster.sortingLayerID };
        var emitter = lamp.AddComponent<OutsideShadowLight>();
        emitter.Configure(light, Vector2.zero, 3);
        Assert.That(emitter.Color, Is.EqualTo(Color.red));
        lighting.Refresh();
        Assert.That(lighting.CurrentLight, Is.SameAs(emitter));
        Assert.That(lighting.RimStrength, Is.GreaterThan(0));
        caster.transform.position += Vector3.right * 20;
        lighting.Refresh();
        Assert.That(lighting.CurrentLight, Is.Null);
        Assert.That(lighting.RimStrength, Is.Zero, "No rim light where no light reaches.");
    }

    [Test]
    public void FreeformStreetLampEmitsFromItsLampHeadNotItsPivot()
    {
        var caster = Make("caster under lamp").AddComponent<SpriteRenderer>();
        caster.transform.position = new Vector3(2000, 2000);
        var lamp = Make("freeform lamp"); lamp.transform.position = caster.transform.position + new Vector3(-.3f, 1.4f);
        var light = lamp.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Freeform;
        light.intensity = 2;
        light.targetSortingLayers = new[] { caster.sortingLayerID };
        // 피벗은 원뿔 가운데, 램프 머리는 원뿔 윗변에 있는 실제 가로등 모양.
        light.SetShapePath(new[] { new Vector3(-.35f, .52f), new Vector3(-.05f, -.62f), new Vector3(.57f, -.43f), new Vector3(-.11f, .55f) });
        var emitter = lamp.AddComponent<OutsideShadowLight>();
        emitter.Configure(light, new Vector2(0, .3f), 5);
        Vector3 head = lamp.transform.position + new Vector3(-.23f, .535f);
        Assert.That(Vector3.Distance(emitter.Position, head), Is.LessThan(.01f));
        var lighting = caster.gameObject.AddComponent<CharacterSpriteLighting>();
        lighting.Refresh();
        Assert.That(Vector2.Distance(lighting.LightPosition, head), Is.LessThan(.01f), "Rim must point at the lamp head on the first frame.");
    }

    [Test]
    public void LightShaftFollowsFreeformConeAndEffectOption()
    {
        var saved = GraphicsEffectOptions.Current;
        var lamp = Make("street lamp");
        var light = lamp.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Freeform;
        light.intensity = 2;
        light.SetShapePath(new[] { new Vector3(-.35f, .52f), new Vector3(-.05f, -.62f), new Vector3(.57f, -.43f), new Vector3(-.11f, .55f) });
        var shaft = lamp.AddComponent<LightShaft>();
        try
        {
            shaft.Configure(light, AssetDatabase.LoadAssetAtPath<Material>("Assets/01.Game/03.Content/03.Vfx/02.Materials/M_OutsideLightShaft.mat"));
            Assert.That(shaft.IsVisible, Is.True);
            var mesh = lamp.GetComponentsInChildren<MeshFilter>(true).Single().sharedMesh;
            Assert.That(mesh.bounds.max.y, Is.EqualTo(.535f).Within(.02f), "The shaft must start at the lamp edge of the cone.");
            Assert.That(mesh.bounds.min.y, Is.LessThan(-.5f), "The shaft must reach down toward the ground.");
            var values = saved; values.LightShafts = false;
            GraphicsEffectOptions.Save(values);
            Assert.That(shaft.IsVisible, Is.False, "Turning light shafts off in settings must hide them immediately.");
            values.LightShafts = true;
            GraphicsEffectOptions.Save(values);
            light.enabled = false; shaft.Refresh();
            Assert.That(shaft.IsVisible, Is.False, "A disabled lamp must not cast a shaft.");
        }
        finally { GraphicsEffectOptions.Save(saved); }
    }

    [Test]
    public void SelectionHonorsLightStateLayerAndRange()
    {
        var caster = Make("caster").AddComponent<SpriteRenderer>();
        caster.transform.position = new Vector3(1000, 1000);
        var lamp = Make("light"); lamp.transform.position = caster.transform.position + Vector3.up;
        var light = lamp.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.intensity = 1;
        light.targetSortingLayers = new[] { caster.sortingLayerID };
        var emitter = lamp.AddComponent<OutsideShadowLight>();
        emitter.Configure(light, Vector2.zero, 3);
        Assert.That(OutsideShadowLight.Select(caster.transform.position, caster, null), Is.SameAs(emitter));
        var competitor = Make("competing light"); competitor.transform.position = lamp.transform.position;
        var competitorLight = competitor.AddComponent<Light2D>();
        competitorLight.lightType = Light2D.LightType.Point;
        competitorLight.targetSortingLayers = new[] { caster.sortingLayerID };
        var competitorEmitter = competitor.AddComponent<OutsideShadowLight>();
        competitorEmitter.Configure(competitorLight, Vector2.zero, 3);
        competitorLight.intensity = 1.1f;
        Assert.That(OutsideShadowLight.Select(caster.transform.position, caster, emitter), Is.SameAs(emitter), "A small score change must not flicker selection.");
        competitorLight.intensity = 2;
        Assert.That(OutsideShadowLight.Select(caster.transform.position, caster, emitter), Is.SameAs(competitorEmitter));
        competitor.SetActive(false);
        light.intensity = 0;
        Assert.That(OutsideShadowLight.Select(caster.transform.position, caster, emitter), Is.Null);
        light.intensity = 1; light.enabled = false;
        Assert.That(emitter.Influence(caster.transform.position, caster), Is.Zero);
        light.enabled = true; light.targetSortingLayers = new int[0];
        Assert.That(emitter.Influence(caster.transform.position, caster), Is.Zero);
        light.targetSortingLayers = new[] { caster.sortingLayerID };
        Assert.That(emitter.Influence(caster.transform.position + Vector3.right * 10, caster), Is.Zero);
        emitter.enabled = false;
        Assert.That(OutsideShadowLight.Select(caster.transform.position, caster, null), Is.Null);
    }

    [Test]
    public void SavedSceneHasSeparateCaptureAndAllRequestedCharacters()
    {
        var scene = EditorSceneManager.OpenScene(OutsideGroundEffectsSetup.ScenePath, OpenSceneMode.Single);
        var objects = scene.GetRootGameObjects();
        var puddles = objects.SelectMany(o => o.GetComponentsInChildren<OutsidePuddleSurface>(true)).ToArray();
        Assert.That(puddles.Length, Is.EqualTo(9));
        var shadows = objects.SelectMany(o => o.GetComponentsInChildren<OutsideCharacterShadow>(true)).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Luna", "Samho", "shiba", "Bubi", "CitizenA", "CitizenB" }, shadows.Select(s => s.name));
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(OutsideGroundEffectsSetup.RendererPath);
        Assert.That(new SerializedObject(renderer).FindProperty("m_CameraSortingLayersTextureBound").intValue, Is.EqualTo(SortingLayer.NameToID("ForeLightground")));
        Assert.That(renderer.rendererFeatures.Any(f => f is WindowRainFeature), Is.True);
        var shared = AssetDatabase.LoadAssetAtPath<Renderer2DData>("Assets/01.Game/05.Settings/02.Rendering/Renderer2D.asset");
        Assert.That(new SerializedObject(shared).FindProperty("m_CameraSortingLayersTextureBound").intValue, Is.EqualTo(SortingLayer.NameToID("ForeFore")));
        Assert.That(SortingLayer.GetLayerValueFromName("GroundPuddle"), Is.GreaterThan(SortingLayer.GetLayerValueFromName("Floorground")));
        Assert.That(SortingLayer.GetLayerValueFromName("GroundPuddle"), Is.GreaterThan(SortingLayer.GetLayerValueFromName("ForeLightground")));
        Assert.That(SortingLayer.GetLayerValueFromName("GroundShadow"), Is.LessThan(SortingLayer.GetLayerValueFromName("Character")));
        Assert.That(puddles.Select(p => p.Shape).Distinct().Count(), Is.EqualTo(4));
        Assert.That(shadows.All(s => s.GetComponent<SpriteRenderer>().sortingLayerName == "Character"), Is.True);
        foreach (var shadow in shadows)
        {
            Assert.That(shadow.GetComponent<CharacterSpriteLighting>(), Is.Not.Null, shadow.name + " needs body shading/rim light.");
            Assert.That(shadow.GetComponent<SpriteRenderer>().sharedMaterial.shader.name, Is.EqualTo(CharacterSpriteLighting.ShaderName));
        }
        var lamp = objects.SelectMany(o => o.GetComponentsInChildren<Light2D>(true)).Single(l => l.name == OutsideGroundEffectsSetup.StreetLampLight);
        Assert.That(lamp.GetComponent<LightShaft>(), Is.Not.Null, "The street lamp needs its light shaft.");
        foreach (string shaderName in new[] { "Custom/Outside/Puddle", "Custom/Outside/CharacterShadow", CharacterSpriteLighting.ShaderName, "Custom/Outside/LightShaft" })
        {
            var shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
        }
    }

    [UnityTest]
    public IEnumerator ActualSceneRendersPuddlesAndShadowsAndCleansUpOnReentry()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
        EditorSceneManager.OpenScene(OutsideGroundEffectsSetup.ScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return CaptureOutside();
        yield return new ExitPlayMode();
    }

    static IEnumerator CaptureOutside()
    {
        float oldCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        var camera = GameObject.Find("Main Camera").GetComponent<Camera>();
        // 배치 실행은 화면 출력 카메라를 그리지 않을 수 있으므로 처음부터 RT에 연결한다.
        var target = new RenderTexture(960, 540, 24) { filterMode = FilterMode.Point };
        var previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        yield return RenderFrames(camera, 90);
        foreach (var brain in Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None)) brain.enabled = false;
        foreach (var controller in Object.FindObjectsByType<CameraControllerNew>(FindObjectsSortMode.None)) controller.enabled = false;
        var window = camera.GetComponent<WindowRainLayer>();
        window.enabled = false;
        var luna = GameObject.Find("Luna").GetComponent<OutsideCharacterShadow>();
        luna.transform.position += new Vector3(-4.4f, -.98f, 0) - luna.FootPosition;
        camera.transform.position = new Vector3(-4.4f, -0.12f, -10);
        camera.orthographicSize = 1.35f;
        var puddles = Object.FindObjectsByType<OutsidePuddleSurface>(FindObjectsSortMode.None);
        var shadows = Object.FindObjectsByType<OutsideCharacterShadow>(FindObjectsSortMode.None);
        Directory.CreateDirectory(OutsideGroundEffectsSetup.ValidationDirectory);
        try
        {
            yield return RenderFrames(camera, 60);
            Time.timeScale = 0;
            yield return RenderFrames(camera, 3);
            Assert.That(luna.CurrentReceiver, Is.Not.Null, "Luna must have a street receiver.");
            var all = Capture(target, "outside-all");
            Assert.That(all.GetPixels32().Count(c => c.r + c.g + c.b > 30), Is.GreaterThan(all.width * all.height / 4), "Waited scene must render before comparing effect pixels.");
            foreach (var puddle in puddles) { puddle.enabled = false; puddle.enabled = true; }
            yield return RenderFrames(camera, 3);
            Object.Destroy(Capture(target, "outside-reflection-without-ripples"));
            foreach (var puddle in puddles) puddle.enabled = false;
            yield return RenderFrames(camera, 3);
            var noPuddles = Capture(target, "outside-no-puddles");
            int puddlePixels = Changed(all, noPuddles);
            Assert.That(puddlePixels, Is.GreaterThan(20), "Puddles must change actual pixels.");
            Assert.That(puddlePixels, Is.LessThan(target.width * target.height / 10), "Only ground pixels should change.");
            foreach (var shadow in shadows) shadow.enabled = false;
            yield return RenderFrames(camera, 3);
            var baseline = Capture(target, "outside-baseline");
            int shadowPixels = Changed(noPuddles, baseline);
            Assert.That(shadowPixels, Is.GreaterThan(100), "Ground shadows must be visibly present, not just a handful of pixels.");
            TestContext.WriteLine($"Puddle pixels={puddlePixels}; shadow pixels={shadowPixels}");
            Object.Destroy(all); Object.Destroy(noPuddles); Object.Destroy(baseline);
            foreach (var puddle in puddles) puddle.enabled = true;
            foreach (var shadow in shadows) shadow.enabled = true;
            Time.timeScale = 1;
            yield return RenderFrames(camera, 30);
            Assert.That(puddles.Any(p => p.LivingRipples > 0), Is.True, "Floor rain must work with WindowRain disabled.");
            window.enabled = true;
            yield return RenderFrames(camera, 30);
            Object.Destroy(Capture(target, "outside-rain-combined"));
        }
        finally
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = oldCaptureDelta;
            camera.targetTexture = previousTarget;
            target.Release(); Object.Destroy(target);
        }
        var old = luna;
        var loading = EditorSceneManager.LoadSceneAsyncInPlayMode(OutsideGroundEffectsSetup.ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        while (!loading.isDone) yield return null;
        yield return null;
        Assert.That(old == null, Is.True);
        Assert.That(Object.FindObjectsByType<OutsidePuddleSurface>(FindObjectsSortMode.None).Length, Is.EqualTo(9));
    }

    [UnityTest]
    public IEnumerator ReflectionReadsBackgroundButExcludesForegroundCharacters()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyReflection();
        yield return new ExitPlayMode();
    }

    static IEnumerator VerifyReflection()
    {
        var root = new GameObject("Reflection verification");
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * 0.5f, Texture2D.whiteTexture.width);
        var unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        SpriteRenderer Block(string name, Vector2 position, Vector2 size, Color color, string layer)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = unlit; renderer.color = color; renderer.sortingLayerName = layer;
            return renderer;
        }
        var background = Block("background", Vector2.zero, Vector2.one * 8, Color.cyan, "BackgroundMid");
        Block("floor", new Vector2(0, -0.9f), new Vector2(8, 0.5f), Color.gray, "Floorground");
        var character = Block("excluded foreground", new Vector2(0, 0.5f), new Vector2(8, 2), Color.magenta, "Character");
        var cameraObject = new GameObject("test camera", typeof(Camera)); cameraObject.transform.SetParent(root.transform);
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 1.35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        var pipeline = new SerializedObject(GraphicsSettings.defaultRenderPipeline);
        var renderers = pipeline.FindProperty("m_RendererDataList");
        int index = Enumerable.Range(0, renderers.arraySize).First(i => AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(i).objectReferenceValue) == OutsideGroundEffectsSetup.RendererPath);
        camera.GetUniversalAdditionalCameraData().SetRenderer(index);
        var target = new RenderTexture(480, 270, 24); camera.targetTexture = target;
        var puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(puddle.GetComponent<Collider>());
        puddle.transform.SetParent(root.transform);
        puddle.transform.position = new Vector3(0, -0.85f);
        puddle.transform.localScale = new Vector3(2, 0.25f, 1);
        var puddleRenderer = puddle.GetComponent<MeshRenderer>();
        puddleRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/01.Game/03.Content/03.Vfx/02.Materials/M_OutsidePuddle.mat");
        puddleRenderer.sortingLayerName = "GroundPuddle";
        puddle.AddComponent<OutsidePuddleSurface>().Configure(camera, null, 1);
        Directory.CreateDirectory(OutsideGroundEffectsSetup.ValidationDirectory);
        try
        {
            yield return RenderFrames(camera, 8);
            Vector3 point = camera.WorldToViewportPoint(puddle.transform.position);
            int x = Mathf.RoundToInt(point.x * target.width), y = Mathf.RoundToInt(point.y * target.height);
            var cyan = Capture(target, "reflection-cyan");
            Color c = cyan.GetPixel(x, y);
            Assert.That(c.g, Is.GreaterThan(c.r + 0.08f), "Reflected cyan must win over excluded magenta character.");
            character.color = Color.red;
            yield return RenderFrames(camera, 3);
            var foregroundChanged = Capture(target, "reflection-character-changed");
            Color unchanged = foregroundChanged.GetPixel(x, y);
            Assert.That(Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(unchanged.r, unchanged.g, unchanged.b)), Is.LessThan(0.02f));
            background.color = Color.yellow;
            yield return RenderFrames(camera, 3);
            var yellow = Capture(target, "reflection-yellow");
            Assert.That(yellow.GetPixel(x, y).r, Is.GreaterThan(c.r + 0.08f), "Reflection must update with its background source.");
            // 수면 중심에 반사되어야 하는 월드 좌표에 작은 표식을 놓고 카메라/줌을 변경한다.
            float reflectedY = -0.725f + 0.125f / 0.18f;
            var nearMarker = Block("near building reflection marker", new Vector2(0, reflectedY), new Vector2(0.4f, 0.16f), Color.blue, "ForeLightground");
            camera.transform.position += new Vector3(0.15f, 0.2f, 0);
            camera.orthographicSize = 1.7f;
            yield return RenderFrames(camera, 3);
            var moved = Capture(target, "reflection-camera-moved");
            point = camera.WorldToViewportPoint(puddle.transform.position);
            Color marker = moved.GetPixel(Mathf.RoundToInt(point.x * target.width), Mathf.RoundToInt(point.y * target.height));
            Assert.That(marker.b, Is.GreaterThan(marker.r + 0.08f), "The same world marker must stay reflected after moving and zooming the camera.");
            nearMarker.sortingLayerName = "ForeNoneLightground";
            nearMarker.color = Color.green;
            yield return RenderFrames(camera, 3);
            var pole = Capture(target, "reflection-near-pole");
            Color polePixel = pole.GetPixel(Mathf.RoundToInt(point.x * target.width), Mathf.RoundToInt(point.y * target.height));
            Assert.That(polePixel.g, Is.GreaterThan(polePixel.b + .08f), "Near unlit streetlight/fence layer must also be captured.");
            Object.Destroy(pole);
            // 최전경은 캐릭터를 가리는 원래 레이어를 유지하면서 동일 스프라이트를 반사한다.
            var surface = puddle.GetComponent<OutsidePuddleSurface>();
            nearMarker.sortingLayerName = "ForeFore";
            nearMarker.color = Color.red;
            surface.SetForegroundReflectors(new[] { nearMarker });
            yield return RenderFrames(camera, 3);
            var foregroundPole = Capture(target, "reflection-foreground-pole");
            Color front = foregroundPole.GetPixel(Mathf.RoundToInt(point.x * target.width), Mathf.RoundToInt(point.y * target.height));
            Assert.That(front.r, Is.GreaterThan(front.g + .08f), "A prop in front of the character must still appear in the water.");
            Assert.That(nearMarker.sortingLayerName, Is.EqualTo("ForeFore"));
            nearMarker.enabled = false;
            yield return RenderFrames(camera, 3);
            var hiddenPole = Capture(target, "reflection-foreground-pole-hidden");
            Color hidden = hiddenPole.GetPixel(Mathf.RoundToInt(point.x * target.width), Mathf.RoundToInt(point.y * target.height));
            Assert.That(hidden.g, Is.GreaterThan(front.g + .08f), "Disabled foreground props must leave the reflection immediately.");
            Object.Destroy(foregroundPole); Object.Destroy(hiddenPole);
            // 같은 수면 사각형에서 실제 GPU 윤곽과 빗방울 대비를 비교한다.
            nearMarker.gameObject.SetActive(false);
            var styles = new List<Texture2D>();
            Time.timeScale = 0;
            for (int i = 0; i < 4; i++)
            {
                surface.SetAppearance((OutsidePuddleSurface.Outline)i, null, .18f);
                yield return RenderFrames(camera, 3);
                styles.Add(Capture(target, "puddle-outline-" + i));
            }
            for (int i = 1; i < styles.Count; i++) Assert.That(Changed(styles[0], styles[i]), Is.GreaterThan(30), "Outline must differ beyond edge noise.");
            surface.SetAppearance(OutsidePuddleSurface.Outline.BrokenOval, null, .18f);
            for (int i = 0; i < 45; i++) surface.Advance(1f / 60, true);
            yield return RenderFrames(camera, 3);
            var rainy = Capture(target, "puddle-ripple-contrast");
            var dryPixels = styles[0].GetPixels32(); var wetPixels = rainy.GetPixels32();
            int bright = 0;
            for (int i = 0; i < dryPixels.Length; i++) if (wetPixels[i].r + wetPixels[i].g + wetPixels[i].b > dryPixels[i].r + dryPixels[i].g + dryPixels[i].b + 45) bright++;
            Assert.That(bright, Is.GreaterThan(15), "Rain rings must add visibly contrasting highlights.");
            TestContext.WriteLine("High-contrast ripple pixels=" + bright);
            foreach (var style in styles) Object.Destroy(style);
            Object.Destroy(rainy);
            Object.Destroy(moved);
            Object.Destroy(cyan); Object.Destroy(foregroundChanged); Object.Destroy(yellow);
        }
        finally
        {
            Time.timeScale = 1;
            camera.targetTexture = null;
            target.Release(); Object.Destroy(target);
            Object.Destroy(root); Object.Destroy(unlit); Object.Destroy(sprite);
        }
    }

    [Test]
    public void BakedShadowLightIsExplicitAndDoesNotRelightHome()
    {
        var caster = Make("home caster").AddComponent<SpriteRenderer>();
        var lamp = Make("baked source").AddComponent<OutsideShadowLight>();
        Assert.That(lamp.Influence(caster.transform.position, caster), Is.Zero);
        lamp.ConfigureBaked(4, 2);
        Assert.That(lamp.GetComponent<Light2D>(), Is.Null);
        Assert.That(lamp.Influence(caster.transform.position, caster), Is.GreaterThan(0));
        var receiver = Make("home room floor").AddComponent<OutsideGroundReceiver>();
        receiver.Configure(new Rect(-1, -.2f, 2, .4f), 0, .2f);
        lamp.ConfigureBaked(4, 2, receiver);
        Assert.That(lamp.Influence(Vector3.zero, caster), Is.GreaterThan(0));
        Assert.That(lamp.Influence(Vector3.down, caster), Is.Zero, "Baked light must not leak to another floor in the same scene.");
        lamp.enabled = false;
        Assert.That(lamp.Influence(caster.transform.position, caster), Is.Zero);
    }

    [UnityTest]
    public IEnumerator HomeRoomAndTerraceRenderGroundShadows()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
        var scene = EditorSceneManager.OpenScene(OutsideGroundEffectsSetup.HomeScenePath, OpenSceneMode.Single);
        var shadows = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OutsideCharacterShadow>(true)).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Luna", "Terrace_0" }, shadows.Select(s => s.name));
        yield return new EnterPlayMode();
        yield return CaptureHome();
        yield return new ExitPlayMode();
    }

    static IEnumerator CaptureHome()
    {
        var camera = GameObject.Find("Main Camera").GetComponent<Camera>();
        var target = new RenderTexture(960, 540, 24) { filterMode = FilterMode.Point };
        var previousTarget = camera.targetTexture; camera.targetTexture = target;
        float oldCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
        try
        {
            yield return RenderFrames(camera, 60);
            foreach (var brain in Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None)) brain.enabled = false;
            foreach (var controller in Object.FindObjectsByType<CameraControllerNew>(FindObjectsSortMode.None)) controller.enabled = false;
            var luna = GameObject.Find("Luna").GetComponent<OutsideCharacterShadow>();
            var terrace = GameObject.Find("Terrace_0").GetComponent<OutsideCharacterShadow>();
            // 정상 씬의 캐릭터 높이를 유지한다. 임의로 투명 여백까지 바닥 아래로 내리지 않는다.
            luna.transform.position += Vector3.right * (.25f - luna.FootPosition.x);
            var visibleFoot = OutsideGroundEffectsSetup.VisibleFoot(luna.GetComponent<SpriteRenderer>().sprite);
            Assert.That(Mathf.Abs(luna.FootPosition.y - luna.transform.TransformPoint(visibleFoot).y), Is.LessThan(.015f), "Anchor must touch visible foot pixels, not the transparent sprite rectangle.");
            var shadows = new[] { luna, terrace };
            foreach (var shadow in shadows)
            {
                bool room = shadow == luna;
                camera.transform.position = room ? new Vector3(.7f, .02f, -10) : new Vector3(.34f, -4.3f, -10);
                camera.orthographicSize = 1.35f;
                yield return RenderFrames(camera, 30);
                Time.timeScale = 0;
                yield return RenderFrames(camera, 3);
                Assert.That(shadow.CurrentReceiver, Is.Not.Null, shadow.name + " must connect to its own floor.");
                Assert.That(shadow.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.name.StartsWith("Projected")), Is.True);
                string name = room ? "home-room" : "home-terrace";
                var withShadow = Capture(target, name);
                shadow.enabled = false;
                yield return RenderFrames(camera, 3);
                var without = Capture(target, name + "-without-shadow");
                int changed = Changed(withShadow, without);
                Assert.That(changed, Is.GreaterThan(150), name + " must visibly change ground pixels.");
                TestContext.WriteLine(name + " shadow pixels=" + changed);
                Object.Destroy(withShadow); Object.Destroy(without);
                shadow.enabled = true;
                Time.timeScale = 1;
            }
        }
        finally
        {
            Time.timeScale = 1; Time.captureDeltaTime = oldCaptureDelta;
            camera.targetTexture = previousTarget; target.Release(); Object.Destroy(target);
        }
    }

    // EditMode 테스트의 null yield 횟수와 실제 카메라 렌더 횟수는 같지 않다.
    // 아직 그려지지 않은 RT 또는 이전 상태의 프레임으로 시각 검증이 통과하지 않도록 한다.
    static IEnumerator RenderFrames(Camera camera, int count)
    {
        int rendered = 0;
        void Count(ScriptableRenderContext context, Camera current) { if (current == camera) rendered++; }
        RenderPipelineManager.endCameraRendering += Count;
        float deadline = Time.realtimeSinceStartup + 30;
        try
        {
            while (rendered < count)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Camera did not render the requested frames.");
                yield return null;
            }
        }
        finally { RenderPipelineManager.endCameraRendering -= Count; }
    }

    static Texture2D Capture(RenderTexture target, string name)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var result = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); result.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(OutsideGroundEffectsSetup.ValidationDirectory, name + ".png"), result.EncodeToPNG());
        return result;
    }
    static int Changed(Texture2D a, Texture2D b)
    {
        var left = a.GetPixels32(); var right = b.GetPixels32(); int count = 0;
        for (int i = 0; i < left.Length; i++)
            if (Mathf.Abs(left[i].r - right[i].r) + Mathf.Abs(left[i].g - right[i].g) + Mathf.Abs(left[i].b - right[i].b) > 6) count++;
        return count;
    }
    [UnityTearDown]
    public IEnumerator ExitAfterFailure()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
