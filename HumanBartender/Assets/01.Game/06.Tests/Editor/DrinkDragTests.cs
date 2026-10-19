using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DrinkDragTests
{
    [TestCase("rejected")]
    [TestCase("served")]
    [TestCase("discarded")]
    [TestCase("disabled")]
    public void OnlyCocktailImageFollowsPointerAndEveryDragExitCleansItUp(string outcome)
    {
        var canvasObject = new GameObject("Serving test canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var tray = new GameObject("Tray", typeof(RectTransform));
        tray.transform.SetParent(canvas.transform, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/03.Serving/ServedDrink.prefab");
        Assert.That(prefab, Is.Not.Null);
        var card = Object.Instantiate(prefab, tray.transform);
        var item = card.GetComponent<DrinkDragItem>();
        var session = new CraftSession("gin_tonic");
        session.BeginGimmicks();
        session.Complete();
        int returned = 0;
        try
        {
            item.Initialize(new CraftedDrink(session, null,
                new NewCocktailData { Id = "gin_tonic", Name = new() { Ko = "진토닉" } }),
                canvas, _ => returned++);
            Vector3 cardPosition = card.transform.localPosition;
            var pointer = new PointerEventData(null) { position = new Vector2(240f, 160f) };
            item.OnBeginDrag(pointer);
            Transform held = canvas.transform.Find("Held Cocktail");
            Assert.That(held, Is.Not.Null);
            Assert.That(held.GetComponent<Image>().sprite, Is.Not.Null, "Use the table cocktail artwork.");
            Assert.That(held.GetComponentsInChildren<TMPro.TMP_Text>(), Is.Empty, "No card labels follow the pointer.");
            Assert.That(held.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(card.transform.parent, Is.SameAs(tray.transform));
            Assert.That(card.transform.localPosition, Is.EqualTo(cardPosition), "The card keeps its layout slot.");
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.Zero);

            pointer.position = new Vector2(320f, 280f);
            item.OnDrag(pointer);
            Assert.That(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, held.position),
                pointer.position), Is.LessThan(0.01f), "The cocktail image is centered on the pointer.");

            if (outcome == "served") item.MarkServed();
            else if (outcome == "discarded") item.MarkDiscarded();
            else if (outcome == "disabled")
            {
                card.SetActive(false);
                typeof(DrinkDragItem).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(item, null);
            }
            item.OnEndDrag(pointer);
            item.OnEndDrag(pointer);
            Assert.That(canvas.transform.Find("Held Cocktail"), Is.Null, "No detached image survives the drag.");
            Assert.That(card.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(card.transform.parent, Is.SameAs(tray.transform));
            Assert.That(card.transform.localPosition, Is.EqualTo(cardPosition));
            Assert.That(returned, Is.EqualTo(outcome == "served" || outcome == "discarded" ? 1 : 0));
        }
        finally { Object.DestroyImmediate(canvasObject); }
    }

    [Test]
    public void DiscardReturnsOnceAfterDragAndRebindPreservesPreviousRecord()
    {
        var canvas = new GameObject("Canvas", typeof(Canvas));
        var zone = new GameObject("Discard", typeof(DrinkDiscardDropZone));
        var events = new GameObject("Events", typeof(EventSystem));
        var go = new GameObject("Drink", typeof(RectTransform), typeof(DrinkDragItem));
        go.transform.SetParent(canvas.transform);
        try
        {
            var item = go.GetComponent<DrinkDragItem>();
            var session = new CraftSession("first");
            session.BeginGimmicks();
            session.Complete();
            var drink = new CraftedDrink(session, null,
                new NewCocktailData { Id = "first", Name = new() { Ko = "First" } });
            int removed = 0, returned = 0, served = 0;
            item.Initialize(drink, canvas.GetComponent<Canvas>(), _ => returned++);
            item.Removed += (_, reason) => { Assert.AreEqual(DrinkRemovalReason.Discarded, reason); removed++; };
            item.Served += _ => served++;
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerDrag = go };
            item.OnBeginDrag(pointer);
            zone.GetComponent<DrinkDiscardDropZone>().OnDrop(pointer);
            zone.GetComponent<DrinkDiscardDropZone>().OnDrop(pointer);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(0, returned);
            Assert.AreEqual(0, served);
            Assert.AreEqual(ECraftPhase.Discarded, session.Phase);
            item.OnEndDrag(pointer);
            item.OnEndDrag(pointer);
            Assert.AreEqual(1, returned);
            Assert.IsNull(item.Drink);

            var next = new CraftSession("second");
            item.Initialize(new CraftedDrink(next, null,
                new NewCocktailData { Id = "second", Name = new() { Ko = "Second" } }),
                canvas.GetComponent<Canvas>(), _ => returned++);
            item.OnBeginDrag(pointer);
            item.MarkServed();
            item.OnEndDrag(pointer);
            Assert.AreEqual(2, returned);
            Assert.AreEqual(1, removed, "Previous subscriptions must be cleared");
            Assert.AreEqual(ECraftPhase.Discarded, session.Phase);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(zone);
            Object.DestroyImmediate(events);
            Object.DestroyImmediate(canvas);
        }
    }

    [Test]
    public void RejectedDropReturnsToTrayAndDisableFinishesConsumedDrag()
    {
        var canvas = new GameObject("Canvas", typeof(Canvas));
        var tray = new GameObject("Tray", typeof(RectTransform));
        tray.transform.SetParent(canvas.transform);
        var go = new GameObject("Drink", typeof(RectTransform), typeof(DrinkDragItem));
        go.transform.SetParent(tray.transform);
        try
        {
            var item = go.GetComponent<DrinkDragItem>();
            int returned = 0;
            item.Initialize(new CraftedDrink(new CraftSession("x"), null,
                new NewCocktailData { Id = "x", Name = new() { Ko = "X" } }),
                canvas.GetComponent<Canvas>(), _ => returned++);
            item.OnBeginDrag(null);
            item.OnEndDrag(null);
            Assert.AreSame(tray.transform, go.transform.parent);
            Assert.AreEqual(0, returned);
            Assert.IsTrue(go.GetComponent<CanvasGroup>().blocksRaycasts);
            item.OnBeginDrag(null);
            item.MarkServed();
            go.SetActive(false);
            // EditMode에서는 비활성 오브젝트에 SendMessage를 보낼 수 없으므로 수명 콜백을 직접 확인한다.
            typeof(DrinkDragItem).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(item, null);
            item.OnEndDrag(null);
            Assert.AreEqual(1, returned);
        }
        finally { Object.DestroyImmediate(canvas); }
    }
}
