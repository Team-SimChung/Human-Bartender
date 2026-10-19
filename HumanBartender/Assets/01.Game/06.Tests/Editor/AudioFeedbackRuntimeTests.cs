using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

public sealed class AudioFeedbackRuntimeTests
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    sealed class Recorder : ISoundEffectPlayer
    {
        readonly ISoundEffectPlayer output;
        public readonly List<string> Keys = new();
        public readonly List<float> Times = new();
        public Recorder(ISoundEffectPlayer output) => this.output = output;
        public void PlaySE(string name) { Keys.Add(name); Times.Add(Time.unscaledTime); output.PlaySE(name); }
    }

    [UnityTest]
    [Explicit("Run in isolation: enters Play Mode and checks real AudioSources and shared mixer routing.")]
    public IEnumerator TypingAndOwnedLoopsUseRegisteredClipsAndStopCleanly()
    {
        DOTween.Clear(true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var root = new GameObject("audio runtime", typeof(RectTransform), typeof(Canvas));
        if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            root.AddComponent<AudioListener>();
        AudioMixer mixer = null;
        float previousDb = 0f;
        bool restoreVolume = false;
        IDisposable pour = null, stir = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab")
                .GetComponentInChildren<SoundManager>(true);
            var managerObject = new GameObject("SoundManager");
            managerObject.SetActive(false);
            managerObject.transform.SetParent(root.transform);
            var manager = managerObject.AddComponent<SoundManager>();
            var output = Get<AudioOutputSettings>(prefab, "output");
            mixer = output.GetGroup(AudioBus.SoundEffects).audioMixer;
            restoreVolume = mixer.GetFloat("SEVolume", out previousDb);
            Set(manager, "output", output);
            Set(manager, "seList", Get<List<Sound>>(prefab, "seList"));
            Set(manager, "bgmList", new List<Sound>());
            managerObject.SetActive(true);
            yield return null;
            // Runtime-only mixer override; never modify the user's persisted volume preference.
            mixer.SetFloat("SEVolume", 0f);

            var bubble = CreateBubble(root.transform);
            var recorder = new Recorder(manager);
            bubble.TextPlayer.ConfigureAudio(recorder);
            yield return bubble.TextPlayer.PlayAsync(new TypingData("가 나! A1", "test", Vector3.zero,
                Color.white, true), null, 0.09f).ToCoroutine();
            Assert.That(recorder.Keys, Has.Count.EqualTo(4));
            Assert.That(recorder.Keys.All(key => key == DialoguePresentationSettings.Shared.TypingSoundKey), Is.True);
            for (int i = 1; i < recorder.Times.Count; i++)
                Assert.That(recorder.Times[i] - recorder.Times[i - 1], Is.GreaterThanOrEqualTo(0.059f));
            var skipped = bubble.TextPlayer.PlayAsync(new TypingData("남은 대사", "test", Vector3.zero,
                Color.white, true), null, 0.4f);
            bubble.TextPlayer.Skip();
            yield return skipped.ToCoroutine();
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelled = bubble.TextPlayer.PlayAsync(new TypingData("취소 대사", "test", Vector3.zero,
                    Color.white, true), null, 0.4f, cancellation.Token);
                cancellation.Cancel();
                yield return ExpectCancellation(cancelled).ToCoroutine();
            }
            Assert.That(recorder.Keys, Has.Count.EqualTo(4), "Skipping and cancelling must not emit remaining character sounds.");

            pour = manager.PlayLoop("SFX_pour");
            stir = manager.PlayLoop("SFX_stir");
            Assert.That(pour, Is.Not.Null);
            Assert.That(stir, Is.Not.Null);
            var sources = manager.GetComponentsInChildren<AudioSource>();
            var pourSource = sources.Single(source => source.name == "SE_Loop_SFX_pour");
            var stirSource = sources.Single(source => source.name == "SE_Loop_SFX_stir");
            Assert.That(pourSource.isPlaying && stirSource.isPlaying, Is.True);
            Assert.That(mixer.FindMatchingGroups("SE"), Does.Contain(pourSource.outputAudioMixerGroup));
            Assert.That(stirSource.outputAudioMixerGroup, Is.SameAs(pourSource.outputAudioMixerGroup));
            Assert.That(pourSource.volume, Is.EqualTo(0.820f).Within(0.001f));
            Assert.That(stirSource.volume, Is.EqualTo(0.846f).Within(0.001f));
            yield return new WaitForSeconds(0.12f);
            var samples = new float[512];
            pourSource.GetOutputData(samples, 0);
            float rms = Mathf.Sqrt(samples.Sum(sample => sample * sample) / samples.Length);
            string pourClip = pourSource.clip.name;
            string stirClip = stirSource.clip.name;
            pour.Dispose();
            pour.Dispose();
            Assert.That(pourSource.isPlaying, Is.False);
            Assert.That(stirSource.isPlaying, Is.True, "Closing one gimmick must not stop another owner's sound.");
            manager.enabled = false;
            Assert.That(stirSource.isPlaying, Is.False, "The sound owner must stop all loops when disabled.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/audio-20261017"));
            Directory.CreateDirectory(folder);
            string evidence = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                typingSounds = recorder.Keys,
                typingTimes = recorder.Times,
                skipAndCancelAreSilent = true,
                pourClip,
                stirClip,
                mixerGroup = pourSource.outputAudioMixerGroup.name,
                measuredPourRms = rms,
                dspTime = AudioSettings.dspTime,
                independentLoopDisposal = true,
                ownerDisableStopsLoops = true
            }, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(folder, "runtime-evidence.json"), evidence);
            Debug.Log("[Audio Runtime] " + evidence);
        }
        finally
        {
            pour?.Dispose();
            stir?.Dispose();
            UnityEngine.Object.Destroy(root);
            if (restoreVolume && mixer != null) mixer.SetFloat("SEVolume", previousDb);
            DOTween.Clear(true);
        }
        yield return new ExitPlayMode();
    }

    static async UniTask ExpectCancellation(UniTask task)
    {
        try { await task; }
        catch (OperationCanceledException) { return; }
        Assert.Fail("Cancelled typing completed normally.");
    }

    static DynamicSpeechBubble CreateBubble(Transform parent)
    {
        var go = new GameObject("bubble", typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var text = new GameObject("text", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(go.transform, false);
        var name = new GameObject("name", typeof(RectTransform), typeof(TextMeshProUGUI));
        name.transform.SetParent(go.transform, false);
        var bubble = go.AddComponent<DynamicSpeechBubble>();
        bubble.bubble = (RectTransform)go.transform;
        bubble.bubble.sizeDelta = new Vector2(400, 100);
        bubble.textLabel = text.GetComponent<TextMeshProUGUI>();
        bubble.textLabel.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/01.Game/03.Content/05.Fonts/NeoDunggeunmo SDF.asset");
        bubble.nameLabel = name.GetComponent<TextMeshProUGUI>();
        go.SetActive(true);
        return bubble;
    }
}
