using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>수리공 한 명을 NPC 실행 경로로 이전한다. 기존 배치와 대화 연결은 그대로 둔다.</summary>
public static class RepairmanNpcSetup
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string StoryId = "neighborhood-repairGreeting";
    private const string SpritePath = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/Repairman_idle.png";
    private const string AnimationFolder = "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Repairman";

    [MenuItem("Tools/Story/Set Up Repairman NPC")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("수리공 배치는 편집 모드에서 실행하세요.");

        var previousActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (!openedHere && scene.isDirty)
            throw new InvalidOperationException("OutSide 씬의 편집 내용을 저장한 다음 수리공 배치를 실행하세요.");
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var target = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
                .Single(value => value.StorySceneId == StoryId);
            SetUpTarget(target);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("OutSide 씬을 저장하지 못했습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log("REPAIRMAN_NPC_SETUP_OK: 기존 배치와 화자 연결을 유지하고 수리공의 Animator·이동·퇴장을 연결했습니다.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    private static void SetUpTarget(WorldInteractionTarget target)
    {
        var renderer = target.GetComponentsInChildren<SpriteRenderer>(true)
            .Single(value => value.sprite != null && AssetDatabase.GetAssetPath(value.sprite) == SpritePath);
        var speakers = target.GetComponent<WorldDialogueSpeakers>();
        if (speakers == null || !speakers.TryResolve("repair", out var anchor, out _, out _) || anchor == null)
            throw new InvalidOperationException("수리공의 기존 말풍선 기준점을 먼저 연결하세요.");

        var legacy = target.GetComponent<WorldStoryActors>();
        if (legacy != null)
        {
            using var data = new SerializedObject(legacy);
            var actors = data.FindProperty("actors");
            if (actors.arraySize != 1 || actors.GetArrayElementAtIndex(0).FindPropertyRelative("Id").stringValue != "repair")
                throw new InvalidOperationException("수리공 대상에 다른 배우가 함께 연결되어 있습니다. 자동 이전을 중단합니다.");
        }

        var animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = target.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        if (animator.runtimeAnimatorController == null)
        {
            string rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, target.transform);
            animator.runtimeAnimatorController = CreateControllerIfMissing(rendererPath);
        }
        BindSpriteClip(animator, renderer);

        var movement = target.GetComponent<NpcMovement2D>();
        if (movement == null)
        {
            movement = target.gameObject.AddComponent<NpcMovement2D>();
            movement.Configure(1.2f);
        }
        var actor = target.GetComponent<NpcActor>();
        if (actor == null)
        {
            actor = target.gameObject.AddComponent<NpcActor>();
            actor.Configure(movement, animator, renderer);
            using var actorData = new SerializedObject(actor);
            actorData.FindProperty("artworkFacesRight").boolValue = false;
            actorData.ApplyModifiedPropertiesWithoutUndo();
        }

        var interaction = NpcInteractionSetup.ConfigureRepairmanTarget(target, actor);

        // 새 실행 경로를 모두 연결한 뒤 이 대상에서만 이전 Sprite 제어자를 제거한다.
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        EditorUtility.SetDirty(target.gameObject);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(actor);
        EditorUtility.SetDirty(interaction);
    }

    private static void BindSpriteClip(Animator animator, SpriteRenderer renderer)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/Repairman_idle.anim");
        if (clip == null || !animator.runtimeAnimatorController.animationClips.Contains(clip)) return;
        string path = AnimationUtility.CalculateTransformPath(renderer.transform, animator.transform);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite" || binding.path == path) continue;
            var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), frames);
            EditorUtility.SetDirty(clip);
        }
    }

    private static AnimatorController CreateControllerIfMissing(string rendererPath)
    {
        string controllerPath = AnimationFolder + "/Repairman.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null) return existing;

        EnsureFolder(AnimationFolder);
        string clipPath = AnimationFolder + "/Repairman_idle.anim";
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (idleClip == null)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>()
                .OrderBy(value => FrameIndex(value.name)).ToArray();
            if (sprites.Length != 9) throw new InvalidOperationException("수리공 대기 시트의 9개 Sprite를 확인하세요.");
            idleClip = new AnimationClip { name = "Repairman_idle", frameRate = 10, wrapMode = WrapMode.Loop };
            var frames = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
                frames[i] = new ObjectReferenceKeyframe { time = i / idleClip.frameRate, value = sprites[i] };
            frames[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / idleClip.frameRate, value = sprites[0] };
            AnimationUtility.SetObjectReferenceCurve(idleClip,
                EditorCurveBinding.PPtrCurve(rendererPath, typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(idleClip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(idleClip, settings);
            AssetDatabase.CreateAsset(idleClip, clipPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle", new Vector3(220, 70));
        idle.motion = idleClip;
        idle.writeDefaultValues = false;
        var walk = machine.AddState("Walk", new Vector3(440, 70));
        // 전용 걷기 원본이 아직 없으므로 대기 시트를 사용한다. 이동 좌표는 NpcMovement2D가 담당한다.
        walk.motion = idleClip;
        walk.writeDefaultValues = false;
        machine.defaultState = idle;
        var start = idle.AddTransition(walk);
        start.hasExitTime = false;
        start.duration = 0;
        start.AddCondition(AnimatorConditionMode.Greater, .01f, "Speed");
        var stop = walk.AddTransition(idle);
        stop.hasExitTime = false;
        stop.duration = 0;
        stop.AddCondition(AnimatorConditionMode.Less, .01f, "Speed");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static int FrameIndex(string name)
    {
        int separator = name.LastIndexOf('_');
        return int.Parse(name.Substring(separator + 1));
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }

    public static void RunBatch()
    {
        try { Setup(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
