using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OutsideResidentsTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string AnimFolder = "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/";
    Scene scene;
    WorldAmbientDialogueController ambient;
    WorldInteractionController foreground;
    WorldInteractionSceneContext context;
    WorldInteractionTarget target;
    Player player;
    OutsideElevator elevator;
    Camera camera;
    Transform leftHead;
    readonly List<CancellationTokenSource> sources = new();
    readonly List<Object> owned = new();
    object previousOptions;

    static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, Private).SetValue(instance, value);

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        previousOptions = typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        var system = Create("System");
        foreground = system.AddComponent<WorldInteractionController>();
        context = system.AddComponent<WorldInteractionSceneContext>();
        ambient = system.AddComponent<WorldAmbientDialogueController>();
        var runner = system.AddComponent<DialogueRunner>();
        player = Create("Player").AddComponent<Player>();
        player.transform.position = new Vector3(-16.4f, 9.6f);
        Set(context, "<IsReady>k__BackingField", true);
        Set(context, "player", player); Set(context, "runner", runner);
        Set(foreground, "ready", true);
        Set(ambient, "context", context); Set(ambient, "foreground", foreground);
        target = Create("Residents").AddComponent<WorldInteractionTarget>();
        target.transform.position = new Vector3(-18.475f, 9.346f);
        target.ConfigureStart(WorldInteractionStartMode.Ambient, 1.2f);
        target.gameObject.AddComponent<WorldAmbientDialogueDistanceGate>();
        camera = Create("OutputCamera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 2f; camera.aspect = 16f / 9f;
        camera.transform.position = new Vector3(-18.475f, 10f, -10f);
        leftHead = Create("LeftHead").transform; leftHead.position = new Vector3(-18.8f, 9.956f);
        elevator = Create("Elevator").AddComponent<OutsideElevator>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var source in sources) source.Cancel();
        foreach (var candidate in WorldInteractionTarget.InScene(scene).ToArray())
            typeof(WorldInteractionTarget).GetMethod("OnDestroy", Private).Invoke(candidate, null);
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        foreach (var item in owned) if (item != null) Object.DestroyImmediate(item);
        foreach (var source in sources) source.Dispose();
        sources.Clear(); owned.Clear();
        typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousOptions);
    }

    GameObject Create(string name)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    CancellationTokenSource Source()
    {
        var source = new CancellationTokenSource(); sources.Add(source); return source;
    }

    void BeginRide(bool fast = false)
    {
        var liftTarget = elevator.gameObject.GetComponent<WorldInteractionTarget>() ?? elevator.gameObject.AddComponent<WorldInteractionTarget>();
        Set(liftTarget, "action", elevator);
        Set(foreground, "activeTarget", liftTarget);
        Set(foreground, "session", Source());
        Set(elevator, "movement", Source()); Set(elevator, "fastTravelling", fast);
        // 일반 탑승과 빠른 이동 모두 InteractionStateLease.Acquire(player)로 이동 입력을 잠근다.
        player.State = EInteractorState.Interct;
        player.transform.position = new Vector3(-12.705f, 5f);
    }

    NewSceneData Story(float radius = 1.2f) => new() { Id = "outside_day0_building_residents", Radius = radius };

    [TestCase(-1.19f, true)]
    [TestCase(1.19f, true)]
    [TestCase(-1.21f, false)]
    [TestCase(1.21f, false)]
    public void ResidentsStartWithinAuthoredRadiusFromEitherSide(float x, bool expected)
    {
        player.transform.position = target.transform.position + Vector3.right * x;
        Assert.That(ambient.CanStartTarget(target, Story()), Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NearbyResidentsDoNotRequireCameraOrScreenAnchors(bool disabledCamera)
    {
        player.transform.position = target.transform.position;
        camera.transform.position = new Vector3(100f, -100f, 10f);
        camera.enabled = !disabledCamera;
        Assert.That(ambient.CanStartTarget(target, Story()), Is.True);
    }

    [Test]
    public void GroundFloorBelowResidentsDoesNotEnterTwoDimensionalRadius()
    {
        player.transform.position = new Vector3(target.transform.position.x, -.7f);
        Assert.That(ambient.CanStartTarget(target, Story()), Is.False);
    }

    [Test]
    public void TargetRadiusControlsStartWithoutUpperFloorThreshold()
    {
        // 배치를 바꾸더라도 고정 Y=9 조건 없이 다른 그룹과 같은 XY 거리 규칙을 쓴다.
        target.transform.position = new Vector3(-18f, 2f);
        player.transform.position = target.transform.position + Vector3.right * 1.5f;
        var story = Story();
        target.ApplyStartSettings(ref story);
        Assert.That(ambient.CanStartTarget(target, story), Is.False);
        target.ConfigureStart(WorldInteractionStartMode.Ambient, 2f);
        target.ApplyStartSettings(ref story);
        Assert.That(ambient.CanStartTarget(target, story), Is.True);
    }

    [UnityTest]
    public IEnumerator NormalRideBlocksStartAndExistingPlaybackResumesAfterExitAndApproach()
    {
        BeginRide();
        player.transform.position = target.transform.position;
        Assert.That(ambient.CanStartTarget(target, Story()), Is.False);
        Assert.That(ambient.IsPaused, Is.True);
        var view = Create("AmbientView").AddComponent<WorldAmbientDialogueView>();
        view.Begin(target, player.transform, () => ambient.IsPaused, 1.2f, 4f, 22f);
        Set(view, "speaker", leftHead);
        var task = view.WaitUntilReadyAsync(Source().Token).SuppressCancellationThrow().AsTask();
        for (int i = 0; i < 3; i++) yield return null;
        Assert.That(task.IsCompleted, Is.False);
        Set(elevator, "movement", null); Set(foreground, "session", null); Set(foreground, "activeTarget", null);
        player.State = EInteractorState.None;
        player.transform.position = new Vector3(-12.705f, 9.6f);
        Assert.That(ambient.IsPaused, Is.False);
        Assert.That(view.IsDistancePaused, Is.True, "하차했어도 가로 청취 범위를 벗어나면 진행하지 않는다.");
        for (int i = 0; i < 3; i++) yield return null;
        Assert.That(task.IsCompleted, Is.False);
        player.transform.position = new Vector3(-16.4f, 9.6f);
        for (int i = 0; i < 8 && !task.IsCompleted; i++) yield return null;
        Assert.That(task.IsCompletedSuccessfully, Is.True);
        Assert.That(task.Result, Is.False);
    }

    [TestCase("normal")]
    [TestCase("menu")]
    [TestCase("fast")]
    [TestCase("otherTarget")]
    [TestCase("handoff")]
    [TestCase("options")]
    [TestCase("testSuppressed")]
    public void ResidentsRespectNormalRideAndOtherBlockingStates(string state)
    {
        BeginRide(state == "fast");
        player.transform.position = target.transform.position; // 반경 안에서도 보호 구간이 우선한다.
        if (state == "menu") { Set(elevator, "movement", null); player.State = EInteractorState.Lock; }
        if (state == "otherTarget") Set(foreground, "activeTarget", target);
        if (state == "handoff") Set(foreground, "handingOff", true);
        if (state == "testSuppressed") foreground.SuppressDetectionForTest = true;
        if (state == "options")
        {
            var options = Create("Options").AddComponent<UIDisplayOptions>();
            Set(options, "optionUI", Create("OptionsRoot"));
            typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, options);
            Assert.That(UIDisplayOptions.IsOptionOpen, Is.True);
        }
        Assert.That(ambient.CanStartTarget(target, Story()), Is.False);
        Assert.That(ambient.IsPaused, Is.True);
    }

    [TestCase("StreetGroup")]
    [TestCase("TV")]
    [TestCase("Radio")]
    public void OtherAmbientTargetsKeepRadiusAndRidePause(string name)
    {
        var other = Create(name).AddComponent<WorldInteractionTarget>();
        other.ConfigureStart(WorldInteractionStartMode.Ambient, 1.2f);
        other.transform.position = player.transform.position + Vector3.right * 3f;
        Assert.That(ambient.CanStartTarget(other, Story()), Is.False);
        other.transform.position = player.transform.position;
        Assert.That(ambient.CanStartTarget(other, Story()), Is.True);
        BeginRide(); other.transform.position = player.transform.position;
        Assert.That(ambient.CanStartTarget(other, Story()), Is.False);
        Assert.That(ambient.IsPaused, Is.True);
    }

    [TestCase("m1")]
    [TestCase("m2")]
    public void IdleStepCurveKeepsOpaqueFeetFixedAtEveryFrameAndLoop(string kind)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "npc_" + kind + "_idle.anim");
        Assert.That(clip, Is.Not.Null);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(frames.Length, Is.EqualTo(6));
        var y = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"));
        Assert.That(y, Is.Not.Null);
        var visual = Create("SampledSprite");
        var renderer = visual.AddComponent<SpriteRenderer>();
        // 실제 NPC와 같은 Animator 연결을 준비한다. 자동 재생은 끄고 아래에서 각 시점을 직접 샘플링한다.
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            AnimFolder + "NPC_" + kind.ToUpperInvariant() + "_Idle.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController.animationClips, Does.Contain(clip));
        animator.enabled = false;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); owned.Add(texture);
        Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(AssetDatabase.GetAssetPath(frames[0].value))), Is.True);
        var pixels = texture.GetPixels32();
        foreach (var time in frames.SelectMany(f => new[] { f.time, f.time + .02f }).Concat(new[] { clip.length - .001f, 0f }))
        {
            clip.SampleAnimation(visual, time);
            var sprite = renderer.sprite;
            Assert.That(sprite, Is.Not.Null, kind + " time=" + time);
            int bottom = OpaqueBottom(pixels, texture.width, sprite.rect);
            float worldFoot = visual.transform.localPosition.y + (bottom - sprite.pivot.y) / sprite.pixelsPerUnit;
            Assert.That(worldFoot, Is.EqualTo(0f).Within(.00001f), kind + " time=" + time);
        }
        for (int i = 0; i < y.length; i++)
        {
            Assert.That(AnimationUtility.GetKeyLeftTangentMode(y, i), Is.EqualTo(AnimationUtility.TangentMode.Constant));
            Assert.That(AnimationUtility.GetKeyRightTangentMode(y, i), Is.EqualTo(AnimationUtility.TangentMode.Constant));
        }
    }

    static int OpaqueBottom(Color32[] pixels, int width, Rect rect)
    {
        for (int y = 0; y < (int)rect.height; y++)
            for (int x = 0; x < (int)rect.width; x++)
                if (pixels[((int)rect.y + y) * width + (int)rect.x + x].a != 0) return y;
        Assert.Fail("[OutsideResidentsTests] 빈 Sprite입니다.");
        return -1;
    }
}
