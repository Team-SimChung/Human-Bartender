using System.Collections.Generic;
using UnityEngine;

/// <summary>이름별 UI 풀을 구성하고 대여·반납 요청을 해당 풀에 전달한다.</summary>
public class PoolManager : MonoBehaviour
{
    [System.Serializable]
    public class PoolPrefab
    {
        public string name;
        public GameObject prefab;
        public Transform uiParent;
        public int initialSize = 10;
    }

    [SerializeField] List<PoolPrefab> uiPrefabs;

    readonly Dictionary<string, ObjectPool> uiPools = new();

    void Awake()
    {
        uiPools.Clear();
        if (uiPrefabs == null) return;

        for (int i = 0; i < uiPrefabs.Count; i++)
        {
            PoolPrefab definition = uiPrefabs[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.name))
            {
                Debug.LogError($"[PoolManager] {i}번 UI 풀의 이름이 비어 있습니다.", this);
                continue;
            }

            if (definition.prefab == null)
            {
                Debug.LogError($"[PoolManager] '{definition.name}' UI 풀의 프리팹이 연결되지 않았습니다.", this);
                continue;
            }

            if (uiPools.ContainsKey(definition.name))
            {
                Debug.LogError($"[PoolManager] '{definition.name}' UI 풀 이름이 중복되었습니다.", this);
                continue;
            }

            ObjectPool pool = new ObjectPool(
                definition.prefab,
                Mathf.Max(0, definition.initialSize),
                definition.uiParent);

            uiPools.Add(definition.name, pool);
        }
    }

    void OnDestroy()
    {
        foreach (KeyValuePair<string, ObjectPool> pair in uiPools)
            pair.Value.Dispose();

        uiPools.Clear();
    }

    public GameObject GetUI(string name)
    {
        GameObject result;
        return TryGetUI(name, out result) ? result : null;
    }

    public bool TryGetUI(string name, out GameObject result)
    {
        result = null;

        ObjectPool pool;
        if (string.IsNullOrEmpty(name) || !uiPools.TryGetValue(name, out pool))
        {
            Debug.LogWarning($"[PoolManager] '{name}' UI 풀을 찾을 수 없습니다.", this);
            return false;
        }

        return pool.TryGet(out result);
    }

    public bool ReturnUI(string name, GameObject obj)
    {
        ObjectPool pool;
        if (string.IsNullOrEmpty(name) || !uiPools.TryGetValue(name, out pool))
        {
            Debug.LogWarning($"[PoolManager] '{name}' UI 풀을 찾을 수 없습니다.", this);
            return false;
        }

        return pool.Return(obj);
    }
}
