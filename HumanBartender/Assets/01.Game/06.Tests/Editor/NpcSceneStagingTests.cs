using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class NpcSceneStagingTests
{
    private SceneSetup[] previousScenes;
    private bool finished;
    private Exception failure;

    private void Prepare()
    {
        previousScenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (previousScenes != null && previousScenes.Any(value => value.isLoaded && value.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private async UniTask Observe(UniTask task)
    {
        finished = false; failure = null;
        try { await task; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private static IEnumerator Until(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, "The scene staging did not finish in time.");
    }

    private static NpcSceneStaging Staging(out Transform player)
    {
        player = new GameObject("Staging Luna").transform;
        player.position = new Vector3(2, 3, 4);
        var staging = new GameObject("Common scene staging").AddComponent<NpcSceneStaging>();
        staging.Initialize(player);
        return staging;
    }

    private static CameraControllerNew CameraRig(Transform player, out Transform anchor,
        out CinemachineCamera vcam, out PixelPerfectCamera pixel)
    {
        var cameraObject = new GameObject("Staging main camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = 16f / 9;
        pixel = cameraObject.AddComponent<PixelPerfectCamera>();
        pixel.refResolutionX = 480;
        pixel.refResolutionY = 270;
        pixel.assetsPPU = 100;
        vcam = new GameObject("Staging virtual camera").AddComponent<CinemachineCamera>();
        var composer = vcam.gameObject.AddComponent<CinemachinePositionComposer>();
        composer.TargetOffset = new Vector3(0, .7f, 0);
        anchor = new GameObject("Staging camera anchor").transform;
        anchor.SetParent(player, false);
        anchor.localPosition = new Vector3(.2f, .1f, 0);
        vcam.Target.TrackingTarget = anchor;
        var lens = vcam.Lens;
        lens.OrthographicSize = 2.5f;
        vcam.Lens = lens;
        var rig = new GameObject("Staging camera controller").AddComponent<CameraControllerNew>();
        using var data = new SerializedObject(rig);
        data.FindProperty("mainCamera").objectReferenceValue = camera;
        data.FindProperty("pixelPerfectCamera").objectReferenceValue = pixel;
        data.FindProperty("vcam").objectReferenceValue = vcam;
        data.FindProperty("cameraAnchor").objectReferenceValue = anchor;
        data.ApplyModifiedPropertiesWithoutUndo();
        return rig;
    }

    [UnityTest]
    public IEnumerator CancelledPlayerPreludeRestoresSessionPoseAndRetryCanKeepItsDestination()
    {
        Prepare();
        yield return new EnterPlayMode();
        yield return VerifyCancelledPlayerPrelude();
    }

    // EnterPlayMode reloads the domain. Keep captured runtime locals in a fresh iterator created afterward.
    private IEnumerator VerifyCancelledPlayerPrelude()
    {
        Time.timeScale = 1;
        var staging = Staging(out var player);
        var origin = player.position;
        staging.BeginSession();
        Observe(staging.MovePlayerAsync(origin + Vector3.right, .65f, NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return Until(() => player.position.x > origin.x + .1f);
        staging.EndSession(true);
        yield return Until(() => finished);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(player.position, Is.EqualTo(origin));
        Assert.That(staging.IsMovingPlayer, Is.False);
        staging.BeginSession();
        var destination = origin + Vector3.left * .78f;
        Observe(staging.MovePlayerAsync(destination, .65f, NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return Until(() => finished);
        Assert.That(failure, Is.Null);
        staging.EndSession(false);
        Assert.That(player.position, Is.EqualTo(destination));
        Assert.That(staging.IsInSession, Is.False);
    }

    [UnityTest]
    public IEnumerator CameraAndPlayerCanMoveTogetherThenRestoreFollowingThePlayersNewPosition()
    {
        Prepare();
        yield return new EnterPlayMode();
        Time.timeScale = 1;
        var staging = Staging(out var player);
        CameraRig(player, out var anchor, out var vcam, out var pixel);
        var local = anchor.localPosition;
        float originalSize = vcam.Lens.OrthographicSize;
        staging.BeginSession();
        var destination = player.position + Vector3.right;
        Observe(UniTask.WhenAll(
            staging.MovePlayerAsync(destination, .65f, NpcMotionEase.SmoothStep, CancellationToken.None),
            staging.FocusCameraAsync(new Vector3(4, 5, 0), 4.8f, .65f, CancellationToken.None))).Forget();
        yield return Until(() => finished);
        Assert.That(failure, Is.Null);
        Assert.That(player.position, Is.EqualTo(destination));
        Assert.That(anchor.parent, Is.Null);
        Assert.That(anchor.position.x, Is.EqualTo(4).Within(.0001f));
        Assert.That(anchor.position.y, Is.EqualTo(4.3f).Within(.0001f), "The camera composer's .7 offset must not be added twice.");
        Assert.That(vcam.Lens.OrthographicSize, Is.EqualTo(1.35f).Within(.0001f));
        Assert.That(pixel.enabled, Is.False);
        Observe(staging.RestoreCameraAsync(.4f, CancellationToken.None)).Forget();
        yield return Until(() => finished);
        Assert.That(failure, Is.Null);
        Assert.That(anchor.parent, Is.SameAs(player));
        Assert.That(anchor.localPosition, Is.EqualTo(local));
        Assert.That(vcam.Lens.OrthographicSize, Is.EqualTo(originalSize));
        Assert.That(pixel.enabled, Is.True);
        Assert.That(staging.HasCameraOverride, Is.False);
        staging.EndSession(false);
    }

    [UnityTest]
    public IEnumerator CancellingCameraPanRestoresCameraAndPlayerWithoutLeakingAnOverride()
    {
        Prepare();
        yield return new EnterPlayMode();
        yield return VerifyCancelledCameraPan();
    }

    private IEnumerator VerifyCancelledCameraPan()
    {
        Time.timeScale = 1;
        var staging = Staging(out var player);
        var rig = CameraRig(player, out var anchor, out var vcam, out var pixel);
        var local = anchor.localPosition;
        var origin = player.position;
        float size = vcam.Lens.OrthographicSize;
        staging.BeginSession();
        staging.SetPlayerPosition(origin + Vector3.right);
        staging.SetCameraFrame(new Vector3(-2, .023f, 0), 4.8f);
        Observe(staging.PanCameraAsync(new Vector3(2, .023f, 0), 4.8f, 2, NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return Until(() => anchor.position.x > -1.8f);
        staging.EndSession(true);
        yield return Until(() => finished);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(player.position, Is.EqualTo(origin));
        Assert.That(anchor.parent, Is.SameAs(player));
        Assert.That(anchor.localPosition, Is.EqualTo(local));
        Assert.That(vcam.Lens.OrthographicSize, Is.EqualTo(size));
        Assert.That(pixel.enabled, Is.True);
        using var next = rig.BeginFrameOverride();
        Assert.That(next.IsValid, Is.True, "The ended camera session must release exclusive ownership.");
    }
}
