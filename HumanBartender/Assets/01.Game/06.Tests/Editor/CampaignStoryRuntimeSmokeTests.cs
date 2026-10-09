using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CampaignStoryRuntimeSmokeTests
{
    [UnityTest]
    [Explicit("Run in isolation: enters the real Main and Play scenes and operates the tutorial UI.")]
    public IEnumerator NewGameShowsIntroNoticeAndCompletesTutorialPreparation()
    {
        DOTween.Clear(true);
        ConfigurePreviewViewport();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        // Allocate captured scene references after EnterPlayMode's domain reload.
        ConfigurePreviewViewport();
        yield return RunInPlayerLoop(RunTutorialJourney(false));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    [Explicit("Run in isolation: verifies gameplay when tutorial prefab and component references are absent.")]
    public IEnumerator NewGameWithoutTutorialPrefabStillStartsAndPrepares()
    {
        DOTween.Clear(true);
        ConfigurePreviewViewport();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity");
        yield return new EnterPlayMode();
        ConfigurePreviewViewport();
        yield return RunInPlayerLoop(RunTutorialJourney(true));
        yield return new ExitPlayMode();
    }

    static IEnumerator RunTutorialJourney(bool missingTutorialReferences)
    {
        var oldBackground = InputSystem.settings.backgroundBehavior;
        var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        // A batch-mode editor has no focused Game View; route test input to the player.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        // Create devices before SceneInput enables and pairs the Play scene's PlayerInput.
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        try { yield return RunTutorialJourneyWithDevices(missingTutorialReferences, keyboard, mouse); }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
        }
    }

    static IEnumerator RunInPlayerLoop(IEnumerator journey)
    {
        yield return WaitFor(() => SceneTransitionManager.Instance != null, "persistent player coroutine host");
        bool finished = false;
        Exception failure = null;
        // EditMode test continuations use the editor window's Screen dimensions. UI raycasts,
        // queued input and previews must run in the actual player's coroutine context.
        SceneTransitionManager.Instance.StartCoroutine(ObserveJourney(journey, error =>
        {
            failure = error;
            finished = true;
        }));
        yield return WaitFor(() => finished, "player tutorial journey");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    static IEnumerator ObserveJourney(IEnumerator journey, Action<Exception> complete)
    {
        yield return null; // StartCoroutine's first MoveNext still runs in its caller's context.
        var stack = new Stack<IEnumerator>();
        stack.Push(journey);
        Exception failure = null;
        while (stack.Count != 0 && failure == null)
        {
            var routine = stack.Peek();
            bool advanced = false;
            object yielded = null;
            try
            {
                advanced = routine.MoveNext();
                if (advanced) yielded = routine.Current;
                else
                {
                    stack.Pop();
                    (routine as IDisposable)?.Dispose();
                }
            }
            catch (Exception error) { failure = error; }
            if (failure != null || !advanced) continue;
            if (yielded is IEnumerator nested) stack.Push(nested);
            else yield return yielded;
        }
        // Unwind iterator finally blocks even when an NUnit assertion fails in the player.
        while (stack.Count != 0)
        {
            try { (stack.Pop() as IDisposable)?.Dispose(); }
            catch (Exception error) { failure ??= error; }
        }
        complete(failure);
    }

    static IEnumerator RunTutorialJourneyWithDevices(bool missingTutorialReferences, Keyboard keyboard, Mouse mouse)
    {
        yield return WaitFor(() => NewDataLoadManager.IsLoaded && SceneTransitionManager.Instance != null &&
            !SceneTransitionManager.Instance.IsBusy, "data loading and title fade");
        void ClearTutorialReferences(Scene scene, LoadSceneMode mode)
        {
            if (!missingTutorialReferences || scene.name != "Play") return;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var controller in root.GetComponentsInChildren<StoryTutorialController>(true))
                    UnityEngine.Object.DestroyImmediate(controller);
                foreach (var runner in root.GetComponentsInChildren<StoryScriptRunner>(true))
                    runner.SetTutorialController(null);
            }
        }
        SceneManager.sceneLoaded += ClearTutorialReferences;
        try
        {
            Find<MainManager>().TempStart();
            yield return WaitFor(() => Find<StoryTutorialView>() is { IsIntroNoticeVisible: true }, "missing intro notice in Play");
        }
        finally { SceneManager.sceneLoaded -= ClearTutorialReferences; }
        Assert.AreEqual("Play", SceneManager.GetActiveScene().name);
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR")))
            Assert.That((float)Screen.width / Screen.height, Is.EqualTo(16f / 9).Within(.002f), "preview player uses the requested widescreen viewport");
        Assert.IsFalse(Find<StoryScriptRunner>().IsRunning, "dialogue waits for the intro click");
        if (missingTutorialReferences)
            Assert.IsNull(Field<StoryTutorialView>(Find<StoryTutorialController>(), "viewPrefab"));
        StringAssert.Contains("현재 컷신 리소스가 적용되지 않았습니다.", Find<StoryTutorialView>().Instruction);
        yield return null; // Let the newly activated overlay register its raycast depths.
        SavePreview(missingTutorialReferences ? "fallback-intro-notice" : "intro-notice");
        ClickAt(new Vector2(Screen.width * .2f, Screen.height * .2f), Find<StoryTutorialView>().IntroClickButton);
        yield return WaitFor(() => SlotSprites(ESlotType.Right).Any(renderer => renderer.sprite != null &&
            renderer.gameObject.activeInHierarchy && renderer.color.a > .2f && renderer.color.a < .8f), "Chris fades in by opacity");
        SavePreview(missingTutorialReferences ? "fallback-character-fade-in" : "character-fade-in");
        yield return WaitFor(() => Find<StoryScriptRunner>() is { IsWaitingForAdvance: true }, "first clickable bar line");
        Assert.IsTrue(SlotSprites(ESlotType.Right).All(renderer => Mathf.Approximately(renderer.color.a, 1)), "entered character becomes fully visible");
        Assert.AreEqual("Play", SceneManager.GetActiveScene().name);
        Assert.AreEqual(0, GameStateManager.Instance.CurrentDay);
        Assert.AreEqual(EGameFlow.Bar, GameStateManager.Instance.GameFlow);
        Assert.AreEqual("dlg_day0_notion_bar_3", Find<StoryScriptRunner>().CurrentDialogueId);
        var text = Find<UIDialogueTextView>();
        Assert.IsTrue(text.customerSpeechBubble.gameObject.activeInHierarchy);
        Assert.IsFalse(text.lunaSpeechBubble.gameObject.activeInHierarchy);
        Assert.IsTrue(Find<StoryFlow>().TryAdvance());
        Assert.IsFalse(text.customerSpeechBubble.gameObject.activeInHierarchy, "previous bubble closes on advance");
        yield return WaitFor(() => Find<StoryScriptRunner>() is
            { IsWaitingForAdvance: true, CurrentDialogueId: "dlg_day0_notion_bar_4" }, "Luna's next line");
        Assert.IsTrue(text.lunaSpeechBubble.gameObject.activeInHierarchy);
        Assert.IsFalse(text.customerSpeechBubble.gameObject.activeInHierarchy);

        yield return DriveDialogueUntil(() => Find<StoryTutorialController>().ActiveLesson == "seatExplore");
        var tutorialView = Find<StoryTutorialView>();
        yield return null;
        Assert.IsTrue(tutorialView.SeatHud.IsVisible, "live seat HUD exists during story");
        Assert.IsTrue(tutorialView.Guide.HasSource && tutorialView.Guide.HasDestination, "seat keys and HUD are highlighted");
        SavePreview(missingTutorialReferences ? "fallback-seat-guide" : "seat-guide");
        yield return ExploreSeatsWithKeyboard(keyboard, mouse);
        yield return DriveDialogueUntil(() => Find<StoryTutorialController>().ActiveLesson == "seatIndicator");
        Assert.IsFalse(Find<StoryFlow>().TryMoveTutorialSeat(-1), "seat keys only operate during exploration");
        ClickButton(Field<Button>(tutorialView, "confirm"));
        Assert.IsTrue(tutorialView.IsSeatLegendVisible, "first confirm opens the color and blinking legend");
        Assert.IsNotNull(tutorialView.transform.Find("Instruction/Seat Signal Legend/Signal Waiting"));
        Assert.IsNotNull(tutorialView.transform.Find("Instruction/Seat Signal Legend/Signal LeavingSoon"));
        yield return null;
        SavePreview(missingTutorialReferences ? "fallback-seat-legend" : "seat-legend");
        ClickButton(Field<Button>(tutorialView, "confirm"));
        yield return DriveDialogueUntil(() => Find<StoryTutorialController>().ActiveLesson == "coaster");

        var pointer = new PointerEventData(EventSystem.current);
        var coaster = Find<CoasterDragItem>();
        var target = Find<StoryCoasterDropTarget>();
        Assert.IsNotNull(coaster, "existing coaster tray resource");
        Assert.IsNotNull(target, "story coaster drop target");
        yield return null;
        Canvas.ForceUpdateCanvases();
        var source = Find<CraftServingView>().CoasterSupply;
        var sourcePoint = RectTransformUtility.WorldToScreenPoint(null, source.TransformPoint(source.rect.center));
        // Start over the supply box/label, not just the coaster's small opaque pixels.
        sourcePoint.y -= source.rect.height * source.GetComponentInParent<Canvas>().scaleFactor * .25f;
        var targetPoint = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
        Vector3 tableScreen = Camera.main.WorldToScreenPoint(new Vector3(
            Find<DialogueCharacterManager>().GetCharacterPosition("chris").x, -1.5f, 0));
        Assert.That(targetPoint.y, Is.EqualTo(tableScreen.y).Within(2), "drop zone is on the tabletop");
        Assert.Less(((RectTransform)target.transform).rect.height, 80, "drop zone is a low tabletop area");
        Assert.IsTrue(tutorialView.Guide.HasDragRoute, "coaster source and tabletop have a visible route");
        SavePreview(missingTutorialReferences ? "fallback-coaster-guide" : "coaster-guide");
        yield return null;
        Canvas.ForceUpdateCanvases();
        sourcePoint = RectTransformUtility.WorldToScreenPoint(null, source.TransformPoint(source.rect.center));
        sourcePoint.y -= source.rect.height * source.GetComponentInParent<Canvas>().scaleFactor * .25f;
        targetPoint = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
        yield return DragCoasterWithMouse(mouse, sourcePoint, targetPoint, target);
        yield return WaitFor(() => Find<StoryTutorialController>().ActiveLesson != "coaster", "coaster drop completes lesson");
        Assert.IsNotNull(target.PlacedCoaster);
        Assert.IsTrue(target.PlacedCoaster.IsPlaced);
        Assert.AreEqual(target.transform, target.PlacedCoaster.transform.parent);
        SavePreview(missingTutorialReferences ? "fallback-coaster-placed" : "coaster-placed");
        yield return DriveDialogueUntil(() => Find<RecipeBrowserScreen>().IsOpen);
        Assert.AreEqual("recipeSelect", Find<StoryTutorialController>().ActiveLesson);
        var recipes = Find<RecipeBrowserScreen>();
        var runner = Find<StoryScriptRunner>();
        var waitingOrder = Field<OrderRequest>(runner, "currentOrder");
        ClickButton(Field<Button>(recipes, "closeButton"));
        yield return null;
        Assert.IsFalse(recipes.IsOpen, "story recipe menu can be closed without losing the order");
        yield return PressTab(keyboard, true);
        yield return PressTab(keyboard, false);
        var menuButton = Field<Button>(Find<ServicePanelController>(), "toggleButton");
        Assert.IsTrue(menuButton.gameObject.activeInHierarchy, "craft menu button remains available during a story order");
        ClickButton(menuButton);
        Assert.IsTrue(recipes.IsOpen, "visible menu button reopens the story recipes");
        Find<CocktailRecipeBrowser>().TryStart("bottle_beer");
        Assert.IsFalse(Find<CraftFlowController>().IsPreparing, "reopening preserves the gin tonic tutorial recipe restriction");
        Assert.AreSame(waitingOrder, Field<OrderRequest>(runner, "currentOrder"));
        Find<CocktailRecipeBrowser>().TryStart("gin_tonic");
        yield return WaitFor(() => Find<CraftPrepStageScreen>() is { IsOpen: true } &&
            Find<StoryTutorialController>().ActiveLesson == "readRecipe", "existing preparation prefab");
        var stage = Find<CraftPrepStageScreen>();
        Field<Button>(stage, "backButton").onClick.Invoke();
        yield return WaitFor(() => !Find<CraftPrepStageHost>().IsOpen && !Find<CraftFlowController>().IsBusy &&
            Find<PlayInputHandler>().GetComponent<PlayerInput>().currentActionMap.name == "Play", "cancelled preparation returns to bar input");
        Assert.IsTrue(runner.CanStartCraft, "story keeps craft authorization after preparation cancellation");
        Assert.AreSame(waitingOrder, Field<OrderRequest>(runner, "currentOrder"), "cancelled preparation keeps the waiting story order");
        yield return PressTab(keyboard, true);
        SavePreview(missingTutorialReferences ? "fallback-reopened-recipes" : "reopened-recipes");
        Find<CocktailRecipeBrowser>().TryStart("bottle_beer");
        Assert.IsFalse(Find<CraftFlowController>().IsPreparing, "cancel and reopen preserve the tutorial restriction");
        Find<CocktailRecipeBrowser>().TryStart("gin_tonic");
        yield return WaitFor(() => Find<CraftPrepStageHost>().IsOpen && Find<StoryTutorialController>().ActiveLesson == "readRecipe", "restart cancelled preparation");
        stage = Find<CraftPrepStageHost>().ActiveStage;
        Assert.IsFalse(Find<CraftFlowController>().Preparation.HasReadRecipeNote, "restart gets a fresh preparation session");
        var startButton = Field<Button>(stage, "startButton");
        Assert.IsFalse(startButton.interactable, "cannot skip tutorial preparation");
        Field<Button>(stage, "recipeButton").onClick.Invoke();
        Field<Button>(stage, "recipeCloseButton").onClick.Invoke();
        yield return Lesson("glass");
        Shelf(stage, "long_drink").OnPointerClick(pointer);
        yield return Lesson("navigateGin");
        stage.Next();
        yield return WaitFor(() => !stage.IsCameraMoving, "tool shelf movement");
        stage.Next();
        yield return Lesson("hoverGin");
        Shelf(stage, "gin").OnPointerEnter(pointer);
        yield return Lesson("addGin");
        Shelf(stage, "gin").OnPointerClick(pointer);
        yield return Lesson("removeGin");
        var ginInTray = stage.GetComponentsInChildren<CraftPrepTrayItem>()
            .Single(item => Field<string>(item, "itemId") == "gin");
        Field<Button>(ginInTray, "removeButton").onClick.Invoke();
        yield return Lesson("ginAgain");
        Shelf(stage, "gin").OnPointerClick(pointer);
        yield return Lesson("navigateSoda");
        stage.Next();
        yield return Lesson("hoverSoda");
        Shelf(stage, "soda_water").OnPointerEnter(pointer);
        yield return Lesson("addSoda");
        Shelf(stage, "soda_water").OnPointerClick(pointer);
        yield return Lesson("start");
        yield return WaitFor(() => startButton.interactable, "manufacture start enabled");
        startButton.onClick.Invoke();
        yield return Lesson("gimmick");
        Assert.IsNotNull(Find<CraftFlowController>().Current, "existing crafting system started");
        yield return VerifyCharacterExitFade();
        yield return VerifyGuestFade();
    }

    static IEnumerable<SpriteRenderer> SlotSprites(ESlotType seat)
    {
        var rig = Field<SlotCharacterPart[]>(Find<DialogueCharacterManager>(), "slotParts").Single(slot => slot.type == seat);
        return rig.parts.Select(part => part.Renderer).Append(rig.portaitSpriteRenderer).Where(renderer => renderer != null);
    }

    static IEnumerator VerifyCharacterExitFade()
    {
        var manager = Find<DialogueCharacterManager>();
        var portrait = SlotSprites(ESlotType.Right).First(renderer => renderer.sprite != null && renderer.gameObject.activeInHierarchy);
        portrait.color = new Color(.72f, .84f, .93f, 1);
        var exit = Find<BarStoryPresenter>().ExitAsync(ESlotType.Right, default).AsTask();
        yield return WaitFor(() => portrait.color.a > .1f && portrait.color.a < .9f, "character fades out before resource cleanup");
        Assert.AreEqual("chris", manager.GetSlotCharacterId(ESlotType.Right));
        Assert.IsNotNull(portrait.sprite, "portrait stays loaded during fade out");
        Assert.That(portrait.color.r, Is.EqualTo(.72f).Within(.001f), "fade preserves original RGB tint");
        yield return WaitFor(() => exit.IsCompleted, "character exit completes");
        Assert.IsFalse(exit.IsFaulted);
        Assert.AreEqual("", manager.GetSlotCharacterId(ESlotType.Right));
        Assert.IsNull(portrait.sprite, "portrait is cleared after fade out");
    }

    static IEnumerator VerifyGuestFade()
    {
        var root = new GameObject("guest fade regression");
        var appearance = new GameObject("temporary guest portrait");
        appearance.transform.SetParent(root.transform);
        var renderer = appearance.AddComponent<SpriteRenderer>();
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        renderer.sprite = sprite;
        renderer.color = new Color(.5f, .7f, .9f, 1);
        appearance.SetActive(false);
        var slot = root.AddComponent<GuestSlot>();
        typeof(GuestSlot).GetField("tempAppearanceObject", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, appearance);
        slot.SetTempAppearanceMode(true);
        try
        {
            slot.Seat(new Guest { appearance = new GuestBodyAppearance("male") });
            yield return WaitFor(() => renderer.color.a > .1f && renderer.color.a < .9f, "random guest fades in");
            slot.Clear(); // Interrupt an arrival, then reuse the same seat.
            var next = new Guest { appearance = new GuestBodyAppearance("male") };
            slot.Seat(next);
            yield return WaitFor(() => Mathf.Approximately(renderer.color.a, 1), "reused seat finishes its new arrival fade");
            slot.MarkLeaving();
            var leave = slot.FadeOutAsync(default).AsTask();
            yield return WaitFor(() => renderer.color.a > .1f && renderer.color.a < .9f, "random guest fades out");
            Assert.AreSame(next, slot.CurrentGuest, "seat remains occupied until fade completion");
            Assert.That(renderer.color.g, Is.EqualTo(.7f).Within(.001f));
            yield return WaitFor(() => leave.IsCompleted, "random guest fade completes");
            Assert.IsFalse(leave.IsFaulted);
            slot.Clear();
            Assert.IsTrue(slot.IsEmpty);
            Assert.IsFalse(appearance.activeSelf);
            Assert.That(renderer.color.a, Is.EqualTo(1), "cleared seat restores opacity for the next guest");
        }
        finally
        {
            slot.Clear();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(sprite);
        }
    }

    static IEnumerator PressTab(Keyboard keyboard, bool expectedOpen)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
        yield return null;
        yield return WaitFor(() => Find<RecipeBrowserScreen>().IsOpen == expectedOpen, "Tab opens or closes the current story craft menu");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
        Assert.AreEqual(expectedOpen, Find<RecipeBrowserScreen>().IsOpen, "Tab release does not toggle the menu a second time");
    }

    static void ConfigurePreviewViewport()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR"))) return;
        // Preview runs use an isolated editor and a fixed 16:9 Game View instead of its 640x480 default.
        var assembly = typeof(EditorWindow).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance").GetValue(null);
        var currentGroup = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { currentGroup });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { Enum.Parse(modeType, "FixedResolution"), 1280, 720, "Tutorial audit 1280x720" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        // The index setter alone does not refresh the player display size.
        viewType.GetMethod("SizeSelectionCallback").Invoke(view, new object[] { count - 1, null });
        view.Repaint();
    }

    static IEnumerator DragCoasterWithMouse(Mouse mouse, Vector2 from, Vector2 to, StoryCoasterDropTarget target)
    {
        var module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
        var uiActions = module.point.action.actionMap.asset;
        var previousDevices = uiActions.devices;
        // Keep the editor's physical mouse from replacing the synthetic test pointer.
        // Queue events for the real player loop; manual editor updates use a different input buffer.
        uiActions.devices = new InputDevice[] { mouse };
        try
        {
            var pointer = new PointerEventData(EventSystem.current) { position = from };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "coaster supply screen point " + from + " / screen " + Screen.width + "x" + Screen.height);
            var coaster = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject)?.GetComponent<CoasterDragItem>();
            Assert.IsNotNull(coaster, "actual supply box raycast reaches a draggable coaster");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from });
            yield return null;
            yield return null;
            Assert.AreEqual(coaster.gameObject,
                ExecuteEvents.GetEventHandler<IDragHandler>(module.GetLastRaycastResult(mouse.deviceId).gameObject),
                "the game UI module points at the actual coaster supply");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = from }.WithButton(MouseButton.Left));
            yield return null;
            for (int i = 1; i <= 12; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, i / 12f) }
                    .WithButton(MouseButton.Left));
                yield return null;
            }
            Assert.IsFalse(coaster.GetComponent<CanvasGroup>().blocksRaycasts, "actual mouse input began the coaster drag");
            pointer.position = to;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(target.gameObject, ExecuteEvents.GetEventHandler<IDropHandler>(hits[0].gameObject),
                "tabletop raycast at " + to + ": " + string.Join(", ", hits.Select(hit => hit.gameObject.name)) +
                "; target enabled=" + target.GetComponent<Image>().raycastTarget);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = to });
            yield return null;
        }
        finally { uiActions.devices = previousDevices; }
    }

    static void SavePreview(string name)
    {
        string directory = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera sceneCamera = Camera.main;
        var previewObject = new GameObject("Tutorial Preview Camera");
        Camera camera = previewObject.AddComponent<Camera>();
        camera.CopyFrom(sceneCamera);
        camera.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
        camera.enabled = false;
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = (float)Screen.width / Screen.height;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var modes = canvases.Select(canvas => (canvas.worldCamera, canvas.planeDistance)).ToArray();
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var render = new RenderTexture(Screen.width, Screen.height, 24);
        var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = modes[i].worldCamera;
                canvases[i].planeDistance = modes[i].planeDistance;
            }
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(render);
            UnityEngine.Object.DestroyImmediate(previewObject);
            Canvas.ForceUpdateCanvases();
        }
    }

    static IEnumerator ExploreSeatsWithKeyboard(Keyboard keyboard, Mouse mouse)
    {
        var tutorial = Find<StoryTutorialController>();
        var playerInput = Find<PlayInputHandler>().GetComponent<PlayerInput>();
        Assert.IsTrue(playerInput.isActiveAndEnabled, "Play scene input remains active during dialogue");
        Assert.IsTrue(playerInput.user.valid, "PlayerInput paired devices at scene entry");
        playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        Assert.AreEqual("Play", playerInput.currentActionMap.name);
        Assert.IsTrue(playerInput.inputIsActive);
        Assert.IsTrue(playerInput.actions.FindAction("Play/Left").enabled);
        StringAssert.Contains("Q: 왼쪽 이동", Find<StoryTutorialView>().Instruction);
        StringAssert.Contains("E: 오른쪽 이동", Find<StoryTutorialView>().Instruction);
        Assert.AreEqual(ESlotType.Right, Field<ESlotType>(tutorial, "currentSeat"));
        yield return PressSeatKey(keyboard, Key.E, ESlotType.Right); // Right boundary.
        Assert.AreEqual("seatExplore", tutorial.ActiveLesson);
        yield return PressSeatKey(keyboard, Key.Q, ESlotType.Middle);
        yield return PressSeatKey(keyboard, Key.Q, ESlotType.Left);
        yield return PressSeatKey(keyboard, Key.Q, ESlotType.Left); // Left boundary.
        Assert.AreEqual("seatExplore", tutorial.ActiveLesson);
        yield return PressSeatKey(keyboard, Key.E, ESlotType.Middle);
        yield return PressSeatKey(keyboard, Key.E, ESlotType.Right);
        yield return WaitFor(() => tutorial.ActiveLesson != "seatExplore", "Q/E return to Chris");
    }

    static IEnumerator PressSeatKey(Keyboard keyboard, Key key, ESlotType expected)
    {
        var tutorial = Find<StoryTutorialController>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();
        Assert.IsTrue(keyboard[key].isPressed, "queued key reaches the game input state");
        var action = Find<PlayInputHandler>().GetComponent<PlayerInput>().actions
            .FindAction(key == Key.Q ? "Play/Left" : "Play/Right");
        Assert.IsTrue(action.IsPressed(), "Play input map receives the Q/E binding");
        yield return WaitFor(() => !Field<bool>(tutorial, "cameraMoving"), "seat camera movement");
        Assert.AreEqual(expected, Field<ESlotType>(tutorial, "currentSeat"), key + " moves one adjacent seat");
        Assert.AreEqual(expected, Find<StoryTutorialView>().SeatHud.SelectedSeat, "HUD ring follows the viewed seat");
        if (expected == ESlotType.Middle)
        {
            var characters = Find<DialogueCharacterManager>();
            Assert.IsTrue(characters.TryGetSlotX(ESlotType.Left, out float left));
            Assert.IsTrue(characters.TryGetSlotX(ESlotType.Right, out float right));
            var anchor = Field<Transform>(Find<CameraControllerNew>(), "cameraAnchor");
            Assert.That(anchor.position.x, Is.EqualTo((left + right) * .5f).Within(.01f), "camera reaches the empty middle seat");
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
        Assert.IsFalse(Field<bool>(tutorial, "cameraMoving"), "key release does not start another movement");
        Assert.AreEqual(expected, Field<ESlotType>(tutorial, "currentSeat"));
    }

    static CraftPrepShelfSlot Shelf(CraftPrepStageScreen stage, string id) =>
        stage.GetComponentsInChildren<CraftPrepShelfSlot>(true).Single(slot => slot.ItemId == id);

    static void ClickButton(Button button) =>
        ClickAt(RectTransformUtility.WorldToScreenPoint(null, button.transform.position), button);

    static void ClickAt(Vector2 point, Button expected)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.IsNotEmpty(hits, "click must reach a visible UI target");
        Assert.AreEqual(expected.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
            "notice/lesson button receives the actual screen click, including its label");
        ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(expected.gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Lesson(string id) => WaitFor(() => Find<StoryTutorialController>().ActiveLesson == id, "lesson " + id);

    static IEnumerator DriveDialogueUntil(Func<bool> done)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!done() && Time.realtimeSinceStartup < deadline)
        {
            var choice = Field<List<ChoicePanel>>(Find<UIDialogueChoiceView>(), "choicePanels")
                .FirstOrDefault(panel => panel.gameObject.activeInHierarchy && panel.GetButton().interactable);
            if (choice != null) choice.GetButton().onClick.Invoke(); // First option repeats the tutorial.
            else Find<StoryFlow>().TryAdvance();
            yield return null;
        }
        Assert.IsTrue(done(), "clickable dialogue did not reach the next tutorial UI");
        yield return null;
    }

    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    static IEnumerator WaitFor(Func<bool> ready, string stage)
    {
        float deadline = Time.realtimeSinceStartup + 30;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), stage + " timed out");
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        DOTween.Clear(true);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
}
