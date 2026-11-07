using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>현재 배치된 NPC에 웹 본편의 프레임·방향·반응 애니메이션을 연결한다. CSV와 배치는 변경하지 않는다.</summary>
public static class WebNpcAnimationSetup
{
    private const string Art = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/";
    private const string Root = "Assets/01.Game/03.Content/07.Animations/01.Characters/NpcInteraction";
    private const string CatFolder = Root + "/WebCat";
    private const string CatTexture = CatFolder + "/WebCatFrames.png";

    [Serializable] private sealed class FrameFile { public Vector2 pivot; public float pixelsPerUnit; public Frame[] frames; }
    [Serializable] private sealed class Frame
    {
        public string name;
        public float time, duration;
        public int x, y, width, height;
    }

    public static void Apply(Scene home, Scene outside)
    {
        if (Application.isPlaying) throw new InvalidOperationException("NPC 애니메이션 설정은 편집 모드에서 실행하세요.");
        var frames = ImportCatFrames();
        foreach (var scene in new[] { home, outside })
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("Home과 OutSide를 먼저 여세요.");
            foreach (var target in scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<WorldInteractionTarget>(true)))
            {
                if (target.GetComponent<NpcInteraction>() == null) continue;
                foreach (var actor in target.GetComponentsInChildren<NpcActor>(true))
                {
                    string role = actor.name.Contains("·") ? actor.name.Split('·').Last().Trim() : "";
                    if (role == "boobi") { ConfigureCat(actor, frames); continue; }
                    var definition = HumanDefinition(target.StorySceneId, role);
                    if (definition.path != null) ConfigureHuman(actor, target.StorySceneId, role, definition.path, definition.frames, definition.rate);
                }
                if (target.StorySceneId == "neighborhood-reveal") ConfigureBox(target, frames);
            }
        }
        ConfigureTerrace();
        AssetDatabase.SaveAssets();
    }

    private static (string path, int frames, float rate) HumanDefinition(string id, string role)
    {
        if (role == "samho") return (Art + "03.Samho/01.Idle/samho_idle.png", 8, 12);
        if (role == "dealer") return (Art + "05.StreetNPC/01.Idle/npc_m1_idle.png", 6, 8);
        if (role == "neighbor" || role == "thug" || id == "day1-corridor-thug")
            return (Art + "05.StreetNPC/01.Idle/Neighbor_idle.png", 9, 12);
        if (role == "chris") return (Art + "02.Chris/Chris_idle.png", 9, 12);
        if (role == "boss") return (Art + "05.StreetNPC/01.Idle/GangNPC5_idle.png", 9, 12);
        if (role == "gangmate") return (Art + "05.StreetNPC/01.Idle/GangNPC3_idle.png", 8, 12f * 8 / 9);
        if (role == "shiba") return (Art + "05.StreetNPC/01.Idle/shiba_idle.png", 10, 12);
        if (id == "neighborhood-repairGreeting") return (Art + "05.StreetNPC/01.Idle/Repairman_idle.png", 9, 12);
        if (id == "neighborhood-guardBlocked") return (Art + "05.StreetNPC/01.Idle/GangNPC1_idle.png", 9, 12);
        return (null, 0, 0);
    }

    private static SpriteRenderer Visual(NpcActor actor)
    {
        using var data = new SerializedObject(actor);
        return (SpriteRenderer)data.FindProperty("spriteRenderer").objectReferenceValue;
    }

    private static void ConfigureHuman(NpcActor actor, string id, string role, string texture, int expectedFrames, float rate)
    {
        var renderer = Visual(actor);
        var sprites = HumanFrames(role, texture);
        if (sprites.Length != expectedFrames) throw new InvalidOperationException(texture + "의 프레임 수를 확인하세요: " + sprites.Length);
        var animator = actor.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("NPC AnimatorController를 확인하세요: " + actor.name);
        string path = AssetDatabase.GetAssetPath(controller);
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal))
        {
            string directory = Root + "/WebOriginal_" + id.Replace('-', '_') + "_" + role;
            Folder(directory);
            string destination = directory + "/Actor.controller";
            var copied = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);
            if (copied == null)
            {
                if (!AssetDatabase.CopyAsset(path, destination)) throw new IOException("NPC 컨트롤러 복사 실패: " + path);
                copied = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);
            }
            controller = copied;
            path = destination;
            animator.runtimeAnimatorController = controller;
        }
        string folder = path[..path.LastIndexOf('/')];
        var idle = SpriteClip(folder + "/Idle.anim", "Idle", actor.transform, renderer, sprites,
            Enumerable.Range(0, sprites.Length).Select(index => index / rate).ToArray(), sprites.Length / rate, true);
        State(controller, "Idle", idle, true);
        // 웹의 이동도 같은 Idle 프레임을 사용한다. 기존 Speed 전이가 옛 정적 그림으로 돌아가지 않게 한다.
        if (controller.layers[0].stateMachine.states.Any(value => value.state.name == "Walk"))
            State(controller, "Walk", idle, false);
        renderer.sprite = sprites[0];
        using (var data = new SerializedObject(actor))
        {
            data.FindProperty("artworkFacesRight").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(actor);
    }

    private static FrameFile ImportCatFrames()
    {
        var file = JsonUtility.FromJson<FrameFile>(File.ReadAllText(CatFolder + "/WebCatFrames.json"));
        AssetDatabase.ImportAsset(CatTexture, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(CatTexture) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("웹 부비 스프라이트 시트가 없습니다.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = file.pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        var slices = new SpriteRect[file.frames.Length];
        for (int i = 0; i < file.frames.Length; i++)
        {
            var frame = file.frames[i];
            slices[i] = new SpriteRect
            {
                name = frame.name, rect = new Rect(frame.x, frame.y, frame.width, frame.height),
                alignment = SpriteAlignment.Custom, pivot = file.pivot
            };
        }
        NpcSpriteSheetImporter.Apply(importer, slices);
        importer.SaveAndReimport();
        return file;
    }

    private static void ConfigureCat(NpcActor actor, FrameFile file)
    {
        var renderer = Visual(actor);
        var animator = actor.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null) throw new InvalidOperationException("부비 AnimatorController가 없습니다.");
        string path = AssetDatabase.GetAssetPath(controller);
        string folder = path[..path.LastIndexOf('/')];
        var sprites = LoadFrames(CatTexture).ToDictionary(value => value.name, StringComparer.Ordinal);
        foreach (string state in new[] { "Idle", "Feed", "Pet", "Meow", "RevealEyes" })
        {
            var frames = file.frames.Where(value => value.name.StartsWith(state + "_", StringComparison.Ordinal)).ToArray();
            var clip = SpriteClip(folder + "/" + state + ".anim", state, actor.transform, renderer,
                frames.Select(value => sprites[value.name]).ToArray(), frames.Select(value => value.time).ToArray(),
                frames[0].duration, state == "Idle");
            State(controller, state, clip, state == "Idle");
        }
        // 기존 사용자 동작 이름은 유지하되, 이전 루트 크기 변화 곡선은 되살리지 않는다.
        // RevealEyes는 눈만 보이는 단계이고, 뒤따르는 포물선 이동은 Interaction이 담당한다.
        foreach (var alias in new[] { ("PulseFeed", "Feed"), ("PulsePet", "Pet"), ("PulseReveal", "RevealEyes") })
            State(controller, alias.Item1, AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + alias.Item2 + ".anim"), false);
        renderer.sprite = sprites["Idle_00"];
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(animator);
    }

    /// <summary>박스 배경을 건드리지 않고 같은 웹 부분 그림을 일시적으로 덧그리는 Animator다.</summary>
    private static void ConfigureBox(WorldInteractionTarget target, FrameFile file)
    {
        var child = target.transform.Find("Box animation");
        if (child == null)
        {
            child = new GameObject("Box animation").transform;
            child.SetParent(target.transform, false);
        }
        var renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
        var reference = target.GetComponentsInChildren<NpcActor>(true).Select(Visual).First();
        renderer.sharedMaterial = reference.sharedMaterial;
        renderer.sortingLayerID = reference.sortingLayerID;
        renderer.sortingOrder = reference.sortingOrder - 1;
        var animator = child.GetComponent<Animator>();
        if (animator == null) animator = child.gameObject.AddComponent<Animator>();
        const string path = CatFolder + "/Box.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        var sprites = LoadFrames(CatTexture).ToDictionary(value => value.name, StringComparer.Ordinal);
        foreach (string state in new[] { "BoxIdle", "BoxShake" })
        {
            var frames = file.frames.Where(value => value.name.StartsWith(state + "_", StringComparison.Ordinal)).ToArray();
            var clip = SpriteClip(CatFolder + "/" + state + ".anim", state, child, renderer,
                frames.Select(value => sprites[value.name]).ToArray(), frames.Select(value => value.time).ToArray(),
                frames[0].duration, state == "BoxIdle");
            State(controller, state == "BoxIdle" ? "Idle" : "Shake", clip, state == "BoxIdle");
        }
        animator.runtimeAnimatorController = controller;
        renderer.sprite = sprites["BoxIdle_00"];
        using (var data = new SerializedObject(target.GetComponent<NpcInteraction>()))
        {
            var cue = data.FindProperty("proximityAnimation");
            cue.FindPropertyRelative("animator").objectReferenceValue = animator;
            cue.FindPropertyRelative("stateName").stringValue = "Shake";
            cue.FindPropertyRelative("returnState").stringValue = "Idle";
            cue.FindPropertyRelative("duration").floatValue = .9f;
            cue.FindPropertyRelative("radius").floatValue = .85f;
            cue.FindPropertyRelative("when").stringValue = "!flag.boobi_box_shaken && !flag.boobi_revealed";
            cue.FindPropertyRelative("completionFlag").stringValue = "boobi_box_shaken";
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(renderer);
    }

    private static Sprite[] LoadFrames(string texture) => AssetDatabase.LoadAllAssetsAtPath(texture).OfType<Sprite>()
        .OrderBy(value => NumberSuffix(value.name)).ThenBy(value => value.name, StringComparer.Ordinal).ToArray();

    private static Sprite[] HumanFrames(string role, string texture)
    {
        // 편집용 슬라이스와 실제 게임용 슬라이스가 함께 있으므로, 원래 게임 클립의 프레임 순서를 유지한다.
        string original = role == "samho"
            ? "Assets/01.Game/03.Content/07.Animations/01.Characters/03.Samho/01.Clips/01.Idle/Samho Idle.anim"
            : role == "shiba" ? "Assets/01.Game/03.Content/07.Animations/01.Characters/07.Shiba/01.Clips/01.Bar/Shiba.anim" : null;
        if (original == null) return LoadFrames(texture);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(original);
        if (clip == null) throw new InvalidOperationException("원본 NPC 클립을 찾을 수 없습니다: " + original);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(value => value.propertyName == "m_Sprite");
        return AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(value => value.value).Cast<Sprite>().ToArray();
    }

    private static int NumberSuffix(string name) => int.TryParse(name[(name.LastIndexOf('_') + 1)..], out var number) ? number : 0;

    private static AnimationClip SpriteClip(string path, string name, Transform actor, SpriteRenderer renderer,
        Sprite[] frames, float[] times, float duration, bool loop)
    {
        if (frames.Length == 0 || frames.Length != times.Length) throw new ArgumentException("애니메이션 프레임이 없습니다: " + name);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.name = name;
        clip.frameRate = times.Length > 1 && times[1] > times[0] ? 1f / (times[1] - times[0]) : 100;
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        var binding = EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(renderer.transform, actor), typeof(SpriteRenderer), "m_Sprite");
        // Unity는 마지막 Sprite 키 뒤에 한 샘플 간격을 포함한다. 종료 시간에 키를 추가하면 한 프레임이 늘어난다.
        var keys = frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = times[index], value = sprite }).ToList();
        if (frames.Length == 1 && duration > 1 / clip.frameRate)
            keys.Add(new ObjectReferenceKeyframe { time = duration - 1 / clip.frameRate, value = frames[0] });
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void State(AnimatorController controller, string name, AnimationClip clip, bool makeDefault)
    {
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == name) ?? machine.AddState(name);
        state.motion = clip;
        state.speed = 1;
        state.writeDefaultValues = false;
        if (makeDefault) machine.defaultState = state;
        if (!controller.parameters.Any(value => value.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureTerrace()
    {
        const string path = "Assets/01.Game/03.Content/07.Animations/01.Characters/01.Luna/03.terrace/Terrace_ani.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) throw new InvalidOperationException("테라스 합성 애니메이션이 없습니다.");
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys.Length != 8) throw new InvalidOperationException("테라스 원본의 8프레임을 확인하세요.");
            for (int i = 0; i < keys.Length; i++) keys[i].time = i / 6f;
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        }
        clip.frameRate = 6;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0;
        settings.stopTime = 8 / 6f;
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path[..slash]);
        AssetDatabase.CreateFolder(path[..slash], path[(slash + 1)..]);
    }
}
