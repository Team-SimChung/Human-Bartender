using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class DistanceAmbientAudioTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    DistanceAmbientAudio ambience;
    AudioSource source;
    Transform listener;

    [SetUp]
    public void SetUp()
    {
        var root = new GameObject("Inactive Distance Ambience Test");
        owned.Add(root);
        root.SetActive(false);
        source = root.AddComponent<AudioSource>();
        ambience = root.AddComponent<DistanceAmbientAudio>();
        var player = new GameObject("Listener");
        owned.Add(player);
        listener = player.transform;
        var clip = AudioClip.Create("AmbientTest", 4410, 1, 44100, false);
        owned.Add(clip);
        Set("source", source);
        Set("clip", clip);
        Set("listener", listener);
        Set("baseVolume", 1f);
        Set("fullVolumeDistance", 1f);
        Set("silentDistance", 5f);
        Set("distanceCurve", AnimationCurve.Linear(0f, 1f, 1f, 0f));
        Set("days", new[]
        {
            new DistanceAmbientAudio.DayPlayback { day = 8, play = true },
            new DistanceAmbientAudio.DayPlayback { day = 1, play = true },
            new DistanceAmbientAudio.DayPlayback { day = 5, play = false }
        });
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var item in owned) Object.DestroyImmediate(item);
        owned.Clear();
    }

    void Set(string field, object value) => typeof(DistanceAmbientAudio).GetField(field, Private).SetValue(ambience, value);
    void Tick(float seconds, int day = 1, bool focused = true, float context = 1f) =>
        typeof(DistanceAmbientAudio).GetMethod("UpdatePlayback", Private).Invoke(ambience, new object[] { seconds, day, focused, context });

    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(5, false)]
    [TestCase(8, true)]
    [TestCase(9, false)]
    public void ScheduleUsesDayValuesAndPlayFlagsInsteadOfArrayIndex(int day, bool expected)
        => Assert.That(ambience.IsScheduledForDay(day), Is.EqualTo(expected));

    [Test]
    public void EmptyAndDuplicateSchedulesDoNotPlay()
    {
        Set("days", System.Array.Empty<DistanceAmbientAudio.DayPlayback>());
        Assert.That(ambience.IsScheduledForDay(1), Is.False);
        Set("days", new[]
        {
            new DistanceAmbientAudio.DayPlayback { day = 1, play = true },
            new DistanceAmbientAudio.DayPlayback { day = 1, play = false }
        });
        Assert.That(ambience.IsScheduledForDay(1), Is.False);
    }

    [TestCase(0f, 1f)]
    [TestCase(1f, 1f)]
    [TestCase(3f, .5f)]
    [TestCase(5f, 0f)]
    [TestCase(10f, 0f)]
    public void DistanceHasFullVolumeFalloffAndSilentRange(float distance, float expected)
        => Assert.That(ambience.EvaluateVolume(new Vector3(distance, 0f, 0f), 1), Is.EqualTo(expected).Within(.0001f));

    [Test]
    public void DistanceUsesWorldXYAndIgnoresCameraDepth()
    {
        ambience.transform.position = new Vector3(10f, 20f, 100f);
        Assert.That(ambience.EvaluateVolume(new Vector3(13f, 20f, -100f), 1), Is.EqualTo(.5f));
        Assert.That(ambience.EvaluateVolume(new Vector3(10f, 23f, -100f), 1), Is.EqualTo(.5f));
        ambience.SetPlaybackAllowed(false);
        Assert.That(ambience.EvaluateVolume(ambience.transform.position, 1), Is.Zero);
        ambience.SetPlaybackAllowed(true);
        Assert.That(ambience.EvaluateVolume(ambience.transform.position, 1), Is.EqualTo(1f));
    }

    [Test]
    public void PlaybackFadesRoutesEffectsAndRespondsToDayLocationFocusAndMute()
    {
        Tick(.25f);
        Assert.That(source.volume, Is.EqualTo(.5f).Within(.0001f));
        Tick(.25f);
        Assert.That(source.volume, Is.EqualTo(1f));
        Assert.That(source.playOnAwake, Is.False);
        Assert.That(source.loop, Is.True);
        Assert.That(source.spatialBlend, Is.Zero);
        Assert.That(source.outputAudioMixerGroup, Is.EqualTo(AudioOutputSettings.Shared.GetGroup(AudioBus.SoundEffects)));
        Tick(.5f, context: .65f);
        Assert.That(source.volume, Is.EqualTo(.65f).Within(.0001f));
        Tick(.5f, day: 5);
        Assert.That(source.volume, Is.Zero);
        Tick(.5f);
        ambience.SetPlaybackAllowed(false);
        Tick(.25f);
        Assert.That(source.volume, Is.EqualTo(.5f).Within(.0001f));
        Tick(.25f);
        Assert.That(source.volume, Is.Zero);
        ambience.SetPlaybackAllowed(true);
        Tick(.5f);
        Tick(0f, focused: false);
        Assert.That(source.volume, Is.Zero);
        Tick(.5f);
        Set("baseVolume", 0f);
        Tick(.01f);
        Assert.That(source.volume, Is.Zero);
    }

    [Test]
    public void SavedHomeWindowUsesReusablePartRainClipPlayerAndIndoorGate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HomeAmbienceSetup.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var part = prefab.GetComponent<DistanceAmbientAudio>();
        Assert.That(Ref<AudioSource>(part, "source"), Is.SameAs(prefab.GetComponent<AudioSource>()));
        Assert.That(Ref<Transform>(part, "listener"), Is.Null, "공용 원본은 씬 바깥의 플레이어를 모른다.");
        var scene = SceneManager.GetSceneByPath(HomeAmbienceSetup.HomePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(HomeAmbienceSetup.HomePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var window = roots.SelectMany(r => r.GetComponentsInChildren<DistanceAmbientAudio>(true)).Single(a => a.name == "WindowRainAudio");
            Assert.That(window.transform.parent.name, Is.EqualTo("IndoorAmbience"));
            Assert.That(window.transform.parent.parent.name, Is.EqualTo("04.Gameplay"));
            Assert.That(AssetDatabase.GetAssetPath(Ref<AudioClip>(window, "clip")), Does.EndWith("/Weather/rain_loop.ogg"));
            Assert.That(Ref<Transform>(window, "listener").GetComponent<Player>(), Is.Not.Null);
            var terrace = roots.SelectMany(r => r.GetComponentsInChildren<HomeTerracePresentation>(true)).Single();
            var entries = new SerializedObject(terrace).FindProperty("indoorAmbience");
            Assert.That(Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).objectReferenceValue == window), Is.True);
            var audio = Ref<AudioSource>(window, "source");
            Assert.That(audio.playOnAwake, Is.False);
            Assert.That(audio.outputAudioMixerGroup, Is.EqualTo(AudioOutputSettings.Shared.GetGroup(AudioBus.SoundEffects)));
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    static T Ref<T>(Object target, string field) where T : Object =>
        new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
}
