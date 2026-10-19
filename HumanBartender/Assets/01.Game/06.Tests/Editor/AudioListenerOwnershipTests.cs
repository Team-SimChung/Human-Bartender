using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using HumanBartender.CutsceneStudio;
using HumanBartender.CutsceneStudio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer.Unity;

// 전역 리스너 소유권과 내보낸 씬·실제 씬 전환의 오디오 수명을 검증함.
public sealed class AudioListenerOwnershipTests
{
    private const string FixtureKey = "AudioListenerOwnershipTests.Fixture";

    [Test]
    public void PreloadedProjectRootOwnsOneEnabledListener()
    {
        VContainerSettings settings = null;
        foreach (var asset in PlayerSettings.GetPreloadedAssets())
            if (asset is VContainerSettings candidate)
            {
                Assert.That(settings, Is.Null, "Only one VContainer configuration may be preloaded.");
                settings = candidate;
            }
        Assert.That(settings, Is.Not.Null);
        var root = settings.RootLifetimeScope;
        Assert.That(root, Is.InstanceOf<ProjectLifetimeScope>());
        Assert.That(root.autoRun && root.gameObject.activeSelf, Is.True);
        var listeners = root.GetComponentsInChildren<AudioListener>(true);
        Assert.That(listeners.Length, Is.EqualTo(1));
        Assert.That(listeners[0].gameObject, Is.EqualTo(root.gameObject));
        Assert.That(listeners[0].enabled, Is.True);
    }

