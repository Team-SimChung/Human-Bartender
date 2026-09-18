using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class OrderRequestTests
{
    GameObject owner;
    TestOrderController controller;
    FakeProcessor processor;

    [SetUp]
    public void SetUp()
    {
        owner = new GameObject("Order requests test");
        controller = owner.AddComponent<TestOrderController>();
        processor = new FakeProcessor();
        controller.Service = processor;
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(owner);

    static OrderDetails Details(string id = "one") => new(id, "guest", "gin");

    static CraftedDrink Drink(string id = "gin", bool scored = true) => new(
        new CraftSession(id), new CraftJudgement { CraftGrade = scored ? ENewGrade.Good : null },
        new NewCocktailData { Id = id });

    [UnityTest] public IEnumerator CallbackAfterSettlementAndCleanup() => CallbackCase().ToCoroutine();
    [UnityTest] public IEnumerator CancellationDuringReactionStopsSettlement() => CancelServingCase().ToCoroutine();
    [UnityTest] public IEnumerator OrdersAndCraftAreIndependent() => IndependenceCase().ToCoroutine();
    [UnityTest] public IEnumerator NoCallbackStillCleansUp() => NoCallbackCase().ToCoroutine();
    [UnityTest] public IEnumerator RejectedDrinkAndDuplicateRequest() => RejectionCase().ToCoroutine();
    [UnityTest] public IEnumerator OpenFailureReportsAfterCleanup() => OpenFailureCase().ToCoroutine();
    [UnityTest] public IEnumerator LateReceiverCannotServeReplacement() => LateReceiverCase().ToCoroutine();
    [Test] public void ExistingJudgementAndSettlementRulesPreserved() => CalculationCase();

    public async UniTask CallbackCase()
    {
        var presentation = new FakePresentation { CloseGate = new UniTaskCompletionSource() };
        var reaction = new UniTaskCompletionSource();
        int callbacks = 0;
        var request = controller.Request(Details(), result =>
        {
            Assert.IsTrue(presentation.Closed);
            Assert.AreEqual(1, processor.Applied);
            Assert.AreEqual(0, controller.ActiveCount);
            Assert.IsTrue(result.Completed);
            callbacks++;
        }, presentation, (result, token) => reaction.Task.AttachExternalCancellation(token));
        var drink = Drink();
        Assert.IsTrue(presentation.Receive(drink));
        Assert.IsFalse(controller.TryServe(request.Id, Drink()));
        await UniTask.NextFrame();
        Assert.AreEqual(0, processor.Applied);
        reaction.TrySetResult();
        await UniTask.WaitUntil(() => presentation.Unbound);
        Assert.AreEqual(0, callbacks);
        presentation.CloseGate.TrySetResult();
        await UniTask.WaitUntil(() => callbacks == 1);
        Assert.IsFalse(controller.TryServe(request.Id, drink));
        Assert.AreEqual(OrderState.Completed, request.State);
    }

    public async UniTask CancelServingCase()
    {
        var presentation = new FakePresentation();
        var reaction = new UniTaskCompletionSource();
        OrderResult received = null;
        var request = controller.Request(Details(), result => received = result, presentation,
            (result, token) => reaction.Task.AttachExternalCancellation(token));
        Assert.IsTrue(presentation.Receive(Drink()));
        await UniTask.NextFrame();
        Assert.IsTrue(controller.CancelOrder(request.Id));
        await UniTask.WaitUntil(() => received != null);
        Assert.AreEqual(OrderState.Cancelled, received.State);
        Assert.AreEqual(0, processor.Applied);
        Assert.IsTrue(presentation.Closed);
        reaction.TrySetResult();
        await UniTask.NextFrame();
        Assert.AreEqual(0, processor.Applied);
    }

    public async UniTask IndependenceCase()
    {
        var p1 = new FakePresentation();
        var p2 = new FakePresentation();
        OrderResult r1 = null, r2 = null;
        var first = controller.Request(Details("one"), result => r1 = result, p1);
        var second = controller.Request(Details("two"), result => r2 = result, p2);
        var craftOwner = new GameObject("Independent craft");
        var craft = craftOwner.AddComponent<CraftFlowController>();
        var data = ScriptableObject.CreateInstance<NewCocktailDataSO>();
        data.cocktailData = new[] { new NewCocktailData { Id = "gin" } };
        typeof(CraftFlowController).GetField("cocktailData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(craft, data);
        try
        {
            Assert.IsTrue(craft.TryBegin("gin", out var session, out _));
            Assert.IsTrue(controller.CancelOrder(first.Id));
            Assert.AreEqual(ECraftPhase.Preparing, session.Phase);
            craft.CancelCraft(session.JobId);
            Assert.AreEqual(OrderState.Waiting, second.State);
            Assert.IsTrue(craft.TryBegin("gin", out _, out _));
            Assert.IsTrue(p2.Receive(Drink()));
            await UniTask.WaitUntil(() => r1 != null && r2 != null);
            Assert.AreEqual(OrderState.Cancelled, r1.State);
            Assert.IsTrue(r2.Completed);
            Assert.AreEqual(1, processor.Applied);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(craftOwner);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    public async UniTask NoCallbackCase()
    {
        var presentation = new FakePresentation();
        controller.Request(Details(), null, presentation);
        Assert.IsTrue(presentation.Receive(Drink()));
        await UniTask.WaitUntil(() => controller.ActiveCount == 0);
        Assert.IsTrue(presentation.Closed);
        Assert.AreEqual(1, processor.Applied);
    }

    public async UniTask RejectionCase()
    {
        var presentation = new FakePresentation();
        var request = controller.Request(Details(), null, presentation);
        Assert.Throws<InvalidOperationException>(() => controller.Request(Details(), null, new FakePresentation()));
        Assert.IsFalse(presentation.Receive(Drink(scored: false)));
        Assert.AreEqual(OrderState.Waiting, request.State);
        Assert.IsTrue(presentation.Receive(Drink("wrong")));
        await UniTask.WaitUntil(() => controller.ActiveCount == 0);
        Assert.AreEqual(OrderState.Completed, request.State);
    }

    public async UniTask OpenFailureCase()
    {
        var presentation = new FakePresentation { FailOpen = true };
        OrderResult result = null;
        var request = controller.Request(Details(), value => result = value, presentation);
        await UniTask.WaitUntil(() => result != null);
        Assert.AreEqual(OrderState.Failed, request.State);
        Assert.IsTrue(presentation.Closed);
        Assert.AreEqual(0, controller.ActiveCount);
        Assert.AreEqual(0, processor.Applied);
    }

    public async UniTask LateReceiverCase()
    {
        var p1 = new FakePresentation();
        var first = controller.Request(Details(), null, p1);
        var oldReceiver = p1.Receive;
        Assert.IsTrue(controller.CancelOrder(first.Id));
        await UniTask.WaitUntil(() => controller.ActiveCount == 0);
        var p2 = new FakePresentation();
        var second = controller.Request(Details(), null, p2);
        Assert.IsFalse(oldReceiver(Drink()));
        Assert.AreEqual(OrderState.Waiting, second.State);
        Assert.IsTrue(p2.Receive(Drink()));
        await UniTask.WaitUntil(() => controller.ActiveCount == 0);
    }

    public void CalculationCase()
    {
        var cocktails = ScriptableObject.CreateInstance<NewCocktailDataSO>();
        var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
        cocktails.cocktailData = new[] { new NewCocktailData { Id = "gin", Price = 101 } };
        balance.balanceData = new NewBalanceDataBase
        {
            Config = new NewBalanceConfig { ForceSewageOnOrderMismatch = true },
            SettlementRules = new Dictionary<string, NewSettlementRule>
            {
                ["sewage"] = new() { SaleRate = 0f, TipRate = 0f, RefundRate = 1f },
                ["good"] = new() { SaleRate = 1f, TipRate = 0.15f, RefundRate = 0f }
            }
        };
        try
        {
            var sales = new DailySales();
            var service = new ServeProcessor(cocktails, balance, sales);
            var order = Details();
            var drink = Drink("wrong");
            var result = service.Evaluate(order, drink);
            Assert.AreEqual(ServeJudge.ResolveFinalGrade(drink, order.CocktailId, balance.balanceData.Config),
                result.FinalGrade);
            Assert.AreEqual(ENewGrade.Good, result.CraftGrade);
            Assert.IsFalse(result.OrderMatch);
            Assert.AreEqual(0, sales.Total);
            service.Apply(result);
            Assert.AreEqual(-101, sales.Total);
            Assert.Throws<InvalidOperationException>(() => service.Apply(result));
            Assert.AreEqual(-101, sales.Total);
            var matched = service.Evaluate(Details("two"), Drink());
            service.Apply(matched);
            Assert.AreEqual(Mathf.RoundToInt(101 * 0.15f), matched.Settlement.TipAmount);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cocktails);
            UnityEngine.Object.DestroyImmediate(balance);
        }
    }

    sealed class FakePresentation : IOrderPresentation
    {
        public Func<CraftedDrink, bool> Receive;
        public UniTaskCompletionSource CloseGate;
        public bool Closed, Unbound, FailOpen;
        public void Open(Func<CraftedDrink, bool> receive)
        {
            Receive = receive;
            if (FailOpen) throw new InvalidOperationException("Open failed");
        }
        public void Unbind() => Unbound = true;
        public async UniTask CloseAsync()
        {
            if (CloseGate != null) await CloseGate.Task;
            Closed = true;
        }
    }

    sealed class FakeProcessor : IServeProcessor
    {
        public int Applied;
        public ServeResult Evaluate(OrderDetails order, CraftedDrink drink) =>
            drink.CraftGrade == null ? null : new ServeResult(order, drink, drink.CraftGrade.Value,
                order.CocktailId == drink.CocktailId, null);
        public void Apply(ServeResult result) => Applied++;
    }
}

public class TestOrderController : OrderRequestController
{
    public IServeProcessor Service;
    protected override IServeProcessor Processor => Service;
}
