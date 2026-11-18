using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>표시 구조 정리 후 저장한 씬을 읽기 전용 미리보기로 검사한다. 임시 셋업 도구 삭제 후에도 유지된다.</summary>
public sealed class OutsideResidentsPlacementTests
{
    Scene scene;
    WorldInteractionContent residents;
    Transform streetChild;
    const string ChildSourceId = "outside_day0_street_children";
    readonly List<UnityEngine.Object> owned = new();
    int previousDay;
    EGameFlow previousPhase;
    object previousOptions;

    [SetUp]
    public void SetUp()
    {
        previousDay = GameStateManager.Instance.CurrentDay;
        previousPhase = GameStateManager.Instance.GameFlow;
        var optionsField = typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic);
        previousOptions = optionsField.GetValue(null);
        optionsField.SetValue(null, null);
        scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/OutSide.unity");
        residents = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionContent>(true))
            .SingleOrDefault(content => content.SourceId == "outside_day0_building_residents");
        Assert.That(residents, Is.Not.Null, "[OutsideResidentsTests] 주민 배치·표시 구조 정리 후 씬을 저장하고 실행하세요.");
        streetChild = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .SingleOrDefault(t => t.name == "StreetChildren");
        Assert.That(streetChild, Is.Not.Null, "[OutsideResidentsTests] 씬에 StreetChildren 주변 대화 그룹이 저장되어 있어야 합니다.");
    }

    [TearDown]
    public void TearDown()
    {
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        owned.Clear();
        GameStateManager.Instance.CurrentDay = previousDay;
        GameStateManager.Instance.GameFlow = previousPhase;
        typeof(UIDisplayOptions).GetField("activeOptions", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousOptions);
    }

    [Test]
    public void SavedGroupUsesRadiusAndBothExistingSpeakersWithoutScreenDependencies()
    {
        Assert.That(residents.name, Is.EqualTo("UpperCorridorResidents"));
        var target = residents.GetComponent<WorldInteractionTarget>();
        Assert.That(target, Is.Not.Null);
        Assert.That(target.Content, Is.SameAs(residents));
        Assert.That(target.IsAmbient, Is.True);
        Assert.That(residents.GetComponent<WorldAmbientDialogueDistanceGate>(), Is.Not.Null);
        Assert.That(residents.GetComponentsInChildren<NpcInteraction>(true), Is.Empty);
        var speakers = residents.GetComponent<WorldDialogueSpeakers>();
        Assert.That(speakers.Resolve("resident-left"), Is.Not.Null);
        Assert.That(speakers.Resolve("resident-right"), Is.Not.Null);
        Assert.That(speakers.Resolve("resident-left"), Is.Not.SameAs(speakers.Resolve("resident-right")));
        Assert.That(target.InteractionRadius, Is.GreaterThan(0f));
        Assert.That(residents.GetComponents<MonoBehaviour>().Any(c => c == null), Is.False);
        Assert.That(residents.GetComponentsInChildren<Transform>(true)
            .Any(t => t.name is "ViewTop" or "RailingTop"), Is.False);
        // 저장된 실제 CSV 연결에서 시작까지 확인: 근처에서 화면 기준점 없이 시작해야 한다.
        var context = MakeContext(out _);
        var player = context.gameObject.AddComponent<Player>();
        var runner = context.gameObject.AddComponent<DialogueRunner>();
        var foreground = context.GetComponent<WorldInteractionController>();
        var ambient = context.gameObject.AddComponent<WorldAmbientDialogueController>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(WorldInteractionSceneContext).GetField("<IsReady>k__BackingField", flags).SetValue(context, true);
        typeof(WorldInteractionSceneContext).GetField("player", flags).SetValue(context, player);
        typeof(WorldInteractionSceneContext).GetField("runner", flags).SetValue(context, runner);
        typeof(WorldInteractionController).GetField("ready", flags).SetValue(foreground, true);
        typeof(WorldAmbientDialogueController).GetField("context", flags).SetValue(ambient, context);
        typeof(WorldAmbientDialogueController).GetField("foreground", flags).SetValue(ambient, foreground);
        var stories = (Dictionary<string, NewSceneData>)typeof(WorldInteractionSceneContext).GetField("sceneStories", flags).GetValue(context);
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        foreach (var row in StoryCsvCompiler.CompileStreet(catalog).Scenes) stories.Add(row.Id, row);
        residents.Initialize(context);
        Refresh(residents, 0, EGameFlow.CommuteOut);
        Assert.That(residents.TryResolve(out var story), Is.True);
        target.ApplyStartSettings(ref story);
        Assert.That(story.Steps.Length, Is.EqualTo(4));
        Assert.DoesNotThrow(() => WorldAmbientDialogueController.ValidateScript(target, story, player.transform));
        player.transform.position = target.transform.position + Vector3.right * (target.InteractionRadius + .1f);
        Assert.That(ambient.CanStartTarget(target, story), Is.False);
        player.transform.position = target.transform.position + Vector3.right * (target.InteractionRadius * .5f);
        Assert.That(ambient.CanStartTarget(target, story), Is.True);
    }

    [Test]
    public void CorridorResidentsAndIndependentStreetChildUseFlatActorsWithoutMasks()
    {
        var luna = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Player>(true)).Single()
            .GetComponent<SpriteRenderer>();
        Assert.That(luna, Is.Not.Null);
        Assert.That(residents.GetComponentsInChildren<SortingGroup>(true), Is.Empty);
        Assert.That(residents.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
        Assert.That(residents.GetComponentsInChildren<Transform>(true).Any(t => t.name is "ResidentsVisualGroup" or "AboveRailMask"), Is.False);
        var sprites = residents.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(sprites.Length, Is.EqualTo(2));
        Assert.That(streetChild.parent, Is.SameAs(residents.transform.parent));
        Assert.That(streetChild.parent.name, Is.EqualTo("AmbientConversations"));
        Assert.That(streetChild.IsChildOf(residents.transform), Is.False);
        Assert.That(streetChild.GetComponentsInChildren<SortingGroup>(true), Is.Empty);
        Assert.That(streetChild.GetComponentsInChildren<SpriteMask>(true), Is.Empty);
        var actors = new[] { "LeftActor", "RightActor" }
            .Select(name => residents.transform.Cast<Transform>().Single(t => t.name == name)).Append(streetChild);
        foreach (var actor in actors)
        {
            var sprite = actor.GetComponentsInChildren<SpriteRenderer>(true).Single();
            Assert.That(sprite.transform.parent, Is.SameAs(actor), "발 보정은 Actor 아래 Sprite의 위치만 바꿉니다.");
            int layer = SortingLayer.GetLayerValueFromID(sprite.sortingLayerID);
            int lunaLayer = SortingLayer.GetLayerValueFromID(luna.sortingLayerID);
            Assert.That(layer < lunaLayer || layer == lunaLayer && sprite.sortingOrder < luna.sortingOrder, Is.True,
                "SpriteRenderer의 Layer·Order로 루나보다 뒤에 표시해야 합니다.");
            Assert.That(sprite.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None));
        }
        Assert.That(luna.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None));
    }

    [Test]
    public void StreetChildrenHaveExistingAmbientComponentsTwoAnchorsAndNineFrameTwelveFpsIdle()
    {
        var content = streetChild.GetComponent<WorldInteractionContent>();
        var target = streetChild.GetComponent<WorldInteractionTarget>();
        Assert.That(content.SourceId, Is.EqualTo(ChildSourceId));
        Assert.That(target.Content, Is.SameAs(content));
        Assert.That(target.IsAmbient, Is.True);
        Assert.That(target.InteractionRadius, Is.EqualTo(1.2f));
        Assert.That(target.Output, Is.EqualTo(WorldInteractionOutput.SpeechBubble));
        Assert.That(new SerializedObject(content).FindProperty("useSpotPosition").boolValue, Is.False);
        Assert.That(new SerializedObject(content).FindProperty("controlObjectVisibility").boolValue, Is.True);
        Assert.That(streetChild.GetComponent<WorldAmbientDialogueDistanceGate>(), Is.Not.Null);
        Assert.That(streetChild.GetComponents<MonoBehaviour>().Any(c => c == null || c.GetType().Name == "WorldDayPhaseVisibility"), Is.False);
        var speakers = streetChild.GetComponent<WorldDialogueSpeakers>();
        var left = speakers.Resolve("child-left");
        var right = speakers.Resolve("child-right");
        Assert.That(left.parent, Is.SameAs(streetChild));
        Assert.That(right.parent, Is.SameAs(streetChild));
        Assert.That(left.position.x, Is.LessThan(right.position.x));
        Assert.That(target.SpeechAnchor, Is.SameAs(left));
        Assert.That(streetChild.GetComponentsInChildren<NpcInteraction>(true), Is.Empty);
        var animator = streetChild.GetComponentsInChildren<Animator>(true).Single();
        Assert.That(animator.transform.parent, Is.SameAs(streetChild));
        var clip = animator.runtimeAnimatorController.animationClips.Single();
        Assert.That(clip.name, Is.EqualTo("npc_children_idle"));
        Assert.That(clip.frameRate, Is.EqualTo(12f));
        Assert.That(clip.length, Is.EqualTo(.75f).Within(.001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b => b.propertyName == "m_Sprite");
        var frames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(frames.Length, Is.EqualTo(9));
        Assert.That(frames.Select(f => f.value.name), Is.EqualTo(Enumerable.Range(0, 9).Select(i => "children_idle_" + i)));
    }

    [TestCase(0, EGameFlow.CommuteIn, false)]
    [TestCase(0, EGameFlow.CommuteOut, true)]
    [TestCase(1, EGameFlow.CommuteIn, false)]
    [TestCase(1, EGameFlow.CommuteOut, false)]
    [TestCase(2, EGameFlow.CommuteIn, false)]
    [TestCase(2, EGameFlow.CommuteOut, false)]
    [TestCase(3, EGameFlow.CommuteIn, false)]
    [TestCase(3, EGameFlow.CommuteOut, false)]
    public void StreetChildKeepsDayZeroReturnTripVisibilityIndependentOfCorridorGroup(int day, EGameFlow phase, bool expected)
    {
        var context = MakeContext(out _);
        var content = streetChild.GetComponent<WorldInteractionContent>();
        content.Initialize(context);
        var sprite = streetChild.GetComponentsInChildren<SpriteRenderer>(true).Single();
        var position = sprite.transform.position;
        residents.gameObject.SetActive(false);
        Refresh(content, day, phase);
        Assert.That(sprite.gameObject.activeInHierarchy, Is.EqualTo(expected));
        Assert.That(content.IsAvailable, Is.False, "대본이 없으면 표시만 하고 빈 대화를 시작하지 않습니다.");
        Assert.That(content.TryResolve(out _), Is.False);
        var controller = context.GetComponent<WorldInteractionController>();
        Assert.That(controller.TryResolve(streetChild.GetComponent<WorldInteractionTarget>(), out _), Is.False);
        Refresh(content, 0, EGameFlow.CommuteOut);
        Assert.That(sprite.gameObject.activeInHierarchy, Is.True, "숨겨진 뒤에도 등장 조건을 만족하면 다시 표시합니다.");
        Refresh(content, 1, EGameFlow.CommuteIn);
        Assert.That(streetChild.gameObject.activeSelf, Is.False);
        Assert.That(sprite.transform.position, Is.EqualTo(position));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ChildrenCsvRowIsValidWithoutInventedDialogueOrFlow()
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"^\[CSV\] \d+개 데이터 묶음 구조와 로더 타입 \d+개를 검증했습니다\.$"));
        CsvContentValidator.ValidateCatalog(catalog);
        var row = catalog.Read<NewInteractPointData[]>("interact_points_outside").Single(r => r.SourceId == ChildSourceId);
        Assert.That(row.Id, Is.EqualTo("p_" + ChildSourceId));
        Assert.That(row.ActionType, Is.EqualTo(EActionType.Dialogue));
        Assert.That(row.ActivationMode, Is.EqualTo(EActivationMode.Proximity));
        Assert.That(row.DialogueFlows ?? Array.Empty<NewInteractDialogueFlowData>(), Is.Empty);
        Assert.That(StoryCsvCompiler.CompileStreet(catalog).Scenes.Any(s => s.Id == ChildSourceId), Is.False);
    }

    [Test]
    public void AddingFutureDialogueFlowMakesPreparedGroupAvailableWithoutChangingSceneComponents()
    {
        var context = MakeContext(out var points);
        int index = Array.FindIndex(points.interactPointData, r => r.SourceId == ChildSourceId);
        var row = points.interactPointData[index];
        row.DialogueFlows = new[] { new NewInteractDialogueFlowData { Day = 0, SceneId = ChildSourceId, PlayType = EPlayType.Once } };
        points.interactPointData[index] = row;
        var stories = (Dictionary<string, NewSceneData>)typeof(WorldInteractionSceneContext)
            .GetField("sceneStories", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
        stories.Add(ChildSourceId, new NewSceneData { Id = ChildSourceId });
        var content = streetChild.GetComponent<WorldInteractionContent>();
        content.Initialize(context);
        Refresh(content, 0, EGameFlow.CommuteOut);
        Assert.That(content.IsAvailable, Is.True);
        Assert.That(content.TryResolve(out var story), Is.True);
        Assert.That(story.Id, Is.EqualTo(ChildSourceId));
        // 실제 CSV 파일을 바꾸지 않고 기존 once 재생 완료 경로도 확인한다.
        content.CompleteDialogue(ChildSourceId, new StoryExecutionResult(StoryExecutionStatus.Completed));
        Assert.That(content.IsAvailable, Is.False);
        Assert.That(streetChild.gameObject.activeSelf, Is.True);
    }

    WorldInteractionSceneContext MakeContext(out NewInteractPointDataSO points)
    {
        var go = new GameObject("ChildrenTestContext");
        SceneManager.MoveGameObjectToScene(go, scene);
        var context = go.AddComponent<WorldInteractionSceneContext>();
        go.AddComponent<WorldInteractionController>();
        points = ScriptableObject.CreateInstance<NewInteractPointDataSO>(); owned.Add(points);
        points.interactPointData = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"))
            .Read<NewInteractPointData[]>("interact_points_outside");
        var playerData = ScriptableObject.CreateInstance<PlayerDataSO>(); owned.Add(playerData);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(WorldInteractionSceneContext).GetField("interactPointData", flags).SetValue(context, points);
        typeof(WorldInteractionSceneContext).GetField("conditions", flags)
            .SetValue(context, new ConditionUtil(GameStateManager.Instance, playerData, playerData));
        return context;
    }

    static void Refresh(WorldInteractionContent content, int day, EGameFlow phase)
    {
        GameStateManager.Instance.CurrentDay = day;
        GameStateManager.Instance.GameFlow = phase;
        content.Refresh();
    }
}
