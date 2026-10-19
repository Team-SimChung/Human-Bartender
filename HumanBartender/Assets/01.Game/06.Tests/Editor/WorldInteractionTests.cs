using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed class WorldInteractionTests
{
    const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    const string PrefabFolder = "Assets/01.Game/03.Content/08.Prefabs/03.UI/03.Outside/";

    [Test]
    public void ChoiceItemUsesAuthoredTmpLabelOnOneLine()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabFolder + "InteractionChoiceItem.prefab");
        try
        {
            root.AddComponent<Canvas>(); // 분리된 프리팹 씬에서 TMP 메시를 생성하기 위한 Canvas를 추가함.
            var item = root.GetComponent<WorldInteractionChoiceItem>();
            Assert.IsTrue(item.IsConfigured, item.ConfigurationError);
            Assert.AreSame(root.GetComponentInChildren<TMPro.TextMeshProUGUI>(true), item.Label);
            var font = item.Label.font;
            Assert.IsFalse(item.Label.fontSharedMaterial.IsKeywordEnabled("GLOW_ON"), "Choice text must remain readable on the cyan selection fill");
            float fontSize = item.Label.fontSize;
            float height = item.Rect.rect.height;
            item.Bind("선택지\r\nTMP<br>연결\u2028확인", true, () => { }, () => { });
            Assert.AreEqual("선택지 TMP 연결 확인", item.Label.text);
            Assert.AreEqual(TMPro.TextWrappingModes.NoWrap, item.Label.textWrappingMode);
            Assert.AreEqual(TMPro.TextOverflowModes.Ellipsis, item.Label.overflowMode);
            item.Label.ForceMeshUpdate(true, true);
            Assert.AreEqual(1, item.Label.textInfo.lineCount);
            Assert.AreEqual(height, item.Rect.rect.height, "Single-line text never changes authored row height");
            Assert.AreSame(font, item.Label.font, "Preserve the authored TMP font");
            Assert.AreEqual(fontSize, item.Label.fontSize, "Preserve the authored TMP size");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void LockedChoiceReasonHasItsOwnLayoutAndClearsOnReuse()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabFolder + "InteractionChoiceItem.prefab");
        try
        {
            root.AddComponent<Canvas>();
            var item = root.GetComponent<WorldInteractionChoiceItem>();
            int callbacks = 0;
            item.Bind("500원을 준다.", false, () => callbacks++, () => { }, "선택할 수 없습니다. (소지금 부족)");
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(item.Rect);
            Assert.AreEqual("500원을 준다.", item.Label.text);
            Assert.IsTrue(item.LockReasonLabel.gameObject.activeSelf);
            Assert.Less(item.LockReasonLabel.fontSize, item.Label.fontSize);
            Assert.That(item.Rect.rect.height, Is.EqualTo(32).Within(.01f));
            Assert.Less(item.LockReasonLabel.rectTransform.position.y, item.Label.rectTransform.position.y);
            item.Button.onClick.Invoke();
            Assert.AreEqual(0, callbacks);
            item.Bind("500원을 준다.", true, () => callbacks++, () => { }, "This must be hidden");
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(item.Rect);
            Assert.IsFalse(item.LockReasonLabel.gameObject.activeSelf);
            Assert.AreEqual("", item.LockReasonLabel.text);
            Assert.That(item.Rect.rect.height, Is.EqualTo(20).Within(.01f));
            Assert.AreEqual(1, root.GetComponent<CanvasGroup>().alpha);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void ChoicePrefabStacksAuthoredItemsInEditMode()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabFolder + "InteractionChoices.prefab");
        try
        {
            var layout = root.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>();
            var items = layout.GetComponentsInChildren<WorldInteractionChoiceItem>();
            // 프리팹에서 미리보기 항목을 지웠더라도 검사할 항목을 채움.
            var prefab = AssetDatabase.LoadAssetAtPath<WorldInteractionChoiceItem>(PrefabFolder + "InteractionChoiceItem.prefab");
            for (int i = items.Length; i < 3; i++) UnityEngine.Object.Instantiate(prefab, layout.transform);
            items = layout.GetComponentsInChildren<WorldInteractionChoiceItem>();
            var rect = (RectTransform)layout.transform;
            layout.spacing = 5;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Assert.That(rect.rect.height, Is.EqualTo(items.Sum(item => item.Rect.rect.height)
                + (items.Length - 1) * layout.spacing + layout.padding.vertical).Within(.01f));
            for (int i = 1; i < items.Length; i++)
            {
                var previous = rect.InverseTransformPoint(items[i - 1].Rect.TransformPoint(items[i - 1].Rect.rect.center));
                var current = rect.InverseTransformPoint(items[i].Rect.TransformPoint(items[i].Rect.rect.center));
                Assert.That(previous.y - current.y, Is.EqualTo((items[i - 1].Rect.rect.height + items[i].Rect.rect.height) * .5f + 5).Within(.01f));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void MissingChoiceLabelReportsLabelInsteadOfConnectedItemPrefab()
    {
        var itemRoot = PrefabUtility.LoadPrefabContents(PrefabFolder + "InteractionChoiceItem.prefab");
        var menuRoot = PrefabUtility.LoadPrefabContents(PrefabFolder + "InteractionChoices.prefab");
        try
        {
            var item = itemRoot.GetComponent<WorldInteractionChoiceItem>();
            var menu = menuRoot.GetComponent<WorldInteractionChoiceView>();
            var itemSettings = new SerializedObject(item);
            itemSettings.FindProperty("label").objectReferenceValue = null;
            itemSettings.ApplyModifiedPropertiesWithoutUndo();
            var menuSettings = new SerializedObject(menu);
            menuSettings.FindProperty("itemPrefab").objectReferenceValue = item;
            menuSettings.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(menu.IsConfigured);
            StringAssert.Contains("InteractionChoiceItem", menu.ConfigurationError);
            StringAssert.Contains("Label", menu.ConfigurationError);
            StringAssert.Contains("TextMeshPro", menu.ConfigurationError);
            StringAssert.DoesNotContain("Item Prefab", menu.ConfigurationError);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(menuRoot);
            PrefabUtility.UnloadPrefabContents(itemRoot);
        }
    }

    [Test]
    public void PosterHasOneOwnerAndUsesExistingCsvAndCanvas()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var targets = roots.SelectMany(r => r.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            var target = targets.Single(t => t.StorySceneId == "outside_day0_shiba_wanted");
            var example = targets.Single(t => t.StorySceneId == "outside_interaction_choice_example");
            Assert.AreEqual("InteractionObjects", target.transform.parent.name);
            Assert.IsNull(target.GetComponent<InteractiveNPCEntity>(), "No legacy poster execution component");
            Assert.NotNull(target.InspectionImage);
            Assert.NotNull(target.SpeechAnchor);
            var controller = roots.SelectMany(r => r.GetComponentsInChildren<WorldInteractionController>(true)).Single();
            WorldInteractionTarget.RegisterScene(scene);
            Assert.IsTrue(controller.OwnsStory(target.StorySceneId));
            Assert.IsTrue(controller.OwnsStory(example.StorySceneId), "Inactive examples must not produce legacy interaction GUI");
            target.gameObject.SetActive(false);
            Assert.IsTrue(controller.OwnsStory(target.StorySceneId), "Disabling a migrated target must not resurrect the legacy GUI");
            Assert.AreEqual("Canvas_Dialogue", controller.View.transform.parent.name);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, controller.View.GetComponentInParent<Canvas>().renderMode);
            var imagePrefab = AssetDatabase.LoadAssetAtPath<WorldInteractionImageView>(PrefabFolder + "ImageViewer.prefab");
            var hintPrefab = AssetDatabase.LoadAssetAtPath<WorldInteractionHintView>(PrefabFolder + "InteractionHint.prefab");
            Assert.NotNull(imagePrefab);
            Assert.NotNull(imagePrefab.InspectionImage);
            Assert.NotNull(hintPrefab);
            Assert.IsTrue(hintPrefab.IsConfigured);
            var viewSettings = new SerializedObject(controller.View);
            Assert.AreEqual(imagePrefab, viewSettings.FindProperty("imageViewerPrefab").objectReferenceValue);
            Assert.AreEqual(hintPrefab, viewSettings.FindProperty("interactionHintPrefab").objectReferenceValue);
            var choicePrefab = AssetDatabase.LoadAssetAtPath<WorldInteractionChoiceView>(PrefabFolder + "InteractionChoices.prefab");
            Assert.IsTrue(choicePrefab.IsConfigured);
            Assert.AreEqual(choicePrefab, viewSettings.FindProperty("choicesPrefab").objectReferenceValue);
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
            var story = catalog.Read<NewStreetData>("script/street").Scenes.Single(s => s.Id == target.StorySceneId);
            Assert.AreEqual(4, story.Steps.Length);
            Assert.That(story.Steps.All(step => step.Type == "say"));
            Assert.AreEqual("지명수배. 이름: 개시바.", story.Steps.OrderBy(step => step.Seq).First().Text.Ko);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [TestCase(480, 270)]
    [TestCase(480, 360)]
    [TestCase(480, 206)]
    public void BottomPivotBubbleStaysInsideCanvasAtBothEdges(int width, int height)
    {
        var bounds = new Rect(-width / 2f, -height / 2f, width, height);
        var size = new Vector2(210, 70);
        foreach (float x in new[] { -1000f, 1000f })
            foreach (float y in new[] { -1000f, 1000f })
            {
                var position = WorldInteractionView.ClampToCanvas(bounds, size, new Vector2(.5f, 0), new Vector2(x, y), 8);
                Assert.GreaterOrEqual(position.x - size.x / 2, bounds.xMin + 8);
                Assert.LessOrEqual(position.x + size.x / 2, bounds.xMax - 8);
                Assert.GreaterOrEqual(position.y, bounds.yMin + 8);
                Assert.LessOrEqual(position.y + size.y, bounds.yMax - 8);
            }
    }

    [UnityTest]
    [Explicit("Run alone: opens the real OutSide scene and verifies both poster outputs with real CSV and Input System.")]
    public IEnumerator PosterSpeechImageInputCancellationAndCameraTracking()
    {
        DOTween.Clear(true);
        ConfigurePreview();
        EditorSceneManager.OpenScene(ScenePath);
        yield return new EnterPlayMode();
        ConfigurePreview();
        yield return RunAfterDomainReload();
        yield return new ExitPlayMode();
    }

    static IEnumerator RunAfterDomainReload()
    {
        float deadline = Time.realtimeSinceStartup + 45;
        WorldInteractionController controller;
        do
        {
            controller = UnityEngine.Object.FindFirstObjectByType<WorldInteractionController>();
            if (controller != null && controller.IsReady) break;
            Assert.Less(Time.realtimeSinceStartup, deadline, "World interaction initialization timed out");
            yield return null;
        } while (true);
        bool done = false;
        Exception failure = null;
        controller.StartCoroutine(InPlayerLoop());
        IEnumerator InPlayerLoop()
        {
            yield return null;
            var run = Exercise(controller).ToCoroutine(error => failure = error);
            while (run.MoveNext()) yield return run.Current;
            done = true;
        }
        while (!done) yield return null;
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static async UniTask Exercise(WorldInteractionController controller)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        bool controllerWasEnabled = controller.enabled;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            var targets = controller.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)).ToArray();
            var target = targets.Single(value => value.StorySceneId == "outside_day0_shiba_wanted");
            targets.Single(value => value.StorySceneId == "outside_interaction_choice_example").gameObject.SetActive(false);
            target.gameObject.SetActive(true);
            SetOutput(target, WorldInteractionOutput.SpeechBubble);
            var player = UnityEngine.Object.FindFirstObjectByType<Player>();
            var runner = UnityEngine.Object.FindFirstObjectByType<DialogueRunner>();
            var input = UnityEngine.Object.FindFirstObjectByType<OutsideInputHandler>().GetComponent<PlayerInput>();
            // 씬을 불러온 뒤 테스트 장치를 추가함.
            // 실제 입력 액션을 보내기 전에 테스트 장치를 플레이어에 연결함.
            input.enabled = false;
            input.enabled = true;
            input.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            player.transform.position = target.transform.position + new Vector3(.13f, -.25f);
            await UniTask.Delay(500, cancellationToken: token);
            Assert.AreEqual(target, controller.FocusedTarget);
            Assert.IsTrue(controller.OwnsStory(target.StorySceneId));
            Assert.AreEqual("상호작용", controller.View.Hint.Label.text);
            Assert.IsTrue(controller.View.Hint.KeyIcon.enabled);
            AssertPrefabLayout(controller.View);
            player.transform.position += Vector3.right * 10;
            await UniTask.NextFrame(token);
            await UniTask.NextFrame(token);
            Assert.IsFalse(controller.View.Hint.gameObject.activeSelf, "No hint outside the CSV interaction radius");
            player.transform.position = target.transform.position + new Vector3(.13f, -.25f);
            await UniTask.NextFrame(token);
            await UniTask.NextFrame(token);
            await SavePreview("poster-hint", token);

            await Press(keyboard, Key.E, token);
            Assert.IsTrue(controller.IsRunning, "E starts a new session");
            Assert.AreEqual(EInteractorState.Interct, player.State);
            Assert.IsFalse(controller.View.IsImageVisible);
            // 시작 입력이 첫 줄을 건너뛰거나 기존 상호작용까지 실행하지 않는지 확인함.
            Assert.AreEqual("지명수배. 이름: 개시바.", controller.View.Bubble.textLabel.text);
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            Assert.AreEqual("다음", controller.View.Hint.Label.text);
            AssertButtonReachable(controller.View.Hint.Button);
            await SavePreview("poster-speech", token);

            var brain = Camera.main.GetComponent<CinemachineBrain>();
            brain.enabled = false;
            var camera = Camera.main;
            var before = controller.View.Bubble.bubble.anchoredPosition;
            var originalCamera = camera.transform.position;
            camera.transform.position += new Vector3(.15f, .1f);
            target.SpeechAnchor.localPosition += Vector3.up * .1f;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            var after = controller.View.Bubble.bubble.anchoredPosition;
            Assert.Less(after.x, before.x - 1, "Bubble follows actual camera movement");
            Assert.That(after.y, Is.EqualTo(before.y).Within(1), "Equal upward camera/anchor movement cancels in screen space");
            AssertPrefabLayout(controller.View);
            camera.transform.position = originalCamera;
            target.SpeechAnchor.localPosition -= Vector3.up * .1f;
            brain.enabled = true;

            for (int line = 0; line < 4; line++)
            {
                await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
                await Press(keyboard, Key.E, token);
            }
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.IsTrue(controller.LastResult.Completed);
            Assert.AreEqual(EInteractorState.None, player.State);

            SetOutput(target, WorldInteractionOutput.Image);
            await Press(keyboard, Key.E, token);
            Assert.IsTrue(controller.View.IsImageVisible);
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            Assert.AreEqual("지명수배. 이름: 개시바.", controller.View.Bubble.textLabel.text);
            Assert.AreEqual("Esc  나가기", controller.View.Hint.Label.text);
            Assert.IsFalse(controller.View.Hint.KeyIcon.enabled, "An E icon must not advertise Escape");
            AssertPrefabLayout(controller.View);
            AssertButtonReachable(controller.View.Hint.Button);
            await SavePreview("poster-image", token);
            await Press(keyboard, Key.E, token);
            Assert.IsTrue(controller.IsRunning, "E continues image captions even while the hint advertises exit");
            Assert.AreEqual("공공질서 혼란, 공무집행 방해, 사기, 무전취식, 인종차별…", controller.View.Bubble.textLabel.text);
            await Press(keyboard, Key.Escape, token);
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.AreEqual(StoryExecutionStatus.Cancelled, controller.LastResult.Status);
            Assert.AreEqual(EInteractorState.None, player.State);
            Assert.IsFalse(controller.View.IsImageVisible);

            // UI 클릭과 대상 비활성화도 세션 및 플레이어 잠금을 해제하는지 확인함.
            Assert.IsFalse(UIDisplayOptions.IsOptionOpen, "Escape cancels interaction without also opening the options overlay");
            await Press(keyboard, Key.E, token);
            controller.View.Hint.Button.onClick.Invoke();
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            await Press(keyboard, Key.E, token);
            target.gameObject.SetActive(false);
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.AreEqual(EInteractorState.None, player.State);
            Assert.IsTrue(controller.OwnsStory(target.StorySceneId));
            target.gameObject.SetActive(true);
            SetOutput(target, WorldInteractionOutput.SpeechBubble);
            await ExerciseChoices(controller, target, player, runner, keyboard, token);
            await Press(keyboard, Key.E, token);
            Assert.IsTrue(controller.View.Hint.KeyIcon.enabled, "E icon returns after leaving image mode");
            controller.enabled = false;
            await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
            Assert.IsFalse(runner.IsRunning);
            Assert.AreEqual(EInteractorState.None, player.State);
            await Press(keyboard, Key.Escape, token);
            Assert.IsTrue(UIDisplayOptions.IsOptionOpen, "Normal Escape menu behavior remains when no new interaction is running");
            await Press(keyboard, Key.Escape, token);
            Assert.IsFalse(UIDisplayOptions.IsOptionOpen);
            Debug.Log("[WorldInteraction Test] Prefabs, image/speech regression, CSV choice continuation/end, E/navigation/click, four single-line choices/layout/localization, cancellation, left/right placement and camera tracking passed.");
        }
        finally
        {
            controller.Cancel();
            controller.enabled = controllerWasEnabled;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
        }
    }

    static async UniTask ExerciseChoices(WorldInteractionController controller, WorldInteractionTarget poster, Player player,
        DialogueRunner runner, Keyboard keyboard, CancellationToken token)
    {
        var example = controller.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
            .Single(t => t.StorySceneId == "outside_interaction_choice_example");
        poster.gameObject.SetActive(false);
        SetOutput(example, WorldInteractionOutput.SpeechBubble);
        example.gameObject.SetActive(true);
        player.transform.position = example.transform.position + new Vector3(.25f, -.25f);
        await UniTask.Delay(200, cancellationToken: token);
        Assert.AreEqual(example, controller.FocusedTarget);

        async UniTask OpenChoices()
        {
            await Press(keyboard, Key.E, token);
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            await Press(keyboard, Key.E, token);
            Assert.AreEqual(DialogueState.WaitingForChoice, runner.CurrentState, "The E that opens choices must not immediately select one");
            Assert.IsTrue(controller.View.IsChoosing);
            Assert.IsFalse(controller.View.Hint.gameObject.activeSelf, "The row cursor replaces the bottom E hint");
            Assert.IsTrue(controller.View.Bubble.gameObject.activeSelf, "Keep the preceding question visible");
            Assert.AreEqual("안내문을 더 읽어 볼까?", controller.View.Bubble.textLabel.text);
            await UniTask.WaitUntil(() => !controller.View.Choices.IsRevealing, cancellationToken: token);
            Assert.IsTrue(controller.View.Choices.Rows[0].IsFocused);
            Assert.IsTrue(controller.View.Choices.Rows[0].Background.gameObject.activeSelf);
            Assert.IsFalse(controller.View.Choices.Rows[1].Background.gameObject.activeSelf);
            Assert.IsTrue(controller.View.Choices.MoneyLabel.gameObject.activeSelf);
            Assert.AreEqual(2, controller.View.Choices.Rows.Count(row => row.gameObject.activeSelf), "Unused authored preview items must be hidden");
            await SavePreview("choices-open", token);
            AssertButtonReachable(controller.View.Choices.Rows[0].Button);
        }

        await OpenChoices();
        var choiceRect = controller.View.Choices.Rect;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)controller.View.transform,
            Camera.main.WorldToScreenPoint(player.transform.position), null, out var playerPoint);
        Assert.Greater(choiceRect.anchoredPosition.x, playerPoint.x, "Right-side player gets choices on the right");
        AssertChoiceInsideCanvas(controller.View);
        await SavePreview("choices-right", token);
        player.transform.position = example.transform.position + new Vector3(-.25f, -.25f);
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)controller.View.transform,
            Camera.main.WorldToScreenPoint(player.transform.position), null, out playerPoint);
        Assert.Less(choiceRect.anchoredPosition.x, playerPoint.x, "Left-side player gets choices on the left");
        AssertChoiceInsideCanvas(controller.View);
        await SavePreview("choices-left", token);

        await Press(keyboard, Key.E, token);
        Assert.IsFalse(controller.View.IsChoosing);
        Assert.AreEqual("조금 더 자세히 읽어 보자.", controller.View.Bubble.textLabel.text);
        for (int line = 0; line < 2; line++)
        {
            await UniTask.WaitUntil(() => runner.CurrentState == DialogueState.WaitingForInput, cancellationToken: token);
            await Press(keyboard, Key.E, token);
        }
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.IsTrue(controller.LastResult.Completed);
        Assert.AreEqual(EInteractorState.None, player.State);

        await OpenChoices();
        await Press(keyboard, Key.DownArrow, token);
        Assert.IsFalse(controller.View.Choices.Rows[0].IsFocused);
        Assert.IsTrue(controller.View.Choices.Rows[1].IsFocused);
        await UniTask.Delay(100, cancellationToken: token);
        await SavePreview("choices-second-selected", token);
        await Press(keyboard, Key.E, token);
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.IsTrue(controller.LastResult.Completed, "Stop-reading branch completes without another line");
        Assert.IsFalse(controller.View.Bubble.gameObject.activeSelf);
        Assert.AreEqual(EInteractorState.None, player.State);

        await OpenChoices();
        var first = controller.View.Choices.Rows[0].Button;
        ExecuteEvents.Execute(first.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        Assert.IsFalse(controller.View.IsChoosing, "Mouse click commits the choice");
        Assert.AreEqual("조금 더 자세히 읽어 보자.", controller.View.Bubble.textLabel.text);
        await Press(keyboard, Key.Escape, token);
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);

        await OpenChoices();
        await Press(keyboard, Key.Escape, token);
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.AreEqual(StoryExecutionStatus.Cancelled, controller.LastResult.Status);
        Assert.IsFalse(UIDisplayOptions.IsOptionOpen);
        first.onClick.Invoke();
        Assert.IsFalse(runner.IsRunning, "A cancelled choice cannot call the old session");

        await OpenChoices();
        example.gameObject.SetActive(false);
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.IsFalse(controller.View.IsChoosing);
        Assert.AreEqual(EInteractorState.None, player.State);

        example.gameObject.SetActive(true);
        await OpenChoices();
        controller.View.gameObject.SetActive(false);
        await UniTask.WaitUntil(() => !controller.IsRunning, cancellationToken: token);
        Assert.IsFalse(controller.View.IsChoosing);
        Assert.AreEqual(EInteractorState.None, player.State);
        controller.View.gameObject.SetActive(true);

        var prefab = AssetDatabase.LoadAssetAtPath<WorldInteractionChoiceView>(PrefabFolder + "InteractionChoices.prefab");
        var isolated = UnityEngine.Object.Instantiate(prefab, controller.View.transform);
        try
        {
            var authoredRows = isolated.GetComponentsInChildren<WorldInteractionChoiceItem>(true);
            var options = new NewStreetOptionData[4];
            for (int index = 0; index < options.Length; index++)
            {
                options[index] = new NewStreetOptionData
                {
                    Id = "option-" + index, Seq = index, Text = new Texts { Ko = "선택 " + index, En = "Choice " + index },
                    Selectable = index != 0, LockReason = new Texts { Ko = "선택할 수 없습니다." }
                };
            }
            options[2].Text = new Texts { Ko = "당장은 결정하지 않고\n조금 더 생각해 본다.", En = "Think about it a little more before deciding on a choice today." };
            int callbacks = 0;
            string picked = null;
            isolated.Show(options, option => { callbacks++; picked = option.Id; }, ELanguage.En, 1234);
            Assert.AreEqual(4, isolated.Rows.Count(row => row.gameObject.activeSelf));
            Assert.AreEqual(4, isolated.GetComponentsInChildren<WorldInteractionChoiceItem>().Length, "Reuse preview children rather than duplicating them");
            foreach (var authored in authoredRows) CollectionAssert.Contains(isolated.Rows, authored);
            Assert.IsTrue(isolated.IsRevealing);
            Assert.AreEqual(0, isolated.RevealViewport.rect.height);
            Assert.AreEqual("Money 1,234", isolated.MoneyLabel.text);
            var fullSize = isolated.Rect.rect.size;
            var textScale = isolated.Rows[1].Label.transform.localScale;
            isolated.Confirm();
            isolated.Rows[1].Button.onClick.Invoke();
            Assert.AreEqual(0, callbacks, "A revealing menu must not accept hidden choices");
            float timeScale = Time.timeScale;
            Time.timeScale = 0;
            try { await UniTask.WaitUntil(() => !isolated.IsRevealing, cancellationToken: token); }
            finally { Time.timeScale = timeScale; }
            Assert.AreEqual(fullSize, isolated.Rect.rect.size, "Revealing does not move the full-size panel bounds");
            Assert.AreEqual(textScale, isolated.Rows[1].Label.transform.localScale, "Revealing never stretches text");
            Assert.That(isolated.RevealViewport.rect.height, Is.EqualTo(fullSize.y).Within(.01f));
            Assert.IsFalse(isolated.Rows[0].Button.interactable);
            Assert.AreEqual("Choice 0", isolated.Rows[0].Label.text);
            Assert.AreEqual("선택할 수 없습니다.", isolated.Rows[0].LockReasonLabel.text, "Missing English reason falls back to Korean");
            foreach (var row in isolated.Rows)
            {
                row.Label.ForceMeshUpdate();
                Assert.AreEqual(1, row.Label.textInfo.lineCount);
                Assert.That(row.Rect.rect.height, Is.EqualTo(row.Button.interactable ? 20 : 32).Within(.01f));
            }
            Assert.IsTrue(isolated.Rows[2].Label.isTextTruncated, "Overlong text is ellipsized, not wrapped");
            var list = isolated.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>();
            Assert.That(isolated.Rect.rect.height, Is.EqualTo(isolated.Rows.Sum(row => row.Rect.rect.height)
                + list.spacing * 3 + list.padding.vertical + 13).Within(.01f));
            foreach (bool right in new[] { true, false })
            {
                isolated.SetSide(right);
                await UniTask.NextFrame(token);
                Assert.That(list.childAlignment, Is.EqualTo(right ? TextAnchor.UpperLeft : TextAnchor.UpperRight));
                AssertButtonReachable(isolated.Rows[3].Button);
                await SavePreview(right ? "four-choices-right" : "four-choices-left", token);
            }
            isolated.Rows[0].Button.onClick.Invoke();
            Assert.AreEqual(0, callbacks);
            isolated.Rows[3].Button.onClick.Invoke();
            isolated.Rows[3].Button.onClick.Invoke();
            Assert.AreEqual(1, callbacks, "A choice is committed once even with repeated events");
            Assert.AreEqual("option-3", picked, "The fourth choice preserves the original CSV option identity");
            isolated.Show(options.Take(2).ToArray(), _ => callbacks++, ELanguage.Ko);
            Assert.AreEqual(2, isolated.Rows.Count(row => row.gameObject.activeSelf), "Reopening with fewer choices hides surplus rows");
            Assert.IsFalse(isolated.MoneyLabel.gameObject.activeSelf, "No invented amount when no money source is supplied");
            isolated.Hide();
            await UniTask.Delay(300, cancellationToken: token);
            isolated.Confirm();
            Assert.IsFalse(isolated.IsOpen, "Closing during reveal cannot reopen the menu");
            Assert.AreEqual(1, callbacks);
            var error = Assert.Throws<InvalidOperationException>(() => isolated.Show(options.Concat(options.Take(1)).ToArray(), _ => callbacks++, ELanguage.Ko));
            StringAssert.Contains("최대 4개", error.Message);
            Assert.IsFalse(isolated.IsOpen, "Invalid data must not leave a partial choice list open");
            await ExerciseMoneyChoices(isolated, keyboard, token);
        }
        finally { UnityEngine.Object.Destroy(isolated.gameObject); }
        example.gameObject.SetActive(false);
        poster.gameObject.SetActive(true);
        player.transform.position = poster.transform.position + new Vector3(.13f, -.25f);
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
    }

    sealed class MoneyChoicePresenter : IDialoguePresenter
    {
        public WorldInteractionChoiceView View;
        public PlayerDataSO Wallet;
        public int Commits;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() => View.Hide();
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token) => UniTask.CompletedTask;
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected) =>
            View.Show(options, option => { Commits++; selected(option); }, ELanguage.Ko, Wallet.HasMoney());
    }

    static async UniTask ExerciseMoneyChoices(WorldInteractionChoiceView view, Keyboard keyboard, CancellationToken token)
    {
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var choice = catalog.Read<NewStreetData>("script/street").Scenes.SelectMany(scene => scene.Steps)
            .Single(step => step.Options?.Any(option => option.Id == "thug-pay") == true);
        var pay = choice.Options.Single(option => option.Id == "thug-pay");
        Assert.AreEqual("money >= 500", pay.When);
        Assert.AreEqual("선택할 수 없습니다. (소지금 부족)", pay.LockReason.Ko);
        var go = new GameObject("isolated money choice test");
        var wallet = ScriptableObject.CreateInstance<PlayerDataSO>();
        var runner = go.AddComponent<DialogueRunner>();
        try
        {
            typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(runner, new ConditionUtil(GameStateManager.Instance, wallet, wallet));
            var presenter = new MoneyChoicePresenter { View = view, Wallet = wallet };
            runner.Bind(presenter);
            foreach (int money in new[] { 300, 499, 500, 501 })
            {
                wallet.SetMoney(money);
                presenter.Commits = 0;
                var run = runner.PlayOutsideAsync(new[] { choice }, token);
                Assert.AreEqual(DialogueState.WaitingForChoice, runner.CurrentState);
                await UniTask.WaitUntil(() => !view.IsRevealing, cancellationToken: token);
                bool affordable = money >= 500;
                var row = view.Rows[0];
                Assert.AreEqual(affordable, row.Button.interactable, "Real CSV condition at " + money);
                Assert.AreEqual(!affordable, row.LockReasonLabel.gameObject.activeSelf);
                Assert.AreEqual(affordable, row.IsFocused, "Default selection skips locked rows");
                if (!affordable)
                {
                    Assert.AreEqual("선택할 수 없습니다. (소지금 부족)", row.LockReasonLabel.text);
                    Assert.Less(row.GetComponent<CanvasGroup>().alpha, 1);
                    row.Button.onClick.Invoke();
                    Assert.AreEqual(0, presenter.Commits, "Locked clicks never reach the runner");
                    view.MoveSelection(-1);
                    Assert.IsTrue(view.Rows[1].IsFocused, "Keyboard navigation skips locked choices");
                    Assert.AreEqual(money, wallet.HasMoney());
                }
                if (money == 300)
                    foreach (bool right in new[] { true, false })
                    {
                        view.SetSide(right);
                        await UniTask.NextFrame(token);
                        Canvas.ForceUpdateCanvases();
                        Assert.AreEqual(right ? TMPro.HorizontalAlignmentOptions.Left : TMPro.HorizontalAlignmentOptions.Right,
                            row.LockReasonLabel.horizontalAlignment);
                        var bounds = new Vector3[4];
                        row.LockReasonLabel.rectTransform.GetWorldCorners(bounds);
                        var next = new Vector3[4];
                        view.Rows[1].Rect.GetWorldCorners(next);
                        Assert.GreaterOrEqual(bounds[0].y, next[1].y, "Description does not overlap the next choice");
                        AssertButtonReachable(view.Rows[1].Button);
                        await SavePreview(right ? "money-locked-right" : "money-locked-left", token);
                    }
                if (money == 500) await SavePreview("money-affordable", token);
                view.Confirm();
                Assert.AreEqual(1, presenter.Commits);
                while (runner.IsRunning)
                {
                    if (runner.CurrentState == DialogueState.WaitingForInput) runner.AdvanceInputOutside();
                    await UniTask.NextFrame(token);
                }
                Assert.IsTrue((await run).Completed);
                Assert.AreEqual(affordable ? money - 500 : money, wallet.HasMoney(), "Only an affordable payment deducts the CSV amount once");
            }
        }
        finally
        {
            await runner.StopAsync();
            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(wallet);
        }
    }

    static void AssertChoiceInsideCanvas(WorldInteractionView view)
    {
        var rect = view.Choices.Rect;
        var bounds = ((RectTransform)view.transform).rect;
        var min = rect.anchoredPosition - rect.rect.size * .5f;
        var max = rect.anchoredPosition + rect.rect.size * .5f;
        Assert.GreaterOrEqual(min.x, bounds.xMin + 7.9f);
        Assert.GreaterOrEqual(min.y, bounds.yMin + 7.9f);
        Assert.LessOrEqual(max.x, bounds.xMax - 7.9f);
        Assert.LessOrEqual(max.y, bounds.yMax - 7.9f);
    }

    static async UniTask Press(Keyboard keyboard, Key key, CancellationToken token)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
    }

    static void ConfigurePreview()
    {
        // 기존 Game View와 오버레이 검증 설정을 재사용함.
        Environment.SetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR", Path.GetFullPath("Library/WorldInteractionValidation"));
        typeof(CampaignStoryRuntimeSmokeTests).GetMethod("ConfigurePreviewViewport", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }
    static void AssertButtonReachable(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = rect.TransformPoint(rect.rect.center) };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits, "Overlay button has a real clickable screen location");
        Assert.AreEqual(button.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
            "Raycast at " + pointer.position + ": " + string.Join(", ", hits.Select(hit => hit.gameObject.name + " depth=" + hit.depth)));
    }

    static void AssertPrefabLayout(WorldInteractionView view)
    {
        Assert.IsNull(view.transform.Find("Advance"));
        Assert.IsNull(view.transform.Find("Close"));
        Assert.AreEqual(4, view.transform.childCount, "Only the four configured presentation prefabs are instantiated");
        var bubblePrefab = AssetDatabase.LoadAssetAtPath<DynamicSpeechBubble>(PrefabFolder + "OutsideDialogueTextBox.prefab");
        Assert.AreEqual(bubblePrefab.baseFontSize, view.Bubble.baseFontSize);
        Assert.AreEqual(bubblePrefab.minFontSize, view.Bubble.minFontSize);
        Assert.AreEqual(bubblePrefab.minSize, view.Bubble.minSize);
        Assert.AreEqual(bubblePrefab.maxSize, view.Bubble.maxSize);
        Assert.AreEqual(bubblePrefab.bubble.pivot, view.Bubble.bubble.pivot);
        Assert.AreEqual(bubblePrefab.bubble.localScale, view.Bubble.bubble.localScale);
        Assert.AreEqual(bubblePrefab.textLabel.rectTransform.offsetMin, view.Bubble.textLabel.rectTransform.offsetMin);
        Assert.AreEqual(bubblePrefab.textLabel.rectTransform.offsetMax, view.Bubble.textLabel.rectTransform.offsetMax);
        var hintPrefab = AssetDatabase.LoadAssetAtPath<WorldInteractionHintView>(PrefabFolder + "InteractionHint.prefab");
        var imagePrefab = AssetDatabase.LoadAssetAtPath<WorldInteractionImageView>(PrefabFolder + "ImageViewer.prefab");
        AssertRectMatches((RectTransform)hintPrefab.transform, (RectTransform)view.Hint.transform);
        AssertRectMatches(imagePrefab.InspectionImage.rectTransform, view.ImageViewer.InspectionImage.rectTransform);
        Assert.AreEqual(hintPrefab.Label.font, view.Hint.Label.font);
        Assert.AreEqual(hintPrefab.Label.fontSize, view.Hint.Label.fontSize);
    }

    private static void SetOutput(WorldInteractionTarget target, WorldInteractionOutput output)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty("output").enumValueIndex = (int)output;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssertRectMatches(RectTransform authored, RectTransform instance)
    {
        Assert.AreEqual(authored.anchorMin, instance.anchorMin);
        Assert.AreEqual(authored.anchorMax, instance.anchorMax);
        Assert.AreEqual(authored.anchoredPosition, instance.anchoredPosition);
        Assert.AreEqual(authored.sizeDelta, instance.sizeDelta);
        Assert.AreEqual(authored.pivot, instance.pivot);
    }

    static async UniTask SavePreview(string name, CancellationToken token)
    {
        // 다른 카메라를 따로 렌더링하지 않고 실제 오버레이가 포함된 화면을 캡처함.
        Directory.CreateDirectory("Library/WorldInteractionValidation");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/WorldInteractionValidation/" + name + ".png"));
        await UniTask.NextFrame(token);
        await UniTask.NextFrame(token);
    }
}
