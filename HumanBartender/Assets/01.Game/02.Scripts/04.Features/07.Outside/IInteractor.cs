using UnityEngine;

/// <summary>상호작용 주체(플레이어 등)의 현재 상태.</summary>
public enum EInteractorState
{
    None,
    Interct,
    ForceMove,
    Lock,
}

/// <summary>상호작용을 "거는" 쪽(주로 플레이어)이 구현하는 인터페이스.</summary>
public interface IInteractor : IEntity, ITrackedble
{
    EInteractorState State { get; set; }
    void InteractorEvent();
}

