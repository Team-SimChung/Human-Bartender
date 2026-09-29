using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 쉐이킹 미니게임에서 경로 위에 목표 노드를 스폰/관리하는 컴포넌트.
/// NodePatternData에 따라 패턴 노드를 스폰하고, StrikeNode가 이벤트를 발생시키면 새 패턴으로 교체한다.
/// </summary>
public class ShakingCatergoryNodeCreator : MonoBehaviour
{
    [SerializeField] NodePatternData nodePatternData;
    [SerializeField] ObjectPool targetNodePool;
    [SerializeField] ObjectPool effectPool;

    [SerializeField] float ratio = 1f;

    [SerializeField] float createTargetDelay;

    [SerializeField] float nodeLifeTime = 2f;

    [SerializeField] int createTargetNodeMinCount;
    [SerializeField] int createTargetNodeMaxCount;

    [SerializeField] Vector3[] targetPostions;
    [SerializeField] Color[] targetColors;
    [SerializeField] PatternData curPatternData;


    bool isStart = false;
    float curTargetNodeTime = 0f;

    [Header("Node")]
    [SerializeField] List<CategoryNode> curActiveTargetNodes = new List<CategoryNode>();
    [SerializeField] List<CategoryNode> staticActiveTargetNodes = new List<CategoryNode>();

    public void Init()
    {
        isStart = false;
        curTargetNodeTime = 0f;
        curActiveTargetNodes = new();
        staticActiveTargetNodes = new();

        effectPool.Init();
        targetNodePool.Init();
    }

    public void Handle()
    {
        if (!isStart) return;
;
        curTargetNodeTime += Time.deltaTime;

        if (curTargetNodeTime >= createTargetDelay * ratio)
        {
            curTargetNodeTime = 0f;
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

    public void InitToStart(Vector3[] dots, Color[] colors)
    {
        isStart = true;

        for(int i =0; i < dots.Length; i++)
            SpawnStaticNode(dots[i]);

        targetPostions = dots;
        targetColors = colors;
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
    public void SpawnPatternNode(bool isDown)
    {
        foreach(var item in curActiveTargetNodes)
        {
            targetNodePool.Return(item.gameObject);
        }
        curActiveTargetNodes.Clear();

        if (isDown)
            curPatternData = nodePatternData.patternDatas[Random.Range(0, nodePatternData.patternDatas.Length)];

        for (int i = 0; i < curPatternData.patternDatas.Length; i++)
        {
            int startIndex = curPatternData.patternDatas[i].lineIndex;
            int nextVal = isDown ? 1 : -1;

            if (!isDown)
                startIndex = targetPostions.Length - 1 - startIndex;

            Vector3 a = targetPostions[startIndex];
            Vector3 b = targetPostions[startIndex + nextVal];

            SpawnNode(a, b, curPatternData.patternDatas[i].patternT);
        }
    }
    public void SpawnNode(Vector3 a, Vector3 b, float t)
    {
        Vector3 pos = GetSpawnPoint(a, b, t);

        CategoryNode node;
        if (!targetNodePool.TryGet(out node)) return;

        node.transform.position = pos;
        node.spawnTime = Time.time;
        node.lifeTime = nodeLifeTime;
        node.Category = "target";

        int colorIndex = Random.Range(0, targetColors.Length);
        node.SetNodeColor(targetColors[colorIndex]);

        curActiveTargetNodes.Add(node);
    }
    public void SpawnStaticNode(Vector3 a)
    {
        Vector3 pos = a;

        CategoryNode node;
        if (!targetNodePool.TryGet(out node)) return;

        node.transform.position = pos;
        node.spawnTime = Time.time;
        node.lifeTime = nodeLifeTime;
        node.Category = "target";

        int colorIndex = Random.Range(0, targetColors.Length);
        node.SetNodeColor(Color.red);

        staticActiveTargetNodes.Add(node);
    }

    public Vector3 GetSpawnPoint(Vector3 a, Vector3 b, float t)
    {
        Vector3 pos = Vector3.Lerp(a, b, t);
        return pos;
    }    
}


