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
            if (command == OutsideStoryCommand.RepairElevator) conditions.ApplyRequired("flag.elevator_repair_paid = true");
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
            conditions.ApplyRequired("flag.elevator_repair_paid = true");
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

    // ── 시바견 상점 남은 상품·판매 종료 (웹 outside-shop.js) ──

    static void WithShop(int day, int money, string flags, Action<PlayerDataSO, IConditionUtil> body)
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        int previousDay = GameStateManager.Instance.CurrentDay;
        try
        {
            GameStateManager.Instance.CurrentDay = day;
            player.SetMoney(money);
            var conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
            if (!string.IsNullOrEmpty(flags)) conditions.ApplyRequired(flags);
            body(player, conditions);
        }
        finally
        {
            GameStateManager.Instance.CurrentDay = previousDay;
            UnityEngine.Object.DestroyImmediate(player);
        }
    }

    [TestCase(1, "", true, TestName = "Day1 bitters left")]
    [TestCase(1, "flag.shiba_bought_bitters = true", false, TestName = "Day1 bitters bought, tuna not yet sold")]
    [TestCase(2, "flag.shiba_bought_bitters = true", true, TestName = "Day2 tuna left")]
    [TestCase(2, "flag.shiba_bought_bitters = true; flag.shiba_bought_tuna_can = true", false, TestName = "Day2 both bought")]
    [TestCase(2, "flag.shiba_bought_bitters = true; flag.boobi_adopted = true", false, TestName = "Day2 tuna not sold after adoption")]
    public void ShopStockMatchesTheWebRule(int day, string flags, bool expected) =>
        WithShop(day, 0, flags, (_, conditions) => Assert.That(ShopStoryEvents.HasStock(day, conditions), Is.EqualTo(expected)));

    [Test]
    public void SoldOutShopEndsWithTheWebLineInsteadOfTheGreeting()
    {
        WithShop(2, 1000, "flag.shiba_shop_met = true; flag.shiba_bought_bitters = true; flag.shiba_bought_tuna_can = true", (_, conditions) =>
        {
            var content = new CsvStoryTestContent();
            var bound = new NewSceneData { Id = "shiba-shop-revisit", Steps = content.GetSceneSteps("shiba-shop-revisit") };
            var entry = OutsideStorySceneRules.ResolveEntry(bound, 2, conditions, content);
            Assert.That(entry.Steps.Select(s => s.Command), Is.EqualTo(new[] { OutsideStoryCommand.ShopSoldOut }));

            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            var lines = new ShopStoryEvents(null, GameStateManager.Instance, content)
                .ExecuteAsync(OutsideStoryCommand.ShopSoldOut, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(lines.Single().Actor, Is.EqualTo("shiba"));
            Assert.That(lines.Single().Text.Ko, Is.EqualTo("이제 팔 거 없어. 꺼져, 시바."));
        });
    }

    [Test]
    public void ShopWithStockKeepsTheAuthoredGreeting()
    {
        WithShop(2, 1000, "flag.shiba_shop_met = true; flag.shiba_bought_bitters = true", (_, conditions) =>
        {
            var content = new CsvStoryTestContent();
            var authored = content.GetSceneSteps("shiba-shop-revisit");
            var entry = OutsideStorySceneRules.ResolveEntry(new NewSceneData { Id = "shiba-shop-revisit", Steps = authored }, 2, conditions, content);
            Assert.That(entry.Steps, Is.SameAs(authored));
        });
    }

    [Test]
    public void PendingOldFashionedLessonComesBeforeTheSoldOutLine()
    {
        WithShop(2, 0, "flag.shiba_shop_met = true; flag.shiba_bought_bitters = true; flag.shiba_bought_tuna_can = true; " +
                       "flag.gang_wild_dog_received = true", (player, conditions) =>
        {
            var content = new CsvStoryTestContent();
            var entry = OutsideStorySceneRules.ResolveEntry(new NewSceneData { Id = "shiba-shop-revisit" }, 2, conditions, content);
            Assert.That(entry.Steps.Single().Command, Is.EqualTo(OutsideStoryCommand.ShopOpen));
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            var lesson = new ShopStoryEvents(player, GameStateManager.Instance, content)
                .ExecuteAsync(OutsideStoryCommand.ShopOpen, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(lesson.Last().Command, Is.EqualTo(OutsideStoryCommand.ShopQuiz));
        });
    }

    [Test]
    public void OpeningAnEmptyShopEndsBeforeTheAllowanceOffer()
    {
        WithShop(2, 0, "flag.shiba_bought_bitters = true; flag.shiba_bought_tuna_can = true", (player, conditions) =>
        {
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            var steps = new ShopStoryEvents(player, GameStateManager.Instance, new CsvStoryTestContent())
                .ExecuteAsync(OutsideStoryCommand.ShopOpen, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(steps.Single().Text.Ko, Is.EqualTo(ShopStoryEvents.SoldOutLine));
        });
    }

    [Test]
    public void AllowanceIsOfferedAfterBuyingBittersWhileTunaIsLeftLikeTheWeb()
    {
        WithShop(2, 0, "flag.shiba_bought_bitters = true", (player, conditions) =>
        {
            var content = new CsvStoryTestContent();
            var run = new OutsideStoryExecution(conditions, PickFirst, null);
            var steps = new ShopStoryEvents(player, GameStateManager.Instance, content)
                .ExecuteAsync(OutsideStoryCommand.ShopOpen, null, run, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(steps, Is.EqualTo(content.GetSceneSteps("shiba-allowance")));
        });
    }
}
