using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum EBarSeatSignal { Empty, Occupied, Arrival, Waiting, LeavingSoon, Departing }

/// <summary>
/// 바 화면 왼쪽 아래의 좌석 세 칸 표시. 일반 영업과 스토리(튜토리얼 포함)가 함께 쓴다.
///
/// Play 씬의 바 UI 캔버스(Canvas_BarSeatHud)에 저장된 오브젝트다. 튜토리얼 오버레이는 이것을 빌려
/// 강조 대상으로만 쓴다 — 오버레이 안에서 만들면 오버레이가 아직 없는 날(1부 영업만 있는 날)에는
/// 좌석 표시가 사라진다. 배치·참조는 Tools/Story/Set Up Bar Seat HUD가 씬에 저장한다.
/// </summary>
public sealed class BarSeatStatusView : MonoBehaviour
{
    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };
    public static readonly Color EmptyColor = new(.29f, .31f, .33f);
    public static readonly Color OccupiedColor = new(.54f, .8f, .73f);
    public static readonly Color SelectedColor = new(.79f, 1f, 1f);
    public static readonly Color WaitingColor = new(1f, .65f, .28f);
    public static readonly Color DangerColor = new(1f, .38f, .36f);

    [Header("Layout")]
    [SerializeField] RectTransform indicators;
    [Tooltip("왼쪽·가운데·오른쪽 좌석 점.")]
    [SerializeField] StoryTutorialShape[] dots = new StoryTutorialShape[3];
    [Tooltip("지금 보고 있는 좌석을 두르는 링. 점과 같은 순서다.")]
    [SerializeField] StoryTutorialShape[] rings = new StoryTutorialShape[3];
    [Tooltip("좌석 둘러보기 수업에서만 켜지는 좌우 키 안내.")]
    [SerializeField] RectTransform leftKey;
    [SerializeField] RectTransform rightKey;

    [Header("Scene")]
    [SerializeField] PlayPhaseController phases;
    [SerializeField] DialogueCharacterManager characters;
    [SerializeField] GuestManager guests;
    [SerializeField] CraftFlowController craft;
    [SerializeField] CraftPrepStageHost preparation;
    [SerializeField] RecipeBrowserScreen recipes;
    [Tooltip("영업 중 선택 좌석의 정본. 되묻기 버튼·서빙 자리와 같은 좌석을 가리키게 한다.")]
    [SerializeField] PlayCamera playCamera;
    [Tooltip("영업 중 표시 조건(CanUseGuestActions). 메뉴·레시피가 열리면 숨는다.")]
    [SerializeField] ServicePanelController servicePanel;

    readonly Dictionary<ESlotType, object> occupants = new();
    readonly Dictionary<ESlotType, float> arrivalAt = new();
    readonly Dictionary<ESlotType, EBarSeatSignal> signals = new();
    readonly Dictionary<ESlotType, GuestSlot> guestSlots = new();
    bool exploring, moving;
    ESlotType current;

    public RectTransform Indicators => indicators;
    public RectTransform LeftKey => leftKey;
    public RectTransform RightKey => rightKey;
    public bool IsVisible => indicators != null && indicators.gameObject.activeInHierarchy;
    public bool Suppressed { get; set; }
    public ESlotType SelectedSeat => current;
    public EBarSeatSignal Signal(ESlotType seat) => signals.TryGetValue(seat, out var signal) ? signal : EBarSeatSignal.Empty;

    void Awake()
    {
        guestSlots.Clear();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var slot in root.GetComponentsInChildren<GuestSlot>(true)) guestSlots[slot.SlotType] = slot;
        SetExploration(false, ESlotType.Right, false);
    }

    void OnEnable()
    {
        GameKeyBindings.Changed += RefreshKeyLabels;
        RefreshKeyLabels();
    }

    void OnDisable() => GameKeyBindings.Changed -= RefreshKeyLabels;

    void RefreshKeyLabels()
    {
        if (leftKey != null) leftKey.GetComponentInChildren<TMP_Text>(true).text = "< " + GameKeyBindings.DisplayName(GameControl.PreviousSeat);
        if (rightKey != null) rightKey.GetComponentInChildren<TMP_Text>(true).text = GameKeyBindings.DisplayName(GameControl.NextSeat) + " >";
    }

    public void SetExploration(bool active, ESlotType seat, bool cameraMoving)
    {
        exploring = active;
        current = seat;
        moving = cameraMoving;
        leftKey.gameObject.SetActive(active);
        rightKey.gameObject.SetActive(active);
    }

    /// <summary>
    /// 지금 보여야 하는지. 영업 중에는 되묻기 버튼과 같은 조건(CanUseGuestActions)을 따르고, 대화 중에는
    /// 준비·레시피 화면이 열려 있지 않을 때 보인다. 어느 쪽이든 기믹 중과 전면 안내 중에는 숨는다.
    /// 카메라가 좌석을 옮기는 동안에는 숨기지 않는다 — 점이 이동을 보여 줘야 한다.
    /// </summary>
    bool ShouldShow(bool tycoon)
    {
        if (Suppressed || phases == null || craft != null && craft.Current?.Phase == ECraftPhase.Playing) return false;
        if (tycoon) return servicePanel == null || servicePanel.CanUseGuestActions;
        return phases.CurrentPhase == EPlayPhase.Dialogue &&
            (preparation == null || !preparation.IsOpen) && (recipes == null || !recipes.IsOpen);
    }

    void LateUpdate()
    {
        bool tycoon = phases != null && phases.CurrentPhase == EPlayPhase.Tycoon;
        bool visible = ShouldShow(tycoon);
        indicators.gameObject.SetActive(visible);
        if (!visible) return;
        if (!exploring) current = tycoon && playCamera != null ? playCamera.SelectedSeat : ClosestSeat();
        leftKey.gameObject.SetActive(exploring);
        rightKey.gameObject.SetActive(exploring);
        if (exploring)
        {
            leftKey.GetComponentInChildren<TMP_Text>().alpha = current == ESlotType.Left || moving ? .3f : 1f;
            rightKey.GetComponentInChildren<TMP_Text>().alpha = current == ESlotType.Right || moving ? .3f : 1f;
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

    /// <summary>대화 중 화면 가운데에 가장 가까운 좌석. 스토리 연출은 좌석 번호가 아니라 카메라 위치로 화면을 잡는다.</summary>
    ESlotType ClosestSeat()
    {
        float cameraX = Camera.main != null ? Camera.main.transform.position.x : 0;
        ESlotType closest = ESlotType.Middle;
        float distance = float.MaxValue;
        foreach (var seat in SeatOrder)
        {
            if (characters == null) continue;
            if (!characters.TryGetSlotX(seat, out float x))
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
