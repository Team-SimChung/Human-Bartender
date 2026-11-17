using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class TerraceBirdArrivalTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/02.Birds/Pigeon.prefab";
    SceneSetup[] previousScenes;
    float previousTimeScale;
    bool changedTimeScale;

    sealed class Presenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        public readonly List<string> Calls = new();
        public Action BeforeReady;
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() => Calls.Add("end");
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken ct)
        { Calls.Add(text); return UniTask.CompletedTask; }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> picked) => picked(options[0]);
        public UniTask WaitUntilReadyAsync(CancellationToken token)
        { BeforeReady?.Invoke(); return UniTask.CompletedTask; }
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    void EmptyScene()
    {
        previousScenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (changedTimeScale) Time.timeScale = previousTimeScale;
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (previousScenes != null && previousScenes.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TestCase(0, "night0")]
    [TestCase(1, "night1")]
    [TestCase(2, "night2")]
    [TestCase(3, "ending")]
    public void InspectorRangeAndPreviewUseTheActualHomeCsv(int day, string id)
    {
        EmptyScene();
        var terrace = new GameObject("Terrace").AddComponent<HomeTerracePresentation>();
        var catalog = CsvDataReader.LoadDirectory(Application.streamingAssetsPath + "/csv");
        var expected = StoryCsvCompiler.CompileHome(catalog).Scenes.Single(scene => scene.Id == id);
        Assert.That(expected.Day, Is.EqualTo(day));
        Assert.That(terrace.GetStoryIdForDay(day), Is.EqualTo(id));
        var actual = TerraceBirdArrivalEditor.GetSteps(terrace, day);
        Assert.That(actual.Select(step => step.Seq), Is.EqualTo(expected.Steps.OrderBy(step => step.Seq).Select(step => step.Seq)));
        Assert.That(actual.All(step => step.Seq > 0), Is.True);
        Assert.That(actual.Select(step => step.Text?.Ko), Is.EqualTo(expected.Steps.OrderBy(step => step.Seq).Select(step => step.Text?.Ko)));
    }

    [Test]
    public void HomeContainsAConnectedPreauthoredBirdAndEditableFlightAnchors()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/Home.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var arrival = roots.SelectMany(root => root.GetComponentsInChildren<TerraceBirdArrival>(true)).Single();
            Assert.That(arrival.transform.parent.name, Is.EqualTo("TerraceBirdsGroup"));
            using var data = new SerializedObject(arrival);
            var bird = (Transform)data.FindProperty("bird").objectReferenceValue;
            var start = (Transform)data.FindProperty("flightStart").objectReferenceValue;
            var landing = (Transform)data.FindProperty("landingPoint").objectReferenceValue;
            Assert.That(Vector3.Distance(bird.position, landing.position), Is.LessThan(.0001f), "씬의 기본 비둘기 배치와 런타임 착지점이 일치해야 한다.");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(bird.gameObject), Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
            Assert.That(data.FindProperty("birdAnimator").objectReferenceValue, Is.EqualTo(bird.GetComponent<Animator>()));
            Assert.That(data.FindProperty("birdIdle").objectReferenceValue, Is.EqualTo(bird.GetComponent<RandomIdleAnimation>()));
            Assert.That(start.parent, Is.EqualTo(arrival.transform));
            Assert.That(landing.parent, Is.EqualTo(arrival.transform));
            Assert.That(start.position, Is.Not.EqualTo(landing.position));
            Assert.That(arrival.GetComponentsInChildren<Transform>(true).All(child => child.localScale == Vector3.one), Is.True);
            Assert.That(arrival.Terrace, Is.Not.Null);
            using var terrace = new SerializedObject(arrival.Terrace);
            var foreground = (Transform)terrace.FindProperty("terraceForeground").objectReferenceValue;
            Assert.That(arrival.transform.IsChildOf(foreground), Is.True, "기본 배치한 새는 Terrace와 함께 이동한다.");
            Assert.That(terrace.FindProperty("birdArrivals").GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(arrival));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void MatchingLandingToPlacementPreservesFlightPathAndCanBeRepeated()
    {
        EmptyScene();
        var root = new GameObject("Arrival");
        root.transform.position = new Vector3(3, 2, 0);
        var arrival = root.AddComponent<TerraceBirdArrival>();
        var bird = new GameObject("Pigeon").transform;
        var landing = new GameObject("LandingPoint").transform;
        var start = new GameObject("FlightStart").transform;
        foreach (var child in new[] { bird, landing, start }) child.SetParent(root.transform, false);
        bird.localPosition = new Vector3(-.5f, -4.55f, 0);
        landing.localPosition = new Vector3(-.5f, -4.99f, 0);
        start.localPosition = new Vector3(3, -4.3f, 0);
        var authoredPosition = bird.position;
        var originalPath = start.position - landing.position;
        using (var data = new SerializedObject(arrival))
        {
            data.FindProperty("bird").objectReferenceValue = bird;
            data.FindProperty("landingPoint").objectReferenceValue = landing;
            data.FindProperty("flightStart").objectReferenceValue = start;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(TerraceBirdArrivalEditor.MatchLandingToPlacement(arrival), Is.True);
        Assert.That(landing.position, Is.EqualTo(authoredPosition));
        Assert.That(bird.position, Is.EqualTo(authoredPosition));
        Assert.That(Vector3.Distance(start.position - landing.position, originalPath), Is.LessThan(.0001f));
        var alignedStart = start.position;
        Assert.That(TerraceBirdArrivalEditor.MatchLandingToPlacement(arrival), Is.False);
        Assert.That(start.position, Is.EqualTo(alignedStart));
    }

    [UnityTest]
    public IEnumerator FlightUsesTheSelectedDayAndStepOnceThenLandsAndResetsWithoutLateMovement()
    {
        EmptyScene();
        yield return new EnterPlayMode();
        previousTimeScale = Time.timeScale;
        changedTimeScale = true;
        var root = new GameObject("Arrival");
        var arrival = root.AddComponent<TerraceBirdArrival>();
        var terrace = root.AddComponent<HomeTerracePresentation>();
        var bird = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
        var start = new GameObject("FlightStart").transform;
        start.SetParent(root.transform, false);
        start.position = new Vector3(2, 1, 0);
        var landing = new GameObject("LandingPoint").transform;
        landing.SetParent(root.transform, false);
        var animator = bird.GetComponent<Animator>();
        var idle = bird.GetComponent<RandomIdleAnimation>();
        using (var data = new SerializedObject(idle))
        {
            data.FindProperty("minimumStartFrame").intValue = 4;
            data.FindProperty("maximumStartFrame").intValue = 4;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        using (var data = new SerializedObject(arrival))
        {
            data.FindProperty("terrace").objectReferenceValue = terrace;
            data.FindProperty("bird").objectReferenceValue = bird.transform;
            data.FindProperty("birdAnimator").objectReferenceValue = animator;
            data.FindProperty("birdIdle").objectReferenceValue = idle;
            data.FindProperty("flightStart").objectReferenceValue = start;
            data.FindProperty("landingPoint").objectReferenceValue = landing;
            data.FindProperty("flightDuration").floatValue = .1f;
            var days = data.FindProperty("days");
            days.arraySize = 3;
            for (int index = 0; index < 2; index++)
            {
                var timing = days.GetArrayElementAtIndex(index);
                timing.FindPropertyRelative("day").intValue = 1 - index;
                timing.FindPropertyRelative("spawn").boolValue = true;
                timing.FindPropertyRelative("startStep").intValue = index == 0 ? 4 : 2;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        using (var data = new SerializedObject(terrace))
        {
            data.FindProperty("birdArrivals").arraySize = 1;
            data.FindProperty("birdArrivals").GetArrayElementAtIndex(0).objectReferenceValue = arrival;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        terrace.BeginStory("night0", 0);
        Assert.That(bird.activeSelf, Is.False);
        Assert.That(arrival.TryStartAtStep(4), Is.False, "다른 날짜의 선택 스텝은 발동하지 않는다.");
        Time.timeScale = 0;
        Assert.That(arrival.TryStartAtStep(2), Is.True);
        Assert.That(arrival.IsFlying, Is.True);
        Assert.That(idle.IsRunning, Is.False, "비행 중에는 랜덤 Idle이 Animator를 제어하지 않는다.");
        Assert.That(animator.speed, Is.GreaterThan(0));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Fly"), Is.True);
        Assert.That(arrival.TryStartAtStep(2), Is.False, "같은 대본 세션에서 반복하지 않는다.");
        float pausedUntil = Time.realtimeSinceStartup + .2f;
        while (Time.realtimeSinceStartup < pausedUntil) yield return null;
        Assert.That(bird.transform.position, Is.EqualTo(start.position));
        Assert.That(arrival.HasArrived, Is.False);
        Time.timeScale = 1;
        float deadline = Time.realtimeSinceStartup + 3;
        while (!arrival.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(arrival.HasArrived, Is.True);
        Assert.That(arrival.IsFlying, Is.False);
        Assert.That(bird.transform.position, Is.EqualTo(landing.position));
        Assert.That(animator.GetBool("IsFlying"), Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        Assert.That(idle.IsRunning, Is.True);
        Assert.That(idle.CurrentStartFrame, Is.EqualTo(4));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(.25f),
            "착지 처리가 공통 파츠의 시작 프레임을 다시 1프레임으로 덮어쓰지 않는다.");
        deadline = Time.realtimeSinceStartup + 4;
        while (!idle.IsWaiting && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(idle.IsWaiting, Is.True);
        Assert.That(animator.speed, Is.Zero);
        terrace.BeginStory("night1", 1);
        Assert.That(arrival.TryStartAtStep(2), Is.False);
        Assert.That(arrival.TryStartAtStep(4), Is.True);
        Assert.That(idle.IsRunning, Is.False);
        Assert.That(animator.speed, Is.GreaterThan(0), "Idle 대기 중 0이었던 속도를 복원해 다음 비행이 멈추지 않게 한다.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Fly"), Is.True);
        arrival.enabled = false;
        Assert.That(bird.activeSelf, Is.False);
        var stoppedPosition = bird.transform.position;
        yield return null;
        yield return null;
        Assert.That(bird.transform.position, Is.EqualTo(stoppedPosition));
        Assert.That(arrival.HasArrived, Is.False, "비활성화 후 이전 이동 완료 콜백이 실행되지 않는다.");
        arrival.enabled = true;
        arrival.PrepareForDay(2);
        Assert.That(arrival.TryStartAtStep(1), Is.False, "사용하지 않는 날짜는 숨김을 유지한다.");
        arrival.PrepareForDay(99);
        Assert.That(arrival.TryStartAtStep(1), Is.False);
        idle.enabled = false;
        using (var data = new SerializedObject(arrival))
        {
            data.FindProperty("flightDuration").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        arrival.PrepareForDay(0);
        Assert.That(arrival.TryStartAtStep(2), Is.True, "새 세션은 다시 재생할 수 있다.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True, "랜덤 파츠를 꺼도 기본 착지 포즈는 유지한다.");
        Assert.That(idle.IsRunning, Is.False);
        terrace.ExitAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        Assert.That(bird.activeSelf, Is.False, "테라스 종료가 진행 중인 비행도 정리한다.");
        Assert.That(arrival.HasTriggered, Is.False);
        Object.Destroy(root);
    }

    [Test]
    public void LegacyDaySettingsKeepTheirValuesAndExplicitDatesSurviveReordering()
    {
        EmptyScene();
        var arrival = new GameObject("Legacy arrival").AddComponent<TerraceBirdArrival>();
        using var data = new SerializedObject(arrival);
        var days = data.FindProperty("days");
        // 날짜 필드가 없던 구버전은 배열 인덱스를 날짜로 해석했다.
        days.arraySize = 2;
        days.GetArrayElementAtIndex(0).FindPropertyRelative("spawn").boolValue = true;
        days.GetArrayElementAtIndex(0).FindPropertyRelative("startStep").intValue = 7;
        days.GetArrayElementAtIndex(1).FindPropertyRelative("spawn").boolValue = false;
        days.GetArrayElementAtIndex(1).FindPropertyRelative("startStep").intValue = 0;
        days.GetArrayElementAtIndex(1).FindPropertyRelative("day").intValue = 0;
        data.FindProperty("daySettingsVersion").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        arrival.OnAfterDeserialize();
        data.Update();
        Assert.That(days.arraySize, Is.EqualTo(2));
        Assert.That(days.GetArrayElementAtIndex(0).FindPropertyRelative("day").intValue, Is.Zero);
        Assert.That(days.GetArrayElementAtIndex(0).FindPropertyRelative("spawn").boolValue, Is.True);
        Assert.That(days.GetArrayElementAtIndex(0).FindPropertyRelative("startStep").intValue, Is.EqualTo(7));
        Assert.That(days.GetArrayElementAtIndex(1).FindPropertyRelative("day").intValue, Is.EqualTo(1));
        Assert.That(days.GetArrayElementAtIndex(1).FindPropertyRelative("spawn").boolValue, Is.False);
        Assert.That(days.GetArrayElementAtIndex(1).FindPropertyRelative("startStep").intValue, Is.Zero);
        days.GetArrayElementAtIndex(0).FindPropertyRelative("day").intValue = 8;
        days.MoveArrayElement(0, 1);
        data.ApplyModifiedPropertiesWithoutUndo();
        arrival.OnAfterDeserialize();
        data.Update();
        Assert.That(days.GetArrayElementAtIndex(0).FindPropertyRelative("day").intValue, Is.EqualTo(1));
        Assert.That(days.GetArrayElementAtIndex(1).FindPropertyRelative("day").intValue, Is.EqualTo(8));
        Assert.That(days.GetArrayElementAtIndex(1).FindPropertyRelative("startStep").intValue, Is.EqualTo(7));
    }

    [Test]
    public void DuplicateDateSettingsAreReportedAndDoNotDisplayTheBird()
    {
        EmptyScene();
        var arrival = new GameObject("Duplicate arrival").AddComponent<TerraceBirdArrival>();
        using var data = new SerializedObject(arrival);
        var days = data.FindProperty("days");
        days.arraySize = 2;
        for (int index = 0; index < days.arraySize; index++)
        {
            var timing = days.GetArrayElementAtIndex(index);
            timing.FindPropertyRelative("day").intValue = 1;
            timing.FindPropertyRelative("startStep").intValue = 0;
            timing.FindPropertyRelative("spawn").boolValue = true;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Warning, "[TerraceBird] 1일차 설정이 중복되어 비둘기를 표시하지 않습니다.");
        arrival.PrepareForDay(1);
        Assert.That(arrival.HasTriggered, Is.False);
        Assert.That(arrival.TryStartAtStep(0), Is.False);
        Assert.That(TerraceBirdDayTimingDrawer.Preview(days.GetArrayElementAtIndex(0), out _, out var type), Does.Contain("중복"));
        Assert.That(type, Is.EqualTo(MessageType.Error));
    }

    [UnityTest]
    public IEnumerator RunnerSignalsOnlyExecutedRootStepsBeforePresentationAndHonorsCancellation()
    {
        EmptyScene();
        yield return new EnterPlayMode();
        // EnterPlayMode의 도메인 재로드 뒤에 콜백 캡처를 만든다.
        yield return VerifyRunnerSignals();
    }

    private IEnumerator VerifyRunnerSignals()
    {
        var root = new GameObject("Step signal");
        var runner = root.AddComponent<DialogueRunner>();
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        var conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        conditions.ApplyRequired("flag.terrace_bird_test_skip = false");
        var conditionField = typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(conditionField, Is.Not.Null, "DialogueRunner의 조건 서비스 주입 필드를 찾을 수 없습니다.");
        Assert.That(runner, Is.Not.Null);
        conditionField.SetValue(runner, conditions);
        var presenter = new Presenter();
        runner.Bind(presenter);
        var steps = new[]
        {
            new Step { Seq = 2, Type = "say", Text = new Texts { Ko = "visible", En = "visible" } },
            new Step { Seq = 1, Type = "say", When = "flag.terrace_bird_test_skip", Text = new Texts { Ko = "skip" } },
            new Step { Seq = 3, Type = "choice", Options = new[] { new NewStreetOptionData
            { Id = "one", Seq = 1, Text = new Texts { Ko = "one" }, ResultSteps = new[]
            { new Step { Seq = 1, Type = "say", Text = new Texts { Ko = "nested", En = "nested" } } } } } }
        };
        var result = runner.PlayOutsideAsync(steps, onStepStarted: step => presenter.Calls.Add("step" + step.Seq)).GetAwaiter().GetResult();
        Assert.That(result.Completed, Is.True, result.Error);
        Assert.That(presenter.Calls, Is.EqualTo(new[] { "step2", "visible", "step3", "nested", "end" }));
        presenter.Calls.Clear();
        using var cancellation = new CancellationTokenSource();
        result = runner.PlayOutsideAsync(steps, cancellation.Token, onStepStarted: step =>
        { presenter.Calls.Add("step" + step.Seq); cancellation.Cancel(); }).GetAwaiter().GetResult();
        Assert.That(result.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
        Assert.That(presenter.Calls, Is.EqualTo(new[] { "step2", "end" }));
        presenter.Calls.Clear();
        using var beforeStep = new CancellationTokenSource();
        presenter.BeforeReady = () => beforeStep.Cancel();
        result = runner.PlayOutsideAsync(steps, beforeStep.Token,
            onStepStarted: step => presenter.Calls.Add("step" + step.Seq)).GetAwaiter().GetResult();
        Assert.That(result.Status, Is.EqualTo(StoryExecutionStatus.Cancelled));
        Assert.That(presenter.Calls, Is.EqualTo(new[] { "end" }), "대사 준비 중 취소하면 스텝 신호도 보내지 않는다.");
        Object.Destroy(root);
        Object.Destroy(player);
        yield break;
    }
}
