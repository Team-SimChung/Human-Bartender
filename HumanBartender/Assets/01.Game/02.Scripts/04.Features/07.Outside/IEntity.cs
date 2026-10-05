using UnityEngine;

/// <summary>실외 씬의 모든 엔티티(오브젝트/NPC 등)가 구현하는 최소 공통 인터페이스.</summary>
public interface IEntity
{
    Transform Transform { get; }
    GameObject GameObject { get; }
}
