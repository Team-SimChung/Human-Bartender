using UnityEngine;

/// <summary>
/// 좌석 표시에 무엇을 보여 줄지 정한다: 지금 보일지, 어느 좌석이 선택됐는지, 좌석마다 어떤 상태인지.
/// 상태 판정은 BarSeatSignalRules, 그리기는 BarSeatStatusView가 한다. 이 클래스만 씬의 다른 시스템을 안다.
///
/// 손님 대기 시간은 매 순간 줄고 도착·퇴장 표시는 깜빡이므로 매 프레임 계산한다. 화면에는 바뀐 값만 넘긴다.
/// </summary>
[RequireComponent(typeof(BarSeatStatusView))]
public sealed class BarSeatHudPresenter : MonoBehaviour
{
    static readonly ESlotType[] SeatOrder = { ESlotType.Left, ESlotType.Middle, ESlotType.Right };

    [SerializeField] BarSeatStatusView view;
    [Tooltip("왼쪽·가운데·오른쪽 손님 좌석. 표시 점과 같은 순서다.")]
    [SerializeField] GuestSlot[] guestSlots = new GuestSlot[3];

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

    readonly object[] occupants = new object[3];
    readonly float[] changedAt = new float[3];

    void Reset() => view = GetComponent<BarSeatStatusView>();

    void LateUpdate()
    {
        bool tycoon = phases != null && phases.CurrentPhase == EPlayPhase.Tycoon;
        bool visible = ShouldShow(tycoon);
        view.SetVisible(visible);
        if (!visible) return;

        ESlotType current = view.IsExploring ? view.ExploringSeat
            : tycoon && playCamera != null ? playCamera.SelectedSeat : ClosestSeat();
        float now = Time.unscaledTime;
        for (int i = 0; i < SeatOrder.Length; i++)
        {
            ESlotType seat = SeatOrder[i];
            GuestSlot slot = guestSlots[i];
            object occupant = tycoon ? slot?.CurrentGuest : characters?.GetSlotCharacterId(seat);
            if (occupant is string id && string.IsNullOrEmpty(id)) occupant = null;
            if (!Equals(occupants[i], occupant))
            {
                occupants[i] = occupant;
                changedAt[i] = now;
            }

            bool seated = tycoon && slot != null && occupant != null;
            bool leaving = seated && slot.CurrentState == EGuestState.Leaving;
            float remaining = 0, total = 0;
            bool waitKnown = seated && !leaving && guests != null && guests.TryGetSeatWaitTime(slot, out remaining, out total);
            float sinceChange = now - changedAt[i];
            EBarSeatSignal signal = BarSeatSignalRules.Evaluate(occupant != null, leaving, waitKnown, remaining, total, sinceChange);
            view.SetSeat(i, signal, seat == current, sinceChange);
        }
    }

    /// <summary>
    /// 영업 중에는 되묻기 버튼과 같은 조건(CanUseGuestActions)을 따르고, 대화 중에는 준비·레시피 화면이 열려 있지 않을 때 보인다.
    /// 어느 쪽이든 기믹 중과 튜토리얼 전면 안내 중에는 숨는다. 카메라가 좌석을 옮기는 동안에는 숨기지 않는다 — 점이 이동을 보여 줘야 한다.
    /// </summary>
    bool ShouldShow(bool tycoon)
    {
        if (view.Suppressed || phases == null || craft != null && craft.Current?.Phase == ECraftPhase.Playing) return false;
        if (tycoon) return servicePanel == null || servicePanel.CanUseGuestActions;
        return phases.CurrentPhase == EPlayPhase.Dialogue &&
            (preparation == null || !preparation.IsOpen) && (recipes == null || !recipes.IsOpen);
    }

    /// <summary>대화 중 화면 가운데에 가장 가까운 좌석. 스토리 연출은 좌석 번호가 아니라 카메라 위치로 화면을 잡는다.</summary>
    ESlotType ClosestSeat()
    {
        float cameraX = Camera.main != null ? Camera.main.transform.position.x : 0;
        ESlotType closest = ESlotType.Middle;
        float distance = float.MaxValue;
        if (characters == null) return closest;
        foreach (var seat in SeatOrder)
        {
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
}
