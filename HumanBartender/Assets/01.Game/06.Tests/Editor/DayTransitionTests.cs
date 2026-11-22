using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DayTransitionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

    sealed class Fixture : IDisposable
    {
        public readonly GameObject host;
        public readonly DayTransitionView view;
        public readonly GameObject root;
        public readonly Image shade;
        public readonly TMP_Text quote, author, oldDigit, newDigit;
        public readonly CanvasGroup title;

        public Fixture()
        {
            host = new GameObject("DayTransitionTest", typeof(RectTransform));
            host.SetActive(false);
            view = host.AddComponent<DayTransitionView>();
            root = Child("Root", host.transform);
            shade = Child("Shade", root.transform).AddComponent<Image>();
            quote = Text("Quote", root.transform);
            author = Text("Author", root.transform);
            title = Child("Title", root.transform).AddComponent<CanvasGroup>();
            var mask = Child("Mask", title.transform);
            ((RectTransform)mask.transform).sizeDelta = new Vector2(61, 55);
            oldDigit = Text("Old", mask.transform);
            newDigit = Text("New", mask.transform);
            foreach (var digit in new[] { oldDigit, newDigit }) digit.rectTransform.sizeDelta = new Vector2(61, 55);
            Set(view, "root", root);
            Set(view, "shade", shade);
            Set(view, "quoteText", quote);
            Set(view, "authorText", author);
            Set(view, "dayTitle", title);
            Set(view, "oldDigit", oldDigit);
            Set(view, "newDigit", newDigit);
            host.SetActive(true);
        }

        public void Dispose() => Object.DestroyImmediate(host);
        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }
        static TMP_Text Text(string name, Transform parent) => Child(name, parent).AddComponent<TextMeshProUGUI>();
    }

    [TestCase(0, 1, "살아갈 이유가 있는 사람은", "『우상의 황혼』")]
    [TestCase(1, 2, "수동적인 감정은", "『에티카』")]
    [TestCase(2, 3, "모든 인간은 예나 지금이나", "『인간적인 너무나 인간적인』")]
    public void EachWebDayChangeShowsItsQuoteAndAuthor(int from, int to, string quoteStart, string book)
    {
        using var f = new Fixture();
        var plan = f.view.Plan(from, to);
        f.view.PreviewAt(from, to, plan.AuthorAt + 1f);
        Assert.That(f.root.activeSelf, Is.True);
        Assert.That(f.quote.text, Does.StartWith("\"" + quoteStart));
        Assert.That(f.author.text, Does.Contain(book));
        Assert.That(f.quote.color.a, Is.EqualTo(1f).Within(1e-4));
        Assert.That(f.oldDigit.text, Is.EqualTo(from.ToString()));
        Assert.That(f.newDigit.text, Is.EqualTo(to.ToString()));
    }

    [Test]
    public void ScheduleFollowsTheWebTimelineWithAGapBeforeTheDayTitle()
    {
        using var f = new Fixture();
        var plan = f.view.Plan(0, 1);
        Assert.That(plan.HasQuote, Is.True);
        Assert.That(plan.QuoteAt, Is.EqualTo(2.15f).Within(1e-4));
        Assert.That(plan.AuthorAt, Is.EqualTo(2.45f).Within(1e-4));
        Assert.That(plan.QuoteOutAt, Is.EqualTo(7.15f).Within(1e-4), "웹과 같은 시각에 명언이 사라지기 시작한다.");
        // 명언이 0.85초 동안 다 사라진 뒤 0.6초 비우고 Day가 나온다(웹은 겹침).
        Assert.That(plan.DayAt, Is.EqualTo(8.60f).Within(1e-4));
        Assert.That(plan.RollAt, Is.EqualTo(9.45f).Within(1e-4));
        Assert.That(plan.PrepareAt, Is.EqualTo(10.15f).Within(1e-4));
        Assert.That(plan.TitleOutAt, Is.EqualTo(10.90f).Within(1e-4));
        Assert.That(plan.RevealAt, Is.EqualTo(11.30f).Within(1e-4));
    }

    [Test]
    public void ScreenStaysBlankBetweenTheQuoteAndTheDayTitle()
    {
        using var f = new Fixture();
        var plan = f.view.Plan(1, 2);
        float quoteGone = plan.QuoteOutAt + .85f;
        foreach (float t in new[] { quoteGone, (quoteGone + plan.DayAt) / 2f, plan.DayAt })
        {
            f.view.PreviewAt(1, 2, t);
            Assert.That(f.quote.color.a, Is.EqualTo(0f).Within(1e-4), "t=" + t);
            Assert.That(f.author.color.a, Is.EqualTo(0f).Within(1e-4), "t=" + t);
            Assert.That(f.title.alpha, Is.EqualTo(0f).Within(1e-4), "t=" + t);
        }
        f.view.PreviewAt(1, 2, plan.DayAt + .3f);
        Assert.That(f.title.alpha, Is.EqualTo(1f).Within(1e-4));
    }

    [Test]
    public void DayChangeWithoutAQuoteOnlyRollsTheNumber()
    {
        using var f = new Fixture();
        var plan = f.view.Plan(3, 4);
        Assert.That(plan.HasQuote, Is.False);
        Assert.That(plan.DayAt, Is.EqualTo(.7f).Within(1e-4));
        f.view.PreviewAt(3, 4, plan.DayAt + .3f);
        Assert.That(f.quote.gameObject.activeSelf, Is.False);
        Assert.That(f.title.alpha, Is.EqualTo(1f).Within(1e-4));
    }

    [Test]
    public void NumberRollsAfterTheQuoteFadesAndTheShadeLiftsOnReveal()
    {
        using var f = new Fixture();
        var plan = f.view.Plan(1, 2);
        f.view.PreviewAt(1, 2, plan.RollAt);
        Assert.That(f.quote.color.a, Is.EqualTo(0f).Within(1e-4), "숫자는 명언이 완전히 사라진 뒤 굴러간다.");
        Assert.That(f.oldDigit.color.a, Is.EqualTo(1f).Within(1e-4));
        f.view.PreviewAt(1, 2, plan.RollAt + 1f);
        Assert.That(f.newDigit.color.a, Is.EqualTo(1f).Within(1e-4));
        Assert.That(f.oldDigit.color.a, Is.EqualTo(0f).Within(1e-4));
        Assert.That(f.shade.color.a, Is.EqualTo(1f).Within(1e-4));
        f.view.PreviewAt(1, 2, plan.RevealAt, 1f);
        Assert.That(f.shade.color.a, Is.EqualTo(0f).Within(1e-4));
        Assert.That(f.title.alpha, Is.EqualTo(0f).Within(1e-4));
    }

    [Test]
    public void HideImmediateTurnsTheRootOff()
    {
        using var f = new Fixture();
        f.view.PreviewAt(0, 1, 3f);
        f.view.HideImmediate();
        Assert.That(f.root.activeSelf, Is.False);
        Assert.That(f.view.IsShowing, Is.False);
    }

    [Test]
    public void ProjectLifeScopeHasTheDayTransitionCanvas()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DayTransitionSetup.PrefabPath);
        var canvas = prefab.transform.Find(DayTransitionSetup.CanvasName);
        Assert.That(canvas, Is.Not.Null, "Setup Day Transition 메뉴를 실행해 주세요.");
        var view = canvas.GetComponent<DayTransitionView>();
        Assert.DoesNotThrow(view.ValidateReferences);
        var unityCanvas = canvas.GetComponent<Canvas>();
        var scaler = canvas.GetComponent<CanvasScaler>();
        Assert.That(unityCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(unityCanvas.sortingOrder, Is.EqualTo(DayTransitionSetup.SortOrder));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)));
        Assert.That(scaler.matchWidthOrHeight, Is.Zero);
        var manager = prefab.GetComponentInChildren<SceneTransitionManager>(true);
        Assert.That(((IDayTransitionSource)manager).DayTransition, Is.SameAs(view));
        var root = (GameObject)typeof(DayTransitionView).GetField("root", Private).GetValue(view);
        Assert.That(root.activeSelf, Is.False);
        var shade = (Image)typeof(DayTransitionView).GetField("shade", Private).GetValue(view);
        Assert.That(shade.raycastTarget, Is.True, "전환 중 아래 입력을 막는다.");
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(!text.raycastTarget && !text.enableAutoSizing && !text.richText, Is.True, text.name);
        foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect == canvas) continue;
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one), rect.name);
            Assert.That(rect.name.Contains(' '), Is.False, rect.name);
        }
    }
}
