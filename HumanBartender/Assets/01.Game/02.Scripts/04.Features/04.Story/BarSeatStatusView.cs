using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum EBarSeatSignal { Empty, Occupied, Arrival, Waiting, LeavingSoon, Departing }

/// <summary>The live three-seat HUD used by both normal service and the story tutorial.</summary>
public sealed class BarSeatStatusView : MonoBehaviour
{
    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };
    public static readonly Color EmptyColor = new(.29f, .31f, .33f);
    public static readonly Color OccupiedColor = new(.54f, .8f, .73f);
    public static readonly Color SelectedColor = new(.79f, 1f, 1f);
    public static readonly Color WaitingColor = new(1f, .65f, .28f);
    public static readonly Color DangerColor = new(1f, .38f, .36f);

    StoryTutorialShape[] dots, rings;
    readonly Dictionary<ESlotType, object> occupants = new();
    readonly Dictionary<ESlotType, float> arrivalAt = new();
    readonly Dictionary<ESlotType, EBarSeatSignal> signals = new();
    readonly Dictionary<ESlotType, GuestSlot> guestSlots = new();
    PlayPhaseController phases;
    DialogueCharacterManager characters;
    GuestManager guests;
    CraftFlowController craft;
    CraftPrepStageHost preparation;
    RecipeBrowserScreen recipes;
    bool exploring, moving;
    ESlotType current;

    public RectTransform Indicators { get; private set; }
    public RectTransform LeftKey { get; private set; }
    public RectTransform RightKey { get; private set; }
    public bool IsVisible => Indicators != null && Indicators.gameObject.activeInHierarchy;
    public bool Suppressed { get; set; }
    public ESlotType SelectedSeat => current;
    public EBarSeatSignal Signal(ESlotType seat) => signals.TryGetValue(seat, out var signal) ? signal : EBarSeatSignal.Empty;

    public static BarSeatStatusView Create(Transform parent, TMP_FontAsset font)
    {
        var root = StoryTutorialView.MakeRect("Live Bar Seat HUD", parent, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        var view = root.gameObject.AddComponent<BarSeatStatusView>();
        view.Indicators = StoryTutorialView.MakeRect("Seat Indicators", root, new Vector2(104, 38), new Vector2(66, 25));
        view.Indicators.anchorMin = view.Indicators.anchorMax = Vector2.zero;
        var frame = view.Indicators.gameObject.AddComponent<StoryTutorialShape>();
        frame.Style(new Color(.035f, .06f, .085f, .94f), 18);
        var border = StoryTutorialView.MakeRect("HUD Border", view.Indicators, new Vector2(104, 38), Vector2.zero)
            .gameObject.AddComponent<StoryTutorialShape>();
        border.Style(new Color(.31f, .52f, .57f, .8f), 18, 1);
        view.dots = new StoryTutorialShape[3];
        view.rings = new StoryTutorialShape[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 position = new Vector2((i - 1) * 32, 0);
            view.rings[i] = StoryTutorialView.MakeRect("Selected Seat " + SeatOrder[i], view.Indicators,
                new Vector2(30, 30), position).gameObject.AddComponent<StoryTutorialShape>();
            view.rings[i].Style(SelectedColor, 15, 1.5f);
            view.dots[i] = StoryTutorialView.MakeRect("Seat " + SeatOrder[i], view.Indicators,
                new Vector2(14, 14), position).gameObject.AddComponent<StoryTutorialShape>();
            view.dots[i].Style(EmptyColor, 7);
        }
        view.LeftKey = MakeKey(root, font, "Left Seat Key", "< Q", new Vector2(0, .5f), new Vector2(29, 0));
        view.RightKey = MakeKey(root, font, "Right Seat Key", "E >", new Vector2(1, .5f), new Vector2(-29, 0));
        view.SetExploration(false, ESlotType.Right, false);
        return view;
    }

    static RectTransform MakeKey(Transform parent, TMP_FontAsset font, string name, string label, Vector2 anchor, Vector2 position)
    {
        var key = StoryTutorialView.MakeRect(name, parent, new Vector2(48, 44), position);
        key.anchorMin = key.anchorMax = anchor;
        var text = StoryTutorialView.MakeText("Key", key, font, new Vector2(48, 44), Vector2.zero, 17);
        text.color = SelectedColor;
        text.text = label;
        return key;
    }

    public void Configure(PlayPhaseController phase, DialogueCharacterManager characterManager, GuestManager guestManager,
        CraftFlowController craftFlow, CraftPrepStageHost prep, RecipeBrowserScreen recipeScreen)
    {
        phases = phase;
        characters = characterManager;
        guests = guestManager;
        craft = craftFlow;
        preparation = prep;
        recipes = recipeScreen;
        guestSlots.Clear();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var slot in root.GetComponentsInChildren<GuestSlot>(true)) guestSlots[slot.SlotType] = slot;
    }

    public void SetExploration(bool active, ESlotType seat, bool cameraMoving)
    {
        exploring = active;
        current = seat;
        moving = cameraMoving;
        LeftKey.gameObject.SetActive(active);
        RightKey.gameObject.SetActive(active);
    }

    void LateUpdate()
    {
        bool visible = !Suppressed && phases != null && (phases.CurrentPhase == EPlayPhase.Dialogue || phases.CurrentPhase == EPlayPhase.Tycoon) &&
            (preparation == null || !preparation.IsOpen) && (recipes == null || !recipes.IsOpen) &&
            (craft == null || craft.Current?.Phase != ECraftPhase.Playing);
        Indicators.gameObject.SetActive(visible);
        if (!visible) return;
        bool tycoon = phases.CurrentPhase == EPlayPhase.Tycoon;
        if (!exploring) current = ClosestSeat(tycoon);
        LeftKey.gameObject.SetActive(exploring);
        RightKey.gameObject.SetActive(exploring);
        if (exploring)
        {
            LeftKey.GetComponentInChildren<TMP_Text>().alpha = current == ESlotType.Left || moving ? .3f : 1f;
            RightKey.GetComponentInChildren<TMP_Text>().alpha = current == ESlotType.Right || moving ? .3f : 1f;
        }
        for (int i = 0; i < 3; i++)
        {
            ESlotType seat = SeatOrder[i];
            guestSlots.TryGetValue(seat, out var slot);
            object occupant = tycoon ? slot?.CurrentGuest : characters?.GetSlotCharacterId(seat);
            if (occupant is string id && string.IsNullOrEmpty(id)) occupant = null;
            if (!occupants.TryGetValue(seat, out var old) || !Equals(old, occupant))
            {
                occupants[seat] = occupant;
                arrivalAt[seat] = Time.unscaledTime;
            }
            EBarSeatSignal signal = occupant == null ? EBarSeatSignal.Empty : EBarSeatSignal.Occupied;
            if (tycoon && slot != null && occupant != null)
            {
                if (slot.CurrentState == EGuestState.Leaving) signal = EBarSeatSignal.Departing;
                else if (guests != null && guests.TryGetSeatWaitTime(slot, out float remaining, out float total))
                    signal = remaining <= 10 ? EBarSeatSignal.LeavingSoon : remaining <= total * .5f ? EBarSeatSignal.Waiting : signal;
            }
            if (signal == EBarSeatSignal.Occupied && Time.unscaledTime - arrivalAt[seat] < 2.4f)
                signal = EBarSeatSignal.Arrival;
            signals[seat] = signal;
            bool selected = seat == current;
            rings[i].gameObject.SetActive(selected);
            Color tint = SignalColor(signal, selected);
            if ((signal == EBarSeatSignal.Arrival || signal == EBarSeatSignal.Departing) &&
                (int)((Time.unscaledTime - arrivalAt[seat]) / .4f) % 2 == 1) tint.a = .24f;
            dots[i].color = tint;
        }
    }

    ESlotType ClosestSeat(bool tycoon)
    {
        float cameraX = Camera.main != null ? Camera.main.transform.position.x : 0;
        ESlotType closest = ESlotType.Middle;
        float distance = float.MaxValue;
        foreach (var seat in SeatOrder)
        {
            float x = 0;
            if (tycoon && guestSlots.TryGetValue(seat, out var slot)) x = slot.transform.position.x;
            else if (characters == null) continue;
            else if (!characters.TryGetSlotX(seat, out x))
            {
                if (seat != ESlotType.Middle || !characters.TryGetSlotX(ESlotType.Left, out float left) ||
                    !characters.TryGetSlotX(ESlotType.Right, out float right)) continue;
                x = (left + right) * .5f;
            }
            if (Mathf.Abs(cameraX - x) >= distance) continue;
            distance = Mathf.Abs(cameraX - x);
            closest = seat;
        }
        return closest;
    }

    public static Color SignalColor(EBarSeatSignal signal, bool selected = false) => signal switch
    {
        EBarSeatSignal.Empty => EmptyColor,
        EBarSeatSignal.Waiting => WaitingColor,
        EBarSeatSignal.LeavingSoon or EBarSeatSignal.Departing => DangerColor,
        _ => selected ? SelectedColor : OccupiedColor
    };
}