    [Test]
    public void MaintainedScenesAndPrefabsDoNotDeclareAnotherListener()
    {
        var settings = AssetDatabase.LoadAssetAtPath<VContainerSettings>("Assets/VContainerSettings.asset");
        string rootPath = AssetDatabase.GetAssetPath(settings.RootLifetimeScope);
        foreach (string folder in new[] { "Assets/01.Game", "Assets/03.Dev/01.Scenes" })
        foreach (string guid in AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == rootPath || path.Contains("/05.Recovery/")) continue;
            Assert.That(File.ReadAllText(path), Does.Not.Contain("\nAudioListener:"), path);
        }
    }

    [Test]
    public void ExportedSceneKeepsCameraAndAutoplayWithoutSceneListener()
    {
        string path = PrepareExport();
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            int cameras = 0, players = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                Assert.That(root.GetComponentsInChildren<AudioListener>(true), Is.Empty);
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
                foreach (var player in root.GetComponentsInChildren<StudioPlayer>(true))
                {
                    players++;
                    Assert.That(player.PlayOnStart, Is.True);
                    Assert.That(player.Sequence, Is.Not.Null);
                }
            }
            Assert.That(cameras, Is.EqualTo(1));
            Assert.That(players, Is.EqualTo(1));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static string PrepareExport()
    {
        // 테스트 러너의 기본 카메라를 배제하고, 이름 있는 전용 씬에서 내보내기를 검증함.
        // 사용자 씬 구성은 Unity Test Framework가 실행 전후에 보관하고 복원함.
        var staging = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string folder = "Assets/__AudioListenerOwnershipTest_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(FixtureKey, folder);
        var sequence = StudioEditorAssets.Create(folder + "/Ownership.asset");
        Assert.That(EditorSceneManager.SaveScene(staging, folder + "/Staging.unity"), Is.True);
        var prefab = AssetDatabase.LoadAssetAtPath<VContainerSettings>("Assets/VContainerSettings.asset").RootLifetimeScope;
        var sound = prefab.GetComponentInChildren<SoundManager>(true);
        var sounds = new SerializedObject(sound).FindProperty("seList");
        AudioClip tone = null;
        for (int i = 0; i < sounds.arraySize; i++)
        {
            var entry = sounds.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("name").stringValue == "SFX_pour")
                tone = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
        }
        Assert.That(tone, Is.Not.Null);
        foreach (AudioBus bus in Enum.GetValues(typeof(AudioBus)))
        {
            var timed = StudioEditorAssets.AddClip(sequence, StudioKind.Audio, 0);
            timed.duration = 10;
            var clip = (StudioClip)timed.asset;
            clip.Sound = tone;
            clip.Bus = bus;
            clip.Volume = .5f;
            clip.Loop = true;
        }
        string path = folder + "/Ownership.unity";
        StudioEditorAssets.ExportScene(sequence, path, false);
        return path;
    }

    [UnityTest, Explicit("Run alone: loads real game scenes in Play Mode.")]
    public IEnumerator SceneTransitionsKeepTheSameGlobalListener()
    {
        PrepareExport();
        EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Main.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyTransitions();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyTransitions()
    {
        var listener = AssertRuntimeListener();
        double deadline = Time.realtimeSinceStartupAsDouble + 20;
        while (SceneTransitionManager.Instance.IsBusy)
        {
            Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline));
            yield return null;
        }
        foreach (string scene in new[] { "Main", "Home", "OutSide", "Play", "Home" })
            yield return Transition(scene, false, listener);
        yield return Transition("OutSide", true, listener);
        var unload = SceneTransitionManager.Instance.RequestUnloadScene("OutSide");
        yield return CompleteTransition(unload, listener, "unload OutSide");
        Assert.That(SceneManager.GetSceneByName("OutSide").isLoaded, Is.False);

        string path = SessionState.GetString(FixtureKey, "") + "/Ownership.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
        Assert.That(AssertRuntimeListener(), Is.EqualTo(listener));
        yield return VerifyExportAudio();
        yield return Transition("Main", false, listener);
    }

    private static IEnumerator Transition(string name, bool additive, AudioListener listener)
    {
        // 씬 전환 서비스만 직접 호출하므로 해당 장소의 정상 진행 단계를 준비함.
        if (name == "Home" || name == "OutSide") GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        else if (name == "Play") GameStateManager.Instance.GameFlow = EGameFlow.Bar;
        var request = SceneTransitionManager.Instance.RequestLoadScene(name,
            additive ? LoadSceneMode.Additive : LoadSceneMode.Single);
        yield return CompleteTransition(request, listener, name + (additive ? " additive" : " single"));
        Assert.That(SceneManager.GetSceneByName(name).isLoaded, Is.True);
    }

    private static IEnumerator CompleteTransition(SceneTransitionRequest request, AudioListener listener, string phase)
    {
        Assert.That(request.Accepted, Is.True, phase);
        var task = request.Completion.AsTask();
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        while (!task.IsCompleted)
        {
            Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), phase);
            Assert.That(AssertRuntimeListener(), Is.EqualTo(listener), phase);
            yield return null;
        }
        Assert.That(task.IsCompletedSuccessfully, Is.True, task.Exception?.ToString());
        Assert.That(task.Result.Succeeded, Is.True, task.Result.Message);
        Assert.That(AssertRuntimeListener(), Is.EqualTo(listener), phase);
        TestContext.WriteLine(phase + ": one persistent ProjectLifeScope listener, transition succeeded");
    }

    [UnityTest, Explicit("Run alone: starts Play Mode directly in a newly exported cutscene scene.")]
    public IEnumerator ExportedSceneStartsWithGlobalListenerAndAudibleAutoplay()
    {
        string path = PrepareExport();
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        AssertRuntimeListener();
        yield return VerifyExportAudio();
        yield return new ExitPlayMode();
    }

    private static AudioListener AssertRuntimeListener()
    {
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        Assert.That(listeners.Length, Is.EqualTo(1), "Exactly one listener must survive every transition frame.");
        var listener = listeners[0];
        Assert.That(listener.isActiveAndEnabled, Is.True);
        Assert.That(listener.GetComponent<ProjectLifetimeScope>(), Is.Not.Null);
        Assert.That(listener.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
        return listener;
    }

    private static IEnumerator VerifyExportAudio()
    {
        bool mute = EditorUtility.audioMasterMute;
        EditorUtility.audioMasterMute = false;
        try
        {
            yield return null;
            var players = UnityEngine.Object.FindObjectsByType<StudioPlayer>(FindObjectsSortMode.None);
            Assert.That(players.Length, Is.EqualTo(1));
            var player = players[0];
            Assert.That(player.IsPlaying, Is.True, "Exported player must start automatically.");
            var samples = new float[1024];
            AudioListener.GetOutputData(samples, 0);
            foreach (var voice in player.GetComponents<AudioSource>()) voice.GetOutputData(samples, 0);
            yield return new WaitForSecondsRealtime(.4f);
            var voices = player.GetComponents<AudioSource>();
            Assert.That(voices.Length, Is.EqualTo(2));
            foreach (var voice in voices)
            {
                Assert.That(voice.isPlaying && voice.loop, Is.True);
                Assert.That(voice.spatialBlend, Is.Zero);
                Assert.That(voice.outputAudioMixerGroup, Is.Not.Null);
                voice.GetOutputData(samples, 0);
                Assert.That(Peak(samples), Is.GreaterThan(.0001f), "Exported audio source signal");
            }
            AudioListener.GetOutputData(samples, 0);
            Assert.That(Peak(samples), Is.GreaterThan(.0001f), "Global listener mixed output");
            TestContext.WriteLine("Exported autoplay: both audio buses produce signal through ProjectLifeScope; peak=" + Peak(samples));
            player.Skip();
            yield return null;
            yield return null;
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(player.GetComponents<AudioSource>(), Is.Empty);
        }
        finally { EditorUtility.audioMasterMute = mute; }
    }

    private static float Peak(float[] samples)
    {
        float value = 0;
        foreach (float sample in samples) value = Mathf.Max(value, Mathf.Abs(sample));
        return value;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        string folder = SessionState.GetString(FixtureKey, "");
        if (!string.IsNullOrEmpty(folder))
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
        }
        SessionState.EraseString(FixtureKey);
    }
}
