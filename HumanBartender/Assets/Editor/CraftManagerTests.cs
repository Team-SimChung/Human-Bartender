using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CraftManagerTests
{
    NewCocktailDataSO cocktails;
    NewShelfItemDataSO shelf;
    FakeExecutor executor;
    TestCraftFlowController manager;
    GameObject owner;

    [SetUp]
    public void SetUp()
    {
        cocktails = ScriptableObject.CreateInstance<NewCocktailDataSO>();
        shelf = ScriptableObject.CreateInstance<NewShelfItemDataSO>();
        cocktails.cocktailData = new[]
        {
            new NewCocktailData
            {
                Id = "test", Glass = "glass", TargetMixMethod = ENewMixMethod.Build,
                Recipe = new[]
                {
                    new NewCocktailRecipeStep
                    { Ingredient = "gin", Action = ENewRecipeAction.Pour, Qty = 30, Unit = ENewUnit.Ml }
                }
            }
        };
        shelf.shelfItemData = Array.Empty<NewShelfItemData>();
        executor = new FakeExecutor();
        owner = new GameObject("Craft flow test");
        owner.SetActive(false);
        manager = owner.AddComponent<TestCraftFlowController>();
        manager.TestExecutor = executor;
        SetField("cocktailData", cocktails);
        SetField("shelfData", shelf);
        SetField("autoPrepareForTest", false);
        owner.SetActive(true);
    }

    void SetField(string name, object value) => typeof(CraftFlowController)
        .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .SetValue(manager, value);

    [TearDown]
    public void TearDown()
    {
        if (manager.Current != null) manager.CancelCraft(manager.Current.JobId);
        UnityEngine.Object.DestroyImmediate(owner);
        UnityEngine.Object.DestroyImmediate(cocktails);
        UnityEngine.Object.DestroyImmediate(shelf);
    }

    CraftSession Prepare(string ingredient = "gin")
    {
        Assert.IsTrue(manager.TryBegin("test", out var session, out var reason), reason);
        manager.SelectGlass("glass");
        manager.ToggleIngredient(ingredient);
        return session;
    }

    [Test]
    public void SuccessfulRequestRecordsStepsAndCleansBeforeCompletion()
    {
        var session = Prepare();
        manager.SelectTool(NewToolIds.MixingGlass); // 오선택 도구도 실제로 실행한다.
        int completed = 0, ended = 0;
        manager.CraftCompleted += (s, j) =>
        {
            Assert.AreEqual(1, executor.EndCount);
            Assert.AreEqual(ECraftPhase.Completed, s.Phase);
            completed++;
        };
        manager.CraftEnded += _ => ended++;

        Assert.AreSame(session, manager.StartGimmicksAsync().GetAwaiter().GetResult());
        CollectionAssert.AreEqual(new[] { ECraftGimmick.Pour, ECraftGimmick.Stir }, executor.Types);
        Assert.AreEqual(2, session.Actual.GimmickResults.Count);
        Assert.AreEqual(2, session.CurrentGimmickIndex);
        Assert.AreEqual(2f, session.Actual.ElapsedManualSec);
        Assert.IsFalse(session.Timer.IsRunning);
        Assert.IsFalse(manager.IsBusy);
        Assert.AreEqual(1, completed);
        Assert.AreEqual(1, ended);
    }

    [Test]
    public void RejectsOverlappingRequestsAndStaleCancellation()
    {
        var first = Prepare();
        Assert.IsFalse(manager.TryBegin("test", out _, out _));
        Assert.IsTrue(manager.CancelCraft(first.JobId));
        Assert.AreEqual(ECraftPhase.Cancelled, first.Phase);
        var second = Prepare();
        Assert.AreNotEqual(first.JobId, second.JobId);
        Assert.IsFalse(manager.CancelCraft(first.JobId));
        Assert.AreEqual(ECraftPhase.Preparing, second.Phase);
    }

    [Test]
    public void CancellationWaitsForExecutorCleanupAndDoesNotRecordLateResult()
    {
        var session = Prepare();
        executor.Pending = new UniTaskCompletionSource<GimmickResult>();
        int completed = 0;
        manager.CraftCompleted += (_, __) => completed++;
        var run = manager.StartGimmicksAsync();
        var cancellation = manager.CancelCraftAsync(session.JobId);
        Assert.AreEqual(UniTaskStatus.Pending, cancellation.Status);
        Assert.IsTrue(manager.IsBusy); // 실행 담당이 아직 반환하지 않았다.
        Assert.IsFalse(manager.TryBegin("test", out _, out _));
        executor.Pending.TrySetResult(GimmickResult.Open("late", 1, 0, true, 0));
        Assert.AreEqual(ECraftPhase.Cancelled, run.GetAwaiter().GetResult().Phase);
        Assert.IsTrue(cancellation.GetAwaiter().GetResult());
        Assert.AreEqual(0, session.Actual.GimmickResults.Count);
        Assert.AreEqual(0, completed);
        Assert.AreEqual(1, executor.EndCount);
        Assert.IsFalse(manager.IsBusy);
        Assert.IsTrue(manager.TryBegin("test", out _, out _));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SetupAndExecutionFailuresCleanUpAndAllowRetry(bool duringSetup)
    {
        Prepare();
        executor.ThrowInBegin = duringSetup;
        executor.ThrowInStep = !duringSetup;
        var result = manager.StartGimmicksAsync().GetAwaiter().GetResult();
        Assert.AreEqual(ECraftPhase.Failed, result.Phase);
        Assert.IsNotEmpty(result.FailureReason);
        Assert.AreEqual(1, executor.EndCount);
        Assert.IsFalse(result.Timer.IsRunning);
        Assert.IsTrue(manager.TryBegin("test", out _, out _));
    }

    [Test]
    public void UnresolvedIngredientFailsBeforeCreatingObjects()
    {
        Prepare("unknown");
        var result = manager.StartGimmicksAsync().GetAwaiter().GetResult();
        Assert.AreEqual(ECraftPhase.Failed, result.Phase);
        Assert.AreEqual(0, executor.BeginCount);
        Assert.IsFalse(manager.IsBusy);
    }

    [Test]
    public void EmptyQueueTerminatesInsteadOfStayingInPreparation()
    {
        // 직접 선택 목록에 들어온 자동 재료는 큐에서 제외되는 기존 규칙을 사용한다.
        cocktails.cocktailData[0] = new NewCocktailData
        {
            Id = "test", Recipe = new[] { new NewCocktailRecipeStep
            { Ingredient = "lemon", Action = ENewRecipeAction.Squeeze, AutoApply = true } }
        };
        Prepare("lemon");
        var result = manager.StartGimmicksAsync().GetAwaiter().GetResult();
        Assert.AreEqual(ECraftPhase.Failed, result.Phase);
        Assert.AreEqual(0, manager.Plan.Count);
        Assert.IsNull(manager.Preparation);
        Assert.IsTrue(manager.TryBegin("test", out _, out _));
    }

    [Test]
    public void BlockerRemovalAllowsRequestWithoutReplacingAnExistingSession()
    {
        Func<string> blocker = () => "서빙할 잔이 남아 있습니다.";
        manager.AddCraftBlocker(blocker);
        Assert.IsFalse(manager.TryBegin("test", out _, out var reason));
        Assert.AreEqual(blocker(), reason);
        Assert.IsNull(manager.Current);
        manager.RemoveCraftBlocker(blocker);
        Assert.IsTrue(manager.TryBegin("test", out _, out _));
    }

    [Test]
    public void TerminalSessionCannotBeChangedByLateFailureOrCompletion()
    {
        var session = Prepare();
        manager.CancelCraft(session.JobId);
        session.BeginGimmicks();
        session.Complete();
        session.Fail("late");
        session.Discard();
        Assert.AreEqual(ECraftPhase.Cancelled, session.Phase);
    }

    [Test]
    public void ButtonEntryPointsUseTheSameRequestAndCancellationState()
    {
        manager.BeginCraft("test");
        var first = manager.Current;
        Assert.IsNotNull(first);
        manager.SelectGlass("glass");
        manager.ToggleIngredient("gin");
        manager.StartGimmicks();
        Assert.AreEqual(ECraftPhase.Completed, first.Phase);
        manager.BeginCraft("test");
        manager.CancelCurrentCraft();
        Assert.AreEqual(ECraftPhase.Cancelled, manager.Current.Phase);
    }

    [Test]
    public void DisablingTheComponentCancelsPreparationAndRejectsNewRequests()
    {
        var session = Prepare();
        owner.SetActive(false);
        // EditMode에서는 ExecuteAlways가 없는 컴포넌트의 수명 콜백을 직접 호출한다.
        typeof(CraftFlowController).GetMethod("OnDisable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(manager, null);
        Assert.AreEqual(ECraftPhase.Cancelled, session.Phase);
        Assert.IsFalse(manager.IsCraftFlowActive);
        Assert.IsFalse(manager.TryBegin("test", out _, out _));
        owner.SetActive(true);
        Assert.IsTrue(manager.TryBegin("test", out _, out _));
    }

    [Test]
    public void ReadyAnnouncementRemainsAvailableToThePreparationUi()
    {
        Prepare();
        manager.AutoPrepare();
        Assert.IsTrue(manager.Preparation.ConsumeReadyAnnouncement());
        Assert.IsFalse(manager.Preparation.ConsumeReadyAnnouncement());
    }

    [Test]
    public void PreparationCancellationCompletesTheAcceptedRequestOnce()
    {
        var session = Prepare();
        var completion = manager.Completion;
        int ended = 0;
        manager.CraftEnded += _ => ended++;
        Assert.IsTrue(manager.CancelCraftAsync(session.JobId).GetAwaiter().GetResult());
        Assert.AreSame(session, completion.GetAwaiter().GetResult());
        Assert.IsFalse(manager.CancelCraft(session.JobId));
        Assert.AreEqual(1, ended);
        Assert.AreEqual(0, executor.BeginCount);
        Assert.IsNull(manager.Preparation);
    }

    sealed class FakeExecutor : ICraftExecutor
    {
        public readonly List<ECraftGimmick> Types = new();
        public int BeginCount, EndCount;
        public bool ThrowInBegin, ThrowInStep;
        public UniTaskCompletionSource<GimmickResult> Pending;

        public void Begin(CraftTimer timer, CraftRunDisplay display)
        {
            BeginCount++;
            if (ThrowInBegin) throw new InvalidOperationException("setup failed");
        }

        public UniTask<GimmickResult> ExecuteAsync(GimmickStep step, CraftContext context,
                                                   CraftTimer timer, CancellationToken token)
        {
            if (ThrowInStep) throw new InvalidOperationException("step failed");
            Types.Add(step.Type);
            timer.Tick(1f);
            if (Pending != null) return Pending.Task;
            return UniTask.FromResult(GimmickResult.Quantity(step.Type, step.IngredientId,
                step.TargetValue, step.TargetUnit, 30, ECraftEndType.ManualNext, timer.ElapsedSec));
        }

        public void End() => EndCount++;
    }
}

public class TestCraftFlowController : CraftFlowController
{
    public ICraftExecutor TestExecutor;
    protected override ICraftExecutor Executor => TestExecutor;
}
