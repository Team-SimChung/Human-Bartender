using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Home·OutSide의 기존 배우 배치를 공통 NPC 연출로 이전한다. CSV와 작성한 Inspector 값은 보존한다.</summary>
public static class AllNpcInteractionSetup
{
    private const string AnimationRoot = "Assets/01.Game/03.Content/07.Animations/01.Characters/NpcInteraction";
    private static readonly string[] ScenePaths =
    {
        "Assets/01.Game/01.Scenes/Home.unity", "Assets/01.Game/01.Scenes/OutSide.unity"
    };

    private sealed class LegacyActor
    {
        internal string Id;
        internal Transform Root;
        internal SpriteRenderer Renderer;
        internal bool Visible;
        internal NpcActor Actor;
    }

    [MenuItem("Tools/Story/Migrate All Home and Outside NPCs")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("NPC 이전은 편집 모드에서 실행하세요.");
        foreach (string path in ScenePaths)
        {
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("NPC 이전 전에 씬 편집 내용을 저장하세요: " + loaded.name);
        }
        var previous = SceneManager.GetActiveScene();
        int migrated = 0;
        try
        {
            foreach (string path in ScenePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var targets = scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
                    int sceneCount = 0;
                    foreach (var target in targets)
                    {
                        if (target.GetComponent<NpcInteraction>() != null) continue;
                        if (target.GetComponent<WorldStoryActors>() != null)
                        {
                            ConfigureLegacyTarget(target);
                            sceneCount++;
                        }
                        else if (target.Content != null && target.Content.SourceId is "bubi" or "shiba")
                        {
                            ConfigureContentTarget(target);
                            sceneCount++;
                        }
                    }
                    if (sceneCount > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene))
                            throw new InvalidOperationException("NPC 이전 후 씬 저장에 실패했습니다: " + scene.name);
                    }
                    migrated += sceneCount;
                }
                finally
                {
                    if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ALL_NPC_INTERACTION_SETUP_OK: " + migrated + "개 대상을 공통 NPC 구조로 이전했습니다.");
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static NpcInteraction ConfigureLegacyTarget(WorldInteractionTarget target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var existing = target.GetComponent<NpcInteraction>();
        if (existing != null) return existing;
        var legacy = target.GetComponent<WorldStoryActors>();
        if (legacy == null) throw new InvalidOperationException("이전할 월드 배우가 없습니다: " + target.name);
        using var source = new SerializedObject(legacy);
        var entries = source.FindProperty("actors");
        var actors = new LegacyActor[entries.arraySize];
        for (int i = 0; i < actors.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            actors[i] = new LegacyActor
            {
                Id = entry.FindPropertyRelative("Id").stringValue,
                Root = (Transform)entry.FindPropertyRelative("Root").objectReferenceValue,
                Renderer = (SpriteRenderer)entry.FindPropertyRelative("Renderer").objectReferenceValue,
                Visible = entry.FindPropertyRelative("VisibleOutsideSession").boolValue
            };
        }
        if (actors.Length == 0 || actors.Any(value => string.IsNullOrWhiteSpace(value.Id) || value.Root == null ||
            value.Renderer == null || !value.Root.IsChildOf(target.transform) || !value.Renderer.transform.IsChildOf(value.Root)) ||
            actors.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != actors.Length)
            throw new InvalidOperationException("기존 배우의 이름·위치·Sprite 연결을 확인하세요: " + target.name);

        float moveDuration = source.FindProperty("moveDuration").floatValue;
        foreach (var value in actors)
            value.Actor = ConfigureActor(value.Root, value.Renderer,
                target.gameObject.scene.name + "_" + target.StorySceneId + "_" + value.Id, value.Id == "boobi");

        var sequences = CreateSequences(target.StorySceneId, actors, moveDuration);
        var result = CreateInteraction(target, actors[0].Actor, true, sequences);
        using (var data = new SerializedObject(result))
        {
            data.FindProperty("actorVisibleOutsideSession").boolValue = actors[0].Visible;
            data.FindProperty("actorPresenceCondition").stringValue = ActorCondition(target, actors[0]);
            data.FindProperty("requireVisibleActorToInteract").boolValue = false;
            data.FindProperty("restorePoseAfterInteraction").boolValue = true;
            var additional = data.FindProperty("additionalActors");
            additional.arraySize = actors.Length - 1;
            for (int i = 1; i < actors.Length; i++)
            {
                var entry = additional.GetArrayElementAtIndex(i - 1);
                entry.FindPropertyRelative("actor").objectReferenceValue = actors[i].Actor;
                entry.FindPropertyRelative("visibleOutsideSession").boolValue = actors[i].Visible;
                entry.FindPropertyRelative("presenceCondition").stringValue = ActorCondition(target, actors[i]);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        UnityEngine.Object.DestroyImmediate(legacy);
        // 집 안 부비는 이전 배치에서 루트가 꺼져 있었다. 이제 Actor의 CSV 표시 조건이 입양 전후를 관리한다.
        if (target.StorySceneId == "neighborhood-homeCat") target.gameObject.SetActive(true);
        EditorUtility.SetDirty(result);
        return result;
    }

    private static string ActorCondition(WorldInteractionTarget target, LegacyActor actor) =>
        target.StorySceneId == "neighborhood-reveal" && actor.Id == "boobi" ? "flag.boobi_revealed" : "";

    public static NpcInteraction ConfigureContentTarget(WorldInteractionTarget target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        var existing = target.GetComponent<NpcInteraction>();
        if (existing != null) return existing;
        if (target.Content == null || target.Content.SourceId is not ("bubi" or "shiba"))
            throw new InvalidOperationException("NPC CSV Source ID를 확인하세요: " + target.name);
        var renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null) throw new InvalidOperationException("기존 NPC Sprite가 없습니다: " + target.name);
        var actor = ConfigureActor(target.transform, renderer,
            target.gameObject.scene.name + "_Content_" + target.Content.SourceId, false);
        // 출현·방향·좌표·반복 여부는 기존 WorldInteractionContent가 CSV에서 계속 선택한다.
        return CreateInteraction(target, actor, false, Array.Empty<NpcSequence>());
    }

    private static NpcActor ConfigureActor(Transform root, SpriteRenderer renderer, string key, bool catPulses)
    {
        var animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        string folder = AnimationRoot + "/" + key;
        EnsureFolder(folder);
        if (animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = CreateController(folder, root, renderer, catPulses);
        else if (animator.runtimeAnimatorController is AnimatorController original &&
            !original.parameters.Any(value => value.name == "Speed" && value.type == AnimatorControllerParameterType.Float))
        {
            // 공용 원본의 기존 상태·전이를 그대로 복사하고 NPC 이동 속도 입력만 추가한다.
            string path = folder + "/Actor.controller";
            var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (copy == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path))
                    throw new InvalidOperationException("기존 NPC Animator 복사에 실패했습니다: " + root.name);
                copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            }
            if (!copy.parameters.Any(value => value.name == "Speed")) copy.AddParameter("Speed", AnimatorControllerParameterType.Float);
            animator.runtimeAnimatorController = copy;
            EditorUtility.SetDirty(copy);
        }
        var movement = root.GetComponent<NpcMovement2D>() ?? root.gameObject.AddComponent<NpcMovement2D>();
        movement.Face(!renderer.flipX);
        var actor = root.GetComponent<NpcActor>() ?? root.gameObject.AddComponent<NpcActor>();
        actor.Configure(movement, animator, renderer);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(actor);
        return actor;
    }

    private static AnimatorController CreateController(string folder, Transform root, SpriteRenderer renderer, bool catPulses)
    {
        string path = folder + "/Actor.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null) return existing;
        var idle = new AnimationClip { name = "Idle", frameRate = 10, wrapMode = WrapMode.Loop };
        AnimationUtility.SetObjectReferenceCurve(idle,
            EditorCurveBinding.PPtrCurve(AnimationUtility.CalculateTransformPath(renderer.transform, root), typeof(SpriteRenderer), "m_Sprite"),
            new[]
            {
                new ObjectReferenceKeyframe { time = 0, value = renderer.sprite },
                new ObjectReferenceKeyframe { time = .1f, value = renderer.sprite }
            });
        if (catPulses)
            for (int axis = 0; axis < 3; axis++)
                AnimationUtility.SetEditorCurve(idle,
                    EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale." + "xyz"[axis]),
                    AnimationCurve.Constant(0, .1f, root.localScale[axis]));
        var settings = AnimationUtility.GetAnimationClipSettings(idle);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings);
        AssetDatabase.CreateAsset(idle, folder + "/Idle.anim");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle", new Vector3(220, 70));
        idleState.motion = idle;
        idleState.writeDefaultValues = false;
        machine.defaultState = idleState;
        if (catPulses)
        {
            AddPulse(controller, folder, "PulseReveal", root.localScale, .2f, 150);
            AddPulse(controller, folder, "PulseFeed", root.localScale, .3f, 230);
            AddPulse(controller, folder, "PulsePet", root.localScale, .15f, 310);
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddPulse(AnimatorController controller, string folder, string name, Vector3 scale, float amount, float y)
    {
        var clip = new AnimationClip { name = name, frameRate = 50, wrapMode = WrapMode.Once };
        for (int axis = 0; axis < 3; axis++)
        {
            float value = scale[axis];
            // 기존 확대·복귀(.18초씩)의 OutQuad/Yoyo 곡선을 Animator로 옮긴다.
            float tangent = value * amount * 2 / .18f;
            var curve = new AnimationCurve(new Keyframe(0, value, 0, tangent),
                new Keyframe(.18f, value * (1 + amount), 0, 0), new Keyframe(.36f, value, -tangent, 0));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale." + "xyz"[axis]), curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
        var state = controller.layers[0].stateMachine.AddState(name, new Vector3(440, y));
        state.motion = clip;
        state.writeDefaultValues = false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    private static NpcSequence[] CreateSequences(string storyId, LegacyActor[] actors, float duration)
    {
        var list = new List<NpcSequence>();
        NpcActor Actor(string id) => actors.Single(value => value.Id == id).Actor;
        void Add(string id, OutsideStoryWorldAction command, params NpcActionStep[] actions) => list.Add(new NpcSequence
        { id = id, trigger = NpcSequenceTrigger.WorldAction, worldAction = command, actions = actions });
        NpcActionStep Show(NpcActor who) => new() { kind = NpcActionKind.Show, performer = who };
        NpcActionStep Hide(NpcActor who) => new() { kind = NpcActionKind.Hide, performer = who };
        NpcActionStep Move(NpcActor who, Vector2 offset = default, Transform target = null) => new()
        {
            kind = NpcActionKind.MoveTo, performer = who, offset = offset, target = target, duration = duration,
            positionOrigin = target != null ? NpcPositionOrigin.Target : NpcPositionOrigin.Initial
        };
        NpcActionStep Place(NpcActor who, Vector2 offset) => new()
        { kind = NpcActionKind.SetPosition, performer = who, positionOrigin = NpcPositionOrigin.Initial, offset = offset };
        NpcActionStep Pulse(NpcActor who, string state) => new()
        { kind = NpcActionKind.Animation, performer = who, stateName = state, returnState = "Idle", duration = .36f };

        switch (storyId)
        {
            case "neighborhood-homeCat":
            {
                var cat = Actor("boobi");
                var chris = Actor("chris");
                Add("pet", OutsideStoryWorldAction.PetCat, Show(cat), Pulse(cat, "PulsePet"));
                Add("meow", OutsideStoryWorldAction.CatMeow, Show(cat), Pulse(cat, "PulsePet"));
                Add("chris-enter", OutsideStoryWorldAction.ChrisEnter, Place(chris, Vector2.right), Show(chris), Move(chris));
                break;
            }
            case "neighborhood-reveal":
            {
                var cat = Actor("boobi");
                var neighbor = Actor("neighbor");
                Add("reveal", OutsideStoryWorldAction.RevealCat, Show(cat), Place(cat, Vector2.down * .25f), Move(cat), Pulse(cat, "PulseReveal"));
                Add("feed", OutsideStoryWorldAction.FeedCat, Show(cat), Pulse(cat, "PulseFeed"));
                Add("pet", OutsideStoryWorldAction.PetCat, Show(cat), Pulse(cat, "PulsePet"));
                Add("adopt", OutsideStoryWorldAction.AdoptCat,
                    new NpcActionStep { kind = NpcActionKind.FadeOut, performer = cat, duration = .35f });
                Add("neighbor-enter", OutsideStoryWorldAction.NeighborEnter, Show(neighbor), Move(neighbor, Vector2.right * .8f, cat.transform));
                Add("neighbor-exit", OutsideStoryWorldAction.NeighborExit, Move(neighbor), Hide(neighbor));
                break;
            }
            case "commute2":
            {
                var samho = Actor("samho");
                Add("samho-depart", OutsideStoryWorldAction.SamhoDepart, Move(samho, Vector2.right * 2), Hide(samho));
                break;
            }
            case "day3-samho-contract":
            {
                list.AddRange(DayThreeContractSetup.CreateSequences(Actor("samho"), Actor("dealer")));
                break;
            }
            case "day2-corridor-thug":
            case "day2-thug-information":
            case "commute3":
            case "outside_day1_samho_smuggler":
            case "shiba-shop":
            case "shiba-shop-revisit":
            case "neighborhood-bossIntro":
                break; // 기존 CSV가 대화·선택·상태 변경만 사용한다.
            default:
                throw new InvalidOperationException("이 사건의 기존 배우 명령을 먼저 확인하세요: " + storyId);
        }
        return list.ToArray();
    }

    private static NpcInteraction CreateInteraction(WorldInteractionTarget target, NpcActor actor, bool storyPresence, NpcSequence[] sequences)
    {
        var result = target.gameObject.AddComponent<NpcInteraction>();
        using var data = new SerializedObject(result);
        data.FindProperty("actor").objectReferenceValue = actor;
        data.FindProperty("useStoryPresence").boolValue = storyPresence;
        data.FindProperty("minimumDay").intValue = 0;
        data.FindProperty("presenceCondition").stringValue = "";
        data.FindProperty("committedCondition").stringValue = "";
        NpcSequenceEditorData.WriteList(data.FindProperty("sequences"), sequences);
        data.ApplyModifiedPropertiesWithoutUndo();
        if (NpcSequenceEditorData.RequiresStaging(sequences) && target.GetComponent<NpcSceneStaging>() == null)
            target.gameObject.AddComponent<NpcSceneStaging>();
        EditorUtility.SetDirty(result);
        return result;
    }
}
