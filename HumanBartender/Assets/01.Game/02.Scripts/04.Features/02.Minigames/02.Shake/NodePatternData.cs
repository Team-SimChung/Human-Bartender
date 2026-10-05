using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PatternNodeData
{
    public string patternName;
    public int lineIndex;
    public float patternT;
}

[System.Serializable]
public struct PatternData
{
    public PatternNodeData[] patternDatas;
}

/// <summary>쉐이킹 미니게임 노드 스폰 패턴 데이터를 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "NodePatternData", menuName = "Scriptable Objects/NodePatternData")]
public class NodePatternData : ScriptableObject
{
    public PatternData[] patternDatas;
}
