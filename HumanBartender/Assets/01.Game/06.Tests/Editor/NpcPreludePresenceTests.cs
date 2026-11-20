using System;
using System.Collections;
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

/// <summary>대화 이전 연출의 취소 경계와 CSV 대화 조건에서 독립된 배경 배우 표시를 검증한다.</summary>
public sealed class NpcPreludePresenceTests
{
    private SceneSetup[] previous;
    private PlayerDataSO data;
    private bool finished;
    private Exception failure;

    private void Prepare()
    {
        previous = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (data != null) Object.DestroyImmediate(data);
        if (previous != null && previous.Any(value => value.isActive && value.isLoaded))
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void Set(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);

    private static NpcActor Actor(string name)
    {
        var root = new GameObject(name);
        var visual = new GameObject("Sprite");
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var movement = root.AddComponent<NpcMovement2D>();
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/01.Game/03.Content/07.Animations/01.Characters/StreetNPC/Repairman/Repairman.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        var actor = root.AddComponent<NpcActor>();
        actor.Configure(movement, animator, renderer);
        actor.SetVisible(true);
        return actor;
    }

    private NpcInteraction Interaction(NpcActor actor, Transform player, params NpcSequence[] sequences)
    {
        var interaction = actor.gameObject.AddComponent<NpcInteraction>();
        actor.gameObject.AddComponent<NpcSceneStaging>();
        Set(interaction, "actor", actor);
        Set(interaction, "useStoryPresence", false);
        Set(interaction, "sequences", sequences);
        if (data == null) data = ScriptableObject.CreateInstance<PlayerDataSO>();
        return interaction;
    }

    private void Initialize(NpcInteraction interaction, Transform player)
    {
        interaction.Initialize(new ConditionUtil(GameStateManager.Instance, data, data), player,
            new NewSceneData { Id = "common-prelude", RuntimeScene = interaction.gameObject.scene.name,
                Route = "both", CompletionFlag = "common_test_done" }, null, null);
    }

    private async UniTask Observe(UniTask task)
    {
        finished = false;
        failure = null;
        try { await task; }
        catch (Exception exception) { failure = exception; }
        finally { finished = true; }
    }

    private static IEnumerator Until(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + 8;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, "The common NPC operation did not finish.");
    }

    [UnityTest]
    public IEnumerator BeforeDialogueCancellationRestoresLunaAndCanRetryWithoutWritingProgress()
    {
        Prepare();
        yield return new EnterPlayMode();
        yield return VerifyPrelude();
    }

