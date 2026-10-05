using System;
using UnityEngine;

/// <summary>IInteractor(상호작용 주체) 관련 알림을 전달하는 ScriptableObject 기반 이벤트 채널.</summary>
[CreateAssetMenu(fileName = "InteractorEventChannel", menuName = "Scriptable Objects/InteractorEventChannel")]
public class InteractorEvent : ScriptableObject
{
    public event Action<IInteractor> OnRaised;

    /// <summary>이벤트를 발생시켜 모든 구독자에게 대상을 전달한다.</summary>
    public void Raise(IInteractor interactor)
    {
        OnRaised?.Invoke(interactor);
    }

    /// <summary>SO 비활성화 시 리스너를 초기화해 참조 잔존을 막는다.</summary>
    private void OnDisable()
    {
        OnRaised = null;
    }

}