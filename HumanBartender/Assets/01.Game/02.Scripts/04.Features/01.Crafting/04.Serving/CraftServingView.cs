using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>좌석의 월드 서빙 자리와 화면의 공급함을 함께 표시한다. 스토리의 방문은 배우별로 관리한다.</summary>
public class CraftServingView : MonoBehaviour
{
    [SerializeField] DialogueCharacterManager characterManager;
    [SerializeField] Canvas serveCanvas;
    [SerializeField] TableServeSpot spotPrefab;
    [SerializeField] Transform tabletopAnchor;
    [SerializeField] GameObject supplyArrow;
    [SerializeField] PlayCamera playCamera;
    [SerializeField] RecipeBrowserScreen recipes;
    [SerializeField] ServicePanelController servicePanel;
    [SerializeField] CraftFlowController craftFlow;
    [SerializeField] CocktailRecipeVisualCatalog visuals;

    readonly StoryTableState tables = new();
    readonly Dictionary<string, StoryPlace> storyPlaces = new(StringComparer.Ordinal);
    readonly List<TableServeSpot> spots = new();
    int openPresentations;
    bool closeWhenIdle;

    sealed class StoryPlace
    {
        public TableServeSpot Spot;
        public int CoasterWaiters;
        public int OpenOrders;
        public readonly UniTaskCompletionSource Placed = new();
    }

    public StoryTableState TableState => tables;
    public void BeginVisit(string actor) => tables.Enter(actor);

    bool BarVisible => serveCanvas.isActiveAndEnabled && !craftFlow.IsCraftFlowActive &&
        !craftFlow.IsAwaitingServeDecision;
    bool CanInteract() => BarVisible && !playCamera.IsMoving && !recipes.IsOpen &&
        !servicePanel.IsMenuOpen && !UIDisplayOptions.IsOptionOpen;

    TableServeSpot CreateSpot(string guest, float worldX)
    {
        var spot = Instantiate(spotPrefab, tabletopAnchor, false);
        spot.name = $"TableServeSpot ({guest})";
        var position = tabletopAnchor.position;
        position.x = worldX;
        spot.transform.position = position;
        spot.Initialize(Camera.main, visuals, CanInteract);
        spots.Add(spot);
        return spot;
    }

    public TableServeSpot OpenGuestSpot(GuestManager manager, GuestSlot slot)
    {
        var spot = CreateSpot(slot.CurrentGuest.id, slot.transform.position.x);
        bool Focused() => manager.isActiveAndEnabled && slot.SlotType == playCamera.SelectedSeat;
        spot.BindCoaster(() => Focused() && slot.CanReceiveCoaster, () => manager.TryPlaceCoaster(slot));
        spot.BindDrink(() => Focused() && slot.CanReceiveDrink, drink => manager.TryServeDrink(slot, drink));
        return spot;
    }

    public void RemoveSpot(TableServeSpot spot)
    {
        spots.Remove(spot);
        if (spot == null) return;
        spot.gameObject.SetActive(false);
        Destroy(spot.gameObject);
    }

    StoryPlace EnsureStoryPlace(string actor)
    {
        if (storyPlaces.TryGetValue(actor, out var place)) return place;
        var visit = tables.Get(actor);
        var spot = CreateSpot(actor, characterManager.GetCharacterPosition(actor).x);
        place = new StoryPlace { Spot = spot };
        storyPlaces.Add(actor, place);
        spot.BindCoaster(() => !visit.HasCoaster && (place.CoasterWaiters > 0 || place.OpenOrders > 0), () =>
        {
            if (visit.HasCoaster) return;
            tables.PlaceCoaster(actor);
            spot.ShowCoaster();
            place.Placed.TrySetResult();
        });
        serveCanvas.gameObject.SetActive(true);
        return place;
    }

    public async UniTask WaitForCoasterAsync(string actor, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var place = EnsureStoryPlace(actor);
        place.CoasterWaiters++;
        try { await place.Placed.Task.AttachExternalCancellation(token); }
        finally { place.CoasterWaiters--; }
    }

    public void EmptyGlass(string actor)
    {
        tables.EmptyGlass(actor);
        if (tables.Get(actor).GlassEmpty) storyPlaces[actor].Spot.ShowEmptyGlass();
    }

    public void EndVisit(string actor)
    {
        tables.Leave(actor);
        if (storyPlaces.Remove(actor, out var place))
        {
            RemoveSpot(place.Spot);
            place.Placed.TrySetCanceled();
        }
        if (storyPlaces.Count == 0 && openPresentations == 0) CloseUi();
    }

    public void EndVisits()
    {
        foreach (var actor in new List<string>(storyPlaces.Keys)) EndVisit(actor);
        tables.Clear();
    }

    void OnDestroy()
    {
        EndVisits();
        while (spots.Count > 0) RemoveSpot(spots[spots.Count - 1]);
    }

    public IOrderPresentation CreatePresentation(OrderDetails order) => new Presentation(this, order);

    sealed class Presentation : IOrderPresentation
    {
        readonly CraftServingView owner;
        readonly OrderDetails order;
        TableServeSpot spot;
        StoryPlace place;
        bool opened;
        bool received;

        public Presentation(CraftServingView owner, OrderDetails order)
        {
            this.owner = owner;
            this.order = order;
        }

        public void Open(Func<CraftedDrink, bool> receive)
        {
            string actor = order.ReceiverId;
            if (owner.tables.Contains(actor))
            {
                place = owner.EnsureStoryPlace(actor);
                place.OpenOrders++;
                spot = place.Spot;
            }
            else spot = owner.CreateSpot(actor, owner.characterManager.GetCharacterPosition(actor).x);
            spot.BindDrink(() => !received && (place == null || owner.tables.Get(actor).HasCoaster), drink =>
            {
                if (!receive(drink)) return false;
                received = true;
                if (place != null) owner.tables.Serve(actor, drink);
                spot.ShowDrink(drink);
                return true;
            });
            owner.openPresentations++;
            opened = true;
            owner.serveCanvas.gameObject.SetActive(true);
        }

        public void Unbind()
        {
            if (spot == null) return;
            spot.BindDrink(null, null);
            if (place == null) owner.RemoveSpot(spot);
            else place.OpenOrders--;
            spot = null;
        }

        public async UniTask CloseAsync()
        {
            await UniTask.NextFrame();
            if (owner == null || !opened) return;
            opened = false;
            owner.openPresentations--;
            if (owner.openPresentations != 0 || owner.storyPlaces.Count != 0) return;
            if (owner.craftFlow.IsBusy) owner.closeWhenIdle = true;
            else owner.CloseUi();
        }
    }

    void LateUpdate()
    {
        if (closeWhenIdle && openPresentations == 0 && !craftFlow.IsBusy) CloseUi();

        bool showSupply = false;
        foreach (var spot in spots) showSupply |= spot.Refresh(BarVisible);
        if (supplyArrow.activeSelf != showSupply) supplyArrow.SetActive(showSupply);
    }

    public void CloseUi()
    {
        closeWhenIdle = false;
        if (serveCanvas != null) serveCanvas.gameObject.SetActive(false);
    }
}
