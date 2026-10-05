using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// GuestSlot 앞에 위치한 드롭 존. 손님 앞에 무언가를 놓는 일은 전부 여기로 들어온다.
///
/// 코스터 아이콘(CoasterDragItem)이 놓이면 GuestManager에 배치를 요청해, 손님이 코스터를 기다리는
/// 상태였다면 주문 대기 상태로 전환시키고 인내심 타이머를 멈춘다. 놓인 코스터는 이 위치(손님 앞)에
/// 그대로 고정되며, 손님이 자리를 뜨면 치운다.
///
/// 완성한 잔(DrinkDragItem)이 놓이면 그 손님에게 서빙한다. 잔은 코스터 위에 올리는 것이라 자리가
/// 같으므로 드롭존을 따로 두지 않는다 — 놓인 코스터가 이 존을 덮고 있어도 드롭은 부모로 올라온다.
/// </summary>
public class CoasterDropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] GuestSlot targetSlot;
    [SerializeField] GuestManager guestManager;

    [Tooltip("코스터가 놓였을 때 이 드롭존 중앙 기준으로 얼마나 떨어진 위치에 고정될지(로컬 UI 좌표). 손님 자리에 맞게 조정.")]
    [SerializeField] Vector2 placedOffset;

    CoasterDragItem placedCoaster;

    void OnEnable()
    {
        if (guestManager != null)
            guestManager.GuestReleased += OnGuestReleased;
    }

    void OnDisable()
    {
        if (guestManager != null)
            guestManager.GuestReleased -= OnGuestReleased;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject dragged = eventData.pointerDrag;
        if (dragged == null) return;

        if (dragged.TryGetComponent(out CoasterDragItem coaster))
        {
            PlaceCoaster(coaster);
            return;
        }

        if (dragged.TryGetComponent(out DrinkDragItem drink))
            ServeDrink(drink);
    }

    void PlaceCoaster(CoasterDragItem coaster)
    {
        if (!guestManager.TryPlaceCoaster(targetSlot)) return;

        coaster.PlaceAt((RectTransform)transform, placedOffset);
        placedCoaster = coaster;
    }

    /// <summary>
    /// 완성한 잔을 이 자리의 손님에게 낸다. 받을 수 없는 상태(코스터 전, 주문 전, 이미 떠나는 중)면
    /// 아무 일도 일어나지 않고, 잔은 드래그가 끝나면서 트레이로 되돌아간다.
    /// </summary>
    void ServeDrink(DrinkDragItem drink)
    {
        if (!guestManager.TryServeDrink(targetSlot, drink.Drink)) return;

        drink.MarkServed();
    }

    /// <summary>
    /// 손님이 응대/이탈로 자리를 떠 슬롯이 비면, 놓여있던 코스터를 트레이로 되돌린다.
    /// 되돌려야 다음 손님에게 다시 내줄 수 있다 — 없애면 트레이가 손님 몇 명 만에 바닥난다.
    /// </summary>
    void OnGuestReleased(Guest guest)
    {
        if (placedCoaster == null || !targetSlot.IsEmpty) return;

        placedCoaster.ReturnToTray();
        placedCoaster = null;
    }
}
