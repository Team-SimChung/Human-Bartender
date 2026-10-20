using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class VisualUIReviewTests
{
    const string ScopePath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    const string ChoicePath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/ChoicePanel.prefab";
    static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    static void Inject(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);

    [Test]
    public void CommonOptionsKeepsItsWidgetsAppliesDisplayExplicitlyAndRestoresFocus()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ScopePath), scene);
        var events = new GameObject("UI review EventSystem", typeof(EventSystem));
        var previous = new GameObject("previous selection", typeof(RectTransform), typeof(Button));
        var previousEvents = EventSystem.current;
        typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(events.GetComponent<EventSystem>(), null);
        EventSystem.current = events.GetComponent<EventSystem>();
        try
        {
            var options = instance.GetComponentInChildren<UIDisplayOptions>(true);
            typeof(UIDisplayOptions).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(options, null);
            var display = new DisplaySpy(); var audio = new AudioSpy();
            Inject(options, "_display", display); Inject(options, "_sound", audio);
            typeof(UIDisplayOptions).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(options, null);
            var bgm = Ref<Slider>(options, "bgmSlider"); var se = Ref<Slider>(options, "seSlider");
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(PrefabUtility.GetCorrespondingObjectFromSource(bgm), out string _, out long id), Is.True);
            Assert.That(id, Is.EqualTo(8568369260507963241L), "existing slider references must survive the layout migration");
            events.GetComponent<EventSystem>().SetSelectedGameObject(previous);
            options.Show();
            var overlay = Ref<GameObject>(options, "optionUI"); var view = overlay.GetComponentInChildren<OptionsPanelView>(true);
            Assert.That(UIDisplayOptions.IsOptionOpen, Is.True);
            Assert.That(bgm.value, Is.EqualTo(.42f).Within(.001)); Assert.That(se.value, Is.EqualTo(.63f).Within(.001));
            Assert.That(bgm.transform.IsChildOf(overlay.transform), Is.True);
            view.SelectTab(1);
            Ref<Toggle>(options, "windowedToggle").isOn = true;
            Ref<TMP_Dropdown>(options, "windowSizeDropdown").value = 1;
            Assert.That(display.ApplyCalls, Is.Zero, "editing a draft must not change the actual screen");
            options.OnApplyClicked();
            Assert.That(display.Fullscreen, Is.False); Assert.That(display.Index, Is.EqualTo(1));
            options.ResetSound();
            Assert.That(audio.Bgm, Is.EqualTo(.3f).Within(.001)); Assert.That(audio.Se, Is.EqualTo(.8f).Within(.001));
            view.SelectTab(0); Capture(options.GetComponent<Canvas>(), "settings-sound", 1280, 720);
            view.SelectTab(1); Capture(options.GetComponent<Canvas>(), "settings-game", 1920, 1080);
            view.SelectTab(2); Capture(options.GetComponent<Canvas>(), "settings-controls", 1024, 768);
            options.Hide();
            Assert.That(UIDisplayOptions.IsOptionOpen, Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(previous));
        }
        finally
        {
            typeof(EventSystem).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(events.GetComponent<EventSystem>(), null);
            Object.DestroyImmediate(previous); Object.DestroyImmediate(events);
            if (previousEvents != null) EventSystem.current = previousEvents;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void MainMenuHasRealSlotControlsAndSeparatesTheLogoEffectFromBackdrop()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Additive);
        try
        {
            var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TitleMenuView>(true)).Single();
            Assert.That(Ref<MainManager>(view, "manager"), Is.Not.Null);
            foreach (string field in new[] { "newGameButton", "continueButton", "settingsButton", "quitButton", "closeSlotsButton" }) Assert.That(Ref<Button>(view, field), Is.Not.Null, field);
            var data = new SerializedObject(view);
            Assert.That(data.FindProperty("slotButtons").arraySize, Is.EqualTo(SaveManager.SlotCount));
            var logo = view.GetComponentInChildren<TitleLogoGlitch>(true);
            Assert.That(logo.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(view.GetComponentInParent<Canvas>().GetComponentsInChildren<TitleLogoGlitch>(true).Length, Is.EqualTo(1));
            var first = Ref<Button>(view, "newGameButton");
            var focusEvent = new BaseEventData(EventSystem.current); first.OnSelect(focusEvent);
            var fx = first.GetComponent<UIChoiceGlitch>();
            typeof(UIChoiceGlitch).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
            fx.OnSelect(focusEvent);
            Capture(view.GetComponentInParent<Canvas>(), "title", 1280, 720);
            Capture(view.GetComponentInParent<Canvas>(), "title-narrow", 1024, 768);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void HoldVisualStateStopsOnReleaseExitDisableAndFocusLossWithoutChangingTiming()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath));
        var events = new GameObject("Choice events", typeof(EventSystem));
        try
        {
            var hold = root.GetComponent<ChoiceHoldButton>();
            var changes = new System.Collections.Generic.List<bool>(); hold.HoldingChanged += changes.Add;
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            hold.OnPointerDown(pointer); Assert.That(hold.IsHolding, Is.True);
            hold.OnPointerUp(pointer); Assert.That(hold.IsHolding, Is.False);
            hold.OnPointerDown(pointer); hold.OnPointerExit(pointer); Assert.That(hold.IsHolding, Is.False);
            hold.OnPointerDown(pointer); root.SetActive(false); Assert.That(hold.IsHolding, Is.False);
            root.SetActive(true); hold.OnPointerDown(pointer); typeof(ChoiceHoldButton).GetMethod("OnApplicationFocus", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hold, new object[] { false }); Assert.That(hold.IsHolding, Is.False);
            Assert.That(changes.ToArray(), Is.EqualTo(new[] { true, false, true, false, true, false, true, false }));
            Assert.That(new SerializedObject(hold).FindProperty("holdDuration").floatValue, Is.EqualTo(1));
            Assert.That(root.GetComponent<UIChoiceGlitch>(), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<TMP_Text>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Text>(), Is.Empty);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
    }

    [Test]
    public void ServiceAndRecipeControlsKeepTheirOwnersAndUseCurrentArt()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity", OpenSceneMode.Additive);
        try
        {
            var service = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ServicePanelController>(true)).Single();
            foreach (string field in new[] { "toggleButton", "closeButton", "cocktailRecipeButton" })
            {
                var button = Ref<Button>(service, field);
                Assert.That(AssetDatabase.GetAssetPath(button.GetComponent<Image>().sprite), Does.Contain("/service_panel/"));
            }
            var screen = Ref<RecipeBrowserScreen>(service, "recipeScreen");
            Assert.That(AssetDatabase.GetAssetPath(Ref<Button>(screen, "closeButton").GetComponent<Image>().sprite), Does.Contain("/recipe_choice/"));
            var row = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/01.Game/03.Content/08.Prefabs/01.Crafting/04.Recipes/CocktailRecipeRow.prefab");
            Assert.That(row.GetComponent<CocktailRecipeRow>(), Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(row.GetComponent<Image>().sprite), Does.Contain("/recipe_choice/"));
            Ref<GameObject>(service, "overlay").SetActive(true); Ref<GameObject>(service, "serviceMenuPanel").SetActive(true);
            Capture(service.GetComponent<Canvas>(), "service-panel", 1280, 720);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator RuntimeMenuWiringAndGlitchRestoreUseTheActualUnityLifecycle()
    {
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return CheckRuntimeViews();
        yield return new ExitPlayMode();
    }

    static IEnumerator CheckRuntimeViews()
    {
        yield return null;
        var view = Object.FindFirstObjectByType<TitleMenuView>();
        var options = Object.FindFirstObjectByType<UIDisplayOptions>();
        Assert.That(view, Is.Not.Null); Assert.That(options, Is.Not.Null);
        var audioField = typeof(UIDisplayOptions).GetField("_sound", BindingFlags.NonPublic | BindingFlags.Instance);
        var previousAudio = audioField.GetValue(options); var audio = new AudioSpy(); audioField.SetValue(options, audio);
        var settings = Ref<Button>(view, "settingsButton");
        EventSystem.current.SetSelectedGameObject(settings.gameObject);
        settings.onClick.Invoke();
        Assert.That(UIDisplayOptions.IsOptionOpen, Is.True);
        Ref<Slider>(options, "bgmSlider").value = .51f;
        Ref<Slider>(options, "seSlider").value = .71f;
        Assert.That(audio.Bgm, Is.EqualTo(.51f).Within(.001), "the preserved persistent event must reach the injected audio service");
        Assert.That(audio.Se, Is.EqualTo(.71f).Within(.001));
        var panel = Ref<GameObject>(options, "optionUI").GetComponentInChildren<OptionsPanelView>();
        Capture(options.GetComponent<Canvas>(), "runtime-settings", 1280, 720);
        panel.SelectTab(1);
        var windowed = Ref<Toggle>(options, "windowedToggle"); windowed.isOn = true;
        Assert.That(windowed.navigation.selectOnDown, Is.EqualTo(Ref<TMP_Dropdown>(options, "windowSizeDropdown")), "enabling windowed mode must make its size selector reachable by keyboard");
        options.ResetDisplayDraft();
        Assert.That(windowed.navigation.selectOnDown, Is.Not.EqualTo(Ref<TMP_Dropdown>(options, "windowSizeDropdown")));
        Ref<Button>(panel, "backButton").onClick.Invoke();
        Assert.That(UIDisplayOptions.IsOptionOpen, Is.False);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(settings.gameObject));
        audioField.SetValue(options, previousAudio);
        Ref<Button>(view, "continueButton").onClick.Invoke();
        Assert.That(Ref<GameObject>(view, "slotsPanel").activeSelf, Is.True);
        Assert.That(Ref<Button>(view, "newGameButton").interactable, Is.False);
        Assert.That(TitleMenuView.TryCloseForEscape(), Is.True);
        Assert.That(Ref<GameObject>(view, "slotsPanel").activeSelf, Is.False);
        Assert.That(Ref<Button>(view, "newGameButton").interactable, Is.True);
        EventSystem.current.SetSelectedGameObject(Ref<Button>(view, "newGameButton").gameObject);
        yield return new WaitForSecondsRealtime(.35f);
        Capture(view.GetComponentInParent<Canvas>(), "runtime-title", 1280, 720);

        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChoicePath), view.GetComponentInParent<Canvas>().transform);
        var label = root.GetComponentInChildren<TMP_Text>(); label.text = "선택지 홀드 확인";
        yield return null;
        label.ForceMeshUpdate(); var original = (Vector3[])label.textInfo.meshInfo[0].vertices.Clone();
        var hold = root.GetComponent<ChoiceHoldButton>(); var fx = root.GetComponent<UIChoiceGlitch>();
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        hold.OnPointerDown(pointer);
        typeof(UIChoiceGlitch).GetField("startedAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(fx, Time.unscaledTimeAsDouble - .08);
        typeof(UIChoiceGlitch).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
        CollectionAssert.AreNotEqual(original, label.textInfo.meshInfo[0].vertices, "holding should actually distort the rendered glyphs");
        hold.OnPointerUp(pointer);
        CollectionAssert.AreEqual(original, label.textInfo.meshInfo[0].vertices, "release must restore every glyph without waiting for the next frame");
        Object.Destroy(root);
    }

    static void Capture(Canvas canvas, string name, int width, int height)
    {
        string directory = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var cameraGO = new GameObject("UI capture camera", typeof(Camera)); var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5; camera.transform.position = new Vector3(0, 0, -10);
        camera.scene = canvas.gameObject.scene;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .04f, .06f); camera.enabled = false;
        var backgrounds = canvas.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
            .Where(c => c != canvas && c.renderMode == RenderMode.ScreenSpaceCamera).ToArray();
        var backgroundCameras = backgrounds.Select(c => c.worldCamera).ToArray();
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
        var render = new RenderTexture(width, height, 24); var texture = new Texture2D(width, height, TextureFormat.RGB24, false); var active = RenderTexture.active;
        try
        {
            foreach (var background in backgrounds) background.worldCamera = camera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = render;
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < backgrounds.Length; i++) backgrounds[i].worldCamera = backgroundCameras[i];
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
            RenderTexture.active = active; Object.DestroyImmediate(texture); Object.DestroyImmediate(render); Object.DestroyImmediate(cameraGO);
        }
    }

    sealed class DisplaySpy : IDisplaySettings
    {
        public Vector2Int[] WindowedSizes => new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };
        public int ApplyCalls, Index; public bool Fullscreen;
        public void SetWindowedSize(int index) { Index = index; ApplyCalls++; }
        public void SetFullscreen(bool fullscreen) { Fullscreen = fullscreen; ApplyCalls++; }
        public void SetFillMode(bool value) { }
        public void RegisterActiveCamera(UnityEngine.Rendering.Universal.PixelPerfectCamera camera) { }
        public void UnregisterActiveCamera(UnityEngine.Rendering.Universal.PixelPerfectCamera camera) { }
    }
    sealed class AudioSpy : IAudioVolumeSettings
    {
        public float Bgm = .42f, Se = .63f;
        public float GetBGMVolume() => Bgm; public float GetSEVolume() => Se;
        public void SetBGMVolume(float value) => Bgm = value; public void SetSEVolume(float value) => Se = value;
    }
}
