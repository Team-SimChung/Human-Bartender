using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
/// <summary>
/// GameObject의 생성, 대여, 반납 상태를 한 곳에서 관리하는 오브젝트 풀.
/// 풀에서 생성하지 않은 오브젝트와 이미 반납된 오브젝트는 받지 않는다.
/// </summary>
public class ObjectPool
{
    [SerializeField] GameObject prefab;
    [SerializeField] int initCount = 10;
    [SerializeField] Transform parentTransform;

    Stack<GameObject> availableObjects = new();
    HashSet<GameObject> availableSet = new();
    HashSet<GameObject> rentedObjects = new();
    HashSet<GameObject> ownedObjects = new();

    bool initialized;

    public int AvailableCount
    {
        get
        {
            EnsureCollections();
            return availableSet.Count;
        }
    }

    public int RentedCount
    {
        get
        {
            EnsureCollections();
            return rentedObjects.Count;
        }
    }

    public int OwnedCount
    {
        get
        {
            EnsureCollections();
            return ownedObjects.Count;
        }
    }

    public ObjectPool(GameObject prefab, int initialSize, Transform parent)
    {
        this.prefab = prefab;
        initCount = Mathf.Max(0, initialSize);
        parentTransform = parent;
        Initialize();
    }

    /// <summary>
    /// 최초 호출에서는 초기 오브젝트를 생성한다. 이후 호출에서는 기존 소유 객체를 전부 회수한다.
    /// 재초기화할 때 기존 Stack을 버리거나 같은 수의 오브젝트를 다시 만들지 않는다.
    /// </summary>
    public bool Init()
    {
        EnsureCollections();

        if (initialized)
        {
            ResetAll();
            return true;
        }

        return Initialize();
    }

    public GameObject Get()
    {
        GameObject result;
        return TryGet(out result) ? result : null;
    }

    public bool TryGet(out GameObject result)
    {
        result = null;
        EnsureCollections();

        if (!initialized && !Initialize()) return false;

        while (true)
        {
            if (availableObjects.Count == 0 && !CreateAvailableObject()) return false;

            GameObject candidate = availableObjects.Pop();
            if (candidate == null)
            {
                availableSet.Remove(candidate);
                ownedObjects.Remove(candidate);
                continue;
            }

            if (!availableSet.Remove(candidate) || !ownedObjects.Contains(candidate))
            {
                Debug.LogError("[ObjectPool] 사용 가능 목록의 소유권 상태가 일치하지 않습니다.");
                continue;
            }

            rentedObjects.Add(candidate);

            PooledObject pooledObject = candidate.GetComponent<PooledObject>();
            if (pooledObject != null) pooledObject.MarkRented(this);

            candidate.SetActive(true);
            result = candidate;
            return true;
        }
    }

    public bool TryGet<T>(out T component) where T : Component
    {
        component = null;

        GameObject rentedObject;
        if (!TryGet(out rentedObject)) return false;

        component = rentedObject.GetComponent<T>();
        if (component != null) return true;

        Debug.LogError(
            $"[ObjectPool] '{rentedObject.name}'에 필요한 {typeof(T).Name} 컴포넌트가 없습니다.",
            rentedObject);
        Return(rentedObject);
        return false;
    }

    public bool Return(GameObject obj)
    {
        return ReturnInternal(obj, 0, false);
    }

    internal bool Return(GameObject obj, ulong expectedLeaseId)
    {
        return ReturnInternal(obj, expectedLeaseId, true);
    }

    /// <summary>현재 대여 중인 객체를 포함해 이 풀이 만든 모든 객체를 한 번씩 회수한다.</summary>
    public void ResetAll()
    {
        EnsureCollections();

        List<GameObject> aliveObjects = new();
        foreach (GameObject obj in ownedObjects)
        {
            if (obj != null) aliveObjects.Add(obj);
        }

        availableObjects.Clear();
        availableSet.Clear();
        rentedObjects.Clear();
        ownedObjects.Clear();

        for (int i = 0; i < aliveObjects.Count; i++)
        {
            GameObject obj = aliveObjects[i];
            ownedObjects.Add(obj);
            PrepareForStorage(obj);
            availableSet.Add(obj);
            availableObjects.Push(obj);
        }
    }

