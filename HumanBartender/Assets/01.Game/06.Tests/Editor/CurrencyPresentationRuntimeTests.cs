using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class CurrencyPresentationRuntimeTests
{
    readonly List<Object> owned = new();
    SceneSetup[] scenes;
    float previousTimeScale;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        scenes = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        previousTimeScale = Time.timeScale;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = previousTimeScale;
        for (int index = owned.Count - 1; index >= 0; index--)
            if (owned[index] != null) Object.Destroy(owned[index]);
        owned.Clear();
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (scenes != null && System.Array.Exists(scenes, scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(scenes);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator BalanceCountsAndCompletionWaitsForNoticeFadeEvenWhileGameTimeIsPaused()
    {
        var panel = MakePanel(out var amount, out var delta, out var notice);
        var player = MakePlayer(panel, 1000);
        Time.timeScale = 0f;
        player.AddMoney(-400);
        Assert.That(player.HasMoney(), Is.EqualTo(600), "gameplay must receive the final balance immediately");
        Assert.That(panel.CurrentAmount, Is.EqualTo(1000), "only the displayed balance should count down");
        Assert.That(ReadAmount(delta), Is.EqualTo(-400));
        Assert.That(notice.gameObject.activeSelf, Is.True);
        bool done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();

        float startedAt = Time.realtimeSinceStartup;
        while (panel.CurrentAmount == 1000 && Time.realtimeSinceStartup < startedAt + .65f) yield return null;
        Assert.That(panel.CurrentAmount, Is.InRange(601, 999), "the number must pass through an intermediate balance");
        Assert.That(ReadAmount(amount), Is.EqualTo(panel.CurrentAmount));

        while (panel.CurrentAmount != 600 && Time.realtimeSinceStartup < startedAt + 1.4f) yield return null;
        Assert.That(panel.CurrentAmount, Is.EqualTo(600), "the count must reach the exact target while game time is paused");
        Assert.That(ReadAmount(amount), Is.EqualTo(600));
        Assert.That(panel.IsAnimating, Is.True, "the notice remains visible after the count completes");
        Assert.That(notice.alpha, Is.EqualTo(1f).Within(.01f));
        Assert.That(done, Is.False);

        while (notice.alpha >= .999f && panel.IsAnimating && Time.realtimeSinceStartup < startedAt + 3.5f) yield return null;
        Assert.That(notice.alpha, Is.GreaterThan(0f).And.LessThan(1f), "the notice must fade before it is hidden");
        Assert.That(done, Is.False, "the next transition must wait for the notice fade");
        while (!done && Time.realtimeSinceStartup < startedAt + 4f) yield return null;
        Assert.That(done, Is.True);
        Assert.That(panel.IsAnimating, Is.False);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(panel.CurrentAmount, Is.EqualTo(600));
        Assert.That(amount.rectTransform.localScale, Is.EqualTo(Vector3.one));
    }

    [UnityTest]
    public IEnumerator ConsecutiveDeductionsAggregateTheNoticeAndReachTheLatestBalance()
    {
        var panel = MakePanel(out var amount, out var delta, out _);
        var player = MakePlayer(panel, 1000);
        player.AddMoney(-100);
        float deadline = Time.realtimeSinceStartup + .65f;
        while (panel.CurrentAmount == 1000 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(panel.CurrentAmount, Is.InRange(901, 999));

        player.AddMoney(-200);
        Assert.That(player.HasMoney(), Is.EqualTo(700));
        Assert.That(ReadAmount(delta), Is.EqualTo(-300), "same-direction changes should share one accumulated notice");
        Assert.That(panel.IsAnimating, Is.True);
        deadline = Time.realtimeSinceStartup + 1.4f;
        while (panel.CurrentAmount != 700 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(panel.CurrentAmount, Is.EqualTo(700));
        Assert.That(ReadAmount(amount), Is.EqualTo(700));
    }

    [UnityTest]
    public IEnumerator ReversingDirectionStartsAtThePreviousTargetAndReplacesTheNotice()
    {
        var panel = MakePanel(out var amount, out var delta, out _);
        var player = MakePlayer(panel, 1000);
        player.AddMoney(-100);
        float deadline = Time.realtimeSinceStartup + .65f;
        while (panel.CurrentAmount == 1000 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(panel.CurrentAmount, Is.InRange(901, 999));

        player.AddMoney(50);
        Assert.That(player.HasMoney(), Is.EqualTo(950));
        Assert.That(panel.CurrentAmount, Is.EqualTo(900), "a reversal begins at the prior change's final balance");
        Assert.That(ReadAmount(delta), Is.EqualTo(50), "opposite-direction changes must not cancel the prior notice's amount");
        deadline = Time.realtimeSinceStartup + 1.4f;
        while (panel.CurrentAmount != 950 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(panel.CurrentAmount, Is.EqualTo(950));
        Assert.That(ReadAmount(amount), Is.EqualTo(950));
    }

    [UnityTest]
    public IEnumerator SettingTheBalanceInterruptsTheCountAndHidesTheNotice()
    {
        var panel = MakePanel(out var amount, out _, out var notice);
        var player = MakePlayer(panel, 1000);
        player.AddMoney(-400);
        bool done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        Assert.That(panel.IsAnimating, Is.True);

        player.SetMoney(45);
        panel.SetAmount(45);
        float deadline = Time.realtimeSinceStartup + 1f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True);
        Assert.That(panel.IsAnimating, Is.False);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(panel.CurrentAmount, Is.EqualTo(45));
        Assert.That(ReadAmount(amount), Is.EqualTo(45));
        yield return null;
        Assert.That(panel.CurrentAmount, Is.EqualTo(45), "the interrupted animation must not overwrite the loaded balance");
    }

    [UnityTest]
    public IEnumerator DiscardWaitsForTheSerializedMoneyEventAndNotice()
        => CheckSerializedDiscard(200);

    [UnityTest]
    public IEnumerator EmptyWalletStillPlaysTheDiscardNoticeAndWaits()
        => CheckSerializedDiscard(0);

    [UnityTest]
    public IEnumerator NegativeWalletStillPlaysTheDiscardNoticeAndWaits()
        => CheckSerializedDiscard(-40);

    IEnumerator CheckSerializedDiscard(int initialMoney)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab");
        var source = prefab.GetComponentInChildren<UICashPanel>(true);
        var panel = Object.Instantiate(source);
        owned.Add(panel.gameObject);
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerDataSO>(
            "Assets/01.Game/04.Data/02.RuntimeCaches/Player/PlayerData.asset"));
        try
        {
            Set(panel, "playerData", player);
            player.SetMoney(initialMoney);
            var flow = Make("Serialized discard flow").AddComponent<CraftFlowController>();
            Set(flow, "playerDataWriter", player);
            Set(flow, "currencyPresentation", panel);
            var screen = Make("Serialized discard result").AddComponent<ServeScreen>();
            Set(flow, "serveScreen", screen);
            var session = new CraftSession("gin_tonic");
            session.BeginGimmicks();
            session.Complete();
            var drink = new CraftedDrink(session, null, new NewCocktailData
            {
                Id = "gin_tonic", Price = 80, Color = "#FFFFFF"
            });
            SetProperty(flow, "Current", session);
            SetProperty(flow, "PendingDrink", drink);
            SetProperty(flow, "IsAwaitingServeDecision", true);
            Set(screen, "currentDrink", drink);

            Assert.That(flow.TryDiscardPendingDrink(drink), Is.True);
            Assert.That(player.HasMoney(), Is.EqualTo(initialMoney - 80));
            Assert.That(panel.IsAnimating, Is.True, "the serialized OnAddMoney listener must start the count and notice");
            Assert.That(panel.CurrentAmount, Is.EqualTo(initialMoney));
            Assert.That(screen.IsShowing, Is.True);
            Assert.That(flow.IsResolvingDiscard, Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (flow.IsResolvingDiscard && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.IsResolvingDiscard, Is.False);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(flow.DiscardCompletion.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(panel.CurrentAmount, Is.EqualTo(initialMoney - 80));
        }
        finally
        {
            Object.Destroy(player);
        }
    }

    [UnityTest]
    public IEnumerator DisabledAndDestroyedPanelsReleaseWaitersAndCanRestartWhenEnabled()
    {
        var panel = MakePanel(out _, out _, out _);
        var player = MakePlayer(panel, 1000);
        panel.AddCurrency(0);
        Assert.That(panel.WaitForAnimationsAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        player.AddMoney(-80);
        bool done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        Assert.That(panel.IsAnimating, Is.True);

        panel.gameObject.SetActive(false);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True);
        Assert.That(panel.IsAnimating, Is.False);

        panel.gameObject.SetActive(true);
        Assert.That(panel.CurrentAmount, Is.EqualTo(920));
        player.AddMoney(-20);
        Assert.That(panel.IsAnimating, Is.True);
        done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        deadline = Time.realtimeSinceStartup + 4f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True, "re-enabling must not retain an interrupted notice");
        Assert.That(panel.CurrentAmount, Is.EqualTo(900));

        player.AddMoney(-20);
        done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        Object.Destroy(panel);
        deadline = Time.realtimeSinceStartup + 1f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True, "destroying the panel must release its waiter");
    }

    [UnityTest]
    public IEnumerator MissingAmountLabelCannotBlockTheNextTransition()
    {
        var panel = MakePanel(out _, out _, out var notice);
        var player = MakePlayer(panel, 1000);
        Set(panel, "amountText", null);
        player.AddMoney(-80);
        Assert.That(player.HasMoney(), Is.EqualTo(920));
        Assert.That(panel.CurrentAmount, Is.EqualTo(920));
        Assert.That(panel.IsAnimating, Is.False);
        Assert.That(notice.gameObject.activeSelf, Is.False);
        Assert.That(panel.WaitForAnimationsAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        yield return null;
    }

    UICashPanel MakePanel(out TMP_Text amount, out TMP_Text delta, out CanvasGroup notice)
    {
        var host = Make("Cash presentation", typeof(Canvas));
        amount = Make("Cash amount", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        amount.transform.SetParent(host.transform, false);
        var noticeHost = Make("Cash change notice", typeof(CanvasGroup));
        noticeHost.transform.SetParent(host.transform, false);
        notice = noticeHost.GetComponent<CanvasGroup>();
        delta = Make("Cash change amount", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        delta.transform.SetParent(noticeHost.transform, false);
        var panel = host.AddComponent<UICashPanel>();
        Set(panel, "amountText", amount);
        Set(panel, "deltaText", delta);
        Set(panel, "noticeRoot", noticeHost.GetComponent<RectTransform>());
        Set(panel, "noticeGroup", notice);
        panel.SetAmount(0);
        return panel;
    }

    PlayerDataSO MakePlayer(UICashPanel panel, int amount)
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        owned.Add(player);
        player.SetMoney(amount);
        Set(panel, "playerData", player);
        panel.SetAmount(amount);
        player.MoneyAdded += panel.AddCurrency;
        return player;
    }

    static int ReadAmount(TMP_Text label)
    {
        var digits = new System.Text.StringBuilder();
        foreach (char character in label.text)
            if (char.IsDigit(character) || character == '+' || character == '-') digits.Append(character);
            else if (character == '\u2212') digits.Append('-');
        return int.Parse(digits.ToString(), CultureInfo.InvariantCulture);
    }

    GameObject Make(string name, params System.Type[] components)
    {
        var host = new GameObject(name, typeof(RectTransform));
        foreach (var component in components) host.AddComponent(component);
        owned.Add(host);
        return host;
    }

    static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void SetProperty(object target, string name, object value) => target.GetType()
        .GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });
}
