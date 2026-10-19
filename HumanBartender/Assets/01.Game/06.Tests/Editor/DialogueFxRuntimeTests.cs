using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

public sealed class DialogueFxRuntimeTests
{
    [UnityTest]
    [Explicit("Run alone: enters Play Mode with an empty scene and the real CSV, text player and camera pipeline.")]
    public IEnumerator CsvDialogueMovesGlyphsAndShakesTheOutputCameraOnlyForShake()
    {
        DOTween.Clear(true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("CSV dialogue runtime", typeof(RectTransform), typeof(Canvas));
        IObjectResolver container = null;
        UIDialogueTextView view = null;
        try
        {
            var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
            var steps = catalog.Read<NewDayScriptBase>("script/bar/day0").Scenes.SelectMany(scene => scene.Steps).ToArray();
            var shake = steps.First(step => step.Text.HasValue && step.Text.Value.Ko.Contains("<fx=reproach>"));
            var soft = steps.First(step => step.Text.HasValue && step.Text.Value.Ko.Contains("<fx=gentle>"));
            var bubble = CreateBubble(root.transform, "CSV bubble");
            view = root.AddComponent<UIDialogueTextView>();
            view.lunaSpeechBubble = bubble;
            var viewSettings = new SerializedObject(view);
            viewSettings.FindProperty("defaultTypingDelay").floatValue = 0f;
            viewSettings.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Output camera", typeof(Camera), typeof(CinemachineBrain));
            cameraObject.transform.SetParent(root.transform, false);
            var output = cameraObject.GetComponent<Camera>();
            output.orthographic = true;
            var brain = cameraObject.GetComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var vcamObject = new GameObject("CM_GamePlay", typeof(CinemachineCamera), typeof(VerticalCameraImpulse));
            vcamObject.transform.SetParent(root.transform, false);
            vcamObject.transform.position = new Vector3(2, 3, -10);
            var impulse = vcamObject.GetComponent<VerticalCameraImpulse>();
            var impulseSettings = new SerializedObject(impulse);
            // Keep the pulse observable even on a slow CI frame; the authored duration is checked in EditMode.
            impulseSettings.FindProperty("duration").floatValue = 0.5f;
            impulseSettings.ApplyModifiedPropertiesWithoutUndo();
            var builder = new ContainerBuilder();
            builder.RegisterInstance(view);
            builder.RegisterInstance<ICameraImpulse>(impulse);
            builder.RegisterEntryPoint<DialogueCameraFeedback>(Lifetime.Scoped);
            container = builder.Build();
            int cues = 0, firstShakeGlyph = -1;
            view.ShakeSpanRevealed += () => { cues++; firstShakeGlyph = bubble.textLabel.maxVisibleCharacters - 1; };
            yield return null;
            brain.ManualUpdate();
            Vector3 baseline = output.transform.position;

            var playback = view.StartType(new TypingData(shake.Text.Value.Ko, shake.Actor, Vector3.zero, Color.white, true));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (cues == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(cues, Is.EqualTo(1), "The authored reproach phrase must emit a real reveal cue.");
            brain.ManualUpdate();
            Vector3 shaken = output.transform.position;
            Assert.That(shaken.y - baseline.y, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(shaken.x, Is.EqualTo(baseline.x).Within(0.001f));
            Assert.That(shaken.z, Is.EqualTo(baseline.z).Within(0.001f));
            Assert.That(bubble.textLabel.text, Does.Not.Contain("<fx="), "Use presets, not readable fallback.");
            var glyph = bubble.textLabel.textInfo.characterInfo[firstShakeGlyph];
            Vector3 before = bubble.textLabel.textInfo.meshInfo[glyph.materialReferenceIndex].vertices[glyph.vertexIndex];
            yield return new WaitForSeconds(0.04f);
            Vector3 after = bubble.textLabel.textInfo.meshInfo[glyph.materialReferenceIndex].vertices[glyph.vertexIndex];
            Assert.That(Vector3.Distance(before, after), Is.GreaterThan(0.001f), "The CSV phrase must move its TMP vertices in Play Mode.");
            view.OnScreenClick();
            yield return playback.ToCoroutine();
            brain.ManualUpdate();
            Vector3 stopped = output.transform.position;
            Assert.That(stopped.y, Is.EqualTo(baseline.y).Within(0.001f), "Skip stops the camera pulse immediately.");

            var gentle = view.StartType(new TypingData(soft.Text.Value.Ko, soft.Actor, Vector3.zero, Color.white, true));
            yield return gentle.ToCoroutine();
            brain.ManualUpdate();
            Assert.That(cues, Is.EqualTo(1), "A gentle wave must not request a camera shake.");
            Assert.That(output.transform.position.y, Is.EqualTo(baseline.y).Within(0.001f));
            Assert.That(bubble.textLabel.text, Does.Not.Contain("<fx="));
            string evidence = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                shakeDialogueId = shake.DialogueId,
                gentleDialogueId = soft.DialogueId,
                cues,
                baseline = new[] { baseline.x, baseline.y, baseline.z },
                shaken = new[] { shaken.x, shaken.y, shaken.z },
                stopped = new[] { stopped.x, stopped.y, stopped.z },
                glyphMotion = Vector3.Distance(before, after),
                cameraPulseStoppedOnSkip = true,
                gentleDoesNotShakeCamera = true
            }, Newtonsoft.Json.Formatting.Indented);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/dialogue-fx-20261017"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "runtime-evidence.json"), evidence);
            Debug.Log("[DialogueFX Runtime] CSV reveal, animated glyphs, output-camera pulse, skip and gentle line passed. " + evidence);
        }
        finally
        {
            view?.StopTyping();
            container?.Dispose();
            UnityEngine.Object.Destroy(root);
            DOTween.Clear(true);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    [Explicit("Run in isolation: unrelated scene DOTween cancellation can fail during EnterPlayMode in the full EditMode batch.")]
    public IEnumerator PlayModeOneGlyphAndIndependentBubbleSessions()
    {
        // EnterPlayMode tears down any editor scene. Remove leftover editor tweens first;
        // their cancellation callbacks are unrelated to this empty-scene text test.
        DOTween.Clear(true);
        Debug.Log("[DialogueFX Runtime] before empty scene");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("[DialogueFX Runtime] before EnterPlayMode");
        yield return new EnterPlayMode();
        Debug.Log("[DialogueFX Runtime] after EnterPlayMode");
        var canvas = new GameObject("DialogueFX runtime canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            DynamicSpeechBubble first = CreateBubble(canvas.transform, "first");
            DynamicSpeechBubble second = CreateBubble(canvas.transform, "second");
            var data = new TypingData("가<color=#FF5555><size=130%><pop><shake>또</shake></pop></size></color>나",
                "first", Vector3.zero, Color.white, true);
            UniTask playback = first.TextPlayer.PlayAsync(data, null, 0.5f);
            second.SetText("다<wave>른</wave>말", null);
            int secondId = second.TextPlayer.CurrentSessionId;
            yield return null;
            first.TextPlayer.Skip();
            yield return playback.ToCoroutine();
            Assert.That(first.TextPlayer.IsHolding, Is.True);
            Assert.That(first.textLabel.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(second.TextPlayer.CurrentSessionId, Is.EqualTo(secondId));
            Assert.That(second.textLabel.text, Is.EqualTo("다른말"));
            first.textLabel.ForceMeshUpdate(true, true);
            var info = first.textLabel.textInfo;
            var middle = info.characterInfo[1];
            var vertices = info.meshInfo[middle.materialReferenceIndex].vertices;
            Vector3 before = vertices[middle.vertexIndex];
            yield return new WaitForSeconds(0.03f);
            Vector3 after = vertices[middle.vertexIndex];
            Assert.That(after, Is.Not.EqualTo(before));
            first.TextPlayer.Stop();
            Assert.That(second.TextPlayer.CurrentSessionId, Is.EqualTo(secondId));

            using var external = new CancellationTokenSource();
            UniTask cancelled = first.TextPlayer.PlayAsync(data, null, 0.5f, external.Token);
            external.Cancel();
            yield return ExpectCancellation(cancelled).ToCoroutine();
            Assert.That(first.TextPlayer.CurrentSessionId, Is.Zero);
            Assert.That(first.textLabel.maxVisibleCharacters, Is.LessThan(3));

            UniTask replaced = first.TextPlayer.PlayAsync(data, null, 0.5f);
            first.SetText("새 문장");
            int replacementId = first.TextPlayer.CurrentSessionId;
            yield return ExpectCancellation(replaced).ToCoroutine();
            Assert.That(first.TextPlayer.CurrentSessionId, Is.EqualTo(replacementId));
            Assert.That(first.textLabel.text, Is.EqualTo("새 문장"));
        }
        finally { UnityEngine.Object.Destroy(canvas); }
        DOTween.Clear(true);
        Debug.Log("[DialogueFX Runtime] before ExitPlayMode");
        yield return new ExitPlayMode();
    }

    static async UniTask ExpectCancellation(UniTask operation)
    {
        try { await operation; }
        catch (OperationCanceledException) { return; }
        Assert.Fail("The replaced or externally cancelled reveal completed normally.");
    }

    static DynamicSpeechBubble CreateBubble(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(400, 100);
        var content = new GameObject("text", typeof(RectTransform), typeof(TextMeshProUGUI));
        content.transform.SetParent(root.transform, false);
        var label = content.GetComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/01.Game/03.Content/05.Fonts/NeoDunggeunmo SDF.asset");
        var title = new GameObject("name", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(root.transform, false);
        var bubble = root.AddComponent<DynamicSpeechBubble>();
        bubble.textLabel = label;
        bubble.nameLabel = title.GetComponent<TextMeshProUGUI>();
        bubble.bubble = rect;
        root.SetActive(true);
        return bubble;
    }
}
