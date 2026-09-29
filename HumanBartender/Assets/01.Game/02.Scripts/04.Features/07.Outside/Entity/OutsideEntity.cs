using UnityEngine;

/// <summary>실외 씬 엔티티들의 공통 베이스. Transform/GameObject 접근자만 제공하는 최소 구현.</summary>
public abstract class OutsideEntity : MonoBehaviour, IEntity
{

    public Transform Transform { get => transform;}
    public GameObject GameObject => this.gameObject;
}
