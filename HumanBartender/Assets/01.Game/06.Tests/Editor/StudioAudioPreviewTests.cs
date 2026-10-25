using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using HumanBartender.CutsceneStudio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

// 실제 오디오 출력과 미리보기 전송 제어·공용 재생기의 회귀를 검증함.
public sealed class StudioAudioPreviewTests
{
    private sealed class Rig : IDisposable
    {
        internal readonly StudioSequence Sequence;
        internal readonly StudioClip Effects, Music;
        internal readonly StudioTrack EffectsTrack;
        internal readonly StudioPreviewController Controller;
        internal readonly List<Object> Owned = new();
        private readonly bool mute;
        private readonly float effectsDb, musicDb;
        internal Rig()
        {
            mute = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;
            var output = AudioOutputSettings.Shared;
            output.GetGroup(AudioBus.SoundEffects).audioMixer.GetFloat(AudioOutputSettings.EffectsVolumeParameter, out effectsDb);
            output.GetGroup(AudioBus.Music).audioMixer.GetFloat(AudioOutputSettings.MusicVolumeParameter, out musicDb);
            Sequence = Own(ScriptableObject.CreateInstance<StudioSequence>());
            Sequence.Id = "studio-audio-preview-test";
            Sequence.Timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            Sequence.Timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            Sequence.Timeline.fixedDuration = 4;
            var sound = Own(AudioClip.Create("Studio test tone", 48000, 1, 48000, false));
            var data = new float[sound.samples];
            for (int i = 0; i < data.Length; i++) data[i] = .08f * Mathf.Sin(2 * Mathf.PI * 440 * i / 48000);
            sound.SetData(data, 0);
            Effects = AddAudio(sound, AudioBus.SoundEffects, out var effectsTrack);
            EffectsTrack = effectsTrack;
            Music = AddAudio(sound, AudioBus.Music, out _);
            var session = new StudioEditSession();
            session.Load(Sequence);
            Controller = new StudioPreviewController(session, Repaint);
        }

        private static void Repaint() { }
        internal T Own<T>(T value) where T : Object { Owned.Add(value); return value; }
        private StudioClip AddAudio(AudioClip sound, AudioBus bus, out StudioTrack track)
        {
            track = Own(Sequence.Timeline.CreateTrack<StudioTrack>());
            var timed = track.CreateClip<StudioClip>();
            timed.start = 0;
            timed.duration = 4;
            var clip = Own((StudioClip)timed.asset);
            clip.Kind = StudioKind.Audio;
            clip.Sound = sound;
            clip.Bus = bus;
            clip.Volume = .8f;
            clip.Loop = true;
            clip.FadeIn = clip.FadeOut = 1;
            clip.AudioOffset = .25f;
            return clip;
        }

        internal void AddWait(double time, StudioKind kind = StudioKind.Wait)
        {
            var track = Own(Sequence.Timeline.CreateTrack<StudioTrack>());
            var timed = track.CreateClip<StudioClip>();
            timed.start = kind == StudioKind.Dialogue ? 0 : time;
            timed.duration = kind == StudioKind.Dialogue ? time : .1;
            var clip = Own((StudioClip)timed.asset);
            clip.Kind = kind;
            clip.Text = "Audio wait test";
            clip.WaitForInput = true;
        }

        internal void Tick(float speed = 1, bool gates = true) =>
            Controller.Tick(EditorApplication.timeSinceStartup, speed, gates);

        internal IEnumerator RunFor(double duration, float speed = 1)
        {
            double end = EditorApplication.timeSinceStartup + duration;
            while (EditorApplication.timeSinceStartup < end)
            {
                Tick(speed);
                foreach (var voice in PreviewVoices()) Peak(voice);
                Peak();
                yield return null;
            }
        }

        public void Dispose()
        {
            Controller.Dispose();
            for (int i = Owned.Count - 1; i >= 0; i--)
                if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
            var output = AudioOutputSettings.Shared;
            output.GetGroup(AudioBus.SoundEffects).audioMixer.SetFloat(AudioOutputSettings.EffectsVolumeParameter, effectsDb);
            output.GetGroup(AudioBus.Music).audioMixer.SetFloat(AudioOutputSettings.MusicVolumeParameter, musicDb);
            EditorUtility.audioMasterMute = mute;
        }
    }