    private IEnumerator VerifyPrelude()
    {
        Time.timeScale = 1;
        GameStateManager.Instance.CurrentDay = 1;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        var actor = Actor("Prelude actor");
        var player = new GameObject("Prelude Luna").transform;
        var origin = player.position = new Vector3(-2, 3, -1);
        var interaction = Interaction(actor, player, new NpcSequence
        {
            id = "align-before-dialogue", trigger = NpcSequenceTrigger.BeforeDialogue,
            actions = new[]
            {
                new NpcActionStep { kind = NpcActionKind.MovePlayer, positionOrigin = NpcPositionOrigin.Current,
                    offset = Vector2.right, duration = .5f, movementEase = NpcMotionEase.SmoothStep },
                new NpcActionStep { kind = NpcActionKind.SetPosition, positionOrigin = NpcPositionOrigin.Initial,
                    offset = Vector2.right }
            }
        });
        Initialize(interaction, player);
        using var cancel = new CancellationTokenSource();
        interaction.BeginInteraction();
        Observe(interaction.PrepareInteractionAsync(cancel.Token)).Forget();
        yield return Until(() => player.position.x > origin.x + .1f);
        cancel.Cancel();
        yield return Until(() => finished);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        interaction.EndInteraction();
        Assert.That(player.position, Is.EqualTo(origin));
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero), "Later actions must not run after cancellation.");
        Assert.That(interaction.CanInteract, Is.True);

        interaction.BeginInteraction();
        Observe(interaction.PrepareInteractionAsync(CancellationToken.None)).Forget();
        yield return Until(() => finished);
        Assert.That(failure, Is.Null);
        Assert.That(player.position, Is.EqualTo(origin + Vector3.right));
        Assert.That(actor.transform.position, Is.EqualTo(Vector3.right));
        Observe(interaction.CompleteInteractionAsync(CancellationToken.None)).Forget();
        yield return Until(() => finished);
        interaction.EndInteraction();
        Assert.That(player.position, Is.EqualTo(origin + Vector3.right), "Successful alignment keeps Luna at her destination.");
        Assert.That(new ConditionUtil(GameStateManager.Instance, data, data).CheckRequired("flag.common_test_done"), Is.False,
            "Movement must not write the CSV completion flag.");
    }

    [UnityTest]
    public IEnumerator ArcMovementUsesAnElevatedPathAndCancellationRestoresItsStart()
    {
        Prepare();
        yield return new EnterPlayMode();
        yield return VerifyArc();
    }

    private IEnumerator VerifyArc()
    {
        Time.timeScale = 1;
        var actor = Actor("Revealed cat stand-in");
        var origin = new Vector3(-2, 3, 4);
        var destination = origin + Vector3.right;
        actor.SetPosition(origin);
        using var cancel = new CancellationTokenSource();
        Observe(actor.MoveArcToAsync(destination, .9f, .55f, cancel.Token)).Forget();
        yield return Until(() => actor.transform.position.x > origin.x + .2f);
        Assert.That(actor.transform.position.y, Is.GreaterThan(origin.y + .3f));
        cancel.Cancel();
        yield return Until(() => finished);
        Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
        Assert.That(actor.transform.position, Is.EqualTo(origin));
        Assert.That(actor.IsVisible, Is.True);
        Observe(actor.MoveArcToAsync(destination, .9f, .55f, CancellationToken.None)).Forget();
        yield return Until(() => finished);
        Assert.That(failure, Is.Null);
        Assert.That(actor.transform.position, Is.EqualTo(destination));
    }

    [UnityTest]
    public IEnumerator BackgroundPresenceStartsOnReturnAndTradeSurvivesItsCompletionFlagOnlyThatDay()
    {
        Prepare();
        yield return new EnterPlayMode();
        yield return VerifyPresence();
    }

    private IEnumerator VerifyPresence()
    {
        Time.timeScale = 1;
        var player = new GameObject("Presence Luna").transform;
        var guard = Actor("Background guard");
        var guardInteraction = Interaction(guard, player);
        Set(guardInteraction, "minimumDayStartsOnReturn", true);
        var trade = Actor("Background trade");
        var tradeInteraction = Interaction(trade, player);
        Set(tradeInteraction, "minimumDay", 1);
        Set(tradeInteraction, "maximumDay", 1);
        Set(tradeInteraction, "presencePhase", NpcPresencePhase.CommuteOut);
        GameStateManager.Instance.CurrentDay = 0;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        Initialize(guardInteraction, player);
        Initialize(tradeInteraction, player);
        Assert.That(guard.IsVisible, Is.False);
        Assert.That(trade.IsVisible, Is.False);
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteOut;
        yield return Until(() => guard.IsVisible);
        Assert.That(trade.IsVisible, Is.False);
        GameStateManager.Instance.CurrentDay = 1;
        yield return Until(() => trade.IsVisible);
        new ConditionUtil(GameStateManager.Instance, data, data).ApplyRequired("flag.common_test_done = true");
        yield return null;
        Assert.That(trade.IsVisible, Is.True, "Completion controls another conversation, not the background presence.");
        GameStateManager.Instance.CurrentDay = 2;
        GameStateManager.Instance.GameFlow = EGameFlow.CommuteIn;
        yield return Until(() => !trade.IsVisible);
        Assert.That(guard.IsVisible, Is.True, "After the first return the camp remains on both routes.");
    }
}