    /// <summary>풀에서 생성한 객체를 모두 파괴하고 런타임 상태를 비운다.</summary>
    public void Dispose()
    {
        EnsureCollections();

        foreach (GameObject obj in ownedObjects)
        {
            if (obj == null) continue;

            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

        availableObjects.Clear();
        availableSet.Clear();
        rentedObjects.Clear();
        ownedObjects.Clear();
        initialized = false;
    }

    bool Initialize()
    {
        EnsureCollections();

        if (prefab == null)
        {
            Debug.LogError("[ObjectPool] 생성할 프리팹이 연결되지 않았습니다.");
            return false;
        }

        initialized = true;
        int count = Mathf.Max(0, initCount);

        for (int i = 0; i < count; i++)
        {
            if (!CreateAvailableObject()) return false;
        }

        return true;
    }

    bool CreateAvailableObject()
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPool] 생성할 프리팹이 연결되지 않았습니다.");
            return false;
        }

        GameObject newObj = Object.Instantiate(prefab, parentTransform);
        if (newObj == null)
        {
            Debug.LogError($"[ObjectPool] '{prefab.name}' 프리팹 생성에 실패했습니다.");
            return false;
        }

        PooledObject pooledObject = newObj.GetComponent<PooledObject>();
        if (pooledObject != null && !pooledObject.SetPool(this))
        {
            Debug.LogError($"[ObjectPool] '{newObj.name}'의 소유 풀을 설정하지 못했습니다.", newObj);
            DestroyObject(newObj);
            return false;
        }

        ownedObjects.Add(newObj);
        PrepareForStorage(newObj);
        availableSet.Add(newObj);
        availableObjects.Push(newObj);
        return true;
    }

    bool ReturnInternal(GameObject obj, ulong expectedLeaseId, bool validateLease)
    {
        EnsureCollections();

        if (obj == null)
        {
            Debug.LogWarning("[ObjectPool] null 오브젝트는 반납할 수 없습니다.");
            return false;
        }

        if (!ownedObjects.Contains(obj))
        {
            Debug.LogWarning($"[ObjectPool] '{obj.name}'은 이 풀이 생성한 오브젝트가 아닙니다.", obj);
            return false;
        }

        PooledObject pooledObject = obj.GetComponent<PooledObject>();
        if (pooledObject != null)
        {
            if (!ReferenceEquals(pooledObject.ParentPool, this))
            {
                Debug.LogWarning($"[ObjectPool] '{obj.name}'의 소유 풀이 일치하지 않습니다.", obj);
                return false;
            }

            if (validateLease && pooledObject.LeaseId != expectedLeaseId)
            {
                Debug.LogWarning($"[ObjectPool] '{obj.name}'의 이전 대여 작업에서 늦은 반납을 요청했습니다.", obj);
                return false;
            }
        }

        if (!rentedObjects.Remove(obj))
        {
            Debug.LogWarning($"[ObjectPool] '{obj.name}'은 대여 중이 아니므로 다시 반납할 수 없습니다.", obj);
            return false;
        }

        PrepareForStorage(obj);
        if (!availableSet.Add(obj))
        {
            Debug.LogError($"[ObjectPool] '{obj.name}'이 사용 가능 목록에 중복 등록되었습니다.", obj);
            return false;
        }

        availableObjects.Push(obj);
        return true;
    }

    void PrepareForStorage(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(parentTransform, false);

        PooledObject pooledObject = obj.GetComponent<PooledObject>();
        if (pooledObject != null) pooledObject.MarkReturned(this);
    }

    void EnsureCollections()
    {
        if (availableObjects == null) availableObjects = new Stack<GameObject>();
        if (availableSet == null) availableSet = new HashSet<GameObject>();
        if (rentedObjects == null) rentedObjects = new HashSet<GameObject>();
        if (ownedObjects == null) ownedObjects = new HashSet<GameObject>();
    }

    static void DestroyObject(GameObject obj)
    {
        if (obj == null) return;

        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
