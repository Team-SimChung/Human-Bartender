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
        using var data = new SerializedObject(presentation);
        data.FindProperty("terraceCamera").objectReferenceValue = camera;
        data.FindProperty("cameraBrain").objectReferenceValue = output.GetComponent<CinemachineBrain>();
        data.FindProperty("speakers").objectReferenceValue = host.AddComponent<WorldDialogueSpeakers>();
        data.FindProperty("lunaAnchor").objectReferenceValue = luna;
        data.FindProperty("terraceForeground").objectReferenceValue = foreground;
        data.FindProperty("fadeDuration").floatValue = 0;
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

    private IEnumerator VerifyEntranceLifetime()
    {
        previousTimeScale = Time.timeScale;
        changedTimeScale = true;
        Time.timeScale = 0;
        var presentation = CreatePresentation(out var foreground, out var camera);
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
        Assert.That(foreground.localPosition, Is.EqualTo(new Vector3(0, 2, 3)));
        AssertCarriedHierarchy();
        CollectionAssert.AreEqual(new[] { "out", "in", "out", "in" }, fades.Calls);
        yield return null;
        Assert.That(foreground.localPosition.x, Is.Zero, "The cancelled tween must not write another frame.");

        var replayFades = new RecordingFade(foreground);
        Observe(presentation.EnterAsync(replayFades, CancellationToken.None)).Forget();
        yield return WaitUntil(() => replayFades.FirstFadeInStarted, "A fresh entrance did not start.");
        Assert.That(foreground.localPosition.x, Is.EqualTo(-1.8f));
        replayFades.ReleaseFirstFadeIn();
        yield return WaitUntil(() => finished, "A fresh entrance did not finish.");
        Assert.That(failure, Is.Null);
        Assert.That(presentation.IsActive, Is.True);
        Assert.That(foreground.localPosition, Is.EqualTo(new Vector3(0, 2, 3)));
        AssertCarriedHierarchy();

        Observe(presentation.ExitAsync(replayFades, CancellationToken.None)).Forget();
        yield return WaitUntil(() => finished, "Exit did not restore the indoor camera.");
        Assert.That(failure, Is.Null);
        Assert.That(presentation.IsActive, Is.False);
        Assert.That(camera.enabled, Is.False);
    }
}
