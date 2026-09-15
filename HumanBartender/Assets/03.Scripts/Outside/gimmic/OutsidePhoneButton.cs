using UnityEngine;
using UnityEngine.EventSystems;

public class OutsidePhoneButton : MonoBehaviour, // 클래스 상속
    IPointerDownHandler,                    // 인터페이스 구현
    IInitializePotentialDragHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField] private OutsidePhoneGimmick controller;
    [SerializeField] private string number;

    public void OnInitializePotentialDrag(PointerEventData data)
    {
        // 조금만 움직여도 바로 드래그 처리
        data.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left)
            return;

        controller.BeginDial(number, (RectTransform)transform, data);
    }

    public void OnDrag(PointerEventData data)
    {
        controller.DragDial(data);
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left)
            return;

        controller.EndDial(data);
    }
}
