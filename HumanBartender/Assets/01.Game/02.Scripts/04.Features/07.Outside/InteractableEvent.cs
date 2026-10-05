using System;
using UnityEngine;

/// <summary>IInteractable(상호작용 대상) 관련 알림을 전달하는 ScriptableObject 기반 이벤트 채널.</summary>
[CreateAssetMenu(fileName = "InteractableEventChannel", menuName = "Scriptable Objects/InteractableEventChannel")]
public class InteractableEvent : ScriptableObject
{
    public event Action<IInteractable> OnRaised;

    /// <summary>이벤트를 발생시켜 모든 구독자에게 대상을 전달한다.</summary>
    public void Raise(IInteractable interactor)
    {
        OnRaised?.Invoke(interactor);
    }

    /// <summary>SO 비활성화 시 리스너를 초기화해 참조 잔존을 막는다.</summary>
    private void OnDisable()
    {
        OnRaised = null;
    }

}