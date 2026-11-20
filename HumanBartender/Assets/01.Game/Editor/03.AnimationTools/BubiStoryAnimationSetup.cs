using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Story actor의 하위 Sprite 경로에 맞춰 Bubi 클립과 기존 스토리 컨트롤러를 연결한다.</summary>
public static class BubiStoryAnimationSetup
{
    public const string Root = "Assets/01.Game/03.Content/07.Animations/01.Characters/04.Bubi/";
    const string StoryClips = Root + "01.Clips/06.StoryActor/";
    const string FieldController = Root + "02.Controllers/Bubi_Field.controller";
    const string LegacyRoot = "Assets/01.Game/03.Content/07.Animations/01.Characters/NpcInteraction/";
    const float MovingThreshold = .01f;

    static readonly (string source, string state)[] Clips =
    {
        ("03.Walk/Bubi_Walk", "Walk"),
        ("04.Box/Bubi_Box_Idle", "BoxIdle"),
        ("04.Box/Bubi_Box_Eyes", "Eyes"),
        ("04.Box/Bubi_Box_Jump", "Jump"),
        ("04.Box/Bubi_Box_Meow", "Meow"),
        ("04.Box/Bubi_Box_Shake", "Shake"),
        ("05.Sleep/Bubi_Sleep", "Sleep"),
    };

    [MenuItem("Tools/Animation/Connect Bubi Story Actors")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("[BubiAnimation] Play 모드를 종료한 뒤 실행하세요.");
        // 저장하지 않은 씬을 자동 저장하지 않는다. 에셋 변경 전 두 씬을 모두 확인한다.
        foreach (string name in new[] { "Home", "OutSide" })
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath(name));
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("[BubiAnimation] 먼저 " + name + " 씬을 저장하세요.");
        }
        foreach (string name in new[] { "Home", "OutSide" })
        {
            var scene = SceneManager.GetSceneByPath(ScenePath(name));
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath(name), OpenSceneMode.Additive);
            try
            {
                var actors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true))
                    .Where(actor => actor.name == "Story actor · boobi").ToArray();
                if (actors.Length != 1)
                    throw new InvalidOperationException("[BubiAnimation] " + name + "의 Story actor · boobi를 확인하세요.");
                Apply(actors[0]);
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
        Debug.Log("[BubiAnimation] Home·OutSide 부비의 스토리 컨트롤러 연결을 완료했습니다.");
    }

    public static void Apply(NpcActor actor)
    {
        var animator = actor.GetComponent<Animator>();
        using var data = new SerializedObject(actor);
        var renderer = data.FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
        if (animator == null || renderer == null || animator.runtimeAnimatorController is not AnimatorController controller)
            throw new InvalidOperationException("[BubiAnimation] 부비의 Animator·SpriteRenderer 연결을 확인하세요.");
        string bindingPath = AnimationUtility.CalculateTransformPath(renderer.transform, actor.transform);
        if (bindingPath != "Sprite")
            throw new InvalidOperationException("[BubiAnimation] Story actor의 Sprite 자식 경로를 확인하세요: " + bindingPath);
        string controllerPath = AssetDatabase.GetAssetPath(controller);
        if (controllerPath != LegacyRoot + "Home_neighborhood-homeCat_boobi/Actor.controller"
            && controllerPath != LegacyRoot + "OutSide_neighborhood-reveal_boobi/Actor.controller")
            throw new InvalidOperationException("[BubiAnimation] 기존 부비 스토리 컨트롤러를 연결하세요.");

        Undo.RecordObject(controller, "Connect Bubi story animations");
        var machine = controller.layers[0].stateMachine;
        foreach (var spec in Clips)
        {
            var clip = Retarget(spec.source, bindingPath);
            ConnectState(machine, spec.state, clip, Path.GetDirectoryName(controllerPath).Replace('\\', '/'));
            if (spec.state == "Sleep")
                ConnectState(machine, "Idle", clip, Path.GetDirectoryName(controllerPath).Replace('\\', '/'));
        }
        var idle = machine.states.Single(value => value.state.name == "Idle").state;
        var walk = machine.states.Single(value => value.state.name == "Walk").state;
        if (!controller.parameters.Any(parameter => parameter.name == "Speed"))
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ConnectMovement(idle, walk, AnimatorConditionMode.Greater);
        ConnectMovement(walk, idle, AnimatorConditionMode.Less);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);

        // 이전 배치 안내에 따라 추가된 Field Animator만 정리한다. 다른 사용자 컨트롤러는 건드리지 않는다.
        var duplicate = renderer.GetComponent<Animator>();
        if (duplicate != null && duplicate != animator)
        {
            if (AssetDatabase.GetAssetPath(duplicate.runtimeAnimatorController) != FieldController)
                throw new InvalidOperationException("[BubiAnimation] Sprite의 다른 Animator가 그림을 함께 제어합니다. 연결을 확인하세요.");
            Undo.DestroyObjectImmediate(duplicate);
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        }
        var sleep = AssetDatabase.LoadAssetAtPath<AnimationClip>(StoryClips + "Bubi_Sleep_Story.anim");
        var first = (Sprite)AnimationUtility.GetObjectReferenceCurve(sleep,
            AnimationUtility.GetObjectReferenceCurveBindings(sleep).Single())[0].value;
        if (renderer.sprite == null || AssetDatabase.GetAssetPath(renderer.sprite) == LegacyRoot + "WebCat/WebCatFrames.png")
        {
            Undo.RecordObject(renderer, "Connect Bubi resting sprite");
            renderer.sprite = first;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        }
    }

    static AnimationClip Retarget(string relative, string bindingPath)
    {
        string name = Path.GetFileName(relative) + "_Story";
        string path = StoryClips + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "01.Clips/" + relative + ".anim");
        if (source == null) throw new InvalidOperationException("[BubiAnimation] 원본 클립이 없습니다: " + relative);
        Folder(StoryClips.TrimEnd('/'));
        var clip = UnityEngine.Object.Instantiate(source);
        clip.name = name;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            if (keys.Any(key => key.value == null))
                throw new InvalidOperationException("[BubiAnimation] 원본 클립의 스프라이트 참조를 확인하세요: " + relative);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            var target = binding;
            target.path = bindingPath;
            AnimationUtility.SetObjectReferenceCurve(clip, target, keys);
        }
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    static void ConnectState(AnimatorStateMachine machine, string name, AnimationClip clip, string legacyFolder)
    {
        var state = machine.states.Select(value => value.state).FirstOrDefault(value => value.name == name);
        if (state == null)
        {
            Undo.RecordObject(machine, "Add Bubi animation state");
            state = machine.AddState(name, new Vector3(550, 80 + machine.states.Length * 70));
            state.writeDefaultValues = false;
        }
        string previous = AssetDatabase.GetAssetPath(state.motion);
        // 수동 교체된 다른 클립은 보존한다. 기존 자동 생성 Idle·Meow만 새 원본으로 교체한다.
        if (state.motion == null || previous == legacyFolder + "/" + name + ".anim")
        {
            Undo.RecordObject(state, "Connect Bubi animation clip");
            state.motion = clip;
            EditorUtility.SetDirty(state);
        }
    }

    static void ConnectMovement(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
    {
        if (from.transitions.Any(transition => transition.destinationState == to)) return;
        Undo.RecordObject(from, "Connect Bubi walking state");
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0;
        transition.AddCondition(mode, MovingThreshold, "Speed");
        EditorUtility.SetDirty(from);
    }

    static string ScenePath(string name) => "Assets/01.Game/01.Scenes/" + name + ".unity";

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
