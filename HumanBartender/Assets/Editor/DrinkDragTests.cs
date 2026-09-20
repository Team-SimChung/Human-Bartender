using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

public class DrinkDragTests
{
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
            go.SendMessage("OnDisable"); // EditMode에서도 수명 종료 경로를 검증한다.
            item.OnEndDrag(null);
            Assert.AreEqual(1, returned);
        }
        finally { Object.DestroyImmediate(canvas); }
    }
}
