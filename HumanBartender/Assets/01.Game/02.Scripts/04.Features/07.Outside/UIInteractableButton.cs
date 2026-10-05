using TMPro;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 상호작용 가능한 대상을 추적해 "F키 등 상호작용" 버튼 UI를 표시하는 트래커.
/// InteractableEvent(타겟 변경 알림)를 구독해 대상이 바뀔 때마다 라벨과 표시 여부를 갱신한다.
/// </summary>
public class UIInteractableButton : UIOutsideTracker
{
    [SerializeField] InteractableEvent OnInteractbleEvent;
    [SerializeField] TMP_Text interactText;

    public override void OnEnable()
    {
        base.OnEnable();

        if (OnInteractbleEvent != null)
            OnInteractbleEvent.OnRaised += OnVisibleInteractButton;
    }

    public override void OnDisable()
    {
        base.OnDisable();

        if (OnInteractbleEvent != null)
            OnInteractbleEvent.OnRaised -= OnVisibleInteractButton;
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
    }

    /// <summary>타겟이 있으면 버튼을 표시하고 라벨/추적 대상을 갱신, null이면 버튼을 숨긴다.</summary>
    public void OnVisibleInteractButton(IInteractable interactable)
    {
        if (target == null)
        {
            trackedTarget = null;
            return;
        }

        if (interactable == null)
        {
            target.gameObject.SetActive(false);
            trackedTarget = null;
            return;
        }

        SetTrackedTarget(interactable);

        if (interactText != null)
            interactText.text = interactable.Label;

        target.gameObject.SetActive(true);
    }

    /// <summary>추적을 해제하고 버튼을 숨긴다. (현재 이벤트 구독처에서 직접 호출되지는 않는 것으로 보임)</summary>
    public void OnUnVisibleInteractButton(IInteractable interactable)
    {
        trackedTarget = null;
        target.gameObject.SetActive(false);
    }
}
