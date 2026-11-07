using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class NpcActorTests
{
    private SceneSetup[] sceneSetup;
    private bool finished;
    private Exception failure;

    private async UniTask Observe(UniTask operation)
    {
        finished = false;
        failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private IEnumerator WaitForOperation()
    {
        float until = Time.realtimeSinceStartup + 5;
        while (!finished && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(finished, Is.True, "NPC operation did not release its lifetime.");
    }

    private static void AssertOverlappingActionRejected(NpcActor actor)
    {
        // EnterPlayMode가 도메인을 다시 로드하므로 캡처할 지역 변수는 검사 반복자 밖에 둔다.
        Assert.Throws<InvalidOperationException>(() => actor.DepartAsync(Vector2.left, .1f, CancellationToken.None)
            .GetAwaiter().GetResult(), "An actor must reject overlapping owners.");
    }

    private static NpcActor CreateActor(string controllerPath =
        "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Repairman/Repairman.controller")
    {
        var root = new GameObject("NPC lifecycle test");
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var movement = root.AddComponent<NpcMovement2D>();
        movement.Configure(2);
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null,
            "The authored NPC Animator Controller is required for this integration test.");
        var actor = root.AddComponent<NpcActor>();
        actor.Configure(movement, animator, renderer);
        actor.SetVisible(true);
        return actor;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator CancelDisableRetryAndSuccessfulDepartureHaveDifferentFinalPoses()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();

        Time.timeScale = 1;
        var actor = CreateActor();
        var root = actor.gameObject;
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var movement = actor.GetComponent<NpcMovement2D>();
        var animator = actor.GetComponent<Animator>();

        using (var cancellation = new CancellationTokenSource())
        {
            Observe(actor.MoveToAsync(Vector3.right * 10, cancellation.Token)).Forget();
            yield return null;
            yield return null;
            Assert.That(actor.IsBusy, Is.True);
            Assert.That(movement.Speed, Is.GreaterThan(0));
            Assert.That(root.transform.position.x, Is.GreaterThan(0));
            AssertOverlappingActionRejected(actor);
            cancellation.Cancel();
            yield return WaitForOperation();
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(actor.IsBusy, Is.False);
            Assert.That(movement.Speed, Is.Zero);
            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
        }

        // 느린 프레임이 페이드를 끝내기 전에 부분 투명 상태에서 취소하는 조건을 보장한다.
        float previousMaximumDeltaTime = Time.maximumDeltaTime;
        try
        {
            Time.maximumDeltaTime = Mathf.Min(previousMaximumDeltaTime, .05f);
            yield return null;
            Observe(actor.DepartAsync(Vector2.zero, .5f, CancellationToken.None)).Forget();
            yield return null;
            yield return null;
            Assert.That(actor.IsBusy, Is.True);
            Assert.That(finished, Is.False);
            Assert.That(renderer.color.a, Is.GreaterThan(0));
            Assert.That(renderer.color.a, Is.LessThan(1));
            actor.SetVisible(false);
            yield return WaitForOperation();
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(renderer.color.a, Is.EqualTo(1));
            Assert.That(renderer.enabled, Is.False);
            Assert.That(animator.enabled, Is.False);
            Assert.That(actor.IsVisible, Is.False);
        }
        finally
        {
            Time.maximumDeltaTime = previousMaximumDeltaTime;
        }
        actor.SetVisible(true);

        Observe(actor.MoveToAsync(Vector3.left * 10, CancellationToken.None)).Forget();
        yield return null;
        root.SetActive(false);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
        root.SetActive(true);

        var destination = Vector3.right * .2f;
        Observe(actor.DepartAsync(new Vector2(.2f, 0), .05f, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(root.transform.position, Is.EqualTo(destination));
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(animator.enabled, Is.False);
        actor.CancelAction();
        yield return null;
        yield return null;
        Assert.That(root.transform.position, Is.EqualTo(destination), "Completed departure must not snap back.");
        Assert.That(renderer.enabled, Is.False);

        actor.SetVisible(true);
        Assert.That(renderer.color.a, Is.EqualTo(1));
        Observe(actor.MoveToAsync(Vector3.zero, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.Null, "A completed or cancelled actor must be reusable.");
        Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
        Object.Destroy(root);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DepartureUsesTheCurrentWorldPositionAndIgnoresParentRotationAndScale()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyWorldDepartureOffsets();
    }

    private IEnumerator VerifyWorldDepartureOffsets()
    {
        Time.timeScale = 1;
        var parent = new GameObject("Rotated and scaled NPC parent");
        parent.transform.SetPositionAndRotation(new Vector3(30, -8, 2), Quaternion.Euler(0, 0, 67));
        parent.transform.localScale = new Vector3(2, 3, 1.5f);
        var actor = CreateActor();
        actor.transform.SetParent(parent.transform, true);
        actor.GetComponent<NpcMovement2D>().Configure(50);
        actor.transform.position = new Vector3(-11.675f, 9.62f, 3);
        var commandOffset = new Vector2(4, 0);
        Observe(actor.DepartAsync(commandOffset, .05f, CancellationToken.None)).Forget();
        // 다음 명령을 준비해도 진행 중인 퇴장 목적지는 바뀌면 안 된다.
        commandOffset = new Vector2(0, 1.5f);
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(Vector3.Distance(actor.transform.position, new Vector3(-7.675f, 9.62f, 3)), Is.LessThan(.0001f));

        foreach (var offset in new[] { commandOffset, new Vector2(0, -2.25f), new Vector2(4, 0) })
        {
            actor.SetVisible(true);
            // 같은 배우와 부모를 재사용해도 매 실행의 출발 위치는 달라진다.
            actor.transform.position += new Vector3(1.25f, -3, 0);
            var start = actor.transform.position;
            Observe(actor.DepartAsync(offset, .05f, CancellationToken.None)).Forget();
            yield return WaitForOperation();
            Assert.That(failure, Is.Null);
            var expected = start + new Vector3(offset.x, offset.y, 0);
            Assert.That(Vector3.Distance(actor.transform.position, expected), Is.LessThan(.0001f),
                "Departure must add the command's offset in world space and preserve Z.");
            Assert.That(actor.IsVisible, Is.False);
        }
        Object.Destroy(parent);
    }

    [UnityTest]
    public IEnumerator AnimatorActionRunsOnceAndCancellationRestoresItsVisualPoseAndSpeed()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyAnimatorAction();
    }

    private IEnumerator VerifyAnimatorAction()
    {
        Time.timeScale = 1;
        var actor = CreateActor("Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/DayOneThug/DayOneThug.controller");
        var animator = actor.GetComponent<Animator>();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        animator.Update(0);
        animator.speed = 1.75f;
        var baseline = renderer.transform.localScale;
        float began = Time.time;
        Observe(actor.PlayAnimationAsync("Punch", "Idle", .3f, CancellationToken.None)).Forget();
        Assert.That(actor.IsBusy, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Punch"), Is.True);
        AssertOverlappingActionRejected(actor);
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(.29f));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(animator.speed, Is.EqualTo(1.75f));
        Assert.That(renderer.transform.localScale, Is.EqualTo(baseline));

        // 잘못된 상태나 반복 클립은 즉시 실패하되 배우를 다시 사용할 수 있어야 한다.
        Observe(actor.PlayAnimationAsync("MissingState", "Idle", .3f, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<InvalidOperationException>());
        Assert.That(actor.IsBusy, Is.False);
        Observe(actor.PlayAnimationAsync("Idle", "Idle", .3f, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<InvalidOperationException>());
        Assert.That(actor.IsBusy, Is.False);

        var sessionPose = actor.CapturePose();
        actor.transform.position = new Vector3(3, 2, 0);
        actor.FaceTowards(Vector3.left * 10);
        Observe(actor.PlayAnimationAsync("Punch", "Idle", 1, CancellationToken.None)).Forget();
        float until = Time.time + .12f;
        while (Time.time < until) yield return null;
        Assert.That(renderer.transform.localScale, Is.Not.EqualTo(baseline), "The authored Animator must actually animate the Sprite.");
        actor.RestorePose(sessionPose);
        actor.SetVisible(false);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
        Assert.That(renderer.transform.localScale, Is.EqualTo(baseline));
        Assert.That(animator.speed, Is.EqualTo(1.75f));
        Assert.That(actor.IsVisible, Is.False, "Pose restoration must not override explicit visibility policy.");
        Assert.That(renderer.enabled, Is.False);
        actor.SetVisible(true);
        animator.Update(0);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Object.Destroy(actor.gameObject);
    }

    [UnityTest]
    public IEnumerator SequencePoseWinsOverTheCancelledDepartureStartPose()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifySequencePoseRestoration();
    }

    private IEnumerator VerifySequencePoseRestoration()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        actor.GetComponent<NpcMovement2D>().Configure(50);
        var sessionPose = actor.CapturePose();
        Observe(actor.MoveToAsync(Vector3.right, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.right));
        Observe(actor.DepartAsync(new Vector2(100, 0), .5f, CancellationToken.None)).Forget();
        Assert.That(actor.IsBusy, Is.True);
        actor.RestorePose(sessionPose);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero),
            "Cancelling departure must restore the full interaction start, not the later departure start.");
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(actor.IsVisible, Is.True);
        Observe(actor.DepartAsync(Vector2.zero, 0, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        actor.RestorePose(sessionPose);
        Assert.That(actor.IsVisible, Is.False, "The interaction decides whether a completed departure should reappear.");
        Assert.That(actor.GetComponentInChildren<SpriteRenderer>().color.a, Is.EqualTo(1));
        Object.Destroy(actor.gameObject);
    }

    [UnityTest]
    public IEnumerator TimedMovementPreservesSpeedSettingsFacingAndCancellation()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyTimedMovement();
    }

    private IEnumerator VerifyTimedMovement()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        var movement = actor.GetComponent<NpcMovement2D>();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        movement.Configure(1.25f);
        var origin = new Vector3(2, 3, 4);
        actor.SetPosition(origin);
        actor.FaceTowards(origin + Vector3.left);
        float began = Time.time;
        Observe(actor.MoveToAsync(origin + Vector3.right * 4, .4f, CancellationToken.None)).Forget();
        Assert.That(renderer.flipX, Is.False);
        while (Time.time < began + .15f) yield return null;
        Assert.That(actor.transform.position.x, Is.GreaterThan(origin.x + .5f),
            "Timed movement must use its duration instead of the much slower authored speed.");
        Assert.That(actor.transform.position.x, Is.LessThan(origin.x + 4));
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(.39f));
        Assert.That(actor.transform.position, Is.EqualTo(origin + Vector3.right * 4));
        using (var data = new SerializedObject(movement))
            Assert.That(data.FindProperty("moveSpeed").floatValue, Is.EqualTo(1.25f));

        actor.FaceTowards(actor.transform.position + Vector3.left);
        var verticalDestination = actor.transform.position + Vector3.up;
        Observe(actor.MoveToAsync(verticalDestination, .15f, CancellationToken.None)).Forget();
        Assert.That(movement.FacingRight, Is.True, "Legacy vertical reveals clear the horizontal flip.");
        Assert.That(renderer.flipX, Is.False);
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(actor.transform.position, Is.EqualTo(verticalDestination));

        actor.FaceTowards(actor.transform.position + Vector3.left);
        using (var cancellation = new CancellationTokenSource())
        {
            Observe(actor.MoveToAsync(verticalDestination + Vector3.right * 20, .8f, cancellation.Token)).Forget();
            began = Time.time;
            while (Time.time < began + .05f) yield return null;
            cancellation.Cancel();
            yield return WaitForOperation();
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(actor.transform.position, Is.EqualTo(verticalDestination));
            Assert.That(movement.FacingRight, Is.False);
            Assert.That(renderer.flipX, Is.True);
            Assert.That(actor.IsBusy, Is.False);
        }

        // 제자리 이동도 지정 시간을 소비한다. 느린 한 프레임이 검사 구간을 건너뛰지 않도록 시간을 잠시 멈춘다.
        float previousTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            yield return null;
            began = Time.time;
            Observe(actor.MoveToAsync(verticalDestination, .15f, CancellationToken.None)).Forget();
            Assert.That(actor.IsBusy, Is.True);
            yield return null;
            Assert.That(finished, Is.False);
        }
        finally
        {
            Time.timeScale = previousTimeScale;
        }
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(Time.time - began, Is.GreaterThanOrEqualTo(.14f));
        Assert.That(actor.transform.position, Is.EqualTo(verticalDestination));
        Object.Destroy(actor.gameObject);
    }

    [UnityTest]
    public IEnumerator HiddenPlacementAndStationaryFadeKeepTheirIndependentLifetimes()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyPlacementAndFade();
    }

    private IEnumerator VerifyPlacementAndFade()
    {
        Time.timeScale = 1;
        var actor = CreateActor();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var origin = new Vector3(7, -2, 4);
        actor.SetVisible(false);
        actor.SetPosition(origin);
        Assert.That(actor.transform.position, Is.EqualTo(origin));
        Assert.That(actor.IsVisible, Is.False, "Preparing a hidden actor must not reveal it.");
        actor.SetVisible(true);
        using (var cancellation = new CancellationTokenSource())
        {
            Observe(actor.FadeOutAsync(.3f, cancellation.Token)).Forget();
            float until = Time.time + .05f;
            while (Time.time < until) yield return null;
            Assert.That(renderer.color.a, Is.InRange(0.01f, .99f));
            Assert.That(actor.transform.position, Is.EqualTo(origin));
            Assert.Throws<InvalidOperationException>(() => actor.SetPosition(Vector3.zero));
            cancellation.Cancel();
            yield return WaitForOperation();
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(renderer.color.a, Is.EqualTo(1));
            Assert.That(actor.IsVisible, Is.True);
            Assert.That(actor.IsBusy, Is.False);
        }
        Observe(actor.FadeOutAsync(.05f, CancellationToken.None)).Forget();
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(origin));
        actor.SetPosition(origin + Vector3.up);
        Assert.That(actor.IsVisible, Is.False);
        actor.SetVisible(true);
        Assert.That(renderer.color.a, Is.EqualTo(1));
        Object.Destroy(actor.gameObject);
    }

    private void PrepareAuthoredCatActor()
    {
        const string controllerPath = "Assets/01.Game/03.Content/07.Animations/01.Characters/NpcInteraction/Home_neighborhood-homeCat_boobi/Actor.controller";
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var home = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Home.unity", OpenSceneMode.Additive);
        try
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            Assert.That(controller, Is.Not.Null, "Run the remaining NPC migration before this animation integration test.");
            var source = home.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NpcActor>(true))
                .Single(value => value.GetComponent<Animator>().runtimeAnimatorController == controller);
            var clone = Object.Instantiate(source.gameObject);
            clone.name = "Authored NPC cat test";
            SceneManager.MoveGameObjectToScene(clone, isolated);
        }
        finally { EditorSceneManager.CloseScene(home, true); }
        SceneManager.SetActiveScene(isolated);
    }

    [UnityTest]
    public IEnumerator AuthoredCatReactionsAnimateSpritesAndKeepRootAndSpeechAnchorPose()
    {
        PrepareAuthoredCatActor();
        yield return new EnterPlayMode();
        yield return VerifyAuthoredCatReactions(false);
    }

    [UnityTest]
    public IEnumerator LegacyCatAnimationNamesKeepWebSpritesAndRestoreIdleAfterCancelOrCompletion()
    {
        PrepareAuthoredCatActor();
        yield return new EnterPlayMode();
        yield return VerifyAuthoredCatReactions(true);
    }

    private IEnumerator VerifyAuthoredCatReactions(bool legacyAliases)
    {
        Time.timeScale = 1;
        var actor = GameObject.Find("Authored NPC cat test").GetComponent<NpcActor>();
        var animator = actor.GetComponent<Animator>();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var anchor = actor.GetComponentsInChildren<Transform>(true).Single(value => value.name == "Speech anchor");
        actor.SetVisible(true);
        animator.Update(0);
        // Inspector에서 조정한 값은 대기 클립과 반응 애니메이션 이후에도 유지되어야 한다.
        actor.transform.localScale = Vector3.Scale(actor.transform.localScale, new Vector3(1.17f, .92f, 1));
        var scale = actor.transform.localScale;
        var position = actor.transform.position;
        var visualPosition = renderer.transform.localPosition;
        var visualScale = renderer.transform.localScale;
        var anchorPosition = anchor.position;
        var anchorScale = anchor.localScale;
        var pose = actor.CapturePose();
        void AssertFixedPose()
        {
            Assert.That(actor.transform.localScale, Is.EqualTo(scale));
            Assert.That(actor.transform.position, Is.EqualTo(position));
            Assert.That(renderer.transform.localPosition, Is.EqualTo(visualPosition));
            Assert.That(renderer.transform.localScale, Is.EqualTo(visualScale));
            Assert.That(anchor.position, Is.EqualTo(anchorPosition), "Sprite animation must not move the speech anchor.");
            Assert.That(anchor.localScale, Is.EqualTo(anchorScale));
        }

        string feed = legacyAliases ? "PulseFeed" : "Feed";
        var feedSprites = new System.Collections.Generic.HashSet<Sprite>();
        Observe(actor.PlayAnimationAsync(feed, "Idle", legacyAliases ? .9f : 1.5f, CancellationToken.None)).Forget();
        float deadline = Time.realtimeSinceStartup + 5;
        while (!finished && feedSprites.Count < 2 && Time.realtimeSinceStartup < deadline)
        {
            AssertFixedPose();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(feed), Is.True);
            Assert.That(renderer.sprite.name, Does.StartWith("Feed_"), "A legacy name must use the same web food animation.");
            feedSprites.Add(renderer.sprite);
            yield return null;
        }
        Assert.That(feedSprites.Count, Is.GreaterThan(1), "Feed must advance actual sprite frames before cancellation.");
        Assert.That(finished, Is.False);
        actor.RestorePose(pose);
        yield return WaitForOperation();
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(actor.IsBusy, Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(renderer.sprite.name, Does.StartWith("Idle_"));
        for (int frame = 0; frame < 4; frame++) { yield return null; AssertFixedPose(); }

        string pet = legacyAliases ? "PulsePet" : "Pet";
        var petSprites = new System.Collections.Generic.HashSet<Sprite>();
        Observe(actor.PlayAnimationAsync(pet, "Idle", legacyAliases ? .9f : 1.6f, CancellationToken.None)).Forget();
        deadline = Time.realtimeSinceStartup + 5;
        while (!finished && Time.realtimeSinceStartup < deadline)
        {
            AssertFixedPose();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(pet), Is.True);
            Assert.That(renderer.sprite.name, Does.StartWith("Pet_"));
            petSprites.Add(renderer.sprite);
            yield return null;
        }
        yield return WaitForOperation();
        Assert.That(failure, Is.Null);
        Assert.That(petSprites.Count, Is.GreaterThan(1), "Pet must play the web relaxed face and heart frames.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(renderer.sprite.name, Does.StartWith("Idle_"));
        for (int frame = 0; frame < 4; frame++) { yield return null; AssertFixedPose(); }

        if (legacyAliases)
        {
            // 발견은 제자리의 눈 프레임이며, 이후 포물선 이동은 상호작용 목록이 담당한다.
            Observe(actor.PlayAnimationAsync("PulseReveal", "Idle", .2f, CancellationToken.None)).Forget();
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("PulseReveal"), Is.True);
            Assert.That(renderer.sprite.name, Is.EqualTo("RevealEyes_00"));
            AssertFixedPose();
            yield return WaitForOperation();
            Assert.That(failure, Is.Null);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(renderer.sprite.name, Does.StartWith("Idle_"));
            for (int frame = 0; frame < 4; frame++) { yield return null; AssertFixedPose(); }
        }
        Object.Destroy(actor.gameObject);
    }
}
