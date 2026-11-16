using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class HomeTerracePresentationTests
{
    private SceneSetup[] sceneSetup;
    private bool finished;
    private Exception failure;
    private float previousTimeScale;
    private bool changedTimeScale;

    private sealed class RecordingFade : ISceneFadeService
    {
        private readonly UniTaskCompletionSource firstFadeIn = new();
        private readonly Transform foreground;
        public readonly List<string> Calls = new();
        public bool FirstFadeInStarted { get; private set; }
        public float FirstFadeInStartX { get; private set; }

        public RecordingFade(Transform foreground) => this.foreground = foreground;

        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("out");
            return UniTask.CompletedTask;
        }

        public async UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("in");
            if (!FirstFadeInStarted)
            {
                FirstFadeInStarted = true;
                FirstFadeInStartX = foreground.localPosition.x;
                await firstFadeIn.Task.AttachExternalCancellation(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void ReleaseFirstFadeIn() => firstFadeIn.TrySetResult();
    }

    private async UniTask Observe(UniTask operation)
    {
        finished = false;
        failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string reason)
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, reason);
    }

    private void PrepareEmptyScene()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static HomeTerracePresentation CreatePresentation(out Transform foreground, out CinemachineCamera camera)
    {
        var host = new GameObject("Terrace_BG_City");
        var presentation = host.AddComponent<HomeTerracePresentation>();
        foreground = new GameObject("Terrace").transform;
        foreground.SetParent(host.transform, false);
        foreground.localPosition = new Vector3(0, 2, 3);
        var luna = new GameObject("LunaAnchor").transform;
        luna.SetParent(host.transform, false);
        var output = new GameObject("Camera", typeof(Camera), typeof(CinemachineBrain));
        camera = new GameObject("CM_Terrace", typeof(CinemachineCamera)).GetComponent<CinemachineCamera>();
        camera.enabled = false;
        var audioHost = new GameObject("IndoorAmbienceTest", typeof(AudioSource), typeof(DistanceAmbientAudio));
        audioHost.transform.SetParent(host.transform, false);
        var ambience = audioHost.GetComponent<DistanceAmbientAudio>();
        using (var audioData = new SerializedObject(ambience))
        {
            audioData.FindProperty("source").objectReferenceValue = audioHost.GetComponent<AudioSource>();
            audioData.ApplyModifiedPropertiesWithoutUndo();
        }
        using var data = new SerializedObject(presentation);
        data.FindProperty("terraceCamera").objectReferenceValue = camera;
        data.FindProperty("cameraBrain").objectReferenceValue = output.GetComponent<CinemachineBrain>();
        data.FindProperty("speakers").objectReferenceValue = host.AddComponent<WorldDialogueSpeakers>();
        data.FindProperty("lunaAnchor").objectReferenceValue = luna;
        data.FindProperty("terraceForeground").objectReferenceValue = foreground;
        data.FindProperty("fadeDuration").floatValue = 0;
        var indoorSounds = data.FindProperty("indoorAmbience");
        indoorSounds.arraySize = 1;
        indoorSounds.GetArrayElementAtIndex(0).objectReferenceValue = ambience;
        data.ApplyModifiedPropertiesWithoutUndo();
        return presentation;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (changedTimeScale) Time.timeScale = previousTimeScale;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void HomeBindsTheTerraceForegroundAndAuthoredEntrance()
    {
        PrepareEmptyScene();
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Home.unity", OpenSceneMode.Single);
        var presentation = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<HomeTerracePresentation>(true)).Single();
        using var data = new SerializedObject(presentation);
        var foreground = data.FindProperty("terraceForeground").objectReferenceValue as Transform;
        Assert.That(foreground, Is.Not.Null);
        Assert.That(foreground.name, Is.EqualTo("Terrace"));
        Assert.That(foreground.parent, Is.EqualTo(presentation.transform));
        Assert.That(presentation.name, Is.EqualTo("Terrace_BG_City"));
        var descendants = foreground.GetComponentsInChildren<Transform>(true).Select(child => child.name).ToArray();
        Assert.That(descendants, Does.Contain("balustrade"), "The railing must move with Terrace.");
        Assert.That(descendants, Does.Contain("Terrace_0"), "The terrace characters and their anchors must move with Terrace.");
        Assert.That(data.FindProperty("entranceStartX").floatValue, Is.EqualTo(-1.8f));
        Assert.That(data.FindProperty("entranceEndX").floatValue, Is.Zero);
        Assert.That(data.FindProperty("entranceDuration").floatValue, Is.GreaterThan(0));
        Assert.That(data.FindProperty("entranceEase").intValue, Is.EqualTo((int)Ease.InOutSine));
    }

    [UnityTest]
    public IEnumerator EntranceWaitsForFadeAndMovementPreservesOtherAxesAndReplaysAfterCancellation()
    {
        PrepareEmptyScene();
        yield return new EnterPlayMode();
        yield return VerifyEntranceLifetime();
    }

    [UnityTest]
    public IEnumerator ZeroStepBirdIsAlreadyIdleBeforeFadeInAndFollowsTheRailingDuringEntrance()
    {
        PrepareEmptyScene();
        yield return new EnterPlayMode();
        yield return VerifyZeroStepBird(false);
    }

    [UnityTest]
    public IEnumerator ZeroStepBirdUnderTerraceDoesNotApplyTheEntranceOffsetTwice()
    {
        PrepareEmptyScene();
        yield return new EnterPlayMode();
        yield return VerifyZeroStepBird(true);
    }

    private IEnumerator VerifyZeroStepBird(bool parentToForeground)
    {
        var presentation = CreatePresentation(out var foreground, out _);
        var host = new GameObject("BirdArrival");
        if (parentToForeground) host.transform.SetParent(foreground, false);
        if (parentToForeground) host.SetActive(false);
        var arrival = host.AddComponent<TerraceBirdArrival>();
        var bird = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/04.Shared/02.Birds/Pigeon.prefab"), host.transform);
        var animator = bird.GetComponent<Animator>();
        var landing = new GameObject("LandingPoint").transform;
        landing.SetParent(host.transform, false);
        landing.position = new Vector3(1, 2, 0);
        var landingAtRest = landing.position;
        using (var data = new SerializedObject(arrival))
        {
            data.FindProperty("terrace").objectReferenceValue = presentation;
            data.FindProperty("bird").objectReferenceValue = bird.transform;
            data.FindProperty("birdAnimator").objectReferenceValue = animator;
            data.FindProperty("birdIdle").objectReferenceValue = bird.GetComponent<RandomIdleAnimation>();
            data.FindProperty("landingPoint").objectReferenceValue = landing;
            var days = data.FindProperty("days");
            days.GetArrayElementAtIndex(0).FindPropertyRelative("spawn").boolValue = true;
            days.GetArrayElementAtIndex(0).FindPropertyRelative("startStep").intValue = 0;
            days.GetArrayElementAtIndex(1).FindPropertyRelative("startStep").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        using (var data = new SerializedObject(presentation))
        {
            data.FindProperty("entranceDuration").floatValue = .1f;
            if (parentToForeground) data.FindProperty("terraceBirdsRoot").objectReferenceValue = host;
            var birds = data.FindProperty("birdArrivals");
            birds.arraySize = 1;
            birds.GetArrayElementAtIndex(0).objectReferenceValue = arrival;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var fades = new RecordingFade(foreground);
        Observe(presentation.EnterAsync(fades, CancellationToken.None, "night0", 0)).Forget();
        yield return WaitUntil(() => fades.FirstFadeInStarted, "The terrace did not reach its first fade-in.");
        Assert.That(bird.activeSelf, Is.True, "스텝 0은 테라스가 보이기 전부터 배치되어 있어야 한다.");
        Assert.That(arrival.HasArrived, Is.True);
        Assert.That(arrival.IsFlying, Is.False);
        Assert.That(animator.GetBool("IsFlying"), Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(Vector3.Distance(bird.transform.position, landingAtRest + Vector3.left * 1.8f), Is.LessThan(.0001f));
        Assert.That(arrival.TryStartAtStep(0), Is.False, "스텝 0은 도착 연출을 다시 실행하지 않는다.");
        fades.ReleaseFirstFadeIn();
        float deadline = Time.realtimeSinceStartup + 3;
        while (!finished && Time.realtimeSinceStartup < deadline)
        {
            Assert.That(bird.transform.position.x, Is.EqualTo(landingAtRest.x + foreground.localPosition.x).Within(.0001f));
            yield return null;
        }
        Assert.That(finished, Is.True);
        Assert.That(failure, Is.Null);
        Assert.That(bird.transform.position, Is.EqualTo(landing.position));
        Assert.That(arrival.TryStartAtStep(1), Is.False);
        presentation.BeginStory("night1", 1);
        Assert.That(bird.activeSelf, Is.False, "재생 체크가 꺼진 날짜는 0이어도 나타나지 않는다.");
        presentation.BeginStory("night0", 0);
        Assert.That(bird.activeSelf, Is.True, "재진입하면 스텝 0의 Idle 배치를 복원한다.");
        Observe(presentation.ExitAsync(fades, CancellationToken.None)).Forget();
        yield return WaitUntil(() => finished, "The terrace exit did not complete.");
        Assert.That(failure, Is.Null);
        Assert.That(bird.activeSelf, Is.False);
        Assert.That(arrival.HasTriggered, Is.False);
    }

    private IEnumerator VerifyEntranceLifetime()
    {
        previousTimeScale = Time.timeScale;
        changedTimeScale = true;
        Time.timeScale = 0;
        var presentation = CreatePresentation(out var foreground, out var camera);
        var birds = new GameObject("TerraceBirdsGroup");
        birds.transform.SetParent(foreground, false);
        birds.SetActive(false);
        var crow = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/04.Shared/02.Birds/Crow.prefab"), birds.transform).GetComponent<RandomIdleAnimation>();
        using (var data = new SerializedObject(presentation))
        {
            data.FindProperty("terraceBirdsRoot").objectReferenceValue = birds;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(birds.activeInHierarchy, Is.False);
        Assert.That(crow.CurrentStartFrame, Is.Zero);
        var child = new GameObject("CarriedGroup").transform;
        child.SetParent(foreground, false);
        var childLocal = new Vector3(.25f, .5f, .75f);
        child.localPosition = childLocal;
        var grandchild = new GameObject("CarriedSprite").transform;
        grandchild.SetParent(child, false);
        var grandchildLocal = new Vector3(.5f, .25f, 0);
        grandchild.localPosition = grandchildLocal;
        var childOrigin = child.position;
        var grandchildOrigin = grandchild.position;
        void AssertCarriedHierarchy()
        {
            Assert.That(child.localPosition, Is.EqualTo(childLocal));
            Assert.That(grandchild.localPosition, Is.EqualTo(grandchildLocal));
            var offset = Vector3.right * foreground.localPosition.x;
            Assert.That(Vector3.Distance(child.position, childOrigin + offset), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(grandchild.position, grandchildOrigin + offset), Is.LessThan(.0001f));
        }
        var fades = new RecordingFade(foreground);
        using var cancellation = new CancellationTokenSource();
        Observe(presentation.EnterAsync(fades, cancellation.Token)).Forget();
        yield return WaitUntil(() => fades.FirstFadeInStarted, "The terrace camera did not reach fade-in.");
        Assert.That(finished, Is.False);
        Assert.That(fades.FirstFadeInStartX, Is.EqualTo(-1.8f));
        Assert.That(birds.activeInHierarchy, Is.True, "화면이 밝아지기 전에 새를 활성화한다.");
        Assert.That(crow.CurrentStartFrame, Is.InRange(1, 4));
        Assert.That(foreground.localPosition, Is.EqualTo(new Vector3(-1.8f, 2, 3)));
        AssertCarriedHierarchy();
        fades.ReleaseFirstFadeIn();
        yield return WaitUntil(() => foreground.localPosition.x > -1.8f, "The entrance did not move with paused game time.");
        Assert.That(finished, Is.False, "EnterAsync must finish after the entrance, before the caller starts dialogue.");
        Assert.That(foreground.localPosition.x, Is.LessThan(0));
        Assert.That(foreground.localPosition.y, Is.EqualTo(2));
        Assert.That(foreground.localPosition.z, Is.EqualTo(3));
        AssertCarriedHierarchy();

        cancellation.Cancel();
        yield return WaitUntil(() => finished, "Cancellation did not finish terrace recovery.");
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(presentation.IsActive, Is.False);
        Assert.That(camera.enabled, Is.False);
        Assert.That(birds.activeSelf, Is.False, "진입 취소는 새의 반복과 대기도 정리한다.");
        Assert.That(crow.CurrentStartFrame, Is.Zero);
        Assert.That(foreground.localPosition, Is.EqualTo(new Vector3(0, 2, 3)));
        AssertCarriedHierarchy();
        CollectionAssert.AreEqual(new[] { "out", "in", "out", "in" }, fades.Calls);
        yield return null;
        Assert.That(foreground.localPosition.x, Is.Zero, "The cancelled tween must not write another frame.");
        Assert.That(crow.CurrentStartFrame, Is.Zero);

        var replayFades = new RecordingFade(foreground);
        Observe(presentation.EnterAsync(replayFades, CancellationToken.None)).Forget();
        yield return WaitUntil(() => replayFades.FirstFadeInStarted, "A fresh entrance did not start.");
        Assert.That(birds.activeInHierarchy, Is.True);
        Assert.That(crow.CurrentStartFrame, Is.InRange(1, 4), "재진입하면 랜덤 재생을 새로 시작한다.");
        Assert.That(foreground.localPosition.x, Is.EqualTo(-1.8f));
        replayFades.ReleaseFirstFadeIn();
        yield return WaitUntil(() => finished, "A fresh entrance did not finish.");
        Assert.That(failure, Is.Null);
        Assert.That(presentation.IsActive, Is.True);
        Assert.That(presentation.GetComponentInChildren<DistanceAmbientAudio>().PlaybackAllowed, Is.False);
        Assert.That(foreground.localPosition, Is.EqualTo(new Vector3(0, 2, 3)));
        AssertCarriedHierarchy();

        Observe(presentation.ExitAsync(replayFades, CancellationToken.None)).Forget();
        yield return WaitUntil(() => finished, "Exit did not restore the indoor camera.");
        Assert.That(failure, Is.Null);
        Assert.That(presentation.IsActive, Is.False);
        Assert.That(presentation.GetComponentInChildren<DistanceAmbientAudio>().PlaybackAllowed, Is.True);
        Assert.That(camera.enabled, Is.False);
        Assert.That(birds.activeSelf, Is.False);
        Assert.That(crow.CurrentStartFrame, Is.Zero);
    }
}
