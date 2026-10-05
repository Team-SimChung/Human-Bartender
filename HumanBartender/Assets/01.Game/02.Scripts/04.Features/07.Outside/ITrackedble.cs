using UnityEngine;

/// <summary>UI(버튼/텍스트)로 화면에 추적 표시될 수 있는 대상이 구현하는 인터페이스.</summary>
public interface ITrackedble : IEntity
{
    Vector2 ButtonOffset { get; }
    Vector2 TextOffset { get; }
    bool IsAvaliable { get; }
}
