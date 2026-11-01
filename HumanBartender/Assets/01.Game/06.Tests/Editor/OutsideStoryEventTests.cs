using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class OutsideStoryEventTests
{
    static UniTask<NewStreetOptionData> PickFirst(NewStreetOptionData[] options, CancellationToken token)
        => UniTask.FromResult(options[0]);

    [Test]
    public void RepeatedAllowanceCommandUsesExistingPlayerFlagsAndPaysOnlyOnce()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            var content = new CsvStoryTestContent();
            var commands = new ShopStoryEvents(player, GameStateManager.Instance, content);
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            var continuation = commands.ExecuteAsync(OutsideStoryCommand.ShopAllowance, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(player.HasMoney(), Is.Zero, "The spoken acceptance must finish before the authored grant command.");
            var grant = System.Array.Find(continuation, value => value.Command == OutsideStoryCommand.GrantAllowance);
            commands.ExecuteAsync(grant.Command, grant.Arg, run, CancellationToken.None).GetAwaiter().GetResult();
            commands.ExecuteAsync(grant.Command, grant.Arg, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(continuation[0].Text.Ko, Is.EqualTo(content.GetSceneSteps("shiba-allowance")
                .Single(step => step.Options?.Length > 0).Options[0].ResultSteps[0].Text.Ko));
            Assert.That(player.HasMoney(), Is.EqualTo(500));
            Assert.That(player.CheckFlag("flag.shiba_allowance_received"), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void RepeatedWildDogGrantDoesNotReopenTheDealOrChangeMoney()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.SetMoney(800);
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            conditions.ApplyRequired("flag.gang_wild_dog_paid = true");
            var commands = new NeighborhoodStoryEvents(null);
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            commands.ExecuteAsync(OutsideStoryCommand.GrantWildDog, null, run, CancellationToken.None).GetAwaiter().GetResult();
            commands.ExecuteAsync(OutsideStoryCommand.GrantWildDog, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(player.HasMoney(), Is.EqualTo(800));
            Assert.That(player.CheckFlag("flag.gang_wild_dog_received"), Is.True);
            Assert.That(player.CheckFlag("flag.shiba_bought_wild_dog"), Is.False, "The gang reward must not invent a shop purchase.");
            Assert.That(player.CheckFlag("flag.gang_wild_dog_paid"), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [TestCase(OutsideStoryCommand.RepairElevator, "elevator_repaired")]
    [TestCase(OutsideStoryCommand.RevealCat, "boobi_revealed")]
    [TestCase(OutsideStoryCommand.AdoptCat, "boobi_adopted")]
    public void MissingWorldActionOwnerCannotCompleteTheEvent(OutsideStoryCommand command, string flag)
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            Assert.Throws<InvalidOperationException>(() => new NeighborhoodStoryEvents(null)
                .ExecuteAsync(command, null, run, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(player.CheckFlag("flag." + flag), Is.False);
            Assert.That(run.Destination, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void CancellationAtCutsceneCompletionCannotCommitEffectsAndNextRunCanRetry()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            var cancelled = new OutsideStoryExecution(conditions, PickFirst, (cutscene, token) =>
            { cancellation.Cancel(); return UniTask.CompletedTask; });
            var handler = new NeighborhoodStoryEvents(null);
            Assert.Throws<OperationCanceledException>(() => handler.ExecuteAsync(OutsideStoryCommand.RepairElevator, null, cancelled,
                cancellation.Token).GetAwaiter().GetResult());
            Assert.That(player.CheckFlag("flag.elevator_repaired"), Is.False);
            int plays = 0;
            var retry = new OutsideStoryExecution(conditions, PickFirst, (cutscene, token) =>
            { plays++; return UniTask.CompletedTask; });
            handler.ExecuteAsync(OutsideStoryCommand.RepairElevator, null, retry, CancellationToken.None).GetAwaiter().GetResult();
            handler.ExecuteAsync(OutsideStoryCommand.RepairElevator, null, retry, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(player.CheckFlag("flag.elevator_repaired"), Is.True);
            Assert.That(plays, Is.EqualTo(1));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void CancelledChoiceCannotChargeMoney()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        using var cancellation = new CancellationTokenSource();
        try
        {
            player.SetMoney(800);
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            var run = new OutsideStoryExecution(conditions, (options, token) =>
            { cancellation.Cancel(); return UniTask.FromResult(options.Single(option => option.Id == "pay")); }, null);
            Assert.Throws<OperationCanceledException>(() => new NeighborhoodStoryEvents(null).ExecuteAsync(
                OutsideStoryCommand.BossFirstChoice, null, run, cancellation.Token).GetAwaiter().GetResult());
            Assert.That(player.HasMoney(), Is.EqualTo(800));
            Assert.That(player.CheckFlag("flag.gang_wild_dog_paid"), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void PendingBossDealCannotChargeAgainEvenWhenMoneyRemains()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            player.SetMoney(2000);
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            var run = new OutsideStoryExecution(conditions, (options, token) =>
                UniTask.FromResult(options.Single(option => option.Id == "pay")), null);
            var handler = new NeighborhoodStoryEvents(new CsvStoryTestContent());
            handler.ExecuteAsync(OutsideStoryCommand.BossFirstChoice, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(player.HasMoney(), Is.EqualTo(1500));
            Assert.That(player.CheckFlag("flag.gang_wild_dog_paid"), Is.True);
            Assert.Throws<InvalidOperationException>(() => handler.ExecuteAsync(OutsideStoryCommand.BossRepeatChoice, null, run,
                CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(player.HasMoney(), Is.EqualTo(1500));
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void AdoptionDefersSceneTravelUntilTheDialogueOwnerReleasesItsSession()
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            int plays = 0;
            var run = new OutsideStoryExecution(conditions, PickFirst, (cutscene, token) =>
            { plays++; return UniTask.CompletedTask; });
            var handler = new NeighborhoodStoryEvents(null);
            handler.ExecuteAsync(OutsideStoryCommand.AdoptCat, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(player.CheckFlag("flag.boobi_adopted"), Is.True);
            Assert.That(run.Destination, Is.EqualTo(GameProgressionDestination.HomeVisit));
            handler.ExecuteAsync(OutsideStoryCommand.AdoptCat, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(plays, Is.EqualTo(1));
            var cancelled = new StoryExecutionResult(StoryExecutionStatus.Cancelled, destination: run.Destination);
            Assert.That(cancelled.Destination, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    [Test]
    public void EveryNativeCommandHasExactlyOneDomainOwnerAndDuplicatesAreRejected()
    {
        IOutsideStoryEventHandler[] handlers = { new ShopStoryEvents(null, null, null), new NeighborhoodStoryEvents(null), new StreetStoryEvents(null, null) };
        foreach (OutsideStoryCommand command in Enum.GetValues(typeof(OutsideStoryCommand)))
            Assert.That(handlers.Count(handler => handler.Handles(command)), Is.EqualTo(command == OutsideStoryCommand.None ? 0 : 1), command.ToString());
        var duplicate = new OutsideStoryEvents(new[] { handlers[0], handlers[0] });
        Assert.Throws<InvalidOperationException>(() => duplicate.ExecuteAsync(OutsideStoryCommand.ShopOpen, null, null, CancellationToken.None));
    }

    [Test]
    public void CsvCompilationDoesNotChangeSourceAndUnknownInstructionsAreRejected()
    {
        var original = new Step { Type = "effect", WebFields = new Dictionary<string, object>
            { ["type"] = "neighborhood-action", ["action"] = "repair" } };
        var raw = new NewStreetData { Scenes = new[] { new NewSceneData { Id = "test", Steps = new[] { original } } } };
        var compiled = StoryCsvCompiler.Compile(raw);
        Assert.That(raw.Scenes[0].Steps[0].WebFields, Is.Not.Null);
        Assert.That(raw.Scenes[0].Steps[0].Command, Is.EqualTo(OutsideStoryCommand.None));
        Assert.That(compiled.Scenes[0].Steps[0].WebFields, Is.Null);
        Assert.That(compiled.Scenes[0].Steps[0].Command, Is.EqualTo(OutsideStoryCommand.RepairElevator));
        Assert.Throws<NotSupportedException>(() => StoryCsvCompiler.Parse("neighborhood-action", null, "unknown"));
    }
}
