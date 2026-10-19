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
                Id = "test", Status = ENewDataStatus.Confirmed,
                Glass = "glass", TargetMixMethod = ENewMixMethod.Build,
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
        int preparationCleared = 0;
        manager.PreparationChanged += current =>
        {
            if (current == null)
            {
                Assert.IsNull(manager.Preparation);
                preparationCleared++;
            }
        };
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
        Assert.AreEqual(1, preparationCleared);
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
            Id = "test", Status = ENewDataStatus.Confirmed,
            Recipe = new[] { new NewCocktailRecipeStep
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
        Assert.IsFalse(manager.TryBegin("test", out _, out _), "미서빙 잔이 있으면 다음 제조를 막는다.");
        Assert.IsTrue(manager.ConsumeDrink(manager.PendingDrink));
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

    [Test]
    public void GimmickRetryKeepsActualSelectionsAndStartsWithFreshResultsAndTimer()
    {
        cocktails.cocktailData[0].Recipe = new[]
        {
            new NewCocktailRecipeStep
            { Ingredient = "gin", Action = ENewRecipeAction.Pour, Qty = 30, Unit = ENewUnit.Ml },
            new NewCocktailRecipeStep
            { Ingredient = "soda", Action = ENewRecipeAction.Pour, Qty = 120, Unit = ENewUnit.Ml }
        };
        var first = Prepare("soda");
        manager.ToggleIngredient("gin");
        manager.SelectGlass("other-glass");
        manager.SelectTool(NewToolIds.MixingGlass);
        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        var drink = manager.PendingDrink;
        var firstCompletion = manager.Completion;
        int discarded = 0;
        manager.DrinkDiscarded += previous =>
        {
            Assert.AreSame(drink, previous);
            Assert.AreEqual(ECraftPhase.Discarded, first.Phase);
            discarded++;
        };
        manager.CraftBegan += _ =>
        {
            Assert.IsFalse(manager.ShouldShowPreparation);
            Assert.AreEqual("other-glass", manager.Preparation.GlassId);
        };
        executor.Pending = new UniTaskCompletionSource<GimmickResult>();

        Assert.IsTrue(manager.TryRetryGimmicks(drink, out var reason), reason);
        var retry = manager.Current;
        Assert.AreNotEqual(first.JobId, retry.JobId);
        Assert.AreEqual(ECraftPhase.Playing, retry.Phase);
        Assert.IsTrue(manager.IsCraftFlowActive);
        Assert.AreEqual("other-glass", retry.Actual.GlassId);
        Assert.AreEqual(NewToolIds.MixingGlass, retry.Actual.ToolId);
        CollectionAssert.AreEqual(new[] { "soda", "gin" }, retry.Actual.IngredientIds);
        Assert.AreEqual(0, retry.Actual.GimmickResults.Count);
        Assert.AreEqual(0, retry.CurrentGimmickIndex);
        Assert.AreEqual(0f, retry.Actual.ElapsedManualSec);
        Assert.AreEqual(1f, retry.Timer.ElapsedSec, "Only the new first step has ticked.");
        Assert.IsNull(manager.Judgement);
        Assert.IsNull(manager.Preparation);
        Assert.IsNull(manager.PendingDrink);
        Assert.AreEqual(1, discarded);
        Assert.AreEqual(3, first.Actual.GimmickResults.Count);
        Assert.AreEqual(3f, first.Actual.ElapsedManualSec);
        Assert.AreSame(first, firstCompletion.GetAwaiter().GetResult());
        Assert.IsFalse(manager.TryRetryGimmicks(drink, out _));

        var pending = executor.Pending;
        executor.Pending = null;
        pending.TrySetResult(GimmickResult.Quantity(ECraftGimmick.Pour, "soda", 120, ENewUnit.Ml,
            120, ECraftEndType.ManualNext, retry.Timer.ElapsedSec));
        Assert.AreSame(retry, manager.Completion.GetAwaiter().GetResult());
        Assert.AreEqual(3, retry.Actual.GimmickResults.Count);
        Assert.AreEqual(3f, retry.Actual.ElapsedManualSec);
        CollectionAssert.AreEqual(new[]
        {
            ECraftGimmick.Pour, ECraftGimmick.Pour, ECraftGimmick.Stir,
            ECraftGimmick.Pour, ECraftGimmick.Pour, ECraftGimmick.Stir
        }, executor.Types);
        Assert.AreSame(retry, manager.PendingDrink.Session);
        Assert.IsFalse(manager.ConsumeDrink(drink));
        Assert.IsFalse(manager.TryRetryGimmicks(drink, out _));
        Assert.AreEqual(1, discarded);
    }

    [Test]
    public void BlockedGimmickRetryPreservesTheCompletedDrink()
    {
        var first = Prepare();
        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        var drink = manager.PendingDrink;
        int discarded = 0;
        manager.DrinkDiscarded += _ => discarded++;
        Func<string> blocker = () => "주문이 취소되었습니다.";
        manager.AddCraftBlocker(blocker);

        Assert.IsFalse(manager.TryRetryGimmicks(drink, out var reason));
        Assert.AreEqual(blocker(), reason);
        Assert.AreSame(first, manager.Current);
        Assert.AreSame(drink, manager.PendingDrink);
        Assert.AreEqual(ECraftPhase.Completed, first.Phase);
        Assert.AreEqual(1, executor.BeginCount);
        Assert.AreEqual(0, discarded);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ServeScreenDiscardUsesItsRetrySetting(bool retryOnDiscard)
    {
        var first = Prepare();
        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        var drink = manager.PendingDrink;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(CraftFlowController).GetField("<IsAwaitingServeDecision>k__BackingField", flags)
            .SetValue(manager, true);
        var screen = owner.AddComponent<ServeScreen>();
        typeof(ServeScreen).GetField("craftFlow", flags).SetValue(screen, manager);
        typeof(ServeScreen).GetField("currentDrink", flags).SetValue(screen, drink);
        typeof(ServeScreen).GetField("retryOnDiscard", flags).SetValue(screen, retryOnDiscard);
        SetField("serveScreen", screen);
        int discarded = 0;
        manager.DrinkDiscarded += _ => discarded++;
        if (retryOnDiscard) executor.Pending = new UniTaskCompletionSource<GimmickResult>();

        screen.Discard();
        screen.Discard();

        Assert.AreEqual(ECraftPhase.Discarded, first.Phase);
        Assert.AreEqual(1, discarded);
        Assert.IsFalse(screen.IsShowing);
        Assert.IsFalse(manager.IsAwaitingServeDecision);
        Assert.IsNull(manager.PendingDrink);
        Assert.AreEqual(retryOnDiscard ? 2 : 1, executor.BeginCount);
        if (retryOnDiscard)
        {
            Assert.AreNotSame(first, manager.Current);
            Assert.AreEqual(ECraftPhase.Playing, manager.Current.Phase);
            Assert.AreEqual(first.Actual.GlassId, manager.Current.Actual.GlassId);
            CollectionAssert.AreEqual(first.Actual.IngredientIds, manager.Current.Actual.IngredientIds);
            Assert.IsFalse(manager.ShouldShowPreparation);
            manager.CancelCurrentCraft();
            executor.Pending.TrySetCanceled();
        }
        else
        {
            Assert.AreSame(first, manager.Current);
            Assert.IsFalse(manager.IsBusy);
        }
    }

    [Test]
    public void GimmickRetryDuringFinishIsRejectedWithoutDiscardingTheDrink()
    {
        var first = Prepare();
        manager.CraftCompleted += (_, __) =>
        {
            var drink = manager.PendingDrink;
            Assert.IsFalse(manager.TryRetryGimmicks(drink, out var reason));
            Assert.AreEqual("이미 제조 또는 정리가 진행 중입니다.", reason);
            Assert.AreSame(drink, manager.PendingDrink);
        };

        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        Assert.AreEqual(ECraftPhase.Completed, first.Phase);
        Assert.AreEqual(1, executor.BeginCount);
    }

    [Test]
    public void GimmickRetrySkipsPreparationStageCreation()
    {
        Prepare();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            "[CraftFlow] balanceData가 없어 등급을 계산하지 못했습니다.");
        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        var host = owner.AddComponent<CraftPrepStageHost>();
        typeof(CraftPrepStageHost).GetField("craftFlow",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(host, manager);
        typeof(CraftPrepStageHost).GetMethod("OnEnable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(host, null);

        // Stage Prefab/Root/Input이 비어 있어도 재시도에서는 화면 생성 경로를 타지 않는다.
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            "[CraftFlow] balanceData가 없어 등급을 계산하지 못했습니다.");
        manager.RetryGimmicks();
        Assert.AreEqual(2, executor.BeginCount);
        Assert.AreEqual(ECraftPhase.Completed, manager.Current.Phase);
        Assert.IsNull(host.ActiveStage);
        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GimmickRetryRemovesDiscardedTrayItemEvenWhenTrayWasDisabled(bool trayDisabled)
    {
        Prepare();
        manager.StartGimmicksAsync().GetAwaiter().GetResult();
        var drink = manager.PendingDrink;
        var canvas = new GameObject("Retry tray test", typeof(Canvas));
        var trayRoot = new GameObject("Tray", typeof(RectTransform));
        var poolRoot = new GameObject("Pool", typeof(RectTransform));
        var prefab = new GameObject("Drink", typeof(RectTransform), typeof(DrinkDragItem));
        trayRoot.transform.SetParent(canvas.transform);
        poolRoot.transform.SetParent(canvas.transform);
        prefab.transform.SetParent(poolRoot.transform);
        var tray = canvas.AddComponent<ServeTrayUI>();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ServeTrayUI).GetField("craftFlow", flags).SetValue(tray, manager);
        typeof(ServeTrayUI).GetField("rootCanvas", flags).SetValue(tray, canvas.GetComponent<Canvas>());
        typeof(ServeTrayUI).GetField("trayRoot", flags).SetValue(tray, (RectTransform)trayRoot.transform);
        typeof(ServeTrayUI).GetField("poolRoot", flags).SetValue(tray, (RectTransform)poolRoot.transform);
        typeof(ServeTrayUI).GetField("drinkPrefab", flags).SetValue(tray, prefab.GetComponent<DrinkDragItem>());
        try
        {
            typeof(ServeTrayUI).GetMethod("OnEnable", flags).Invoke(tray, null);
            Assert.AreEqual(1, tray.Count);
            if (trayDisabled) typeof(ServeTrayUI).GetMethod("OnDisable", flags).Invoke(tray, null);
            executor.Pending = new UniTaskCompletionSource<GimmickResult>();

            Assert.IsTrue(manager.TryRetryGimmicks(drink, out var reason), reason);
            if (trayDisabled) typeof(ServeTrayUI).GetMethod("OnEnable", flags).Invoke(tray, null);
            Assert.AreEqual(0, tray.Count);

            var retry = manager.Current;
            var pending = executor.Pending;
            executor.Pending = null;
            pending.TrySetResult(GimmickResult.Quantity(ECraftGimmick.Pour, "gin", 30, ENewUnit.Ml,
                30, ECraftEndType.ManualNext, retry.Timer.ElapsedSec));
            Assert.AreEqual(1, tray.Count);
            Assert.AreSame(retry, manager.PendingDrink.Session);
        }
        finally
        {
            typeof(ServeTrayUI).GetMethod("OnDisable", flags).Invoke(tray, null);
            UnityEngine.Object.DestroyImmediate(canvas);
        }
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
