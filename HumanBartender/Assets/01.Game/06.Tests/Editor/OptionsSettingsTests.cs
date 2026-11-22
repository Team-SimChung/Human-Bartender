using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public sealed class OptionsSettingsTests
{
    readonly Dictionary<string, int?> preferences = new();
    static readonly string[] GraphicsKeys = new[] { "FillStretch", "VSync", "FrameRateIndex", "PostProcessing" }.Concat(GraphicsEffectOptions.Keys).ToArray();

    [SetUp]
    public void SavePreferences()
    {
        preferences.Clear();
        foreach (string key in GameKeyBindings.Bindings.Select(b => GameKeyBindings.PreferenceKey(b.Control)).Concat(GraphicsKeys))
        {
            preferences[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
            PlayerPrefs.DeleteKey(key);
        }
    }

    [TearDown]
    public void RestorePreferences()
    {
        foreach (var pair in preferences)
            if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
            else PlayerPrefs.DeleteKey(pair.Key);
        PlayerPrefs.Save();
    }

    [TestCase(1280, 720, 1280, 720)]
    [TestCase(1920, 1080, 1920, 1080)]
    [TestCase(2560, 1440, 2560, 1440)]
    [TestCase(3840, 2160, 3840, 2160)]
    [TestCase(3440, 1440, 2560, 1440)]
    [TestCase(1920, 1200, 1920, 1080)]
    [TestCase(1280, 1024, 1280, 720)]
    [TestCase(1080, 1920, 1072, 603)]
    [TestCase(1366, 768, 1360, 765)]
    public void FullscreenFitsSixteenByNineInsideDisplay(int width, int height, int expectedWidth, int expectedHeight)
    {
        var size = DisplaySettings.GetFullscreenSize(width, height);
        Assert.That(size, Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
        Assert.That(size.x * 9, Is.EqualTo(size.y * 16), "the render target must include Overlay UI in the same 16:9 area");
        Assert.That(size.x, Is.LessThanOrEqualTo(width));
        Assert.That(size.y, Is.LessThanOrEqualTo(height));
    }

    [Test]
    public void WindowSizesAndPlayerSettingsKeepSixteenByNine()
    {
        foreach (var size in new DisplaySettings().WindowedSizes)
            Assert.That(size.x * 9, Is.EqualTo(size.y * 16), $"window size {size} must be 16:9");
        Assert.That(PlayerSettings.defaultScreenWidth * 9, Is.EqualTo(PlayerSettings.defaultScreenHeight * 16));
        Assert.That(PlayerSettings.defaultIsNativeResolution, Is.False, "startup must not adopt a non-16:9 monitor resolution");
        Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
        Assert.That(PlayerSettings.resizableWindow, Is.True, "window resizing must remain available");
        Assert.That(PlayerSettings.allowFullscreenSwitch, Is.True, "Alt+Enter must remain available");
    }

    [TestCase(1600, 720, 1600, 900)]
    [TestCase(1280, 900, 1600, 900)]
    [TestCase(960, 720, 960, 540)]
    [TestCase(1280, 540, 960, 540)]
    [TestCase(1599, 899, 1600, 900)]
    public void WindowResizeFollowsTheDraggedAxis(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.That(DisplaySettings.GetResizedWindowSize(new Vector2Int(width, height), new Vector2Int(1280, 720)),
            Is.EqualTo(new Vector2Int(expectedWidth, expectedHeight)));
    }

    [Test]
    public void DragSettlesBeforeCorrectionAndDoesNotRepeatOurResolutionRequest()
    {
        var state = new DisplaySettings.WindowState(new Vector2Int(1280, 720));
        var display = new Vector2Int(1920, 1080);
        Assert.That(state.TryUpdate(new Vector2Int(1280, 720), false, display, 0, out _), Is.False);
        Assert.That(state.TryUpdate(new Vector2Int(1400, 720), false, display, 1, out _), Is.False);
        Assert.That(state.TryUpdate(new Vector2Int(1600, 720), false, display, 2, out _), Is.False);
        Assert.That(state.TryUpdate(new Vector2Int(1600, 720), false, display, 3, out var corrected), Is.True);
        Assert.That(corrected, Is.EqualTo(new Vector2Int(1600, 900)));
        state.RecordRequest(corrected, false, 3);
        Assert.That(state.TryUpdate(new Vector2Int(1600, 720), false, display, 3, out _), Is.False);
        Assert.That(state.TryUpdate(corrected, false, display, 4, out _), Is.False);
        Assert.That(state.TryUpdate(corrected, false, display, 5, out _), Is.False);
        Assert.That(state.WindowedSize, Is.EqualTo(corrected));
    }

    [Test]
    public void AltEnterFitsUltrawideAndRestoresTheLastWindowSize()
    {
        var window = new Vector2Int(1600, 900);
        var display = new Vector2Int(3440, 1440);
        var state = new DisplaySettings.WindowState(window);
        state.TryUpdate(window, false, display, 0, out _);
        Assert.That(state.TryUpdate(display, true, display, 1, out var fullscreen), Is.True);
        Assert.That(state.FullscreenChanged, Is.True);
        Assert.That(fullscreen, Is.EqualTo(new Vector2Int(2560, 1440)));
        state.RecordRequest(fullscreen, true, 1);
        Assert.That(state.TryUpdate(fullscreen, true, display, 2, out _), Is.False);
        Assert.That(state.FullscreenChanged, Is.False);
        Assert.That(state.TryUpdate(new Vector2Int(1280, 720), false, display, 3, out var restored), Is.True);
        Assert.That(state.FullscreenChanged, Is.True);
        Assert.That(restored, Is.EqualTo(window));
    }

    [Test]
    public void OwnModeChangesAndMinimizedWindowDoNotTriggerAnotherSwitch()
    {
        var state = new DisplaySettings.WindowState(new Vector2Int(1280, 720));
        var display = new Vector2Int(1920, 1200);
        var fullscreen = new Vector2Int(1920, 1080);
        state.RecordRequest(fullscreen, true, 0);
        Assert.That(state.TryUpdate(new Vector2Int(1280, 720), false, display, 0, out _), Is.False);
        Assert.That(state.TryUpdate(fullscreen, true, display, 1, out _), Is.False);
        Assert.That(state.FullscreenChanged, Is.False);
        Assert.That(state.TryUpdate(Vector2Int.zero, true, display, 2, out _), Is.False);
        Assert.That(state.TryUpdate(fullscreen, true, display, 3, out _), Is.False);
    }

    [Test]
    public void RebindingUpdatesRealActionsKeepsArrowAndGamepadAlternativesAndSurvivesNewAssets()
    {
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/01.Game/05.Settings/01.Input/PlayerInput.inputactions");
        var asset = Object.Instantiate(source);
        InputActionAsset nextScene = null;
        try
        {
            Assert.That(GameKeyBindings.TrySetKey(GameControl.MoveLeft, Key.J, out _), Is.True);
            Assert.That(GameKeyBindings.TrySetKey(GameControl.Interact, Key.F, out _), Is.True);
            Assert.That(GameKeyBindings.TrySetKey(GameControl.CapPress, Key.C, out _), Is.True);
            GameKeyBindings.ApplyTo(asset);
            var move = asset.FindAction("Outside/Move");
            Assert.That(move.bindings.Any(b => b.effectivePath == "<Keyboard>/j" && b.name == "left"), Is.True);
            Assert.That(move.bindings.Any(b => b.effectivePath == "<Keyboard>/leftArrow"), Is.True);
            Assert.That(move.bindings.Any(b => b.effectivePath == "<Gamepad>/leftStick"), Is.True);
            Assert.That(asset.FindAction("Outside/Interact").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f"));
            Assert.That(asset.FindAction("CraftGimmick/CapPress").bindings.Any(b => b.effectivePath == "<Mouse>/leftButton"), Is.True);
            Assert.That(asset.FindAction("CraftGimmick/CapPress").bindings.Any(b => b.effectivePath == "<Keyboard>/c"), Is.True);
            nextScene = Object.Instantiate(source);
            GameKeyBindings.ApplyTo(nextScene);
            Assert.That(nextScene.FindAction("Outside/Interact").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f"));
            GameKeyBindings.Reset(); GameKeyBindings.ApplyTo(asset);
            Assert.That(asset.FindAction("Outside/Interact").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));
        }
        finally { Object.DestroyImmediate(asset); if (nextScene != null) Object.DestroyImmediate(nextScene); }
    }

    [Test]
    public void DuplicateKeysAreRejectedWithinTheirContextAndEscapeIsReserved()
    {
        Assert.That(GameKeyBindings.TrySetKey(GameControl.MoveLeft, Key.D, out var message), Is.False);
        Assert.That(message, Does.Contain("이동 · 오른쪽"));
        Assert.That(GameKeyBindings.GetKey(GameControl.MoveLeft), Is.EqualTo(Key.A));
        Assert.That(GameKeyBindings.TrySetKey(GameControl.Interact, Key.Escape, out _), Is.False);
        Assert.That(GameKeyBindings.TrySetKey(GameControl.Interact, Key.RightArrow, out _), Is.False);
        Assert.That(GameKeyBindings.TrySetKey(GameControl.ShakeLeft, Key.F, out _), Is.True);
        Assert.That(GameKeyBindings.TrySetKey(GameControl.Interact, Key.F, out _), Is.True, "different gameplay contexts may share keys");
    }

    [Test]
    public void GraphicsApplyToCamerasAndRestoreAuthoredPostProcessing()
    {
        int vsync = QualitySettings.vSyncCount, frameRate = Application.targetFrameRate;
        var root = new GameObject("settings test camera", typeof(Camera), typeof(UniversalAdditionalCameraData), typeof(PixelPerfectCamera));
        var settings = new DisplaySettings();
        var data = root.GetComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
        try
        {
            settings.SetPostProcessing(false);
            Assert.That(data.renderPostProcessing, Is.False);
            settings.SetPostProcessing(true);
            Assert.That(data.renderPostProcessing, Is.True);
            settings.SetFillMode(false);
            Assert.That(root.GetComponent<PixelPerfectCamera>().cropFrame, Is.EqualTo(PixelPerfectCamera.CropFrame.Windowbox));
            settings.SetVSync(false); settings.SetFrameRate(2);
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Assert.That(Application.targetFrameRate, Is.EqualTo(120));
            Assert.That(PlayerPrefs.GetInt("FrameRateIndex"), Is.EqualTo(2));
            int changes = 0;
            System.Action count = () => changes++;
            GraphicsEffectOptions.Changed += count;
            var values = GraphicsEffectOptions.Values.Default;
            values.Bloom = GraphicsEffectOptions.BloomLevel.Off; values.PuddleReflections = false;
            settings.SetEffects(values);
            GraphicsEffectOptions.Changed -= count;
            Assert.That(changes, Is.EqualTo(1), "effect components must hear the applied options");
            Assert.That(GraphicsEffectOptions.Current.Bloom, Is.EqualTo(GraphicsEffectOptions.BloomLevel.Off));
            Assert.That(GraphicsEffectOptions.PuddleReflections, Is.False);
            var bloomVolume = Resources.FindObjectsOfTypeAll<UnityEngine.Rendering.Volume>().Single(v => v.priority >= 1000);
            Assert.That(bloomVolume.sharedProfile.TryGet(out Bloom bloom) && bloom.intensity.overrideState && bloom.intensity.value == 0, Is.True, "bloom off must override scene bloom to zero");
            settings.SetEffects(GraphicsEffectOptions.Values.Default);
            Assert.That(bloomVolume == null, Is.True, "normal bloom keeps the scene's authored value");
        }
        finally
        {
            settings.Dispose(); QualitySettings.vSyncCount = vsync; Application.targetFrameRate = frameRate;
            Object.DestroyImmediate(root);
        }
    }
}