    private Rig rig;
    [TearDown] public void TearDown() { rig?.Dispose(); rig = null; }

    private static AudioSource[] PreviewVoices()
    {
        var voices = new List<AudioSource>();
        foreach (var source in Resources.FindObjectsOfTypeAll<AudioSource>())
            if (source.gameObject.name == "Cutscene Studio Preview Audio") voices.Add(source);
        return voices.ToArray();
    }
    private static AudioSource Voice(AudioSource[] voices, AudioBus bus)
    {
        foreach (var voice in voices)
            if (voice.outputAudioMixerGroup.name == AudioOutputSettings.Shared.GetGroup(bus).name) return voice;
        Assert.Fail("Missing voice: " + bus);
        return null;
    }
    private static float Peak(AudioSource voice = null)
    {
        var samples = new float[1024];
        if (voice == null) AudioListener.GetOutputData(samples, 0);
        else voice.GetOutputData(samples, 0);
        float peak = 0;
        foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
        return peak;
    }
    private static void AssertOutput(AudioSource[] voices, string phase)
    {
        Assert.That(voices.Length, Is.EqualTo(2), phase);
        foreach (var voice in voices)
        {
            voice.outputAudioMixerGroup.audioMixer.GetFloat(AudioOutputSettings.VolumeParameter(voice.outputAudioMixerGroup.name == AudioOutputSettings.Shared.GetGroup(AudioBus.Music).name ? AudioBus.Music : AudioBus.SoundEffects), out float db);
            TestContext.WriteLine($"{phase}: source={voice.outputAudioMixerGroup.name} time={voice.time} gain={voice.volume} mixer={db} peak={Peak(voice)} listener={Peak()}");
            Assert.That(voice.isPlaying, Is.True, phase);
            Assert.That(Peak(voice), Is.GreaterThan(.0001f), phase + " source signal");
        }
        float listener = Peak();
        TestContext.WriteLine(phase + " listener peak=" + listener);
        Assert.That(listener, Is.GreaterThan(.0001f), phase + " mixed output signal");
    }

    [UnityTest]
    public IEnumerator PreviewOutputsBothBusesAndHonoursPauseSeekSpeedAndEnd()
    {
        rig = new Rig();
        rig.Controller.Seek(.35);
        rig.Controller.Toggle();
        yield return rig.RunFor(.15);
        var voices = PreviewVoices();
        AssertOutput(voices, "editor preview");
        var effects = Voice(voices, AudioBus.SoundEffects);
        var music = Voice(voices, AudioBus.Music);
        double time = rig.Controller.Clock.Time;
        Assert.That(effects.volume, Is.EqualTo((float)time * .8f * .55f).Within(.015f));
        Assert.That(music.volume, Is.EqualTo((float)time * .8f).Within(.015f));
        Assert.That(effects.time, Is.EqualTo((time + .25) % 1).Within(.1));
        var preferences = new PlayerPrefsAudioVolumeStore();
        foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
        {
            var mixer = Voice(voices, bus).outputAudioMixerGroup.audioMixer;
            Assert.That(mixer, Is.Not.SameAs(AudioOutputSettings.Shared.GetGroup(bus).audioMixer));
            mixer.GetFloat(AudioOutputSettings.VolumeParameter(bus), out float db);
            float expected = Mathf.Clamp01(preferences.Load(bus));
            Assert.That(db, Is.EqualTo(expected <= .0001f ? -80 : Mathf.Log10(expected) * 20).Within(.001f));
        }
        rig.Controller.Toggle();
        Assert.That(PreviewVoices(), Is.Empty, "Pause releases output immediately.");
        rig.Controller.Seek(2.15);
        Assert.That(PreviewVoices(), Is.Empty, "Scrubbing must be silent.");
        rig.Controller.Toggle();
        yield return rig.RunFor(.12, 2);
        effects = Voice(PreviewVoices(), AudioBus.SoundEffects);
        Assert.That(effects.pitch, Is.EqualTo(2));
        Assert.That(effects.time, Is.EqualTo((rig.Controller.Clock.Time + .25) % 1).Within(.12));
        rig.Controller.Seek(3.5);
        rig.Controller.Toggle();
        rig.Tick();
        Assert.That(Voice(PreviewVoices(), AudioBus.SoundEffects).volume, Is.EqualTo(.8f * .55f * .5f).Within(.03));
        yield return rig.RunFor(.65);
        Assert.That(rig.Controller.Playing, Is.False);
        Assert.That(PreviewVoices(), Is.Empty, "End releases output.");
    }

