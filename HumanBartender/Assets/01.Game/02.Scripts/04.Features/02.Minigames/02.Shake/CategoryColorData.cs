using System.Collections.Generic;
using UnityEngine;

/// <summary>칵테일 키워드 카테고리 이름과 대응하는 색상을 매핑하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "CategoryColorData", menuName = "Scriptable Objects/CategoryColorData")]
public class CategoryColorData : ScriptableObject
{
    public List<string> categorys = new List<string>();
    public List<Color> colors = new List<Color>();
}
