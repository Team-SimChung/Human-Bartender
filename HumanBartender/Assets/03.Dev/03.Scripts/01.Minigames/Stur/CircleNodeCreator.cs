using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// 스터링 미니게임에서 원형 경로 위에 목표 노드를 스폰/관리하는 컴포넌트.
/// 일정 시간마다 랜덤 위치에 새 노드를 생성하고, 수명이 다한 노드는 자동으로 풀에 반납한다.
/// </summary>
public class CircleNodeCreator : MonoBehaviour
{
    [SerializeField] ObjectPool targetNodePool;
    [SerializeField] ObjectPool effectPool;

    [SerializeField] float ratio = 1f;
    [SerializeField] int staticNodeCount = 3;
    [SerializeField] int angleStep;
    [SerializeField] int nodePadding = 10;
    [SerializeField] float createTargetDelay;
    [SerializeField] int createNodeCount = 2;
    [SerializeField] float nodeLifeTime = 2f;

    [SerializeField] float radiusX = 2f;
    [SerializeField] float radiusY = 2f;
    [SerializeField] Vector3 centerPos;

    [SerializeField] Color[] targetColors;

    bool isStart = false;
    float curTargetNodeTime = 0f;

    [Header("Node")]
    [SerializeField] List<CategoryNode> curActiveTargetNodes = new List<CategoryNode>();
    [SerializeField] List<CategoryNode> staticActiveTargetNodes = new List<CategoryNode>();
    [SerializeField] List<int> staticAngle = new List<int>();

    public void Init(float radX, float radY, Vector3 center, Color[] colors)
    {
        isStart = false;
        curTargetNodeTime = 0f;
        curActiveTargetNodes = new();
        staticActiveTargetNodes = new();
        staticAngle = new();

        effectPool.Init();
        targetNodePool.Init();

        radiusX = radX;
        radiusY = radY;
        centerPos = center;

        targetColors = colors;
    }

    public void InitToStart()
    {
        isStart = true;
        angleStep = 360 / staticNodeCount;

        SpawnStaticNode(270);

        return;

        for (int i = 0; i < staticNodeCount; i++)
        {
            int angle = Random.Range(angleStep * i + nodePadding, angleStep * (i + 1) - nodePadding);
            Logger.Log(angle);
            staticAngle.Add(angle);
            SpawnStaticNode(angle);
        }
    }

    public void Handle()
    {
        if (!isStart) return;

        curTargetNodeTime += Time.deltaTime;

        if (curTargetNodeTime >= createTargetDelay * ratio)
        {
            curTargetNodeTime = 0f;
            SpawnRandomNode2();
        }

        for (int i = curActiveTargetNodes.Count - 1; i >= 0; i--)
        {
            CategoryNode node = curActiveTargetNodes[i];

            if (Time.time - node.spawnTime >= node.lifeTime)
            {
                curActiveTargetNodes.RemoveAt(i);
                targetNodePool.Return(node.gameObject);
            }
        }
    }

    public bool TryHitNearestNode(Vector3 pos, float judgeRange, out Vector3 hitPosition, out Color hitColor)
    {
        hitPosition = default;
        hitColor = default;

        CategoryNode nearest = null;
        float minSqr = float.MaxValue;
        float sqr;
        bool isStatic = false;

        for (int i = 0; i < curActiveTargetNodes.Count; i++)
        {
            sqr = (curActiveTargetNodes[i].transform.position - pos).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr = sqr;
                nearest = curActiveTargetNodes[i];
            }
        }
        for (int i = 0; i < staticActiveTargetNodes.Count; i++)
        {
            sqr = (staticActiveTargetNodes[i].transform.position - pos).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr = sqr;
                isStatic = true;
                nearest = staticActiveTargetNodes[i];
            }
        }

        if (nearest != null && minSqr <= judgeRange * judgeRange)
        {
            hitPosition = nearest.transform.position;
            hitColor = nearest.curColor;

            if (!isStatic)
            {
                curActiveTargetNodes.Remove(nearest);
                targetNodePool.Return(nearest.gameObject);
            }

            return true;
        }

        return false;
    }

    public void StopAndReturnNodes()
    {
        isStart = false;

        ReturnNodes(curActiveTargetNodes);
        ReturnNodes(staticActiveTargetNodes);

        curActiveTargetNodes.Clear();
        staticActiveTargetNodes.Clear();
    }

    void OnDisable()
    {
        StopAndReturnNodes();
    }

    void ReturnNodes(List<CategoryNode> nodes)
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            CategoryNode node = nodes[i];
            if (node != null) targetNodePool.Return(node.gameObject);
        }
    }

    public void CreateEffectNode(Vector3 vec, Color color)
    {
        NodeEffect node;
        if (!effectPool.TryGet(out node)) return;

        node.transform.position = vec;
        node.SetNodeColor(color);
        node.PlayEffect();
    }

    public void SpawnRandomNode()
    {
        int line = Random.Range(0, staticAngle.Count);
        int startline = line;
        int endline = (line + 1) % staticAngle.Count;

        int start = staticAngle[startline];
        int end = staticAngle[endline];
        if (end <= start) end += 360;

        int min = start + nodePadding;
        int max = end - nodePadding;

        if (min >= max) return;

        int angle = Random.Range(min, max + 1) % 360;
        SpawnNode(angle);
    }

    public void SpawnRandomNode2()
    {
        for (int i = 0; i < createNodeCount; i++)
        {
            int angle = Random.Range(0, 360);
            SpawnNode(angle);
        }
    }

    void SpawnNode(float t)
    {
        Vector3 pos = GetSpawnPoint(t);

        CategoryNode node;
        if (!targetNodePool.TryGet(out node)) return;

        node.transform.position = centerPos + pos;
        node.spawnTime = Time.time;
        node.lifeTime = nodeLifeTime;
        node.Category = "target";

        int colorIndex = Random.Range(0, targetColors.Length);
        node.SetNodeColor(targetColors[colorIndex]);

        curActiveTargetNodes.Add(node);
    }
    void SpawnStaticNode(float t)
    {
        Vector3 pos = GetSpawnPoint(t);

        CategoryNode node;
        if (!targetNodePool.TryGet(out node)) return;

        node.transform.position = centerPos + pos;
        node.spawnTime = Time.time;
        node.lifeTime = nodeLifeTime;
        node.Category = "target";

        int colorIndex = Random.Range(0, targetColors.Length);
        node.SetNodeColor(Color.red);

        staticActiveTargetNodes.Add(node);
    }

    public Vector3 GetSpawnPoint(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * radiusX,
            Mathf.Sin(-rad) * radiusY,
            0f
        );
        return offset;
    }
}
