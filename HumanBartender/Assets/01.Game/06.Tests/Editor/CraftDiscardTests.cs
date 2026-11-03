using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CraftDiscardTests
{
    readonly List<Object> owned = new();
    readonly List<int> moneyChanges = new();
    CraftFlowController flow;
    PlayerDataSO player;

    [SetUp]
    public void SetUp()
    {
        var host = Own(new GameObject("Craft discard test"));
        flow = host.AddComponent<CraftFlowController>();
        player = Own(ScriptableObject.CreateInstance<PlayerDataSO>());
        player.SetMoney(200);
        player.MoneyAdded += moneyChanges.Add;
        SetField(flow, "playerDataWriter", player);
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = owned.Count - 1; index >= 0; index--)
            if (owned[index] != null) Object.DestroyImmediate(owned[index]);
        owned.Clear();
        moneyChanges.Clear();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ManualDiscardChargesOnceAndRejectsRepeatedOrReentrantRequests(bool awaitingDecision)
    {
        var drink = PrepareDrink(awaitingDecision);
        int notifications = 0;
        bool acceptedAgain = true;
        flow.DrinkDiscarded += discarded =>
        {
            notifications++;
            acceptedAgain = flow.TryDiscardDrink(discarded);
        };

        bool accepted = awaitingDecision ? flow.TryDiscardPendingDrink(drink) : flow.TryDiscardDrink(drink);

        Assert.That(accepted, Is.True);
        Assert.That(acceptedAgain, Is.False);
        Assert.That(flow.TryDiscardDrink(drink), Is.False);
        Assert.That(flow.TryDiscardPendingDrink(drink), Is.False);
        Assert.That(drink.Session.Phase, Is.EqualTo(ECraftPhase.Discarded));
        Assert.That(flow.PendingDrink, Is.Null);
        Assert.That(flow.IsAwaitingServeDecision, Is.False);
        Assert.That(player.HasMoney(), Is.EqualTo(120));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
        Assert.That(notifications, Is.EqualTo(1));
    }

    [Test]
    public void DisablingWhileAwaitingDecisionClearsDrinkWithoutCharging()
    {
        var drink = PrepareDrink(true);
        flow.enabled = false;
        // Edit-mode tests invoke the lifecycle method explicitly as well.
        typeof(CraftFlowController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(flow, null);

        Assert.That(drink.Session.Phase, Is.EqualTo(ECraftPhase.Discarded));
        Assert.That(flow.PendingDrink, Is.Null);
        Assert.That(flow.IsAwaitingServeDecision, Is.False);
        Assert.That(player.HasMoney(), Is.EqualTo(200));
        Assert.That(moneyChanges, Is.Empty);
    }

    [TestCase(30, -50)]
    [TestCase(0, -80)]
    [TestCase(-40, -120)]
    public void ManualDiscardChargesTheFullPriceEvenWithoutEnoughMoney(int initialMoney, int expectedMoney)
    {
        var drink = PrepareDrink(true);
        player.SetMoney(initialMoney);

        Assert.That(flow.TryDiscardPendingDrink(drink), Is.True);
        Assert.That(player.HasMoney(), Is.EqualTo(expectedMoney));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
    }

    [Test]
    public void NegativeBalanceSurvivesSettingFurtherChangesAndProgressRestore()
    {
        player.SetMoney(-50);
        player.AddMoney(-80);
        Assert.That(player.HasMoney(), Is.EqualTo(-130));
        player.AddMoney(100);
        Assert.That(player.HasMoney(), Is.EqualTo(-30));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80, 100 }));

        player.ReplaceProgress(-130, 0, player.ReadAffinity(), player.ReadFlags());
        Assert.That(player.HasMoney(), Is.EqualTo(-130));
    }

    [Test]
    public void SavedNegativeBalanceIsRecognizedAsLoadable()
    {
        var daysField = typeof(NewDataLoadManager).GetField("loadedDays", BindingFlags.Static | BindingFlags.NonPublic);
        var loadedProperty = typeof(NewDataLoadManager).GetProperty("IsLoaded");
        var previousDays = daysField.GetValue(null);
        bool previousLoaded = NewDataLoadManager.IsLoaded;
        string directory = Path.Combine(Path.GetTempPath(), "HumanBartender-NegativeMoney-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "slot-1.json");
        try
        {
            daysField.SetValue(null, new[] { new NewDayInfoData { Day = 0 } });
            loadedProperty.GetSetMethod(true).Invoke(null, new object[] { true });
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonConvert.SerializeObject(new
            {
                save_version = 1, saved_at = DateTime.UtcNow.ToString("O"), current_day = 0,
                money = -80, skill_amount = 0,
                affinity = new Dictionary<string, int>(), flags = new Dictionary<string, bool>()
            }));
            var saves = (SaveManager)Activator.CreateInstance(typeof(SaveManager),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { player, null, GameStateManager.Instance, directory }, null);

            Assert.That(saves.TryDescribeSlot(1, out string description, out bool canLoad), Is.True);
            Assert.That(canLoad, Is.True, description);
        }
        finally
        {
            daysField.SetValue(null, previousDays);
            loadedProperty.GetSetMethod(true).Invoke(null, new object[] { previousLoaded });
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [Test]
    public void DropZoneUsesManualDiscardAndPooledCleanupDoesNotChargeAgain()
    {
        var drink = PrepareDrink(false);
        var host = Own(new GameObject("Discarded drink", typeof(RectTransform), typeof(CanvasGroup)));
        var item = host.AddComponent<DrinkDragItem>();
        int releases = 0;
        item.Initialize(drink, null, _ => releases++, flow.TryDiscardDrink);
        flow.DrinkDiscarded += _ => item.MarkDiscarded();
        var zone = Own(new GameObject("Discard drop zone")).AddComponent<DrinkDiscardDropZone>();
        var pointer = new PointerEventData(null) { pointerDrag = host };

        zone.OnDrop(pointer);
        zone.OnDrop(pointer);
        item.MarkDiscarded();

        Assert.That(player.HasMoney(), Is.EqualTo(120));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
        Assert.That(releases, Is.EqualTo(1));
        Assert.That(item.Drink, Is.Null);

        var next = PrepareDrink(false);
        item.Initialize(next, null, _ => releases++, flow.TryDiscardDrink);
        item.MarkDiscarded();
        Assert.That(player.HasMoney(), Is.EqualTo(120), "UI cleanup must not request payment");
        Assert.That(releases, Is.EqualTo(2));
    }

    [Test]
    public void RejectedDiscardKeepsTheDisplayedDrink()
    {
        var current = PrepareDrink(false);
        var stale = NewDrink();
        var host = Own(new GameObject("Stale drink", typeof(RectTransform), typeof(CanvasGroup)));
        var item = host.AddComponent<DrinkDragItem>();
        int releases = 0;
        item.Initialize(stale, null, _ => releases++, flow.TryDiscardDrink);

        item.Discard();

        Assert.That(item.Drink, Is.SameAs(stale));
        Assert.That(stale.Session.Phase, Is.EqualTo(ECraftPhase.Completed));
        Assert.That(flow.PendingDrink, Is.SameAs(current));
        Assert.That(releases, Is.Zero);
        Assert.That(moneyChanges, Is.Empty);
    }

    [Test]
    public void AcceptedRetryChargesTheDiscardedDrinkPriceOnce()
    {
        var drink = PrepareDrink(true);
        SetRetryData();
        // Cancel the new attempt at its normal entry event; no minigame is needed here.
        flow.CraftBegan += _ => flow.CancelCurrentCraft();

        Assert.That(flow.TryRetryGimmicks(drink, out var reason), Is.True, reason);
        Assert.That(flow.TryRetryGimmicks(drink, out _), Is.False);
        Assert.That(drink.Session.Phase, Is.EqualTo(ECraftPhase.Discarded));
        Assert.That(player.HasMoney(), Is.EqualTo(120));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
    }

    [Test]
    public void RejectedRetryKeepsOriginalDrinkAndMoney()
    {
        var drink = PrepareDrink(true);
        SetRetryData();
        flow.AddCraftBlocker(() => "retry blocked");

        Assert.That(flow.TryRetryGimmicks(drink, out var reason), Is.False);
        Assert.That(reason, Is.EqualTo("retry blocked"));
        Assert.That(flow.PendingDrink, Is.SameAs(drink));
        Assert.That(drink.Session.Phase, Is.EqualTo(ECraftPhase.Completed));
        Assert.That(player.HasMoney(), Is.EqualTo(200));
        Assert.That(moneyChanges, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ResultScreenStaysUntilCurrencyAnimationCompletes(bool retryGimmicks)
    {
        var drink = PrepareDrink(true);
        SetRetryData();
        var presentation = new DelayedCurrencyPresentation();
        SetField(flow, "currencyPresentation", presentation);
        var screen = SetResultScreen(drink, out var restart);
        int starts = 0;
        flow.CraftBegan += _ => { starts++; flow.CancelCurrentCraft(); };

        Assert.That(flow.TryDiscardPendingDrink(drink, retryGimmicks), Is.True);
        Assert.That(player.HasMoney(), Is.EqualTo(120), "payment is committed before waiting");
        Assert.That(flow.Current, Is.SameAs(drink.Session), "a new session must not begin early");
        Assert.That(flow.IsResolvingDiscard, Is.True);
        Assert.That(flow.IsBusy, Is.True);
        Assert.That(screen.IsShowing, Is.True);
        Assert.That(restart.interactable, Is.False);
        Assert.That(starts, Is.Zero);
        screen.Discard();
        screen.Provide();
        Assert.That(moneyChanges, Has.Count.EqualTo(1));
        Assert.That(flow.TryBegin("discard_test", out _, out _), Is.False);

        presentation.Complete();

        Assert.That(flow.IsResolvingDiscard, Is.False);
        Assert.That(flow.DiscardCompletion.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        Assert.That(screen.IsShowing, Is.False);
        Assert.That(starts, Is.EqualTo(retryGimmicks ? 1 : 0));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
    }

    [Test]
    public void DisablingDuringAnimationCancelsDeferredRetryWithoutChargingAgain()
    {
        var drink = PrepareDrink(true);
        SetRetryData();
        var presentation = new DelayedCurrencyPresentation();
        SetField(flow, "currencyPresentation", presentation);
        var screen = SetResultScreen(drink, out _);
        int starts = 0;
        flow.CraftBegan += _ => starts++;
        Assert.That(flow.TryRetryGimmicks(drink, out _), Is.True);

        flow.enabled = false;
        typeof(CraftFlowController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(flow, null);
        flow.enabled = true;
        presentation.Complete();

        Assert.That(starts, Is.Zero);
        Assert.That(flow.IsResolvingDiscard, Is.False);
        Assert.That(screen.IsShowing, Is.False);
        Assert.That(flow.DiscardCompletion.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        Assert.That(moneyChanges, Is.EqualTo(new[] { -80 }));
    }

    ServeScreen SetResultScreen(CraftedDrink drink, out Button restart)
    {
        var host = Own(new GameObject("Discard result screen"));
        var screen = host.AddComponent<ServeScreen>();
        restart = Own(new GameObject("Discard button", typeof(RectTransform))).AddComponent<Button>();
        SetField(screen, "Restart", restart);
        SetField(screen, "craftFlow", flow);
        SetField(screen, "currentDrink", drink);
        SetField(flow, "serveScreen", screen);
        screen.Apply();
        return screen;
    }

    sealed class DelayedCurrencyPresentation : ICurrencyPresentation
    {
        readonly UniTaskCompletionSource finished = new();
        public UniTask WaitForAnimationsAsync(CancellationToken cancellationToken = default)
            => finished.Task.AttachExternalCancellation(cancellationToken);
        public void Complete() => finished.TrySetResult();
    }

    CraftedDrink PrepareDrink(bool awaitingDecision)
    {
        var drink = NewDrink();
        SetProperty(flow, "Current", drink.Session);
        SetProperty(flow, "PendingDrink", drink);
        SetProperty(flow, "IsAwaitingServeDecision", awaitingDecision);
        return drink;
    }

    static CraftedDrink NewDrink()
    {
        var session = new CraftSession("discard_test");
        session.Actual.SetGlass("test_glass");
        session.Actual.SelectIngredient("test_ingredient");
        session.BeginGimmicks();
        session.Complete();
        return new CraftedDrink(session, null, new NewCocktailData
        {
            Id = "discard_test", Price = 80, Color = "#FFFFFF"
        });
    }

    void SetRetryData()
    {
        var data = Own(ScriptableObject.CreateInstance<NewCocktailDataSO>());
        data.cocktailData = new[] { new NewCocktailData
        {
            Id = "discard_test", Status = ENewDataStatus.Confirmed, Price = 900,
            UnlockDay = int.MinValue, Recipe = Array.Empty<NewCocktailRecipeStep>()
        } };
        SetField(flow, "cocktailData", data);
    }

    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void SetField(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void SetProperty(object target, string name, object value) => target.GetType()
        .GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
}