    [UnityTest]
    public IEnumerator InputWaitAndDialogueWaitPauseAllVoicesAndResumeAtClockTime()
    {
        rig = new Rig();
        rig.AddWait(.2);
        rig.AddWait(.6, StudioKind.Dialogue);
        rig.Controller.Toggle();
        yield return rig.RunFor(.3);
        Assert.That(rig.Controller.Clock.Waiting.HasValue, Is.True);
        foreach (var source in PreviewVoices()) Assert.That(source.isPlaying, Is.False);
        Assert.That(rig.Controller.Clock.Time, Is.EqualTo(.2).Within(.001));
        rig.Controller.Clock.Advance();
        yield return rig.RunFor(.15);
        AssertOutput(PreviewVoices(), "wait resumed");
        yield return rig.RunFor(.4);
        Assert.That(rig.Controller.Clock.Waiting.Value.Asset.Kind, Is.EqualTo(StudioKind.Dialogue));
        foreach (var source in PreviewVoices()) Assert.That(source.isPlaying, Is.False);
        rig.Tick(1, false);
        yield return rig.RunFor(.1);
        AssertOutput(PreviewVoices(), "wait bypassed");
    }

    [UnityTest]
    public IEnumerator LiveAudioEditsMuteLoopOffsetAndMissingSoundAreReflected()
    {
        rig = new Rig();
        rig.Controller.Seek(1.1);
        rig.Controller.Toggle();
        yield return rig.RunFor(.1);
        rig.Effects.Volume = .2f;
        rig.Effects.AudioOffset = .5f;
        rig.Tick();
        var effects = Voice(PreviewVoices(), AudioBus.SoundEffects);
        Assert.That(effects.volume, Is.EqualTo(.2f * .55f).Within(.001));
        Assert.That(effects.time, Is.EqualTo((rig.Controller.Clock.Time + .5) % 1).Within(.1));
        rig.EffectsTrack.muted = true;
        rig.Tick();
        Assert.That(PreviewVoices().Length, Is.EqualTo(1));
        rig.EffectsTrack.muted = false;
        rig.Effects.Loop = false;
        rig.Tick();
        Assert.That(PreviewVoices().Length, Is.EqualTo(1), "An exhausted one-shot must stay silent.");
        rig.Effects.Loop = true;
        rig.Tick();
        Assert.That(PreviewVoices().Length, Is.EqualTo(2));
        rig.Music.Sound = null;
        rig.Tick();
        Assert.That(PreviewVoices().Length, Is.EqualTo(1));
        rig.Controller.Rebuild(false);
        Assert.That(PreviewVoices(), Is.Empty, "Rebuild releases previous voices.");
        rig.Tick();
        Assert.That(PreviewVoices().Length, Is.EqualTo(1));
        rig.Controller.Dispose();
        Assert.That(PreviewVoices(), Is.Empty);
    }

    [Test]
    public void StartingAnotherPreviewStopsThePreviousOwner()
    {
        rig = new Rig();
        rig.Controller.Toggle();
        rig.Tick();
        var session = new StudioEditSession();
        session.Load(rig.Sequence);
        using var other = new StudioPreviewController(session, NoRepaint);
        other.Playing = true;
        Assert.That(rig.Controller.Playing, Is.False);
        Assert.That(PreviewVoices(), Is.Empty);
    }
    private static void NoRepaint() { }

    private sealed class TestVolumeStore : IAudioVolumeStore
    {
        public float Load(AudioBus bus) => bus == AudioBus.Music ? .3f : .6f;
        public void Save(AudioBus bus, float value) => Assert.Fail("Preview must not save volume preferences.");
        public bool LoadMuted(AudioBus bus) => false;
        public void SaveMuted(AudioBus bus, bool muted) => Assert.Fail("Preview must not save mute preferences.");
    }

