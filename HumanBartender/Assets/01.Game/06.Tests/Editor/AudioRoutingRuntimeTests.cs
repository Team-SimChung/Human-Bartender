using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

public sealed class AudioRoutingRuntimeTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

    sealed class MemoryVolumeStore : IAudioVolumeStore
    {
        public float Music = .8f, Effects = .4f;
        public int Saves;
        public float Load(AudioBus bus) => bus == AudioBus.Music ? Music : Effects;
        public void Save(AudioBus bus, float value)
        {
            if (bus == AudioBus.Music) Music = value;
            else Effects = value;
            Saves++;
        }
    }

    [Test]
    public void LegacyPreferencesAndSharedPrefabKeepTheirSingleSource()
    {
        var store = new PlayerPrefsAudioVolumeStore();
        Assert.That(store.Load(AudioBus.Music), Is.EqualTo(PlayerPrefs.GetFloat("vol_bgm", .30f)));
        Assert.That(store.Load(AudioBus.SoundEffects), Is.EqualTo(PlayerPrefs.GetFloat("vol_se", .80f)));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab");
        Assert.That(Get<AudioOutputSettings>(prefab.GetComponentInChildren<SoundManager>(true), "output"),
            Is.SameAs(AudioOutputSettings.Shared));
        Assert.That(AudioOutputSettings.Shared.GetGroup(AudioBus.Music).audioMixer,
            Is.SameAs(AudioOutputSettings.Shared.GetGroup(AudioBus.SoundEffects).audioMixer));
    }

    [Test]
    public void UnconfiguredOutputCannotSilentlyBypassUserVolume()
    {
        var settings = ScriptableObject.CreateInstance<AudioOutputSettings>();
        var go = new GameObject("unconfigured source");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                settings.Route(go.AddComponent<AudioSource>(), AudioBus.SoundEffects));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    [UnityTest]
    [Explicit("Run alone: enters Play Mode to verify Studio routing, volume and audio lifetime.")]
    public IEnumerator StudioAndGameEffectsRespectIndependentUserVolumes()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        // Construct captured test locals after the domain reload, not in its serialized iterator.
        yield return VerifyRouting();
        yield return new ExitPlayMode();
    }

    static IEnumerator VerifyRouting()
    {
        yield return null;
        var output = AudioOutputSettings.Shared;
        Assert.That(output, Is.Not.Null, "Shared routing settings must load in Play Mode.");
        var mixer = output.GetGroup(AudioBus.SoundEffects).audioMixer;
        Assert.That(mixer, Is.Not.Null, "The effect group must resolve its owning mixer in Play Mode.");
        Assert.That(mixer.GetFloat(AudioOutputSettings.EffectsVolumeParameter, out float originalEffects), Is.True);
        Assert.That(mixer.GetFloat(AudioOutputSettings.MusicVolumeParameter, out float originalMusic), Is.True);
        var root = new GameObject("audio routing runtime");
        if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            root.AddComponent<AudioListener>();
        StudioSequence sequence = null;
        TimelineAsset timeline = null;
        IDisposable studio = null;
        IDisposable loop = null;
        try
        {
            var store = new MemoryVolumeStore();
            var settings = new AudioVolumeSettings(output, store);
            settings.Start();
            AssertVolume(output, AudioBus.SoundEffects, Mathf.Log10(.4f) * 20);
            AssertVolume(output, AudioBus.Music, Mathf.Log10(.8f) * 20);
            Assert.That(store.Saves, Is.Zero, "Loading preferences must not overwrite them.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab")
                .GetComponentInChildren<SoundManager>(true);
            var sounds = Get<List<Sound>>(prefab, "seList");
            var go = new GameObject("game effects");
            go.transform.SetParent(root.transform);
            go.SetActive(false);
            var manager = go.AddComponent<SoundManager>();
            Set(manager, "output", output);
            Set(manager, "seList", sounds);
            go.SetActive(true);
            manager.PlaySE("SFX_fizz");
            loop = manager.PlayLoop("SFX_pour");
            var gameSources = go.GetComponentsInChildren<AudioSource>();
            Assert.That(gameSources.Count(source => source.isPlaying), Is.EqualTo(2));
            Assert.That(gameSources.All(source => source.outputAudioMixerGroup == output.GetGroup(AudioBus.SoundEffects)), Is.True);

            sequence = ScriptableObject.CreateInstance<StudioSequence>();
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            Set(sequence, "timeline", timeline);
            var effectsClip = AddClip(timeline, sounds.Single(sound => sound.name == "SFX_pour").clip, AudioBus.SoundEffects);
            var musicClip = AddClip(timeline, sounds.Single(sound => sound.name == "SFX_stir").clip, AudioBus.Music);
            var type = typeof(StudioPlayer).Assembly.GetType("HumanBartender.CutsceneStudio.StudioAudioPlayback");
            studio = (IDisposable)Activator.CreateInstance(type, Private, null, new object[] { root, output }, null);
            var evaluate = type.GetMethod("Evaluate", Private);
            void Evaluate(double time, bool waiting) => evaluate.Invoke(studio, new object[] { sequence, time, waiting });

            Evaluate(.5, false);
            var voices = root.GetComponents<AudioSource>();
            Assert.That(voices.Length, Is.EqualTo(2));
            var se = voices.Single(voice => voice.clip == effectsClip.Sound);
            var bgm = voices.Single(voice => voice.clip == musicClip.Sound);
            Assert.That(se.outputAudioMixerGroup, Is.SameAs(output.GetGroup(AudioBus.SoundEffects)));
            Assert.That(bgm.outputAudioMixerGroup, Is.SameAs(output.GetGroup(AudioBus.Music)));
            Assert.That(se.isPlaying && bgm.isPlaying, Is.True);
            Assert.That(se.time, Is.EqualTo(.75f).Within(.08f), "Keep timeline position plus the authored offset.");
            Assert.That(se.volume, Is.EqualTo(.35f * .55f).Within(.001f), "Clip fade and the cutscene output multiplier are separate from user mixer gain.");

            settings.SetSEVolume(0);
            AssertVolume(output, AudioBus.SoundEffects, -80);
            AssertVolume(output, AudioBus.Music, Mathf.Log10(.8f) * 20);
            Assert.That(store.Effects, Is.Zero);
            Assert.That(se.volume, Is.EqualTo(.35f * .55f).Within(.001f));
            Evaluate(.5, true);
            Assert.That(voices.All(voice => !voice.isPlaying), Is.True, "Input wait pauses both Studio channels.");
            Evaluate(.5, false);
            Assert.That(voices.All(voice => voice.isPlaying), Is.True);
            settings.SetSEVolume(2);
            settings.SetBGMVolume(-1);
            Assert.That(store.Effects, Is.EqualTo(1));
            Assert.That(store.Music, Is.Zero);
            AssertVolume(output, AudioBus.SoundEffects, 0);
            AssertVolume(output, AudioBus.Music, -80);
            Assert.That(store.Saves, Is.EqualTo(3));
            Evaluate(6, false);
            Assert.That(voices.All(voice => !voice.isPlaying), Is.True);
            studio.Dispose();
            studio.Dispose();
            studio = null;
            loop.Dispose();
            loop = null;
            manager.enabled = false;
            yield return null;
            Assert.That(root.GetComponents<AudioSource>(), Is.Empty);

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/audio-refactor-20261017"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "routing-runtime-evidence.json"),
                Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    sharedRouting = new[] { "game one-shot", "game loop", "Studio effects", "Studio music" },
                    effectsMuteDb = -80, musicIndependent = true,
                    authoredFadePreserved = .35f, authoredOffsetPreserved = true,
                    waitResumeAndEndPassed = true, storeWrites = store.Saves,
                    persistedUserPreferencesModified = false
                }, Newtonsoft.Json.Formatting.Indented));
        }
        finally
        {
            studio?.Dispose();
            loop?.Dispose();
            UnityEngine.Object.Destroy(root);
            if (timeline != null)
            {
                foreach (var track in timeline.GetOutputTracks().ToArray())
                    timeline.DeleteTrack(track);
                UnityEngine.Object.Destroy(timeline);
            }
            if (sequence != null) UnityEngine.Object.Destroy(sequence);
            mixer.SetFloat(AudioOutputSettings.EffectsVolumeParameter, originalEffects);
            mixer.SetFloat(AudioOutputSettings.MusicVolumeParameter, originalMusic);
        }
    }

    static StudioClip AddClip(TimelineAsset timeline, AudioClip sound, AudioBus bus)
    {
        var track = timeline.CreateTrack<StudioTrack>(null, bus.ToString());
        var timed = track.CreateClip<StudioClip>();
        timed.start = 0;
        timed.duration = 5;
        var clip = (StudioClip)timed.asset;
        Set(clip, "kind", StudioKind.Audio);
        Set(clip, "sound", sound);
        Set(clip, "bus", bus);
        Set(clip, "volume", .7f);
        Set(clip, "fadeIn", 1f);
        Set(clip, "audioOffset", .25f);
        return clip;
    }

    static void AssertVolume(AudioOutputSettings output, AudioBus bus, float expected)
    {
        Assert.That(output.GetGroup(bus).audioMixer.GetFloat(AudioOutputSettings.VolumeParameter(bus), out float db), Is.True);
        Assert.That(db, Is.EqualTo(expected).Within(.001f));
    }
}
