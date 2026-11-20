using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

/// <summary>실제 CSV와 기존 재화·진행 상태를 사용해 수리 사건의 재방문·중단 경계를 검증한다.</summary>
public sealed class RepairmanStoryTests
{
    private const string RepairSceneId = "neighborhood-repairGreeting";
    private CsvStoryTestContent content;
    private PlayerDataSO player;
    private IConditionUtil conditions;
    private NeighborhoodStoryEvents events;

    [OneTimeSetUp]
    public void LoadAuthoredContent() => content = new CsvStoryTestContent();

    [SetUp]
    public void CreateProgress()
    {
        player = ScriptableObject.CreateInstance<PlayerDataSO>();
        conditions = new ConditionUtil(GameStateManager.Instance, player, player).CreateExecutionScope();
        events = new NeighborhoodStoryEvents(content);
    }

    [TearDown]
    public void DestroyProgress() => UnityEngine.Object.DestroyImmediate(player);

    private OutsideStoryExecution Choose(string id,
        Func<OutsideStoryWorldAction, CancellationToken, UniTask> play = null)
        => new(conditions, (options, token) => UniTask.FromResult(options.Single(option => option.Id == id)), play);

    private Step[] Execute(OutsideStoryCommand command, OutsideStoryExecution run, CancellationToken token = default)
        => events.ExecuteAsync(command, null, run, token).GetAwaiter().GetResult();

    private NewSceneData Entry() => new()
    {
        Id = RepairSceneId,
        Steps = content.GetSceneSteps(RepairSceneId)
    };