    [Test]
    public void PreviewMixerUsesSavedVolumesWithoutChangingOriginalAssets()
    {
        var source = AudioOutputSettings.Shared;
        var originals = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source.GetGroup(AudioBus.Music).audioMixer));
        var before = new Dictionary<Object, string>();
        foreach (var original in originals) before.Add(original, EditorJsonUtility.ToJson(original));
        var store = new TestVolumeStore();
        using (var mixer = new StudioPreviewMixer(source, store))
        {
            StudioEditorAudio.Update(null);
            foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
            {
                var group = mixer.Output.GetGroup(bus);
                Assert.That(group, Is.Not.SameAs(source.GetGroup(bus)));
                Assert.That(EditorUtility.IsPersistent(group), Is.False);
                Assert.That(group.audioMixer, Is.Not.SameAs(source.GetGroup(bus).audioMixer));
                Assert.That(group.audioMixer.GetFloat(AudioOutputSettings.VolumeParameter(bus), out float db), Is.True);
                Assert.That(db, Is.EqualTo(Mathf.Log10(store.Load(bus)) * 20).Within(.001));
            }
            foreach (var original in originals) Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(before[original]));
        }
        foreach (var original in originals) Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(before[original]));
    }

    [UnityTest]
    [Explicit("Run alone: enters Play Mode and verifies StudioPlayer audio output and lifetime.")]
    public IEnumerator ActualPlayerOutputsSharedSettingsAndWaitsThenCleansUp()
    {
        // 테스트 러너의 기본 카메라 대신 실제 프로젝트 씬의 오디오 구성을 검증함.
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyRuntime();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyRuntime()
    {
        yield return new WaitForSecondsRealtime(.25f);
        using var runtime = new Rig();
        runtime.AddWait(.35);
        int listeners = 0;
        foreach (var listener in Resources.FindObjectsOfTypeAll<AudioListener>())
        {
            if (!EditorUtility.IsPersistent(listener)) TestContext.WriteLine($"runtime listener: {listener.gameObject.name}, scene={listener.gameObject.scene.name}, active={listener.isActiveAndEnabled}, flags={listener.gameObject.hideFlags}");
            if (!EditorUtility.IsPersistent(listener) && listener.isActiveAndEnabled && listener.gameObject.scene.IsValid())
            {
                listeners++;
                Assert.That(listener.GetComponent<ProjectLifetimeScope>(), Is.Not.Null,
                    "Only ProjectLifeScope owns the runtime listener.");
            }
        }
        Assert.That(listeners, Is.EqualTo(1), "The configured project root must supply exactly one listener.");
        var root = new GameObject("Studio audio runtime verification");
        var player = root.AddComponent<StudioPlayer>();
        player.PauseGameplay = false;
        player.HandleKeyboardInput = false;
        new AudioVolumeSettings(AudioOutputSettings.Shared, new PlayerPrefsAudioVolumeStore()).Start();
        var playback = player.PlayAsync(runtime.Sequence).AsTask();
        try
        {
            foreach (var voice in root.GetComponents<AudioSource>()) Peak(voice);
            Peak();
            yield return new WaitForSecondsRealtime(.2f);
            var voices = root.GetComponents<AudioSource>();
            AssertOutput(voices, "actual StudioPlayer");
            var effects = Voice(voices, AudioBus.SoundEffects);
            Assert.That(effects.pitch, Is.EqualTo(1));
            Assert.That(effects.volume, Is.EqualTo((float)player.CurrentTime * .8f * .55f).Within(.025));
            Assert.That(effects.time, Is.EqualTo((player.CurrentTime + .25) % 1).Within(.1));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(player.IsWaiting, Is.True);
            foreach (var voice in voices) Assert.That(voice.isPlaying, Is.False);
            player.Continue();
            yield return new WaitForSecondsRealtime(.15f);
            AssertOutput(voices, "actual wait resumed");
            player.Skip();
            yield return null;
            yield return null;
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(root.GetComponents<AudioSource>(), Is.Empty);
            Assert.That(playback.IsCompletedSuccessfully, Is.True, playback.Exception?.ToString());
        }
        finally
        {
            player.Stop();
            Object.Destroy(root);
        }
    }
}
