using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class DialogueCameraFeedbackTests
{
    GameObject root;
    DynamicSpeechBubble bubble;
    DialoguePresentationSettings settings;
    object previousSettings;
    float previousTime;
    static readonly FieldInfo SharedSettings = typeof(DialoguePresentationSettings)
        .GetField("shared", BindingFlags.NonPublic | BindingFlags.Static);

    [SetUp]
    public void SetUp()
    {
        previousTime = CinemachineCore.CurrentTimeOverride;
        previousSettings = SharedSettings.GetValue(null);
        settings = ScriptableObject.CreateInstance<DialoguePresentationSettings>();
        settings.Presets = new[] { new DialogueFxPreset
            { Id = "testshake", Motion = DialogueFxMotion.Shake, Amplitude = 0.5f, Frequency = 10f } };
        SharedSettings.SetValue(null, settings);

        root = new GameObject("feedback test", typeof(RectTransform), typeof(Canvas));
        var actor = new GameObject("bubble", typeof(RectTransform));
        actor.SetActive(false);
        actor.transform.SetParent(root.transform, false);
        var text = new GameObject("text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(actor.transform, false);
        var title = new GameObject("title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(actor.transform, false);
        bubble = actor.AddComponent<DynamicSpeechBubble>();
        bubble.bubble = (RectTransform)actor.transform;
        bubble.bubble.sizeDelta = new Vector2(400, 100);
        bubble.textLabel = text.GetComponent<TextMeshProUGUI>();
        bubble.textLabel.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/01.Game/03.Content/08.Fonts/NeoDunggeunmo SDF.asset");
        bubble.nameLabel = title.GetComponent<TextMeshProUGUI>();
        actor.SetActive(true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        SharedSettings.SetValue(null, previousSettings);
        UnityEngine.Object.DestroyImmediate(settings);
        CinemachineCore.CurrentTimeOverride = previousTime;
    }

    static TypingData Data(string text) => new(text, "test", Vector3.zero, Color.white, true);

    [TestCase(DynamicSpeechBubble.BubbleSizeMode.GrowPerCharacter)]
    [TestCase(DynamicSpeechBubble.BubbleSizeMode.PreExpand)]
    [TestCase(DynamicSpeechBubble.BubbleSizeMode.Fixed)]
    public void ResolvedShakeSpansCueAtFirstVisibleGlyphOnly(DynamicSpeechBubble.BubbleSizeMode sizeMode)
    {
        bubble.sizeMode = sizeMode;
        var visible = new List<int>();
        bubble.TextPlayer.ShakeSpanRevealed += () => visible.Add(bubble.textLabel.maxVisibleCharacters);
        bubble.TextPlayer.PlayAsync(Data("a<shake> \nBC<color=#FF0000>D</color></shake>e<fx=testshake> F</fx>g<wave>h</wave><shake amp=0>i</shake><shake hz=0>j</shake>"),
            null, 0f).GetAwaiter().GetResult();
        Assert.That(visible, Is.EqualTo(new[] { 4, 9 }));
        Assert.That(bubble.TextPlayer.IsHolding, Is.True);
    }

    [Test]
    public void SkipFromCueStopsMotionWithoutTriggeringRemainingSpans()
    {
        int cues = 0, stops = 0;
        bubble.TextPlayer.ShakeSpanRevealed += () => { cues++; bubble.TextPlayer.Skip(); };
        bubble.TextPlayer.MotionStopped += () => stops++;
        bubble.TextPlayer.PlayAsync(Data("<shake>ab</shake>c<shake>d</shake>"), null, 0f).GetAwaiter().GetResult();
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(stops, Is.EqualTo(1));
        Assert.That(bubble.textLabel.maxVisibleCharacters, Is.EqualTo(4));
        Assert.That(bubble.TextPlayer.IsHolding, Is.True);
    }

    [Test]
    public void InstantAndReducedMotionNeverEmitCues()
    {
        int cues = 0;
        bubble.TextPlayer.ShakeSpanRevealed += () => cues++;
        bubble.TextPlayer.ShowInstant("<shake>a</shake>");
        settings.ReducedMotion = true;
        bubble.TextPlayer.PlayAsync(Data("<shake>b</shake>"), null, 0f).GetAwaiter().GetResult();
        Assert.That(cues, Is.Zero);
    }

    [Test]
    public void ReplacementDuringCueCannotContinueOldSession()
    {
        int cues = 0;
        bubble.TextPlayer.ShakeSpanRevealed += () => { cues++; bubble.TextPlayer.ShowInstant("replacement"); };
        var task = bubble.TextPlayer.PlayAsync(Data("<shake>a</shake>b<shake>c</shake>"), null, 0f);
        Assert.Throws<OperationCanceledException>(() => task.GetAwaiter().GetResult());
        Assert.That(cues, Is.EqualTo(1));
        Assert.That(bubble.textLabel.text, Is.EqualTo("replacement"));
        Assert.That(bubble.TextPlayer.IsHolding, Is.True);
    }

    [Test]
    public void ExternalCancellationWhileHoldingStopsMotion()
    {
        int stops = 0;
        using var source = new CancellationTokenSource();
        bubble.TextPlayer.MotionStopped += () => stops++;
        bubble.TextPlayer.PlayAsync(Data("<shake>a</shake>"), null, 0f, source.Token).GetAwaiter().GetResult();
        source.Cancel();
        Assert.That(stops, Is.EqualTo(1));
        Assert.That(bubble.TextPlayer.CurrentSessionId, Is.Zero);
    }

    sealed class CameraSpy : ICameraImpulse
    {
        public int Plays, Stops;
        public void PlayVerticalPulse() => Plays++;
        public void StopImpulse() => Stops++;
    }

    [Test]
    public void ScopedRegistrationConnectsViewAndUnsubscribesOnDisposal()
    {
        var view = root.AddComponent<UIDialogueTextView>();
        view.lunaSpeechBubble = bubble;
        var serialized = new SerializedObject(view);
        serialized.FindProperty("defaultTypingDelay").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var spy = new CameraSpy();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(view);
        builder.RegisterInstance<ICameraImpulse>(spy);
        builder.RegisterEntryPoint<DialogueCameraFeedback>(Lifetime.Scoped);
        var container = builder.Build();
        try
        {
            view.StartType(Data("<shake>a</shake>")).GetAwaiter().GetResult();
            view.StartType(Data("<shake>b</shake>")).GetAwaiter().GetResult();
            Assert.That(spy.Plays, Is.EqualTo(2), "one subscription per active player");
            view.StopTyping();
            Assert.That(spy.Stops, Is.GreaterThanOrEqualTo(2));
        }
        finally { container.Dispose(); }
        view.StartType(Data("<shake>c</shake>")).GetAwaiter().GetResult();
        Assert.That(spy.Plays, Is.EqualTo(2), "disposed scene adapter must be detached");
    }

    [Test]
    public void PulseChangesOnlyYAndReturnsToTheLiveCameraState()
    {
        var cameraObject = new GameObject("camera", typeof(CinemachineCamera));
        cameraObject.transform.SetParent(root.transform);
        var camera = cameraObject.GetComponent<CinemachineCamera>();
        var impulse = cameraObject.AddComponent<VerticalCameraImpulse>();
        var sourceLens = camera.Lens;
        CinemachineCore.CurrentTimeOverride = 10f;
        impulse.PlayVerticalPulse();

        CameraState Sample(float now, Vector3 position, float orthographicSize = 3.6f)
        {
            CinemachineCore.CurrentTimeOverride = now;
            var state = CameraState.Default;
            state.RawPosition = position;
            state.PositionCorrection = new Vector3(0.1f, 0.2f, 0.3f);
            var lens = state.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = orthographicSize;
            state.Lens = lens;
            impulse.InvokePostPipelineStageCallback(camera, CinemachineCore.Stage.Noise, ref state, 1f / 60f);
            Assert.That(state.RawPosition, Is.EqualTo(position));
            Assert.That(state.PositionCorrection.x, Is.EqualTo(0.1f));
            Assert.That(state.PositionCorrection.z, Is.EqualTo(0.3f));
            Assert.That(state.OrientationCorrection, Is.EqualTo(Quaternion.identity));
            Assert.That(state.Lens.OrthographicSize, Is.EqualTo(orthographicSize),
                "the pulse must not change the current camera zoom");
            Assert.That(camera.Lens.OrthographicSize, Is.EqualTo(sourceLens.OrthographicSize),
                "the stored camera zoom must stay untouched");
            return state;
        }

        var up = Sample(10f, Vector3.zero);
        Assert.That(up.PositionCorrection.y, Is.EqualTo(0.24f).Within(0.0001f),
            "the hit should start at full strength, with no ease-in");
        Assert.That(Sample(10.03f, Vector3.one).PositionCorrection.y,
            Is.EqualTo(up.PositionCorrection.y).Within(0.0001f), "hold the hit briefly");
        impulse.PlayVerticalPulse(); // repeat requests do not restart or stack
        var rebound = Sample(10.05f, Vector3.one, 3f);
        Assert.That(rebound.PositionCorrection.y,
            Is.EqualTo(0.19f).Within(0.0001f), "one-pixel counter hit");
        var restored = Sample(10.08f, Vector3.one * 2, 3f);
        Assert.That(restored.PositionCorrection.y,
            Is.EqualTo(0.2f), "return abruptly to the current camera pose");
        Assert.That(restored.Lens.OrthographicSize, Is.EqualTo(3f), "return to the current lens");
        Assert.That(Sample(10.12f, Vector3.one * 3).PositionCorrection.y, Is.EqualTo(0.2f));
        impulse.PlayVerticalPulse();
        impulse.StopImpulse();
        Assert.That(Sample(10.17f, Vector3.one * 4).Lens.OrthographicSize, Is.EqualTo(3.6f));
        impulse.PlayVerticalPulse();
        impulse.enabled = false;
        impulse.SendMessage("OnDisable"); // EditMode does not dispatch runtime-only MonoBehaviour callbacks.
        impulse.enabled = true;
        Assert.That(Sample(10.22f, Vector3.one * 5).Lens.OrthographicSize, Is.EqualTo(3.6f));
    }
}
