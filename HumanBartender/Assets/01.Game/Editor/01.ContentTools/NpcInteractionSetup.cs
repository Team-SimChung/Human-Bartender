using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 NPC를 공통 Actor·Interaction 연결로 한 번 이전한다. 작성한 설정은 보존한다.</summary>
public static class NpcInteractionSetup
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string GuardSpritePath = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/GangNPC1_idle.png";
    private const string GuardAnimationFolder = "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Guard";

    [MenuItem("Tools/Story/Migrate NPC Interaction Sequences")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("NPC 연출 이전은 편집 모드에서 실행하세요.");
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty)
            throw new InvalidOperationException("OutSide 씬의 편집 내용을 저장한 다음 NPC 연출 이전을 실행하세요.");
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            var repairman = targets.Single(value => value.StorySceneId == "neighborhood-repairGreeting");
            var thug = targets.Single(value => value.StorySceneId == "day1-corridor-thug");
            ConfigureRepairmanTarget(repairman, repairman.GetComponent<NpcActor>());
            ConfigureThugTarget(thug, thug.GetComponent<NpcActor>());
            ConfigureGuardTarget(targets.Single(value => value.StorySceneId == "neighborhood-guardBlocked"));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("NPC 연출 이전 후 씬을 저장하지 못했습니다.");
            AssetDatabase.SaveAssets();
            Debug.Log("NPC_INTERACTION_SETUP_OK: 수리공·1일차 불량배·경비원을 공통 NPC 구조로 이전했습니다.");
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static NpcInteraction ConfigureGuardTarget(WorldInteractionTarget target)
    {
        var existing = target.GetComponent<NpcInteraction>();
        if (existing != null) return existing;
        var renderer = target.GetComponentsInChildren<SpriteRenderer>(true)
            .Single(value => value.sprite != null && AssetDatabase.GetAssetPath(value.sprite) == GuardSpritePath);
        var speakers = target.GetComponent<WorldDialogueSpeakers>();
        if (speakers == null || !speakers.TryResolve("guard", out var anchor, out _, out _) || anchor == null)
            throw new InvalidOperationException("경비원의 기존 화자와 말풍선 기준점을 먼저 연결하세요.");

        var legacy = target.GetComponent<WorldStoryActors>();
        Transform legacyRoot = null;
        if (legacy != null)
        {
            using var data = new SerializedObject(legacy);
            var actors = data.FindProperty("actors");
            if (actors.arraySize != 1 || actors.GetArrayElementAtIndex(0).FindPropertyRelative("Id").stringValue != "guard")
                throw new InvalidOperationException("경비원 대상에 다른 배우가 함께 연결되어 있어 자동 이전할 수 없습니다.");
            legacyRoot = (Transform)actors.GetArrayElementAtIndex(0).FindPropertyRelative("Root").objectReferenceValue;
        }
        // 빈 묶음만 평탄화하고 Sprite·말풍선의 실제 위치와 참조를 보존한다.
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
            animator.runtimeAnimatorController = CreateGuardController(renderer, target.transform);
        var movement = target.GetComponent<NpcMovement2D>() ?? target.gameObject.AddComponent<NpcMovement2D>();
        var actor = target.GetComponent<NpcActor>();
        if (actor == null)
        {
            actor = target.gameObject.AddComponent<NpcActor>();
            actor.Configure(movement, animator, renderer);
        }

        // 기존 일차·출퇴근·통행 허가 조건을 읽는다. 대화 종료 자체로 퇴장시키지 않는다.
        var result = Create(target, actor, true, 0, "", "", Array.Empty<NpcSequence>());
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        EditorUtility.SetDirty(target.gameObject);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(actor);
        return result;
    }

    private static AnimatorController CreateGuardController(SpriteRenderer renderer, Transform root)
    {
        string controllerPath = GuardAnimationFolder + "/Guard.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder(GuardAnimationFolder))
            AssetDatabase.CreateFolder("Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC", "Guard");
        string clipPath = GuardAnimationFolder + "/Guard_idle.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(GuardSpritePath).OfType<Sprite>()
                .OrderBy(value => int.Parse(value.name.Substring(value.name.LastIndexOf('_') + 1))).ToArray();
            if (sprites.Length != 9) throw new InvalidOperationException("경비원 대기 시트의 9개 Sprite를 확인하세요.");
            clip = new AnimationClip { name = "Guard_idle", frameRate = 10, wrapMode = WrapMode.Loop };
            var frames = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
                frames[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = sprites[i] };
            frames[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / clip.frameRate, value = sprites[0] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(renderer.transform, root), typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle", new Vector3(220, 70));
        idle.motion = clip;
        idle.writeDefaultValues = false;
        var walk = machine.AddState("Walk", new Vector3(440, 70));
        // 현 경비원에는 이동 연출이 없다. 전용 걷기 원본이 생기면 이 상태의 Motion을 교체한다.
        walk.motion = clip;
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

    public static NpcInteraction ConfigureRepairmanTarget(WorldInteractionTarget target, NpcActor actor)
    {
        var existing = target.GetComponent<NpcInteraction>();
        if (existing != null) return existing;
        var legacy = Legacy(target, "RepairmanInteraction");
        using var previous = legacy != null ? new SerializedObject(legacy) : null;
        float fade = Float(previous, "repairFadeDuration", .35f);
        float hold = Float(previous, "repairHoldDuration", 1f);
        var offset = Vector(previous, "departureOffset", new Vector2(2, 0));
        float departureFade = Float(previous, "departureFadeDuration", .35f);
        var result = Create(target, actor, false, 1, "!flag.elevator_repaired", "flag.elevator_repaired", new[]
        {
            new NpcSequence
            {
                id = "repair", trigger = NpcSequenceTrigger.WorldAction, worldAction = OutsideStoryWorldAction.RepairElevator,
                requiredCondition = "flag.elevator_repair_paid",
                actions = new[]
                {
                    Step(NpcActionKind.ScreenFadeOut, fade), Step(NpcActionKind.Wait, hold), Step(NpcActionKind.ScreenFadeIn, fade)
                }
            },
            new NpcSequence
            {
                id = "repair-complete", trigger = NpcSequenceTrigger.DialogueCompleted, requiredCondition = "flag.elevator_repaired",
                actions = new[]
                {
                    new NpcActionStep { kind = NpcActionKind.ObjectivePopup, text = "엘리베이터 수리" },
                    new NpcActionStep { kind = NpcActionKind.Depart, offset = offset, duration = departureFade }
                }
            }
        });
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        return result;
    }

    public static NpcInteraction ConfigureThugTarget(WorldInteractionTarget target, NpcActor actor)
    {
        var existing = target.GetComponent<NpcInteraction>();
        if (existing != null) return existing;
        var legacy = Legacy(target, "DayOneThugInteraction");
        using var previous = legacy != null ? new SerializedObject(legacy) : null;
        float distance = Float(previous, "approachStopDistance", .65f);
        float duration = Float(previous, "attackDuration", .45f);
        string state = String(previous, "attackState", "Punch");
        string returnState = String(previous, "returnState", "Idle");
        var offset = Vector(previous, "departureOffset", new Vector2(2, 0));
        float fade = Float(previous, "departureFadeDuration", .35f);
        var result = Create(target, actor, true, 0, "", "", new[]
        {
            new NpcSequence
            {
                id = "punch", trigger = NpcSequenceTrigger.WorldAction, worldAction = OutsideStoryWorldAction.ThugPunch,
                actions = new[]
                {
                    new NpcActionStep { kind = NpcActionKind.Approach, distance = distance },
                    new NpcActionStep { kind = NpcActionKind.Face },
                    new NpcActionStep { kind = NpcActionKind.Animation, stateName = state, returnState = returnState, duration = duration }
                }
            },
            new NpcSequence
            {
                id = "depart", trigger = NpcSequenceTrigger.WorldAction, worldAction = OutsideStoryWorldAction.ThugDepart,
                actions = new[] { new NpcActionStep { kind = NpcActionKind.Depart, offset = offset, duration = fade } }
            }
        });
        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
        return result;
    }

    private static NpcInteraction Create(WorldInteractionTarget target, NpcActor actor, bool storyPresence,
        int minimumDay, string presenceCondition, string committedCondition, NpcSequence[] sequences)
    {
        if (actor == null) throw new InvalidOperationException("기존 NPC Actor 연결이 없습니다: " + target.name);
        var result = target.gameObject.AddComponent<NpcInteraction>();
        using var data = new SerializedObject(result);
        data.FindProperty("actor").objectReferenceValue = actor;
        data.FindProperty("useStoryPresence").boolValue = storyPresence;
        data.FindProperty("minimumDay").intValue = minimumDay;
        data.FindProperty("presenceCondition").stringValue = presenceCondition;
        data.FindProperty("committedCondition").stringValue = committedCondition;
        var list = data.FindProperty("sequences");
        list.arraySize = sequences.Length;
        for (int i = 0; i < sequences.Length; i++)
        {
            var source = sequences[i];
            var sequence = list.GetArrayElementAtIndex(i);
            sequence.FindPropertyRelative("id").stringValue = source.id;
            sequence.FindPropertyRelative("trigger").enumValueIndex = (int)source.trigger;
            sequence.FindPropertyRelative("worldAction").enumValueIndex = (int)source.worldAction;
            sequence.FindPropertyRelative("requiredCondition").stringValue = source.requiredCondition ?? "";
            var actions = sequence.FindPropertyRelative("actions");
            actions.arraySize = source.actions.Length;
            for (int j = 0; j < source.actions.Length; j++)
            {
                var step = source.actions[j];
                var row = actions.GetArrayElementAtIndex(j);
                row.FindPropertyRelative("kind").enumValueIndex = (int)step.kind;
                row.FindPropertyRelative("target").objectReferenceValue = step.target;
                row.FindPropertyRelative("distance").floatValue = step.distance;
                row.FindPropertyRelative("offset").vector2Value = step.offset;
                row.FindPropertyRelative("stateName").stringValue = step.stateName ?? "";
                row.FindPropertyRelative("returnState").stringValue = step.returnState ?? "";
                row.FindPropertyRelative("duration").floatValue = step.duration;
                row.FindPropertyRelative("text").stringValue = step.text ?? "";
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(result);
        return result;
    }

    private static MonoBehaviour Legacy(WorldInteractionTarget target, string name) =>
        target.GetComponents<MonoBehaviour>().SingleOrDefault(component => component != null && component.GetType().Name == name);
    private static float Float(SerializedObject data, string name, float fallback) => data?.FindProperty(name)?.floatValue ?? fallback;
    private static Vector2 Vector(SerializedObject data, string name, Vector2 fallback) => data?.FindProperty(name)?.vector2Value ?? fallback;
    private static string String(SerializedObject data, string name, string fallback) => data?.FindProperty(name)?.stringValue ?? fallback;
    private static NpcActionStep Step(NpcActionKind kind, float duration) => new() { kind = kind, duration = duration };
}
