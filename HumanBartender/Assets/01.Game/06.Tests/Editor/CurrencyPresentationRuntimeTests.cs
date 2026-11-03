using System.Collections;
using System.Collections.Generic;
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
    readonly List<GameObject> owned = new();
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
        foreach (var host in owned) if (host != null) Object.Destroy(host);
        owned.Clear();
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (scenes != null && System.Array.Exists(scenes, scene => scene.isLoaded && scene.isActive))
            EditorSceneManager.RestoreSceneManagerSetup(scenes);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator CompletionWaitsForFadeAndPunchEvenWhileGameTimeIsPaused()
    {
        var panel = MakePanel(out var amount, out var layer, out var template);
        Time.timeScale = 0f;
        panel.AddCurrency(-80);
        bool done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        CurrencyGainText popup = null;
        foreach (var candidate in layer.GetComponentsInChildren<CurrencyGainText>())
            if (candidate != template) popup = candidate;
        Assert.That(popup, Is.Not.Null);

        float deadline = Time.realtimeSinceStartup + 5f;
        while (amount.localScale == Vector3.one && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(amount.localScale, Is.Not.EqualTo(Vector3.one), "arrival must start the punch");
        Assert.That(done, Is.False, "arrival is not the end of the popup");
        while (popup != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(popup == null, Is.True, "fade must finish using unscaled time");
        Assert.That(done, Is.False, "the longer punch must still be awaited");
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True);
        Assert.That(panel.IsAnimating, Is.False);
        Assert.That(amount.localScale, Is.EqualTo(Vector3.one));
    }

    [UnityTest]
    public IEnumerator DiscardWaitsForTheSerializedMoneyEventAndPopup()
        => CheckSerializedDiscard(200);

    [UnityTest]
    public IEnumerator EmptyWalletStillPlaysTheDiscardPopupAndWaits()
        => CheckSerializedDiscard(0);

    [UnityTest]
    public IEnumerator NegativeWalletStillPlaysTheDiscardPopupAndWaits()
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
            Assert.That(panel.IsAnimating, Is.True, "the serialized OnAddMoney listener must start the popup");
            Assert.That(screen.IsShowing, Is.True);
            Assert.That(flow.IsResolvingDiscard, Is.True);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (flow.IsResolvingDiscard && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.IsResolvingDiscard, Is.False);
            Assert.That(screen.IsShowing, Is.False);
            Assert.That(flow.DiscardCompletion.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }
        finally
        {
            Object.Destroy(player);
        }
    }

    [UnityTest]
    public IEnumerator MissingAnimationAndDisabledPanelCannotBlockTheNextTransition()
    {
        var panel = MakePanel(out _, out _, out _);
        panel.AddCurrency(0);
        Assert.That(panel.WaitForAnimationsAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        panel.AddCurrency(-80);
        bool done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        Assert.That(panel.IsAnimating, Is.True);

        panel.gameObject.SetActive(false);
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True);
        Assert.That(panel.IsAnimating, Is.False);

        panel.gameObject.SetActive(true);
        panel.AddCurrency(-20);
        Assert.That(panel.IsAnimating, Is.True);
        done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True, "re-enabling must not retain an interrupted popup");

        panel.AddCurrency(-20);
        done = false;
        panel.WaitForAnimationsAsync().ContinueWith(() => done = true).Forget();
        Object.Destroy(panel);
        while (!done && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(done, Is.True, "destroying the panel must release its waiter");
    }

    UICashPanel MakePanel(out RectTransform amount, out RectTransform layer, out CurrencyGainText template)
    {
        var host = Make("Cash presentation", typeof(Canvas));
        layer = (RectTransform)host.transform;
        var label = Make("Cash amount", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(host.transform, false);
        amount = label.rectTransform;
        var popupHost = Make("Popup template", typeof(CanvasGroup), typeof(CurrencyGainText));
        popupHost.transform.SetParent(host.transform, false);
        template = popupHost.GetComponent<CurrencyGainText>();
        var popupLabel = Make("Popup label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        popupLabel.transform.SetParent(popupHost.transform, false);
        Set(template, "label", popupLabel);
        Set(template, "travelDuration", .08f);
        Set(template, "mergeDuration", .18f);
        var panel = host.AddComponent<UICashPanel>();
        Set(panel, "amountText", label);
        Set(panel, "mergeTarget", amount);
        Set(panel, "gainLayer", layer);
        Set(panel, "gainPrefab", template);
        Set(panel, "punchTime", .8f);
        return panel;
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
