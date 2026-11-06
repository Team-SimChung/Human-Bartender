using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class CraftGuestClockTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    readonly List<bool> activeChanges = new();
    CraftFlowController flow;
    GuestManager guests;
    BarOperationClock clock;
    ServeScreen screen;
    Button provide;
    bool guestsConnected;

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [SetUp]
    public void SetUp()
    {
        flow = Own(new GameObject("Craft clock flow")).AddComponent<CraftFlowController>();
        var data = Own(ScriptableObject.CreateInstance<NewCocktailDataSO>());
        data.cocktailData = new[] { new NewCocktailData
        {
            Id = "clock_test", Status = ENewDataStatus.Confirmed, UnlockDay = int.MinValue,
            Glass = "test_glass", Recipe = Array.Empty<NewCocktailRecipeStep>()
        } };
        Set(flow, "cocktailData", data);
        flow.CraftFlowActiveChanged += activeChanges.Add;

        var guestHost = Own(new GameObject("Guest clock observer"));
        guestHost.SetActive(false);
        guests = guestHost.AddComponent<GuestManager>();
        Set(guests, "slots", Array.Empty<GuestSlot>());
        Set(guests, "craftFlow", flow);
        clock = (BarOperationClock)typeof(GuestManager).GetField("barClock", Private).GetValue(guests);

        var screenHost = Own(new GameObject("Result clock screen", typeof(Animator)));
        screen = screenHost.AddComponent<ServeScreen>();
        Set(screen, "animator", screenHost.GetComponent<Animator>());
        Set(screen, "craftFlow", flow);
        Set(screen, "retryOnDiscard", false);
        provide = Own(new GameObject("Provide", typeof(RectTransform))).AddComponent<Button>();
        Set(screen, "Accept", provide);
        var clip = Own(new AnimationClip { name = "Clock test result" });
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        var field = typeof(ServeScreen).GetField("_animations", Private);
        var animations = (IList)Activator.CreateInstance(field.FieldType);
        var entry = Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]);
        entry.GetType().GetField("id").SetValue(entry, "clock_test");
        entry.GetType().GetField("clip").SetValue(entry, clip);
        animations.Add(entry);
        field.SetValue(screen, animations);
        Set(flow, "serveScreen", screen);
    }

    void ConnectGuests()
    {
        Call(guests, "OnEnable");
        guestsConnected = true;
        // 주문 생성은 이 테스트의 범위 밖이다. 실제 시계 구독은 유지하고 시작 제한만 제거한다.
        var blocker = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), guests,
            typeof(GuestManager).GetMethod("DescribeNoOrderBlock", Private));
        flow.RemoveCraftBlocker(blocker);
    }

    void Begin()
    {
        Assert.That(flow.TryBegin("clock_test", out _, out string reason), Is.True, reason);
    }

    CraftedDrink Complete(bool resultScreen = true)
    {
        var session = flow.Current;
        session.Actual.SetGlass("test_glass");
        session.Actual.SelectIngredient("test_ingredient");
        if (session.Phase == ECraftPhase.Preparing)
        {
            session.BeginGimmicks();
            session.Timer.Tick(12f);
        }
        session.Complete();
        SetProperty(flow, "Judgement", new CraftJudgement { CraftScore = 100f, CraftGrade = ENewGrade.Excellent });
        if (!resultScreen) Set(flow, "serveScreen", null);
        Call(flow, "Finish", session);
        if (resultScreen) screen.Apply();
        return flow.PendingDrink;
    }

    [TearDown]
    public void TearDown()
    {
        if (guestsConnected) Call(guests, "OnDisable");
        screen.Reset();
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); activeChanges.Clear(); guestsConnected = false;
    }

    [Test]
    public void GuestClockStaysPausedFromSelectionThroughResultUntilProvideButton()
    {
        ConnectGuests();
        clock.Tick(5f);
        Begin();
        Assert.That(flow.IsPreparing && clock.IsPaused, Is.True);
        clock.Tick(30f);
        flow.Current.BeginGimmicks();
        flow.Current.Timer.Tick(12f);
        clock.Tick(40f);
        Assert.That(flow.Current.Timer.ElapsedSec, Is.EqualTo(12f), "제조 시계는 별도로 진행한다.");
        Complete();
        Assert.That(flow.IsAwaitingServeDecision && flow.IsCraftFlowActive && screen.IsShowing, Is.True);
        clock.Tick(60f);
        Assert.That(clock.ElapsedSec, Is.EqualTo(5f));
        provide.onClick.Invoke();
        Assert.That(flow.IsAwaitingServeDecision || flow.IsCraftFlowActive || screen.IsShowing || clock.IsPaused, Is.False);
        clock.Tick(3f);
        Assert.That(clock.ElapsedSec, Is.EqualTo(8f), "멈춘 130초를 나중에 더하지 않는다.");
        Assert.That(activeChanges, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void GuestManagerEnabledDuringPreparationImmediatelyPausesItsClock()
    {
        clock.Tick(2f);
        Begin();
        ConnectGuests();
        Assert.That(clock.IsPaused, Is.True);
        clock.Tick(30f);
        Assert.That(clock.ElapsedSec, Is.EqualTo(2f));
    }

    [Test]
    public void CancelingSelectionResumesGuestClock()
    {
        ConnectGuests(); Begin();
        flow.CancelCurrentCraft();
        Assert.That(flow.Current.Phase, Is.EqualTo(ECraftPhase.Cancelled));
        Assert.That(flow.IsCraftFlowActive || clock.IsPaused, Is.False);
        clock.Tick(3f);
        Assert.That(clock.ElapsedSec, Is.EqualTo(3f));
    }

    [Test]
    public void RejectedProvideDoesNotResumeClock()
    {
        ConnectGuests(); Begin(); Complete();
        Assert.That(flow.TryAcceptPendingDrink(null), Is.False);
        Assert.That(flow.IsAwaitingServeDecision && flow.IsCraftFlowActive && clock.IsPaused, Is.True);
        clock.Tick(20f);
        Assert.That(clock.ElapsedSec, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DiscardAnimationKeepsPauseAndRetryDoesNotBrieflyResume(bool retry)
    {
        ConnectGuests(); Begin();
        var drink = Complete();
        var currency = new DelayedCurrency();
        Set(flow, "currencyPresentation", currency);
        bool retryStartedPaused = false;
        if (retry) flow.CraftBegan += _ =>
        {
            retryStartedPaused = clock.IsPaused;
            flow.CancelCurrentCraft();
        };
        Assert.That(flow.TryDiscardPendingDrink(drink, retry), Is.True);
        Assert.That(flow.IsResolvingDiscard && flow.IsCraftFlowActive && clock.IsPaused, Is.True);
        clock.Tick(20f);
        Assert.That(clock.ElapsedSec, Is.Zero);
        currency.Complete();
        Assert.That(retryStartedPaused, Is.EqualTo(retry));
        Assert.That(flow.IsResolvingDiscard || flow.IsCraftFlowActive || clock.IsPaused, Is.False);
        Assert.That(activeChanges, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void DisablingWhileResultIsOpenReleasesPause()
    {
        ConnectGuests(); Begin(); Complete();
        flow.enabled = false;
        Call(flow, "OnDisable");
        Assert.That(flow.IsAwaitingServeDecision || flow.IsCraftFlowActive || clock.IsPaused, Is.False);
    }

    [Test]
    public void CompletingWithoutResultScreenDoesNotLeaveClockPaused()
    {
        ConnectGuests(); Begin(); Complete(resultScreen: false);
        Assert.That(flow.IsAwaitingServeDecision || flow.IsCraftFlowActive || clock.IsPaused, Is.False);
    }

    sealed class DelayedCurrency : ICurrencyPresentation
    {
        readonly UniTaskCompletionSource completion = new();
        public UniTask WaitForAnimationsAsync(CancellationToken token = default) => completion.Task.AttachExternalCancellation(token);
        public void Complete() => completion.TrySetResult();
    }
}
