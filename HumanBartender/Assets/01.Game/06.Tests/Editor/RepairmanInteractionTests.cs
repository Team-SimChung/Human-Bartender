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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>씬에 작성된 수리공을 격리해 실제 Animator·대본·NPC 수명을 함께 검증한다.</summary>
public sealed class RepairmanInteractionTests
{
    private const string ScenePath = "Assets/01.Game/01.Scenes/OutSide.unity";
    private const string TestActorName = "Repairman integration test";
    private SceneSetup[] sceneSetup;
    private PlayerDataSO player;
    private IConditionUtil conditions;
    private NpcInteraction interaction;
    private NpcActor actor;
    private RecordingFade fades;
    private RecordingPopup popups;
    private bool finished;
    private Exception failure;
    private bool dialogueFinished;
    private StoryExecutionResult dialogueResult;

    private sealed class RecordingFade : ISceneFadeService
    {
        public bool Dark;
        public bool BlockNextFadeIn;
        public bool WaitingForFadeIn;
        public int FadeOutCount;
        public int FadeInCount;
        public readonly List<float> Durations = new();

        public UniTask FadeOutAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FadeOutCount++;
            Durations.Add(duration);
            Dark = true;
            return UniTask.CompletedTask;
        }

        public async UniTask FadeInAsync(float duration = -1, CancellationToken cancellationToken = default)
        {
            FadeInCount++;
            Durations.Add(duration);
            if (BlockNextFadeIn)
            {
                BlockNextFadeIn = false;
                WaitingForFadeIn = true;
                try { await UniTask.WaitUntil(() => false, cancellationToken: cancellationToken); }
                finally { WaitingForFadeIn = false; }
            }
            cancellationToken.ThrowIfCancellationRequested();
            Dark = false;
        }
    }

    private sealed class RecordingPopup : IPopupService
    {
        public int CompletionCount;
        public UniTask<bool> Popup(PopupType type, string data)
        {
            if (type == PopupType.ObjectiveCompleted) CompletionCount++;
            return UniTask.FromResult(true);
        }
    }

    private sealed class CompletionLinePresenter : IDialoguePresenter, IDialogueAutomaticPlayback
    {
        private readonly string completionLine;
        private readonly UniTaskCompletionSource release = new();
        public bool AtCompletionLine;
        public bool Ended;

        public CompletionLinePresenter(string text) => completionLine = text;
        public void Release() => release.TrySetResult();
        public void SkipTyping() { }
        public void HideDialogue() { }
        public void EndScene() => Ended = true;
        public EActivationMode GetPlayMode() => EActivationMode.Interact;
        public UniTask ShowDialogueAsync(string actorId, string text, string arg, CancellationToken token)
        {
            if (text != completionLine) return UniTask.CompletedTask;
            AtCompletionLine = true;
            return release.Task.AttachExternalCancellation(token);
        }
        public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> selected)
            => throw new InvalidOperationException("Paid repair must resume without another payment choice.");
        public UniTask WaitUntilReadyAsync(CancellationToken token) => UniTask.CompletedTask;
        public UniTask WaitAfterLineAsync(CancellationToken token) => UniTask.CompletedTask;
    }

    private static WorldInteractionTarget FindRepairman(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
        .Single(target => target.StorySceneId == "neighborhood-repairGreeting");

    private void PrepareIsolatedAuthoredActor()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outside = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var source = FindRepairman(outside);
            Assert.That(source.GetComponent<NpcInteraction>(), Is.Not.Null, "Run RepairmanNpcSetup before these tests.");
            var clone = Object.Instantiate(source.gameObject);
            clone.name = TestActorName;
            SceneManager.MoveGameObjectToScene(clone, isolated);
            clone.GetComponent<NpcMovement2D>().Configure(10);
            var repair = clone.GetComponent<NpcInteraction>();
            using (var serialized = new SerializedObject(repair))
            {
                var departure = NpcSequenceTestSettings.Action(serialized, NpcActionKind.DepartWhileMoving);
                departure.FindPropertyRelative("offset").vector2Value = new Vector2(2, 0);
                departure.FindPropertyRelative("walkDuration").floatValue = .2f;
                departure.FindPropertyRelative("duration").floatValue = .6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            NpcSequenceTestSettings.SetDuration(repair, NpcActionKind.Wait, .05f);
            SetScreenFade(repair, .05f);
        }
        finally { EditorSceneManager.CloseScene(outside, true); }
        SceneManager.SetActiveScene(isolated);
    }

    private void InitializeRuntime(int day)
    {
        Time.timeScale = 1;
        GameStateManager.Instance.CurrentDay = day;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        GameStateManager.Instance.Language = ELanguage.Ko;
        interaction = GameObject.Find(TestActorName).GetComponent<NpcInteraction>();
        actor = interaction.GetComponent<NpcActor>();
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        conditions = new ConditionUtil(GameStateManager.Instance, player, player);
        fades = new RecordingFade();
        popups = new RecordingPopup();
        var luna = new GameObject("Test Luna").transform;
        NpcSequenceTestSettings.CreateStagingCamera(luna);
        interaction.Initialize(conditions, luna, new NewSceneData
        {
            Id = "neighborhood-repairGreeting", RuntimeScene = interaction.gameObject.scene.name, Route = "both"
        }, fades, popups);
    }

    private static void SetScreenFade(NpcInteraction value, float amount)
    {
        NpcSequenceTestSettings.SetDuration(value, NpcActionKind.ScreenFadeOut, amount);
        NpcSequenceTestSettings.SetDuration(value, NpcActionKind.ScreenFadeIn, amount);
    }

    private async UniTask Observe(UniTask operation)
    {
        finished = false;
        failure = null;
        try { await operation; }
        catch (Exception error) { failure = error; }
        finally { finished = true; }
    }

    private async UniTask ObserveDialogue(DialogueRunner runner, Step[] steps)
    {
        dialogueFinished = false;
        dialogueResult = await runner.PlayOutsideAsync(steps, playWorldAction: interaction.PlayWorldActionAsync);
        dialogueFinished = true;
    }

    private static IEnumerator WaitUntil(Func<bool> predicate, string message)
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, message);
    }

    private static IEnumerator WaitForGameFrames(int initialFrame, int count)
    {
        float deadline = Time.realtimeSinceStartup + 5;
        while (Time.frameCount - initialFrame < count && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Time.frameCount - initialFrame, Is.GreaterThanOrEqualTo(count),
            "The editor test advanced without the required game frames.");
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (player != null) Object.DestroyImmediate(player);
        if (sceneSetup != null && sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Test]
    public void AuthoredRepairmanHasOneNpcOwnerAndRetainsTheCsvSpeaker()
    {
        sceneSetup = EditorSceneManager.GetSceneManagerSetup();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var target = FindRepairman(scene);
            var repair = target.GetComponent<NpcInteraction>();
            Assert.That(repair, Is.Not.Null);
            Assert.That(target.Participant, Is.SameAs(repair));
            Assert.That(target.GetComponent<WorldStoryActors>(), Is.Null, "The legacy system must not write the same NPC pose.");
            Assert.That(target.GetComponent<NpcActor>(), Is.Not.Null);
            Assert.That(target.GetComponent<NpcMovement2D>(), Is.Not.Null);
            var animator = target.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            var idle = animator.runtimeAnimatorController.animationClips.Distinct().Single(clip => clip.name == "Idle");
            Assert.That(idle.frameRate, Is.EqualTo(12));
            Assert.That(idle.length, Is.EqualTo(.75f).Within(.001f));
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(idle)
                .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(idle, binding))
                .Select(key => key.value).Distinct().Count(), Is.EqualTo(9));

            var speakers = target.GetComponent<WorldDialogueSpeakers>();
            Assert.That(speakers.TryResolve("repair", out var anchor, out _, out _), Is.True);
            Assert.That(anchor.IsChildOf(target.transform), Is.True);
            using var serialized = new SerializedObject(repair);
            Assert.That(serialized.FindProperty("actor").objectReferenceValue, Is.SameAs(target.GetComponent<NpcActor>()));
            Assert.That(serialized.FindProperty("exitPoint"), Is.Null, "Departure uses an offset without an external target.");
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.ScreenFadeOut), Is.Not.Null);
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.Wait), Is.Not.Null);
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.ScreenFadeIn), Is.Not.Null);
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.ScreenFadeOut)
                .FindPropertyRelative("duration").floatValue, Is.EqualTo(.35f));
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.Wait)
                .FindPropertyRelative("duration").floatValue, Is.EqualTo(1.35f));
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.ScreenFadeIn)
                .FindPropertyRelative("duration").floatValue, Is.EqualTo(.45f));
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.DepartWhileMoving)
                .FindPropertyRelative("duration").floatValue, Is.EqualTo(.5f));
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.DepartWhileMoving)
                .FindPropertyRelative("walkDuration").floatValue, Is.EqualTo(1.1f));
            Assert.That(NpcSequenceTestSettings.Action(serialized, NpcActionKind.DepartWhileMoving)
                .FindPropertyRelative("target").objectReferenceValue, Is.Null, "Relative X/Y departure has no world Exit Point.");
            using var npcSettings = new SerializedObject(target.GetComponent<NpcActor>());
            Assert.That(npcSettings.FindProperty("departureOffset"), Is.Null, "The interaction owns its departure settings.");
            Assert.That(npcSettings.FindProperty("departureFadeDuration"), Is.Null);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    [UnityTest]
    public IEnumerator DayOneEnablesTheRealIdleAnimationAndUnpaidVisitsLeaveTheNpcPresent()
    {
        PrepareIsolatedAuthoredActor();
        yield return new EnterPlayMode();
        InitializeRuntime(0);
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(interaction.CanInteract, Is.False);

        int dayChangeFrame = Time.frameCount;
        GameStateManager.Instance.CurrentDay = 1;
        yield return WaitForGameFrames(dayChangeFrame, 2);
        Assert.That(actor.IsVisible, Is.True,
            $"Day={GameStateManager.Instance.CurrentDay}, frames={Time.frameCount - dayChangeFrame}, " +
            $"actorActive={actor.gameObject.activeInHierarchy}, actorEnabled={actor.enabled}, " +
            $"interactionActive={interaction.gameObject.activeInHierarchy}, interactionEnabled={interaction.enabled}, " +
            $"repaired={player.CheckFlag("elevator_repaired")}, canInteract={interaction.CanInteract}");
        Assert.That(interaction.CanInteract, Is.True);
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        var frames = new HashSet<Sprite>();
        float deadline = Time.realtimeSinceStartup + .45f;
        while (Time.realtimeSinceStartup < deadline)
        {
            frames.Add(renderer.sprite);
            yield return null;
        }
        Assert.That(frames.Count, Is.GreaterThan(1), "The authored Animator must advance the real sprite sheet.");

        interaction.BeginInteraction();
        Assert.That(interaction.CanInteract, Is.False);
        Observe(interaction.CompleteInteractionAsync(CancellationToken.None)).Forget();
        yield return WaitUntil(() => finished, "Unpaid dialogue did not finish.");
        Assert.That(failure, Is.Null);
        interaction.EndInteraction();
        dayChangeFrame = Time.frameCount;
        GameStateManager.Instance.CurrentDay = 3;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        yield return WaitForGameFrames(dayChangeFrame, 2);
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(interaction.CanInteract, Is.True);
        Assert.That(popups.CompletionCount, Is.Zero);
        Assert.That(player.CheckFlag("elevator_repaired"), Is.False);
    }

    [Test]
    public void ReapplyingRepairmanSetupKeepsAuthoredSequenceOrderAndDurations()
    {
        PrepareIsolatedAuthoredActor();
        var target = GameObject.Find(TestActorName).GetComponent<WorldInteractionTarget>();
        var common = target.GetComponent<NpcInteraction>();
        using (var data = new SerializedObject(common))
        {
            data.FindProperty("sequences").MoveArrayElement(0, 1);
            NpcSequenceTestSettings.Action(data, NpcActionKind.ScreenFadeOut).FindPropertyRelative("duration").floatValue = .77f;
            NpcSequenceTestSettings.Action(data, NpcActionKind.DepartWhileMoving).FindPropertyRelative("offset").vector2Value = new Vector2(-3.25f, 2);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        string authored = EditorJsonUtility.ToJson(common);
        Assert.That(NpcInteractionSetup.ConfigureRepairmanTarget(target, target.GetComponent<NpcActor>()), Is.SameAs(common));
        Assert.That(EditorJsonUtility.ToJson(common), Is.EqualTo(authored));
    }

    [UnityTest]
    public IEnumerator CancellingPaidRepairDuringHoldOrFadeInRestoresTheScreenAndAllowsRetry()
    {
        PrepareIsolatedAuthoredActor();
        yield return new EnterPlayMode();
        InitializeRuntime(1);
        player.AddFlag("elevator_repair_paid", true);
        var handler = new NeighborhoodStoryEvents(null);

        foreach (bool cancelFadeIn in new[] { false, true })
        {
            NpcSequenceTestSettings.SetDuration(interaction, NpcActionKind.Wait, cancelFadeIn ? .05f : 1f);
            fades.BlockNextFadeIn = cancelFadeIn;
            interaction.BeginInteraction();
            using var cancellation = new CancellationTokenSource();
            // Choice callbacks are required even though this direct repair command never asks for one.
            var run = new OutsideStoryExecution(conditions.CreateExecutionScope(),
                (options, token) => UniTask.FromResult(options[0]), interaction.PlayWorldActionAsync);
            Observe(handler.ExecuteAsync(OutsideStoryCommand.RepairElevator, null, run, cancellation.Token).AsUniTask()).Forget();
            yield return WaitUntil(() => cancelFadeIn ? fades.WaitingForFadeIn : fades.Dark,
                "Repair did not enter the expected fade stage.");
            cancellation.Cancel();
            yield return WaitUntil(() => finished, "Cancelled repair did not release its operation.");
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(fades.Dark, Is.False);
            Assert.That(player.CheckFlag("elevator_repaired"), Is.False);
            Assert.That(player.CheckFlag("elevator_repair_paid"), Is.True);
            interaction.EndInteraction();
            yield return null;
            Assert.That(actor.IsVisible, Is.True);
            Assert.That(interaction.CanInteract, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator ActualCsvCompletionLineKeepsTheNpcVisibleUntilDialogueEndsThenDeparturePersists()
    {
        PrepareIsolatedAuthoredActor();
        yield return new EnterPlayMode();
        // EnterPlayMode 도메인 재로드가 끝난 뒤 캡처되는 지역 변수들을 생성한다.
        yield return VerifyActualCsvCompletionAndDeparture();
    }

    private IEnumerator VerifyActualCsvCompletionAndDeparture()
    {
        InitializeRuntime(1);
        player.AddFlag("elevator_repair_paid", true);
        var content = new CsvStoryTestContent();
        var steps = content.GetSceneSteps("neighborhood-repairPaid");
        var presenter = new CompletionLinePresenter(steps.Last(step => step.Type == "say").Text.Ko);
        var runner = new GameObject("Repair dialogue test").AddComponent<DialogueRunner>();
        typeof(DialogueRunner).GetField("conditionUtil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner, conditions);
        typeof(DialogueRunner).GetField("storyEvents", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner,
            new OutsideStoryEvents(new[] { new NeighborhoodStoryEvents(content) }));
        typeof(DialogueRunner).GetField("popupService", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runner, popups);
        runner.Bind(presenter);
        var start = actor.transform.position;
        interaction.BeginInteraction();
        ObserveDialogue(runner, steps).Forget();
        yield return WaitUntil(() => presenter.AtCompletionLine, "The authored completion line was not reached.");

        Assert.That(player.CheckFlag("elevator_repaired"), Is.True);
        Assert.That(dialogueFinished, Is.False);
        Assert.That(actor.IsVisible, Is.True);
        Assert.That(actor.transform.position, Is.EqualTo(start));
        Assert.That(popups.CompletionCount, Is.Zero);
        Assert.That(fades.Durations, Is.EqualTo(new[] { .05f, .05f }), "Only the repair setting controls the screen fade.");
        yield return null;
        yield return null;
        Assert.That(actor.IsVisible, Is.True, "The completion flag must not hide the speaking NPC.");

        presenter.Release();
        yield return WaitUntil(() => dialogueFinished, "The completion dialogue did not end.");
        Assert.That(dialogueResult.Completed, Is.True, dialogueResult.Error);
        Assert.That(presenter.Ended, Is.True);
        Assert.That(actor.IsVisible, Is.True);

        // Changing the repair screen fade must not change the separate .6-second NPC departure fade.
        SetScreenFade(interaction, 8);
        Observe(interaction.CompleteInteractionAsync(CancellationToken.None)).Forget();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>();
        yield return WaitUntil(() => renderer.color.a < .9f && renderer.color.a > .01f,
            "Departure did not enter its independent moving fade.");
        Assert.That(actor.GetComponent<NpcMovement2D>().IsMoving, Is.True, "The web departure keeps moving during the fade.");
        Assert.That(actor.transform.position.x, Is.GreaterThan(start.x));
        Assert.That(actor.transform.position.x, Is.LessThan(start.x + 2));
        Assert.That(finished, Is.False, "Completion must wait for the entire moving fade.");
        Assert.That(actor.IsVisible, Is.True);
        yield return WaitUntil(() => finished, "Completed repair did not finish the departure.");
        Assert.That(failure, Is.Null);
        Assert.That(popups.CompletionCount, Is.EqualTo(1));
        Assert.That(actor.IsVisible, Is.False);
        var destination = actor.transform.position;
        Assert.That(Vector3.Distance(destination, start + Vector3.right * 2), Is.LessThan(.0001f));
        Assert.That(fades.Durations, Is.EqualTo(new[] { .05f, .05f }), "NPC departure must not fade the whole screen.");
        interaction.EndInteraction();
        GameStateManager.Instance.CurrentDay = 3;
        yield return null;
        yield return null;
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(destination), "Successful departure must not snap back.");
        Assert.That(interaction.CanInteract, Is.False);
    }

    [UnityTest]
    public IEnumerator CancellingDepartureDoesNotUndoRepairOrMakeTheNpcAppearAgain()
    {
        PrepareIsolatedAuthoredActor();
        yield return new EnterPlayMode();
        InitializeRuntime(1);
        interaction.BeginInteraction();
        player.AddFlag("elevator_repair_paid", true);
        player.AddFlag("elevator_repaired", true);
        actor.GetComponent<NpcMovement2D>().Configure(.5f);
        using var cancellation = new CancellationTokenSource();
        Observe(interaction.CompleteInteractionAsync(cancellation.Token)).Forget();
        yield return null;
        yield return null;
        Assert.That(actor.IsBusy, Is.True);
        cancellation.Cancel();
        yield return WaitUntil(() => finished, "Cancelled departure did not stop.");
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        interaction.EndInteraction();
        yield return null;
        yield return null;
        Assert.That(player.CheckFlag("elevator_repaired"), Is.True);
        Assert.That(actor.IsVisible, Is.False);
        Assert.That(interaction.CanInteract, Is.False);
    }
}
