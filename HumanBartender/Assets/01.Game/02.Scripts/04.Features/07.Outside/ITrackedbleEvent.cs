using System;
using UnityEngine;

/// <summary>ITrackedble 대상 등록/해제를 알리는 ScriptableObject 기반 이벤트 채널(옵저버 패턴).</summary>
[CreateAssetMenu(fileName = "ITrackedbleEvent", menuName = "Scriptable Objects/ITrackedbleEvent")]
public class ITrackedbleEvent : ScriptableObject
{
    public event Action<ITrackedble> OnRaised;

    /// <summary>이벤트를 발생시켜 모든 구독자에게 대상을 전달한다.</summary>
    public void Raise(ITrackedble interactor)
    {
        OnRaised?.Invoke(interactor);
    }

    /// <summary>SO 비활성화(씬 전환 등) 시 구독 해제 누락으로 인한 참조 잔존을 막기 위해 리스너를 초기화한다.</summary>
    private void OnDisable()
    {
        OnRaised = null;
    }
}
