using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>1일차 복도 불량배만 NPC 실행 경로로 이전한다. 대본·조건·기존 화자 연결은 보존한다.</summary>
public static class DayOneThugNpcSetup
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string StoryId = "day1-corridor-thug";
    private const string SpritePath = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/Neighbor_idle.png";
    private const string AnimationFolder = "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/DayOneThug";

    [MenuItem("Tools/Story/Set Up Day One Thug NPC")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("불량배 배치는 편집 모드에서 실행하세요.");
        var previousActive = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (!openedHere && scene.isDirty)
            throw new InvalidOperationException("OutSide 씬의 편집 내용을 저장한 다음 불량배 배치를 실행하세요.");
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
            Debug.Log("DAY_ONE_THUG_NPC_SETUP_OK: 기존 배치·화자·CSV 연결을 유지하고 접근·타격·퇴장을 연결했습니다.");
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
        if (speakers == null || !speakers.TryResolve(StoryId, out var anchor, out _, out _) || anchor == null)
            throw new InvalidOperationException("불량배의 기존 CSV 화자와 말풍선 기준점을 먼저 연결하세요.");

        var legacy = target.GetComponent<WorldStoryActors>();
        Transform legacyRoot = null;
        if (legacy != null)
        {
            using var data = new SerializedObject(legacy);
            var actors = data.FindProperty("actors");
            if (actors.arraySize != 1 || actors.GetArrayElementAtIndex(0).FindPropertyRelative("Id").stringValue != "thug")
                throw new InvalidOperationException("불량배 대상에 다른 배우가 함께 연결되어 있습니다. 자동 이전을 중단합니다.");
            legacyRoot = (Transform)actors.GetArrayElementAtIndex(0).FindPropertyRelative("Root").objectReferenceValue;
        }

        // 현재 생성 계층의 빈 묶음만 제거한다. 사용자 컴포넌트나 추가 자식이 있으면 유지한다.
        if (legacyRoot != null && legacyRoot.parent == target.transform && legacyRoot != renderer.transform &&
            legacyRoot.GetComponents<Component>().Length == 1 && legacyRoot.childCount == 2 &&
            renderer.transform.parent == legacyRoot && anchor.parent == legacyRoot)
        {
            renderer.transform.SetParent(target.transform, true);
            anchor.SetParent(target.transform, true);
            UnityEngine.Object.DestroyImmediate(legacyRoot.gameObject);
        }

        var animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            animator = target.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        if (animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = CreateControllerIfMissing(renderer, target.transform);
        BindClips(animator, renderer);

        var movement = target.GetComponent<NpcMovement2D>();
        if (movement == null)
        {
            movement = target.gameObject.AddComponent<NpcMovement2D>();
            movement.Configure(2f);
        }
        var actor = target.GetComponent<NpcActor>();
        if (actor == null)
        {
            actor = target.gameObject.AddComponent<NpcActor>();
            actor.Configure(movement, animator, renderer);
        }
        var interaction = NpcInteractionSetup.ConfigureThugTarget(target, actor);

        // 모든 연결이 갖춰진 대상에서만 이전 Sprite 제어자를 제거한다.
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        EditorUtility.SetDirty(target.gameObject);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(actor);
        EditorUtility.SetDirty(interaction);
    }

    private static AnimatorController CreateControllerIfMissing(SpriteRenderer renderer, Transform root)
    {
        string controllerPath = AnimationFolder + "/DayOneThug.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null) return existing;
        EnsureFolder(AnimationFolder);
        string rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, root);
        var idleClip = CreateIdleIfMissing(rendererPath, renderer.transform.localScale);
        var punchClip = CreatePunchIfMissing(rendererPath, renderer.transform.localScale, renderer.sprite);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle", new Vector3(220, 70));
        idle.motion = idleClip;
        idle.writeDefaultValues = false;
        var walk = machine.AddState("Walk", new Vector3(440, 70));
        // 전용 걷기 시트가 없어 대기 시트를 공유한다. 이동은 NpcMovement2D가 담당한다.
        walk.motion = idleClip;
        walk.writeDefaultValues = false;
        var punch = machine.AddState("Punch", new Vector3(220, 170));
        punch.motion = punchClip;
        punch.writeDefaultValues = false;
        machine.defaultState = idle;
        var start = idle.AddTransition(walk);
        start.hasExitTime = false;
        start.duration = 0;
        start.AddCondition(AnimatorConditionMode.Greater, .01f, "Speed");
        var stop = walk.AddTransition(idle);
        stop.hasExitTime = false;
        stop.duration = 0;
        stop.AddCondition(AnimatorConditionMode.Less, .01f, "Speed");
        // Punch는 NpcActor가 직접 재생·완료 대기 후 Idle로 복귀한다.
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateIdleIfMissing(string path, Vector3 scale)
    {
        string clipPath = AnimationFolder + "/DayOneThug_idle.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null) return existing;
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>()
            .OrderBy(value => FrameIndex(value.name)).ToArray();
        if (sprites.Length != 9) throw new InvalidOperationException("불량배 대기 시트의 9개 Sprite를 확인하세요.");
        var clip = new AnimationClip { name = "DayOneThug_idle", frameRate = 10, wrapMode = WrapMode.Loop };
        var frames = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
            frames[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = sprites[i] };
        frames[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / clip.frameRate, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), frames);
        SetScaleCurve(clip, path, "x", AnimationCurve.Constant(0, .9f, scale.x));
        SetScaleCurve(clip, path, "y", AnimationCurve.Constant(0, .9f, scale.y));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static AnimationClip CreatePunchIfMissing(string path, Vector3 scale, Sprite sprite)
    {
        string clipPath = AnimationFolder + "/DayOneThug_punch.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null) return existing;
        // 전용 주먹 그림이 없는 현재 리소스용 타격 동작. 이후 Punch의 Motion만 교체할 수 있다.
        var clip = new AnimationClip { name = "DayOneThug_punch", frameRate = 30, wrapMode = WrapMode.Once };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"),
            new[] { new ObjectReferenceKeyframe { time = 0, value = sprite }, new ObjectReferenceKeyframe { time = .45f, value = sprite } });
        SetScaleCurve(clip, path, "x", new AnimationCurve(new Keyframe(0, scale.x), new Keyframe(.12f, scale.x * .93f),
            new Keyframe(.2f, scale.x * 1.12f), new Keyframe(.45f, scale.x)));
        SetScaleCurve(clip, path, "y", new AnimationCurve(new Keyframe(0, scale.y), new Keyframe(.12f, scale.y * 1.04f),
            new Keyframe(.2f, scale.y * .94f), new Keyframe(.45f, scale.y)));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static void SetScaleCurve(AnimationClip clip, string path, string axis, AnimationCurve curve) =>
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale." + axis), curve);

    private static void BindClips(Animator animator, SpriteRenderer renderer)
    {
        string path = AnimationUtility.CalculateTransformPath(renderer.transform, animator.transform);
        foreach (string name in new[] { "DayOneThug_idle", "DayOneThug_punch" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + "/" + name + ".anim");
            if (clip == null || !animator.runtimeAnimatorController.animationClips.Contains(clip)) continue;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite" || binding.path == path) continue;
                var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, binding.type, binding.propertyName), frames);
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal) || binding.path == path) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, binding.type, binding.propertyName), curve);
            }
            EditorUtility.SetDirty(clip);
        }
    }

    private static int FrameIndex(string name) => int.Parse(name.Substring(name.LastIndexOf('_') + 1));

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
}
