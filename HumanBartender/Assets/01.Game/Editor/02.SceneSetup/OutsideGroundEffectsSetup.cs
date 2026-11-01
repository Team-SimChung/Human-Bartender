using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>OutSide의 물웅덩이/그림자 연결을 재현한다. 기존 오브젝트의 아트 튜닝은 재실행해도 보존한다.</summary>
public static class OutsideGroundEffectsSetup
{
    public const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    public const string HomeScenePath = "Assets/01.Game/01.Scenes/Home.unity";
    public const string RendererPath = "Assets/01.Game/05.Settings/02.Rendering/Renderer2D_Outside.asset";
    const string SharedRendererPath = "Assets/01.Game/05.Settings/02.Rendering/Renderer2D.asset";
    const string PipelinePath = "Assets/01.Game/05.Settings/02.Rendering/UniversalRP.asset";
    const string MaterialFolder = "Assets/01.Game/03.Content/03.Vfx/02.Materials/";
    const string ShaderFolder = "Assets/01.Game/03.Content/03.Vfx/03.Shaders/";
    public static string ValidationDirectory => Path.Combine(Path.GetTempPath(), "OutsideGroundValidation");

    [MenuItem("Tools/Human Bartender/Outside Ground/Apply Puddle Revision And Home Shadows")]
    public static void ReviseScenes()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        Run();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == 0).ToArray();
        var root = all.First(t => t.name == "Outside Ground Effects");
        var horizon = root.Find("Reflection Ground Line");
        if (horizon == null) { horizon = new GameObject("Reflection Ground Line").transform; horizon.SetParent(root, false); }
        // 바닥 띠의 윗변(벽 밑선)이 거울축이다. 더 아래에 두면 웅덩이가 배경 대신 바닥을 비춘다.
        horizon.position = new Vector3(0, -.86f, 0);
        float[] depths = { .20f, .25f, .16f, .23f, .19f, .26f, .18f, .22f, .17f };
        var puddles = root.GetComponentsInChildren<OutsidePuddleSurface>().OrderBy(p => p.name).ToArray();
        var foreground = all.Select(t => t.GetComponent<SpriteRenderer>()).Where(r => r != null && r.sprite != null && r.sortingLayerName == "ForeFore")
            .OrderBy(r => r.sortingOrder).ThenByDescending(r => r.transform.position.z).ToArray();
        if (foreground.Length > 4) throw new InvalidOperationException("Puddle foreground reflector limit exceeded; choose explicit reflection props.");
        for (int i = 0; i < puddles.Length; i++)
        {
            var puddle = puddles[i];
            puddle.SetAppearance((OutsidePuddleSurface.Outline)(i % 4), horizon);
            puddle.SetForegroundReflectors(foreground);
            var scale = puddle.transform.localScale; scale.y = depths[i % depths.Length]; puddle.transform.localScale = scale;
            var pos = puddle.transform.position; pos.y = -1.13f + (i % 3) * .012f; puddle.transform.position = pos;
            var data = new SerializedObject(puddle);
            data.FindProperty("opacity").floatValue = .6f;
            data.FindProperty("dropsPerSecond").floatValue = 10;
            data.FindProperty("distortionPixels").floatValue = 1.25f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var caster in all.Select(t => t.GetComponent<OutsideCharacterShadow>()).Where(s => s != null))
        {
            if (caster.name != "shiba") caster.Configure(Material("M_OutsideCharacterShadow", "SH_OutsideCharacterShadow.shader"), VisibleFoot(DefaultSprite(caster.GetComponent<SpriteRenderer>())));
            TuneShadow(caster);
            // Citizen A/B는 배경 레이어에 있었으므로 배경 캡처와 분리한다.
            if (caster.name == "CitizenA" || caster.name == "CitizenB") caster.GetComponent<SpriteRenderer>().sortingLayerName = "Character";
        }
        var lights = all.Select(t => t.GetComponent<Light2D>()).Where(l => l != null && l.lightType != Light2D.LightType.Global && l.intensity > 0 && l.targetSortingLayers.Contains(SortingLayer.NameToID("Character"))).ToArray();
        // 거리 서쪽과 위층도 실제 Light2D를 추적한다. 고정된 아래층 7개만으로는 광원 공백이 있었다.
        foreach (var foot in new[] { new Vector2(-14, -1), new Vector2(-10, -1), new Vector2(-8, -1), new Vector2(-17, 9.3f) })
        {
            var lamp = lights.OrderBy(l => Vector2.Distance(l.transform.position, foot)).FirstOrDefault();
            if (lamp != null && lamp.GetComponent<OutsideShadowLight>() == null)
                lamp.gameObject.AddComponent<OutsideShadowLight>().Configure(lamp, new Vector2(0, .3f), 5);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ConfigureHome();
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
        Debug.Log("Revised ground effects: near environment capture, four puddle outlines, stronger ripples, OutSide and Home shadows.");
    }

    /// <summary>바닥 그림자가 있는 캐릭터에 본체 음영/림라이트를 붙인다. 웅덩이와 기존 그림자 튜닝값은 건드리지 않는다.</summary>
    [MenuItem("Tools/Human Bartender/Outside Ground/Apply Character Lighting")]
    public static void ApplyCharacterLighting()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var material = Material("M_CharacterSpriteLit", "SH_CharacterSpriteLit.shader");
        foreach (string path in new[] { ScenePath, HomeScenePath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var casters = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<OutsideCharacterShadow>(true))
                .Where(s => (s.gameObject.hideFlags & HideFlags.DontSave) == 0).ToArray();
            foreach (var caster in casters)
            {
                var renderer = caster.GetComponent<SpriteRenderer>();
                // 원래 2D 라이트를 받던 캐릭터만 계속 받게 해서 기존 밝기를 유지한다.
                if (caster.GetComponent<CharacterSpriteLighting>() == null)
                {
                    bool wasLit = renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.Contains("Sprite-Lit");
                    var data = new SerializedObject(caster.gameObject.AddComponent<CharacterSpriteLighting>());
                    data.FindProperty("receive2DLights").boolValue = wasLit;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            if (path == HomeScenePath)
            {
                // 실내 바닥은 화면상 매우 얕아 접지 실루엣도 짧게 눕힌다.
                foreach (var caster in casters)
                {
                    var data = new SerializedObject(caster);
                    data.FindProperty("contactFlatten").floatValue = .06f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
        Debug.Log("Character body shading and rim light applied to ground shadow casters.");
    }

    public const string StreetLampLight = "Light_Freeform_X-7.1_Y0.4_01";

    /// <summary>가로등 스프라이트는 바닥과 합쳐져 있으므로 가로등 Freeform 라이트에 빛줄기를 붙인다.</summary>
    [MenuItem("Tools/Human Bartender/Outside Ground/Apply Street Lamp Light Shaft")]
    public static void ApplyLightShafts()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var light = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light2D>(true)).First(l => l.name == StreetLampLight);
        AddLightShaft(light);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath) EditorSceneManager.OpenScene(previousScene);
        Debug.Log("Street lamp light shaft applied to " + StreetLampLight);
    }

    [MenuItem("Tools/Human Bartender/Outside Ground/Add Light Shaft To Selected Lights")]
    public static void AddLightShaftToSelection()
    {
        var lights = Selection.gameObjects.Select(g => g.GetComponent<Light2D>()).Where(l => l != null && l.lightType == Light2D.LightType.Freeform).ToArray();
        if (lights.Length == 0) { Debug.LogWarning("Freeform Light2D를 선택하세요. 빛줄기는 라이트 모양을 따라 그려집니다."); return; }
        foreach (var light in lights) AddLightShaft(light);
    }
    [MenuItem("Tools/Human Bartender/Outside Ground/Add Light Shaft To Selected Lights", true)]
    static bool CanAddLightShaft() => Selection.gameObjects.Any(g => g.GetComponent<Light2D>() != null);

    static void AddLightShaft(Light2D light)
    {
        var shaft = light.GetComponent<LightShaft>();
        if (shaft == null) shaft = Undo.AddComponent<LightShaft>(light.gameObject);
        shaft.Configure(light, Material("M_OutsideLightShaft", "SH_OutsideLightShaft.shader"));
        EditorUtility.SetDirty(shaft);
        EditorSceneManager.MarkSceneDirty(light.gameObject.scene);
    }

    public const string OutsideVolumeProfilePath = "Assets/01.Game/05.Settings/02.Rendering/VP_OutSide.asset";
    public static readonly string[] FogLayers = { "Fog Far", "Fog Mid", "Fog Near" };

    /// <summary>라이트가 1.0을 넘어 블룸에 걸리도록 HDR을 켜고, OutSide 전용 볼륨과 빌딩 층 사이 안개를 배치한다.
    /// 이미 있는 안개/프로파일은 다시 만들지 않아 아트 튜닝을 보존한다.</summary>
    [MenuItem("Tools/Human Bartender/Outside Ground/Apply Background Fog And Light Bloom")]
    public static void ApplyFogAndBloom()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        // HDR이 꺼져 있으면 2D 라이트를 받은 색이 1.0에서 잘려 블룸 임계값을 넘지 못한다.
        var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath));
        pipeline.FindProperty("m_SupportsHDR").boolValue = true;
        pipeline.ApplyModifiedPropertiesWithoutUndo();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == 0).ToArray();
        var volume = all.Select(t => t.GetComponent<Volume>()).First(v => v != null && v.isGlobal);
        volume.sharedProfile = OutsideProfile(volume.sharedProfile);
        EditorUtility.SetDirty(volume);

        var fogMaterial = Material("M_OutsideFog", "SH_OutsideFog.shader");
        SpriteRenderer Sprite(string name) => all.First(t => t.name == name && t.GetComponent<SpriteRenderer>() != null).GetComponent<SpriteRenderer>();
        Transform Group(string name) => all.First(t => t.name == name && t.parent != null && t.parent.name == "StreetEnvironment");
        // 먼 층일수록 짙게: 하늘 빌딩 위 → 뒤 빌딩 위 → 중간 빌딩 앞의 순서로 겹친다.
        var fogColor = new Color(.5f, .66f, .88f, 1);
        AddFog(Group("Background"), FogLayers[0], fogMaterial, Sprite("C_Back_Building_2"), "BackgroundFar", .7f, 7, fogColor);
        AddFog(Group("Background"), FogLayers[1], fogMaterial, Sprite("C_Back_Building_1"), "BackgroundMid", .55f, 6, fogColor);
        AddFog(Group("Middleground"), FogLayers[2], fogMaterial, Sprite("C_Middle_Building_2"), "CityLight", .35f, 4.5f, fogColor);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath) EditorSceneManager.OpenScene(previousScene);
        Debug.Log("Background fog layers and HDR light bloom applied to OutSide.");
    }

    /// <summary>현재 열린 OutSide에서 거리 화면을 안개/블룸 켜고 끈 상태로 렌더해 비교 이미지를 남긴다.</summary>
    [MenuItem("Tools/Human Bartender/Outside Ground/Capture Fog And Bloom Comparison")]
    public static void CaptureFogAndBloom()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var camera = all.First(t => t.name == "Main Camera").GetComponent<Camera>();
        var fogs = all.Select(t => t.GetComponent<BackgroundFog>()).Where(f => f != null).ToArray();
        var volume = all.Select(t => t.GetComponent<Volume>()).First(v => v != null && v.isGlobal);
        var oldPosition = camera.transform.position; float oldSize = camera.orthographicSize; var oldTarget = camera.targetTexture;
        float oldWeight = volume.weight;
        var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        Directory.CreateDirectory(ValidationDirectory);
        var report = new StringBuilder();
        void Shot(string name)
        {
            foreach (var fog in fogs) fog.Refresh();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(ValidationDirectory, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            report.AppendLine(name);
        }
        try
        {
            camera.targetTexture = target;
            camera.transform.position = new Vector3(-4.4f, -0.12f, -10);
            camera.orthographicSize = 1.35f;
            Shot("fogbloom-all");
            foreach (var fog in fogs) fog.enabled = false;
            Shot("fogbloom-no-fog");
            foreach (var fog in fogs) fog.enabled = true;
            volume.weight = 0;
            Shot("fogbloom-no-bloom");
            report.AppendLine($"fogs={fogs.Length} visible={fogs.Count(f => f.IsVisible)} areas={string.Join(" | ", fogs.Select(f => f.name + ":" + f.Area))}");
            report.AppendLine($"hdr={camera.allowHDR} post={camera.GetUniversalAdditionalCameraData().renderPostProcessing} profile={AssetDatabase.GetAssetPath(volume.sharedProfile)}");
        }
        finally
        {
            volume.weight = oldWeight;
            camera.transform.position = oldPosition; camera.orthographicSize = oldSize; camera.targetTexture = oldTarget;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
        File.WriteAllText(Path.Combine(ValidationDirectory, "fogbloom.txt"), report.ToString());
        Debug.Log("Fog/bloom comparison saved to " + ValidationDirectory + "\n" + report);
    }

    static void AddFog(Transform parent, string name, Material material, SpriteRenderer target, string layer, float density, float height, Color color)
    {
        if (parent.Find(name) != null) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var fog = go.AddComponent<BackgroundFog>();
        fog.Configure(material, target, layer, -10, density, color);
        var data = new SerializedObject(fog);
        data.FindProperty("height").floatValue = height;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>개발용 테스트 프로파일을 직접 고치지 않도록 OutSide 전용 복사본을 만들고 라이트 블룸 값을 맞춘다.</summary>
    static VolumeProfile OutsideProfile(VolumeProfile source)
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(OutsideVolumeProfilePath);
        if (existing != null) return existing;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, OutsideVolumeProfilePath);
        if (source != null)
            foreach (var component in source.components)
            {
                var copy = UnityEngine.Object.Instantiate(component);
                copy.name = component.name;
                copy.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(copy, profile);
                profile.components.Add(copy);
            }
        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = ScriptableObject.CreateInstance<Bloom>();
            bloom.name = nameof(Bloom);
            bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(bloom, profile);
            profile.components.Add(bloom);
        }
        // HDR에서 라이트를 받은 부분과 밝은 네온이 넓고 부드럽게 번지도록 한다.
        bloom.active = true;
        bloom.threshold.Override(.75f);
        bloom.intensity.Override(1.4f);
        bloom.scatter.Override(.75f);
        bloom.highQualityFiltering.Override(true);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    static void TuneShadow(OutsideCharacterShadow shadow)
    {
        var data = new SerializedObject(shadow);
        data.FindProperty("opacity").floatValue = .65f;
        data.FindProperty("flatten").floatValue = .28f;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureHome()
    {
        var scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == 0).ToArray();
        var world = all.First(t => t.name == "03.World");
        var root = world.Find("Home Ground Shadows");
        if (root == null) { root = new GameObject("Home Ground Shadows").transform; root.SetParent(world, false); }
        var material = Material("M_OutsideCharacterShadow", "SH_OutsideCharacterShadow.shader");
        var luna = all.First(t => t.name == "Luna" && t.GetComponent<SpriteRenderer>() != null);
        var shadow = luna.GetComponent<OutsideCharacterShadow>();
        if (shadow == null)
        {
            var sprite = luna.GetComponent<SpriteRenderer>().sprite;
            shadow = luna.gameObject.AddComponent<OutsideCharacterShadow>();
            shadow.Configure(material, new Vector2(sprite.bounds.center.x, sprite.bounds.min.y));
        }
        shadow.Configure(material, VisibleFoot(DefaultSprite(luna.GetComponent<SpriteRenderer>())));
        TuneShadow(shadow);
        // 실내 바닥은 화면상 매우 얕으므로 그림자를 옆으로 길게 눕힌다.
        var roomShadow = new SerializedObject(shadow);
        roomShadow.FindProperty("flatten").floatValue = .055f;
        roomShadow.ApplyModifiedPropertiesWithoutUndo();
        // Home 배경은 이미 조명이 그려진 스프라이트다. 광원 기준만 추가해 원본 밝기를 유지한다.
        HomeReceiver(root, "Room floor", new Rect(-1.55f, -1.28f, 4.65f, .5f), -1.04f, 2);
        var terrace = all.First(t => t.name == "Terrace_0");
        var terraceShadow = terrace.GetComponent<OutsideCharacterShadow>();
        if (terraceShadow == null)
        {
            terraceShadow = terrace.gameObject.AddComponent<OutsideCharacterShadow>();
            // 합성된 두 인물 프레임의 아래쪽에는 투명 여백이 있으므로 실제 발 위치를 사용한다.
            terraceShadow.Configure(material, new Vector2(0, -.33f));
        }
        terraceShadow.Configure(material, VisibleFoot(DefaultSprite(terrace.GetComponent<SpriteRenderer>())));
        TuneShadow(terraceShadow);
        terrace.GetComponent<SpriteRenderer>().sortingOrder = 3;
        all.First(t => t.name == "balustrade").GetComponent<SpriteRenderer>().sortingOrder = 4;
        HomeReceiver(root, "Terrace floor", new Rect(-2.05f, -5.22f, 4.8f, .48f), -4.91f, 2);
        BakedLamp(root, "Room ceiling light", new Vector2(-.9f, .6f), 6, 2.5f, root.Find("Room floor").GetComponent<OutsideGroundReceiver>());
        BakedLamp(root, "Terrace city light", new Vector2(1.6f, -3.1f), 5, 2, root.Find("Terrace floor").GetComponent<OutsideGroundReceiver>());
        foreach (var camera in all.Select(t => t.GetComponent<Camera>()).Where(c => c != null)) camera.rect = new Rect(0, 0, 1, 1);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    static void HomeReceiver(Transform root, string name, Rect area, float y, int order)
    {
        var target = root.Find(name);
        if (target == null) { target = new GameObject(name).transform; target.SetParent(root, false); }
        var receiver = target.GetComponent<OutsideGroundReceiver>();
        if (receiver == null) receiver = target.gameObject.AddComponent<OutsideGroundReceiver>();
        receiver.Configure(area, y, .4f); receiver.SetSorting("Default", order);
    }
    static void BakedLamp(Transform root, string name, Vector2 position, float range, float intensity, OutsideGroundReceiver ground)
    {
        var target = root.Find(name);
        if (target == null) { target = new GameObject(name).transform; target.SetParent(root, false); }
        target.position = position;
        var light = target.GetComponent<OutsideShadowLight>();
        if (light == null) light = target.gameObject.AddComponent<OutsideShadowLight>();
        light.ConfigureBaked(range, intensity, ground);
    }

    static Sprite DefaultSprite(SpriteRenderer renderer)
    {
        var animator = renderer.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)
        {
            Motion motion = controller.layers[0].stateMachine.defaultState?.motion;
            while (motion is UnityEditor.Animations.BlendTree tree && tree.children.Length > 0)
                motion = tree.children.OrderBy(c => c.threshold).First().motion;
            if (motion is AnimationClip clip)
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    if (binding.path == "" && binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                    {
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        if (keys.Length > 0 && keys[0].value is Sprite first) return first;
                    }
        }
        return renderer.sprite;
    }

    // 에디터에서 원본 PNG만 읽는다. 런타임에는 Read/Write 텍스처나 매 프레임 알파 스캔이 필요 없다.
    public static Vector2 VisibleFoot(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Assign a ground anchor for non-PNG sprite: " + path);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read sprite alpha: " + path);
            var pixels = texture.GetPixels32(); var rect = sprite.rect;
            for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
            {
                int left = (int)rect.xMax, right = -1;
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                    if (pixels[y * texture.width + x].a > 32) { left = Mathf.Min(left, x); right = x; }
                if (right >= left)
                {
                    // 양발 높이가 1~3픽셀 다른 프레임도 한쪽 발로 중심이 쏠리지 않게 한다.
                    for (int row = y + 1; row < Mathf.Min(y + 5, (int)rect.yMax); row++)
                        for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                            if (pixels[row * texture.width + x].a > 32) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
                    return new Vector2((left + right + 1) * .5f - rect.x - sprite.pivot.x, y - rect.y - sprite.pivot.y) / sprite.pixelsPerUnit;
                }
            }
            throw new InvalidOperationException("Sprite is fully transparent: " + sprite.name);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    [MenuItem("Tools/Human Bartender/Outside Ground/Connect Scene")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AddSortingLayers();
        int rendererIndex = ConfigureRenderer();
        Material puddleMaterial = Material("M_OutsidePuddle", "SH_OutsidePuddle.shader");
        Material shadowMaterial = Material("M_OutsideCharacterShadow", "SH_OutsideCharacterShadow.shader");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == 0).ToArray();
        var camera = transforms.First(t => t.name == "Main Camera").GetComponent<Camera>();
        var rain = transforms.First(t => t.name == "FX_Rain").gameObject;
        camera.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);
        EditorUtility.SetDirty(camera.GetUniversalAdditionalCameraData());

        var root = transforms.FirstOrDefault(t => t.name == "Outside Ground Effects");
        if (root == null)
        {
            root = new GameObject("Outside Ground Effects").transform;
            root.SetParent(transforms.First(t => t.name == "03.World"), false);
        }
        AddReceiver(transforms.First(t => t.name == "1F"), new Rect(-22.3f, -1.42f, 28.9f, 0.58f), -1.12f);
        var upper = transforms.First(t => t.name == "2F");
        var upperCollider = upper.GetComponent<BoxCollider2D>();
        float upperTop = upperCollider.offset.y + upperCollider.size.y * 0.5f;
        AddReceiver(upper, new Rect(upperCollider.offset.x - upperCollider.size.x * 0.5f, upperTop - 0.35f,
            upperCollider.size.x, 0.55f), upperTop, "Character", -1);
        var elevatorFloor = transforms.First(t => t.name == "Floor" && t.GetComponent<BoxCollider2D>() != null);
        AddReceiver(elevatorFloor, new Rect(-0.58f, -0.35f, 1.16f, 0.65f), 0.05f, "ForeLightground", 1);

        float[] xs = { -14.2f, -12.0f, -9.5f, -6.4f, -4.4f, -2.2f, 0.3f, 2.9f, 5.0f };
        float[] widths = { 1.2f, 0.85f, 1.5f, 1.6f, 1.1f, 1.7f, 1.25f, 1.45f, 1.0f };
        for (int i = 0; i < xs.Length; i++)
        {
            string name = "Puddle " + (i + 1).ToString("00");
            if (root.Find(name) != null) continue;
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            puddle.name = name;
            UnityEngine.Object.DestroyImmediate(puddle.GetComponent<Collider>());
            puddle.layer = LayerMask.NameToLayer("Water");
            puddle.transform.SetParent(root, false);
            puddle.transform.position = new Vector3(xs[i], -1.22f + (i % 2) * 0.025f, 0);
            puddle.transform.localScale = new Vector3(widths[i], 0.13f + (i % 3) * 0.025f, 1);
            var renderer = puddle.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = puddleMaterial;
            renderer.sortingLayerName = "GroundPuddle";
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            puddle.AddComponent<OutsidePuddleSurface>().Configure(camera, rain, 713 + i * 97);
        }

        string[] casters = { "Luna", "CitizenA", "CitizenB", "Samho", "shiba", "Bubi" };
        foreach (string name in casters)
        {
            var target = transforms.First(t => t.name == name && t.GetComponent<SpriteRenderer>() != null);
            if (target.GetComponent<OutsideCharacterShadow>() != null) continue;
            var sprite = target.GetComponent<SpriteRenderer>();
            if (sprite.sprite == null) throw new InvalidOperationException("Missing sprite for " + name);
            Vector3 localFoot = sprite.sprite.bounds.center;
            localFoot.y = sprite.sprite.bounds.min.y;
            // shiba의 기본 프레임은 상반신 조각이므로 지면 앵커를 별도로 둔다.
            if (name == "shiba") localFoot.y = -1.05f - target.position.y;
            target.gameObject.AddComponent<OutsideCharacterShadow>().Configure(shadowMaterial, localFoot);
        }

        string[] lightNames = { "Atmospheric Light (8)", "Atmospheric Light (7)", "Atmospheric Light (1)",
            "Atmospheric Light (2)", "Atmospheric Light (5)", "Atmospheric Light (6)", "Light_Freeform_X-12.2_Y-0.5_01" };
        foreach (string name in lightNames)
        {
            var target = transforms.FirstOrDefault(t => t.name == name && t.GetComponent<Light2D>() != null);
            if (target == null) throw new InvalidOperationException("Shadow light missing: " + name);
            if (target.GetComponent<OutsideShadowLight>() == null)
                target.gameObject.AddComponent<OutsideShadowLight>().Configure(target.GetComponent<Light2D>(), new Vector2(0, 0.2f), 3.5f);
        }

        // PixelPerfectCamera가 배치 실행의 임시 창 비율로 계산한 viewport를 씬에 저장하지 않는다.
        camera.rect = new Rect(0, 0, 1, 1);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(ValidationDirectory);
        var report = new StringBuilder();
        foreach (var sprite in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)))
            if (casters.Contains(sprite.name) || sprite.name.Contains("Floor") || sprite.name == "StreetEnvironment")
                report.AppendLine($"{sprite.name}: sprite={sprite.sprite?.name}, position={sprite.transform.position}, bounds={sprite.bounds}, foot={sprite.GetComponent<OutsideCharacterShadow>()?.FootPosition}");
        report.AppendLine($"Puddles={root.GetComponentsInChildren<OutsidePuddleSurface>().Length}, RendererIndex={rendererIndex}");
        File.WriteAllText(Path.Combine(ValidationDirectory, "setup.txt"), report.ToString());
        Debug.Log("Outside ground effects connected. " + report);
    }

    static void AddReceiver(Transform target, Rect bounds, float top, string layer = "GroundShadow", int order = 0)
    {
        if (target.GetComponent<OutsideGroundReceiver>() != null) return;
        var receiver = target.gameObject.AddComponent<OutsideGroundReceiver>();
        receiver.Configure(bounds, top, 0.45f);
        receiver.SetSorting(layer, order);
    }

    static Material Material(string name, string shaderFile)
    {
        string path = MaterialFolder + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + shaderFile);
        if (shader == null) throw new InvalidOperationException("Shader missing: " + shaderFile);
        var material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static int ConfigureRenderer()
    {
        if (AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath) == null && !AssetDatabase.CopyAsset(SharedRendererPath, RendererPath))
            throw new InvalidOperationException("Could not copy Renderer2D.");
        var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
        var data = new SerializedObject(renderer);
        data.FindProperty("m_UseCameraSortingLayersTexture").boolValue = true;
        data.FindProperty("m_CameraSortingLayersTextureBound").intValue = SortingLayer.NameToID("ForeLightground");
        data.FindProperty("m_CameraSortingLayerDownsamplingMethod").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath));
        var list = pipeline.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
        if (index < 0)
        {
            index = list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            pipeline.ApplyModifiedPropertiesWithoutUndo();
        }
        return index;
    }

    static void AddSortingLayers()
    {
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("m_SortingLayers");
        AddSortingLayer(layers, "GroundPuddle", "ForeLightground", 1620135031);
        AddSortingLayer(layers, "GroundShadow", "GroundPuddle", 1620135032);
        MoveSortingLayerAfter(layers, "GroundPuddle", "ForeLightground");
        MoveSortingLayerAfter(layers, "GroundShadow", "GroundPuddle");
        tags.ApplyModifiedPropertiesWithoutUndo();
    }
    static void MoveSortingLayerAfter(SerializedProperty layers, string name, string after)
    {
        int source = -1, target = -1;
        for (int i = 0; i < layers.arraySize; i++)
        {
            string value = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
            if (value == name) source = i;
            if (value == after) target = i;
        }
        if (source < 0 || target < 0) throw new InvalidOperationException("Missing sorting layer");
        layers.MoveArrayElement(source, source < target ? target : target + 1);
    }
    static void AddSortingLayer(SerializedProperty layers, string name, string after, int id)
    {
        int insert = -1;
        for (int i = 0; i < layers.arraySize; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (layer.FindPropertyRelative("name").stringValue == name) return;
            if (layer.FindPropertyRelative("name").stringValue == after) insert = i + 1;
            if (layer.FindPropertyRelative("uniqueID").intValue == id) throw new InvalidOperationException("Sorting layer ID is occupied: " + id);
        }
        if (insert < 0) throw new InvalidOperationException("Sorting layer missing: " + after);
        layers.InsertArrayElementAtIndex(insert);
        var item = layers.GetArrayElementAtIndex(insert);
        item.FindPropertyRelative("name").stringValue = name;
        item.FindPropertyRelative("uniqueID").intValue = id;
        item.FindPropertyRelative("locked").boolValue = false;
    }
}
