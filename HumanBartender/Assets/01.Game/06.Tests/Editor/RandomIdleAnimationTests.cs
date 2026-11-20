using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class RandomIdleAnimationTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/02.Birds/Crow.prefab";
    SceneSetup[] previousScenes;
    float previousTimeScale;
    UnityEngine.Random.State previousRandomState;
    bool changedGlobals;

    [UnityTest]
    public IEnumerator FixedIntervalMeasuresReplayStartsAndRestoresAnimatorOnStop()
    {
        previousScenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyFixedInterval();
    }

    private IEnumerator VerifyFixedInterval()
    {
        previousTimeScale = Time.timeScale;
        previousRandomState = UnityEngine.Random.state;
        changedGlobals = true;
        Time.timeScale = 1;
        var parent = new GameObject("Fixed interval test");
        parent.SetActive(false);
        var crow = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent.transform);
        var playback = crow.GetComponent<RandomIdleAnimation>();
        var animator = crow.GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        using (var data = new SerializedObject(playback))
        {
            data.FindProperty("playOnEnable").boolValue = false;
            data.FindProperty("minimumStartFrame").intValue = 1;
            data.FindProperty("maximumStartFrame").intValue = 1;
            data.FindProperty("minimumWaitSeconds").floatValue = 5;
            data.FindProperty("maximumWaitSeconds").floatValue = 5;
            data.FindProperty("repeatIntervalSeconds").floatValue = .4f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        animator.speed = 20;
        parent.SetActive(true);
        yield return null;
        yield return null;
        float lastStart = Time.time;
        playback.Play();
        for (int i = 0; i < 3; i++)
        {
            yield return WaitUntil(() => playback.IsWaiting, "The fixed cycle did not reach its wait.");
            Assert.That(playback.CurrentWaitSeconds, Is.InRange(0, .4f));
            yield return WaitUntil(() => !playback.IsWaiting, "The fixed cycle did not restart.");
            Assert.That(Time.time - lastStart, Is.InRange(.39f, .55f), "재생 시간까지 포함한 시작 간격이어야 한다.");
            Assert.That(playback.CurrentStartFrame, Is.EqualTo(1));
            lastStart = Time.time;
        }
        playback.Stop();
        Assert.That(playback.IsRunning, Is.False);
        Assert.That(animator.speed, Is.EqualTo(20));
        Object.Destroy(parent);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (changedGlobals)
        {
            Time.timeScale = previousTimeScale;
            UnityEngine.Random.state = previousRandomState;
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (previousScenes != null && previousScenes.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void ExistingHomeCrowInstancesInheritThePrefabAnimation()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var crows = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RandomIdleAnimation>(true))
                .Where(loop => PrefabUtility.GetCorrespondingObjectFromSource(loop.gameObject) == source).ToArray();
            Assert.That(crows.Length, Is.EqualTo(2));
            var terrace = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HomeTerracePresentation>(true)).Single();
            using var settings = new SerializedObject(terrace);
            var birds = settings.FindProperty("terraceBirdsRoot").objectReferenceValue as GameObject;
            Assert.That(birds, Is.Not.Null);
            Assert.That(birds.name, Is.EqualTo("TerraceBirdsGroup"));
            Assert.That(birds.activeSelf, Is.False, "테라스에 들어가기 전에는 새와 반복 재생이 비활성화되어야 한다.");
            foreach (var crow in crows)
            {
                Assert.That(crow.transform.IsChildOf(birds.transform), Is.True);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(crow.gameObject),
                    Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                using var data = new SerializedObject(crow);
                Assert.That(data.FindProperty("animator").objectReferenceValue, Is.EqualTo(crow.GetComponent<Animator>()));
                Assert.That(data.FindProperty("minimumWaitSeconds").floatValue, Is.EqualTo(1));
                Assert.That(data.FindProperty("maximumWaitSeconds").floatValue, Is.EqualTo(3));
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator StartsAtFramesOneToFourStopsWaitsAndReplaysWithPauseAndDisableCleanup()
    {
        previousScenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyLoop();
    }

    private IEnumerator VerifyLoop()
    {
        previousTimeScale = Time.timeScale;
        previousRandomState = UnityEngine.Random.state;
        changedGlobals = true;
        Time.timeScale = 1;
        UnityEngine.Random.InitState(143);
        var parent = new GameObject("Crow test");
        parent.SetActive(false);
        var crow = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent.transform);
        crow.transform.localPosition = new Vector3(1, 2, 3);
        var placement = crow.transform.localPosition;
        var animator = crow.GetComponent<Animator>();
        var playback = crow.GetComponent<RandomIdleAnimation>();
        var renderer = crow.GetComponent<SpriteRenderer>();
        var frames = AssetDatabase.LoadAllAssetsAtPath("Assets/01.Game/03.Content/01.ArtResources/01.Characters/02.Outside/05.StreetNPC/01.Idle/Crow_idle.png")
            .OfType<Sprite>().OrderBy(sprite => int.Parse(sprite.name[(sprite.name.LastIndexOf('_') + 1)..])).ToArray();
        using (var data = new SerializedObject(playback))
        {
            data.FindProperty("minimumWaitSeconds").floatValue = .08f;
            data.FindProperty("maximumWaitSeconds").floatValue = .12f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        animator.speed = 10;
        parent.SetActive(true);
        Assert.That(playback.CurrentStartFrame, Is.InRange(1, 4));
        Assert.That(renderer.sprite, Is.EqualTo(frames[playback.CurrentStartFrame - 1]));
        Assert.That(playback.IsWaiting, Is.False);

        Time.timeScale = 0;
        yield return null; // timeScale 변경은 다음 프레임부터 적용된다.
        float pausedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        yield return null;
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(pausedTime));
        Time.timeScale = 1;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            yield return WaitUntil(() => playback.IsWaiting, "The crow did not stop at the end of its animation.");
            Assert.That(animator.speed, Is.Zero);
            Assert.That(renderer.sprite, Is.EqualTo(frames.Last()), "대기 중에는 마지막 프레임을 유지한다.");
            Assert.That(playback.CurrentWaitSeconds, Is.InRange(.08f, .12f));
            if (cycle == 0)
            {
                Time.timeScale = 0;
                float until = Time.realtimeSinceStartup + .2f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.That(playback.IsWaiting, Is.True, "일시정지 중에는 랜덤 대기 시간도 진행하지 않는다.");
                Assert.That(renderer.sprite, Is.EqualTo(frames.Last()));
                Time.timeScale = 1;
            }
            yield return WaitUntil(() => !playback.IsWaiting, "The crow did not replay after its random wait.");
            Assert.That(playback.CurrentStartFrame, Is.InRange(1, 4));
            Assert.That(animator.speed, Is.EqualTo(10));
            Assert.That(crow.transform.localPosition, Is.EqualTo(placement));
        }
        yield return WaitUntil(() => playback.IsWaiting, "The crow did not reach its wait before leaving.");
        parent.SetActive(false);
        Assert.That(animator.speed, Is.EqualTo(10));
        Assert.That(playback.IsWaiting, Is.False);
        Assert.That(playback.CurrentStartFrame, Is.Zero);
        yield return null;
        yield return null;
        Assert.That(playback.CurrentStartFrame, Is.Zero, "비활성화 후 이전 반복 코루틴이 다시 시작하지 않는다.");
        parent.SetActive(true);
        Assert.That(playback.CurrentStartFrame, Is.InRange(1, 4));
        playback.enabled = false;
        Assert.That(animator.speed, Is.EqualTo(10));
        Assert.That(playback.CurrentStartFrame, Is.Zero);
        Object.Destroy(parent);
    }

    [UnityTest]
    public IEnumerator StandaloneAnimatorSupportsManualStartStopAndYieldsControlDuringWait()
    {
        previousScenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyManualPart();
    }

    private IEnumerator VerifyManualPart()
    {
        previousTimeScale = Time.timeScale;
        previousRandomState = UnityEngine.Random.state;
        changedGlobals = true;
        Time.timeScale = 1;
        var prop = new GameObject("AmbientProp", typeof(SpriteRenderer), typeof(Animator));
        prop.SetActive(false);
        var animator = prop.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Birds/Crow/Crow.controller");
        animator.speed = 10;
        var part = prop.AddComponent<RandomIdleAnimation>();
        using (var data = new SerializedObject(part))
        {
            data.FindProperty("animator").objectReferenceValue = animator;
            data.FindProperty("idleClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Birds/Crow/Crow_idle.anim");
            data.FindProperty("playOnEnable").boolValue = false;
            data.FindProperty("minimumStartFrame").intValue = 2;
            data.FindProperty("maximumStartFrame").intValue = 2;
            data.FindProperty("minimumWaitSeconds").floatValue = .2f;
            data.FindProperty("maximumWaitSeconds").floatValue = .3f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        prop.SetActive(true);
        Assert.That(part.IsRunning, Is.False, "수동 모드는 활성화만으로 반복을 시작하지 않는다.");
        part.Play();
        Assert.That(part.IsRunning, Is.True);
        Assert.That(part.CurrentStartFrame, Is.EqualTo(2));
        yield return WaitUntil(() => part.IsWaiting, "The standalone part did not reach its wait.");
        Assert.That(animator.speed, Is.Zero);
        part.Stop();
        Assert.That(part.IsRunning, Is.False);
        Assert.That(animator.speed, Is.EqualTo(10));
        animator.speed = 4; // 다른 연출이 제어하는 동안 이전 대기가 다시 끼어들지 않아야 한다.
        float until = Time.realtimeSinceStartup + .4f;
        while (Time.realtimeSinceStartup < until) yield return null;
        Assert.That(part.IsRunning, Is.False);
        Assert.That(part.CurrentStartFrame, Is.Zero);
        Assert.That(animator.speed, Is.EqualTo(4));
        part.Play();
        Assert.That(part.CurrentStartFrame, Is.EqualTo(2));
        part.Stop();
        Assert.That(animator.speed, Is.EqualTo(4));
        Object.Destroy(prop);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, string failure)
    {
        float deadline = Time.realtimeSinceStartup + 4;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, failure);
    }
}
