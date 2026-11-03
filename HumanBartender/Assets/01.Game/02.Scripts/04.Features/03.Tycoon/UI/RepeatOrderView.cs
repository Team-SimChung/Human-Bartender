using UnityEngine;
using UnityEngine.UI;

/// <summary>바 화면에 되묻기 버튼 하나를 두고, 현재 바라보는 좌석의 주문 상태를 표시한다.</summary>
public sealed class RepeatOrderView : MonoBehaviour
{
    [SerializeField] GameObject background;
    [SerializeField] Button button;
    [SerializeField] CanvasGroup buttonGroup;
    [SerializeField] PlayCamera playCamera;
    [SerializeField] GuestManager guests;
    [SerializeField] ServicePanelController servicePanel;

    void OnEnable()
    {
        background.SetActive(false);
        button.onClick.AddListener(Submit);
        playCamera.SelectedSeatChanged += Refresh;
        servicePanel.OpenChanged += Refresh;
    }

    void OnDisable()
    {
        servicePanel.OpenChanged -= Refresh;
        playCamera.SelectedSeatChanged -= Refresh;
        button.onClick.RemoveListener(Submit);
        background.SetActive(false);
    }

    GuestSlot FocusedSeat() => servicePanel.CanUseGuestActions && !playCamera.IsMoving
        ? guests.GetSeat(playCamera.SelectedSeat) : null;

    void LateUpdate() => Refresh();

    void Refresh()
    {
        var state = guests.GetRepeatOrderState(FocusedSeat());
        background.SetActive(state != RepeatOrderState.Hidden);
        button.interactable = state == RepeatOrderState.Ready;
        float alpha = state == RepeatOrderState.Speaking ? 0.36f : 1f;
        if (buttonGroup.alpha != alpha) buttonGroup.alpha = alpha;
    }

    public void Submit()
    {
        if (!isActiveAndEnabled) return;
        guests.TryRepeatOrder(FocusedSeat());
        Refresh();
    }
}
