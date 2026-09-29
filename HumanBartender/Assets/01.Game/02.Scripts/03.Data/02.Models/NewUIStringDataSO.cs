using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StreamingAssets/json/ui_strings.json을 보유하는 ScriptableObject.
/// 루트가 문자열 키 -> LocalizedText 딕셔너리 구조다.
/// </summary>
[CreateAssetMenu(fileName = "NewUIStringDataSO", menuName = "Data/New/UIStringDataSO")]
public class NewUIStringDataSO : ScriptableObject
{
    public Dictionary<string, LocalizedText> uiStringData;
}
