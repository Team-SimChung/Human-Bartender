using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>웹 원본의 접근 보간과 이동 중 페이드 규칙을 실제 공통 Actor에서 검증한다.</summary>
public sealed class NpcWebMotionTests
{
    private SceneSetup[] sceneSetup;
    private bool finished;
    private Exception failure;

    private static NpcActor CreateActor()
    {
        var root = new GameObject("Web motion actor");
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var movement = root.AddComponent<NpcMovement2D>();
        movement.Configure(7);
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Repairman/Repairman.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        var actor = root.AddComponent<NpcActor>();
        actor.Configure(movement, animator, renderer);
        actor.SetVisible(true);
        return actor;
    }

    private void PrepareScene()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private async UniTask Observe(UniTask task)
    {
        finished = false;
        failure = null;
        try { await task; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string message)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, message);
    }

    private IEnumerator WaitForOperation() => WaitUntil(() => finished, "NPC motion did not release its lifetime.");

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator TimedApproachUsesWebSmoothStepAndExactDestination()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return VerifyApproach();
    }

    private IEnumerator VerifyApproach()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        var origin = new Vector3(-2, 7, 3);
        var destination = new Vector3(2, 7, 3);
        actor.SetPosition(origin);
        float began = Time.time;
        Observe(actor.MoveToAsync(destination, .95f, NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return WaitUntil(() => Time.time - began >= .2375f, "Approach clock did not advance.");
        float progress = Mathf.Clamp01((Time.time - began) / .95f);
        float expected = origin.x + 4 * progress * progress * (3 - 2 * progress);
        // Allow one render/update boundary; a linear move at quarter time remains distinctly farther ahead.
        float tolerance = .03f + 4 / .95f * 1.5f * Time.deltaTime;
        Assert.That(actor.transform.position.x, Is.EqualTo(expected).Within(tolerance));
        Assert.That(Mathf.Abs(actor.transform.position.x - (origin.x + 4 * progress)), Is.GreaterThan(.1f),
            "The approach must ease in, not silently use the old linear movement.");
        Assert.That(actor.transform.position.y, Is.EqualTo(origin.y));
        Assert.That(actor.transform.position.z, Is.EqualTo(origin.z));
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(.93f));
        Assert.That(actor.transform.position, Is.EqualTo(destination));
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(actor.GetComponent<NpcMovement2D>().Speed, Is.Zero);
        using (var data = new SerializedObject(actor.GetComponent<NpcMovement2D>()))
            Assert.That(data.FindProperty("moveSpeed").floatValue, Is.EqualTo(7), "Timed moves must preserve authored speed.");
        Object.Destroy(actor.gameObject);
    }

    [UnityTest]
    public IEnumerator BothExitDirectionsKeepMovingAtWebSpeedDuringSmoothFade()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return VerifyExits();
    }

    private IEnumerator VerifyExits()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var movement = actor.GetComponent<NpcMovement2D>();
        foreach (float direction in new[] { -1f, 1f })
        {
            actor.SetVisible(true);
            var origin = new Vector3(4, -.7f, 2);
            actor.SetPosition(origin);
            float began = Time.time;
            Observe(actor.DepartWhileMovingAsync(new Vector2(direction * .845f, 0), 1, .3f,
                NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
            yield return WaitUntil(() => Mathf.Abs(actor.transform.position.x - origin.x) >= .3f, "Exit did not start moving.");
            Assert.That(renderer.color.a, Is.EqualTo(1), "The first second must remain fully visible.");
            Assert.That(movement.Speed, Is.EqualTo(.65f).Within(.015f));
            Assert.That(movement.FacingRight, Is.EqualTo(direction > 0));
            yield return WaitUntil(() => renderer.color.a < .9f, "The exit fade never started.");
            Assert.That(renderer.color.a, Is.GreaterThan(0));
            Assert.That(actor.IsVisible, Is.True);
            float distance = Mathf.Abs(actor.transform.position.x - origin.x);
            float fadeProgress = Mathf.Clamp01((distance / .65f - 1) / .3f);
            float expectedAlpha = 1 - fadeProgress * fadeProgress * (3 - 2 * fadeProgress);
            Assert.That(renderer.color.a, Is.EqualTo(expectedAlpha).Within(.002f),
                "Movement and smooth fade must share the web's elapsed time.");
            float fadingX = actor.transform.position.x;
            float alpha = renderer.color.a;
            yield return WaitUntil(() => renderer.color.a < alpha - .1f, "Fade did not continue.");
            Assert.That((actor.transform.position.x - fadingX) * direction, Is.GreaterThan(0),
                "The actor must keep moving while its opacity decreases.");
            yield return WaitForOperation();
            Assert.That(failure, Is.Null);
            Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(1.28f));
            Assert.That(actor.transform.position.x, Is.EqualTo(origin.x + direction * .845f).Within(.00001f));
            Assert.That(actor.transform.position.y, Is.EqualTo(origin.y));
            Assert.That(actor.transform.position.z, Is.EqualTo(origin.z));
            Assert.That(renderer.color.a, Is.Zero);
            Assert.That(actor.IsVisible, Is.False);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(actor.GetComponent<Animator>().enabled, Is.False);
            Assert.That(movement.IsMoving, Is.False);
            Assert.That(actor.IsBusy, Is.False);
        }
        Object.Destroy(actor.gameObject);
    }

    [UnityTest]
    public IEnumerator InterruptedMovingFadeRestoresSessionPoseAndPreservesExplicitHiding()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return VerifyCancellationAndValidation();
    }

    private IEnumerator VerifyCancellationAndValidation()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var movement = actor.GetComponent<NpcMovement2D>();
        var origin = new Vector3(3, 6, 2);
        actor.SetPosition(origin);
        var sessionPose = actor.CapturePose();
        actor.SetPosition(origin + Vector3.right);
        Observe(actor.DepartWhileMovingAsync(Vector2.left * .845f, 1, .3f,
            NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return WaitUntil(() => renderer.color.a < .9f, "Cancellation test did not reach fade.");
        actor.RestorePose(sessionPose);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.transform.position, Is.EqualTo(origin), "Session restoration must win over action-start restoration.");
        Assert.That(renderer.color.a, Is.EqualTo(1));
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(movement.FacingRight, Is.True);
        Assert.That(movement.Speed, Is.Zero);
        Assert.That(actor.IsBusy, Is.False);

        Observe(actor.DepartWhileMovingAsync(Vector2.right * .26f, .1f, .3f,
            NpcMotionEase.SmoothStep, CancellationToken.None)).Forget();
        yield return WaitUntil(() => renderer.color.a < .9f, "Explicit hide test did not reach fade.");
        actor.SetVisible(false);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.transform.position, Is.EqualTo(origin));
        Assert.That(renderer.color.a, Is.EqualTo(1));
        Assert.That(actor.IsVisible, Is.False, "Cancellation cleanup must not undo an explicit hide.");
        Assert.That(renderer.enabled, Is.False);
        actor.SetVisible(true);
        AssertInvalidSettings(actor);
        Assert.That(actor.IsBusy, Is.False);
        Object.Destroy(actor.gameObject);
    }

    private static void AssertInvalidSettings(NpcActor actor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.DepartWhileMovingAsync(Vector2.right, 0, 0,
            NpcMotionEase.SmoothStep, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.DepartWhileMovingAsync(Vector2.right, -1, .3f,
            NpcMotionEase.SmoothStep, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.DepartWhileMovingAsync(Vector2.right, 1, float.NaN,
            NpcMotionEase.SmoothStep, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.MoveToAsync(Vector3.right, .95f,
            (NpcMotionEase)99, CancellationToken.None).GetAwaiter().GetResult());
    }
}