    [TestCase(0, EGameFlow.CommuteIn, false)]
    [TestCase(0, EGameFlow.CommuteOut, false)]
    [TestCase(1, EGameFlow.CommuteIn, true)]
    [TestCase(1, EGameFlow.CommuteOut, true)]
    [TestCase(2, EGameFlow.CommuteIn, true)]
    [TestCase(3, EGameFlow.CommuteOut, true)]
    [TestCase(10, EGameFlow.CommuteIn, true)]
    public void WaitingRepairmanDoesNotDependOnMoneyPartsOrAnUpperDayLimit(int day, EGameFlow flow, bool expected)
    {
        player.SetMoney(0);
        Assert.That(player.CheckFlag("has_gear_part"), Is.False);
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, day, flow, conditions), Is.EqualTo(expected));

        player.AddFlag("repairman_met", true);
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, day, flow, conditions), Is.EqualTo(expected),
            "Finishing the introduction must not remove a waiting repairman.");
    }

    [TestCase("pay", 600, true, "gear")]
    [TestCase("gear", 1000, false, "pay")]
    public void DeliveryConsumesOnlyTheSelectedResourceAndCannotBePaidAgain(
        string option, int expectedMoney, bool expectedPart, string secondOption)
    {
        player.SetMoney(1000);
        player.AddFlag("has_gear_part", true);

        var continuation = Execute(OutsideStoryCommand.RepairChoice, Choose(option));
        Assert.That(player.HasMoney(), Is.EqualTo(expectedMoney));
        Assert.That(player.CheckFlag("has_gear_part"), Is.EqualTo(expectedPart));
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.True);
        Assert.That(player.CheckFlag("elevator_repaired"), Is.False,
            "Payment alone must not unlock elevator travel or the gang repair deal.");
        Assert.That(continuation.Any(step => step.Command == OutsideStoryCommand.RepairElevator), Is.True);

        Assert.Throws<InvalidOperationException>(() => Execute(OutsideStoryCommand.RepairChoice, Choose(secondOption)));
        Assert.That(player.HasMoney(), Is.EqualTo(expectedMoney));
        Assert.That(player.CheckFlag("has_gear_part"), Is.EqualTo(expectedPart));
    }

    [TestCase("pay")]
    [TestCase("gear")]
    public void UnavailableDeliveryDoesNotConsumeResourcesOrHideTheRepairman(string option)
    {
        player.SetMoney(399);
        Assert.Throws<InvalidOperationException>(() => Execute(OutsideStoryCommand.RepairChoice, Choose(option)));
        Assert.That(player.HasMoney(), Is.EqualTo(399));
        Assert.That(player.CheckFlag("has_gear_part"), Is.False);
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.False);
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, 3, EGameFlow.CommuteOut, conditions), Is.True);
    }

    [Test]
    public void APartCanPayForRepairWithNoMoney()
    {
        player.SetMoney(0);
        player.AddFlag("has_gear_part", true);

        Execute(OutsideStoryCommand.RepairChoice, Choose("gear"));

        Assert.That(player.HasMoney(), Is.Zero);
        Assert.That(player.CheckFlag("has_gear_part"), Is.False);
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.True);
        Assert.That(player.CheckFlag("elevator_repaired"), Is.False);
    }

    [Test]
    public void CancelMenuKeepsBothResourcesAndTheWaitingNpc()
    {
        player.SetMoney(800);
        player.AddFlag("has_gear_part", true);
        var continuation = Execute(OutsideStoryCommand.RepairChoice, Choose("cancel"));

        Assert.That(continuation, Is.Empty);
        Assert.That(player.HasMoney(), Is.EqualTo(800));
        Assert.That(player.CheckFlag("has_gear_part"), Is.True);
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.False);
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, 1, EGameFlow.CommuteIn, conditions), Is.True);
    }

    [TestCase("pay")]
    [TestCase("gear")]
    public void CancellationWhileConfirmingDeliveryDoesNotCommitPayment(string option)
    {
        player.SetMoney(800);
        player.AddFlag("has_gear_part", true);
        using var cancellation = new CancellationTokenSource();
        var run = new OutsideStoryExecution(conditions, (options, token) =>
        {
            cancellation.Cancel();
            return UniTask.FromResult(options.Single(value => value.Id == option));
        }, null);

        Assert.Throws<OperationCanceledException>(() => Execute(OutsideStoryCommand.RepairChoice, run, cancellation.Token));
        Assert.That(player.HasMoney(), Is.EqualTo(800));
        Assert.That(player.CheckFlag("has_gear_part"), Is.True);
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.False);
    }

    [Test]
    public void ANewVisitAfterPaymentResumesTheAuthoredRepairWithoutAnotherChoice()
    {
        player.SetMoney(400);
        Execute(OutsideStoryCommand.RepairChoice, Choose("pay"));

        var resumed = OutsideStorySceneRules.ResolveEntry(Entry(), 2, conditions, content);
        Assert.That(resumed.Steps, Is.SameAs(content.GetSceneSteps("neighborhood-repairPaid")));
        Assert.That(resumed.Steps.Count(step => step.Command == OutsideStoryCommand.RepairElevator), Is.EqualTo(1));
        Assert.That(resumed.Steps.Any(step => step.Command == OutsideStoryCommand.RepairChoice), Is.False);
        Assert.That(player.HasMoney(), Is.Zero);
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, 2, EGameFlow.CommuteIn, conditions), Is.True);
    }

    [Test]
    public void RepairCannotStartBeforeDelivery()
    {
        int plays = 0;
        var run = Choose("cancel", (action, token) => { plays++; return UniTask.CompletedTask; });
        Assert.Throws<InvalidOperationException>(() => Execute(OutsideStoryCommand.RepairElevator, run));
        Assert.That(plays, Is.Zero);
        Assert.That(player.CheckFlag("elevator_repaired"), Is.False);
    }

    [Test]
    public void InterruptedPaidRepairCanRetryWithoutChargingAndOnlyCommitsOnce()
    {
        player.SetMoney(800);
        Execute(OutsideStoryCommand.RepairChoice, Choose("pay"));
        using var cancellation = new CancellationTokenSource();
        var interrupted = Choose("cancel", (action, token) =>
        {
            cancellation.Cancel();
            return UniTask.CompletedTask;
        });

        Assert.Throws<OperationCanceledException>(() => Execute(OutsideStoryCommand.RepairElevator, interrupted, cancellation.Token));
        Assert.That(player.CheckFlag("elevator_repair_paid"), Is.True);
        Assert.That(player.CheckFlag("elevator_repaired"), Is.False);
        Assert.That(player.HasMoney(), Is.EqualTo(400));

        int repairs = 0;
        var retry = Choose("cancel", (action, token) => { repairs++; return UniTask.CompletedTask; });
        Execute(OutsideStoryCommand.RepairElevator, retry);
        Execute(OutsideStoryCommand.RepairElevator, retry);

        Assert.That(repairs, Is.EqualTo(1));
        Assert.That(player.CheckFlag("elevator_repaired"), Is.True);
        Assert.That(player.HasMoney(), Is.EqualTo(400));
        Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, 3, EGameFlow.CommuteOut, conditions), Is.False);
    }

    [Test]
    public void TheGangRepairAlternativeReadsTheSameCompletionFlagWithoutChargingMoney()
    {
        player.SetMoney(0);
        player.AddFlag("elevator_repair_paid", true);
        Assert.Throws<InvalidOperationException>(() => Execute(OutsideStoryCommand.BossFirstChoice, Choose("repair")));
        Assert.That(player.CheckFlag("gang_wild_dog_repair_promised"), Is.False);

        Execute(OutsideStoryCommand.RepairElevator, Choose("cancel", (action, token) => UniTask.CompletedTask));
        var continuation = Execute(OutsideStoryCommand.BossFirstChoice, Choose("repair"));

        Assert.That(player.CheckFlag("gang_wild_dog_repair_promised"), Is.True);
        Assert.That(player.HasMoney(), Is.Zero);
        Assert.That(continuation.Any(step => step.Command == OutsideStoryCommand.GrantWildDog), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExistingPlayerProgressRestorePreservesPendingAndCompletedRepair(bool completed)
    {
        player.SetMoney(123);
        player.AddFlag("repairman_met", true);
        player.AddFlag("elevator_repair_paid", true);
        player.AddFlag("elevator_repaired", completed);
        var restored = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            // SaveManager writes ReadFlags and restores through ReplaceProgress; no new save fields are needed.
            restored.ReplaceProgress(player.HasMoney(), player.GetSkillValue(), player.ReadAffinity(), player.ReadFlags());
            var restoredConditions = new ConditionUtil(GameStateManager.Instance, restored, restored).CreateExecutionScope();

            Assert.That(restored.CheckFlag("repairman_met"), Is.True);
            Assert.That(restored.CheckFlag("elevator_repair_paid"), Is.True);
            Assert.That(restored.CheckFlag("elevator_repaired"), Is.EqualTo(completed));
            Assert.That(restored.HasMoney(), Is.EqualTo(123));
            Assert.That(OutsideStorySceneRules.IsAvailable(RepairSceneId, 3, EGameFlow.CommuteIn, restoredConditions), Is.EqualTo(!completed));
            if (!completed)
                Assert.That(OutsideStorySceneRules.ResolveEntry(Entry(), 3, restoredConditions, content).Steps,
                    Is.SameAs(content.GetSceneSteps("neighborhood-repairPaid")));
        }
        finally { UnityEngine.Object.DestroyImmediate(restored); }
    }
}
