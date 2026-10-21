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
    static readonly string[] GraphicsKeys = { "FillStretch", "VSync", "FrameRateIndex", "PostProcessing" };

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
        }
        finally
        {
            settings.Dispose(); QualitySettings.vSyncCount = vsync; Application.targetFrameRate = frameRate;
            Object.DestroyImmediate(root);
        }
    }
}
