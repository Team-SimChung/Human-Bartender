using UnityEngine;

/// <summary>
/// 풀 소유권과 현재 대여 번호를 보관한다.
/// 비동기 작업은 시작 시 대여 번호를 저장한 뒤 같은 대여가 유지될 때만 반납해야 한다.
/// </summary>
public class PooledObject : MonoBehaviour
{
    public ObjectPool ParentPool { get; private set; }
    public ulong LeaseId { get; private set; }
    public bool IsRented { get; private set; }

    public bool SetPool(ObjectPool pool)
    {
        if (pool == null) return false;
        if (ParentPool != null && !ReferenceEquals(ParentPool, pool)) return false;

        ParentPool = pool;
        return true;
    }

    internal void MarkRented(ObjectPool pool)
    {
        if (!ReferenceEquals(ParentPool, pool)) return;

        LeaseId++;
        if (LeaseId == 0) LeaseId++;
        IsRented = true;
    }

    internal void MarkReturned(ObjectPool pool)
    {
        if (!ReferenceEquals(ParentPool, pool)) return;
        IsRented = false;
    }

    protected bool TryReturnToPool(ulong expectedLeaseId)
    {
        if (ParentPool == null) return false;
        return ParentPool.Return(gameObject, expectedLeaseId);
    }
}
