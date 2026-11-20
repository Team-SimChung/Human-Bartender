using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class HomeObjectInteractionTests
{
    private const string CoverPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/06.Ect/ImageViewer/nietzsche-the-gay-science.png";

    [TestCase("home_bed", "home-bed")]
    [TestCase("home_wardrobe", "home-wardrobe")]
    [TestCase("home_book", "home-book")]
    public void RoomInspectionsRemainAvailableEveryDayAndCommutePhase(string sourceId, string storyId)
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var definitions = catalog.Read<NewInteractPointData[]>("interact_points_home");
        var stories = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>("script/home")).Scenes;
        var state = GameStateManager.Instance;
        int previousDay = state.CurrentDay;
        var previousFlow = state.GameFlow;
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(state, player, player);
            for (int day = 0; day <= 3; day++)
                foreach (var phase in new[] { EGameFlow.CommuteIn, EGameFlow.CommuteOut })
                {
                    state.CurrentDay = day;
                    state.GameFlow = phase;
                    var definition = WorldInteractionContentRules.SelectDefinition(definitions, sourceId, phase, conditions);
                    Assert.That(definition.HasValue, Is.True, sourceId + ": " + day + "/" + phase);
                    Assert.That(conditions.CheckRequired(definition.Value.InteractWhen), Is.True);
                    var flow = WorldInteractionContentRules.SelectDialogue(definition.Value.DialogueFlows, day, conditions,
                        new HashSet<string>(StringComparer.Ordinal) { storyId });
                    Assert.That(flow.HasValue, Is.True, "Finishing an inspection must not consume it.");
                    Assert.That(flow.Value.SceneId, Is.EqualTo(storyId));
                    Assert.That(flow.Value.PlayType, Is.EqualTo(EPlayType.Repeat));
                    Assert.That(stories.Single(value => value.Id == storyId).Steps, Is.Not.Empty);
                }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            state.CurrentDay = previousDay;
            state.GameFlow = previousFlow;
        }
    }

    [Test]
    public void ExistingLinesArePreservedAndWardrobeSpeaksBeforeChoosing()
    {
        var content = new CsvStoryTestContent();
        Assert.That(content.GetSceneSteps("home-bed").Single().Text.Ko,
            Is.EqualTo("크리스가 사용하는 침대. 푹신해 보인다."));
        Assert.That(content.GetSceneSteps("home-wardrobe-pressed").Single().Text.Ko,
            Is.EqualTo("아직 애니메이션이 없는 걸..."));
        Assert.That(content.GetSceneSteps("home-book").Single().Text.Ko,
            Is.EqualTo("즐거운 학문 - 프리드리히 니체"));
        var wardrobe = content.GetSceneSteps("home-wardrobe").OrderBy(value => value.Seq).ToArray();
        Assert.That(wardrobe.Length, Is.EqualTo(2));
        Assert.That(wardrobe[0].Type, Is.EqualTo("say"));
        Assert.That(wardrobe[0].Actor, Is.EqualTo("luna"));
        Assert.That(wardrobe[0].Text.Ko, Is.EqualTo("기능을 알 수 없는 버튼이 보인다."));
        Assert.That(wardrobe[1].Command, Is.EqualTo(OutsideStoryCommand.WardrobeChoice));
    }

    [TestCase("press", 1)]
    [TestCase("cancel", 0)]
    public void WardrobeUsesExistingPressAndCancelBranches(string selectedId, int expectedLines)
    {
        var content = new CsvStoryTestContent();
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var state = GameStateManager.Instance;
            var conditions = new ConditionUtil(state, player, player).CreateExecutionScope();
            var run = new OutsideStoryExecution(conditions, (options, token) =>
            {
                CollectionAssert.AreEqual(new[] { "누른다", "취소" }, options.Select(value => value.Text.Ko));
                return UniTask.FromResult(options.Single(value => value.Id == selectedId));
            }, null);
            var result = new StreetStoryEvents(content, state)
                .ExecuteAsync(OutsideStoryCommand.WardrobeChoice, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.Length, Is.EqualTo(expectedLines));
            if (selectedId == "press") Assert.That(result[0].Text.Ko, Is.EqualTo("아직 애니메이션이 없는 걸..."));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    // 소파의 고정 메뉴는 TV처럼 CSV 메뉴 장면에서 읽는다. 코드가 처리하는 선택지 ID가 CSV에 그대로 있어야 한다.
    [Test]
    public void SofaMenusComeFromCsvWithTheIdsTheActionHandles()
    {
        var host = new GameObject("SofaMenuTest");
        try
        {
            var sofa = host.AddComponent<HomeSofaAction>();
            using var data = new SerializedObject(sofa);
            var content = new CsvStoryTestContent();
            var conditions = new ConditionUtil(GameStateManager.Instance, null, null);
            NewStreetOptionData[] Menu(string property) => WorldInteractionController.PrepareOutsideChoices(content, conditions,
                data.FindProperty(property).stringValue, null);

            var main = Menu("menuSceneId");
            CollectionAssert.AreEqual(new[] { "sleep", "save", "cancel" }, main.Select(value => value.Id));
            CollectionAssert.AreEqual(new[] { "수면", "저장", "취소" }, main.Select(value => value.Text.Ko));
            var overwrite = Menu("overwriteSceneId");
            CollectionAssert.AreEqual(new[] { "overwrite", "back" }, overwrite.Select(value => value.Id));
            CollectionAssert.AreEqual(new[] { "ok" }, Menu("messageSceneId").Select(value => value.Id));
            Assert.That(main.Concat(overwrite).Where(value => value.IsCancel == true).Select(value => value.Id),
                Is.EqualTo(new[] { "cancel", "back" }), "Esc·패드 B 취소는 cancel·back에만 걸립니다.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void NewGameOpeningSharesTheSofaWakeUpAnimation()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var opening = roots.SelectMany(root => root.GetComponentsInChildren<NewGameOpening>(true)).Single();
            var sofa = roots.SelectMany(root => root.GetComponentsInChildren<HomeSofaAction>(true)).Single();
            using var openingData = new SerializedObject(opening);
            using var sofaData = new SerializedObject(sofa);
            Assert.That(openingData.FindProperty("sofaAction").objectReferenceValue, Is.SameAs(sofa));

            var sleepData = sofaData.FindProperty("sleepAnimation");
            var animator = (Animator)sleepData.FindPropertyRelative("sleepAnimator").objectReferenceValue;
            var wakeUp = (AnimationClip)sleepData.FindPropertyRelative("wakeUpClip").objectReferenceValue;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.gameObject.name, Is.EqualTo("LunaSleepAnimation"));
            Assert.That(wakeUp, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips, Does.Contain(wakeUp));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void HomeArrivalAndSofaWakeUpSetLunasFacing()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var player = roots.SelectMany(root => root.GetComponentsInChildren<Player>(true)).Single();
            var sofa = roots.SelectMany(root => root.GetComponentsInChildren<HomeSofaAction>(true)).Single();
            var movement = player.GetComponent<PlayerMovement2D>();
            var sprite = player.GetComponent<SpriteRenderer>();

            movement.Face(false);
            Assert.That(player.TrySetPosition(EPlayerSpawn.Home), Is.True);
            Assert.That(movement.IsFacingRight && sprite.flipX, Is.True);

            movement.Face(false);
            sprite.flipX = true; // 스프라이트 반전과 이동 방향이 서로 다른 상태에서도 연출이 바로잡아야 한다.
            var sleep = sofa.SleepAnimation;
            sleep.BeginSleep();
            sleep.Restore(wakingUp: true);
            Assert.That(movement.IsFacingRight, Is.False);
            Assert.That(sprite.flipX, Is.False);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void OpeningCallUsesAuthoredHologramsAndBothCsvResponses()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var opening = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NewGameOpening>(true)).Single();
            using var serialized = new SerializedObject(opening);
            var target = (WorldInteractionTarget)serialized.FindProperty("firstContact").objectReferenceValue;
            Assert.That(target, Is.Not.Null);
            Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.ScriptOnly));
            Assert.That(target.Output, Is.EqualTo(WorldInteractionOutput.SpeechBubble));
            Assert.That(target.SpeechAnchor.parent, Is.SameAs(target.transform));
            foreach (var field in new[] { "hologramOpen", "hologramClose" })
            {
                var effect = (ParticleSystem)serialized.FindProperty(field).objectReferenceValue;
                Assert.That(effect, Is.Not.Null);
                Assert.That(effect.transform.parent, Is.SameAs(target.transform));
                Assert.That(effect.gameObject.activeSelf, Is.False);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(effect), Is.True);
            }

            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
            var call = StoryCsvCompiler.CompileHome(catalog).Scenes.Single(value => value.Id == target.StorySceneId);
            Assert.That(call.RuntimeScene, Is.EqualTo("Home"));
            Assert.That(call.Route, Is.EqualTo("in"));
            Assert.That(call.Day, Is.EqualTo(0));
            Assert.That(call.Steps.Select(step => step.Type), Is.EqualTo(new[] { "say", "choice" }));
            Assert.That(call.Steps[0].Actor, Is.EqualTo("chris"));
            var branches = call.Steps[1].Options;
            Assert.That(branches.Select(option => option.Id), Is.EqualTo(new[] { "awake", "silent" }));
            Assert.That(branches.Select(option => option.ResultSteps.Length), Is.EqualTo(new[] { 4, 7 }));
            foreach (var option in branches)
            {
                Assert.That(option.ResultSteps[0].Actor, Is.EqualTo("luna"));
                Assert.That(option.ResultSteps[0].Text.Ko, Is.EqualTo(option.Text.Ko));
                Assert.That(option.ResultSteps.Last().Actor, Is.EqualTo("chris"));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void AuthoredHomeObjectsUseManualInteractionAndTheActualBookCover()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var targets = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            foreach (string sourceId in new[] { "home_bed", "home_wardrobe", "home_book" })
            {
                var target = targets.Single(value => value.Content != null && value.Content.SourceId == sourceId);
                Assert.That(target.transform.parent.name, Is.EqualTo("InteractionObjects"));
                Assert.That(target.StartMode, Is.EqualTo(WorldInteractionStartMode.Interact));
                Assert.That(target.InteractionRadius, Is.GreaterThan(0));
                using var data = new SerializedObject(target.Content);
                Assert.That(data.FindProperty("useSpotPosition").boolValue, Is.False, "Scene placement must remain authored.");
                Assert.That(data.FindProperty("controlObjectVisibility").boolValue, Is.False);
                Assert.That(target.GetComponent<OutlineHighlight>(), Is.Not.Null);
                Assert.That(target.GetComponent<SpriteRenderer>().sharedMaterial.HasProperty("_OutlineIntensity"), Is.True);
                Assert.That(target.Output, Is.EqualTo(sourceId == "home_book" ? WorldInteractionOutput.Image : WorldInteractionOutput.SpeechBubble));
                if (sourceId == "home_book")
                {
                    Assert.That(AssetDatabase.GetAssetPath(target.InspectionImage), Is.EqualTo(CoverPath));
                    Assert.That(target.InspectionImage, Is.Not.SameAs(target.GetComponent<SpriteRenderer>().sprite));
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void SharedInspectionPrefabFitsPortraitAndLandscapeWithoutStretching()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01.Game/03.Content/08.Prefabs/03.UI/03.Outside/ImageViewer.prefab");
        var image = prefab.GetComponent<WorldInteractionImageView>().InspectionImage;
        Assert.That(image.preserveAspect, Is.True);
        Assert.That(image.raycastTarget, Is.False);
        Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(image.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
        Assert.That(image.rectTransform.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
        // The shared Home/Outside canvas is 480 x 270. Keep room below for the description and exit hint.
        var imageBounds = image.rectTransform.rect;
        Assert.That(imageBounds.width, Is.GreaterThanOrEqualTo(196).And.LessThanOrEqualTo(432));
        Assert.That(imageBounds.height, Is.GreaterThanOrEqualTo(180));
        Assert.That(image.rectTransform.anchoredPosition.y + imageBounds.yMax, Is.LessThanOrEqualTo(125));
        Assert.That(image.rectTransform.anchoredPosition.y + imageBounds.yMin, Is.GreaterThanOrEqualTo(-75));
    }
}
