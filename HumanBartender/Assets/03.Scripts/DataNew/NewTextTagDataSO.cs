using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// json/text_tags.json의 항목 하나. 태그 이름 -> 이 값의 딕셔너리로 읽는다.
///
/// value의 타입이 kind에 따라 달라진다(color는 "#RRGGBB", speed_ms·size_pct는 숫자)라서 object로 받는다.
/// 문자열로 못 박으면 숫자 항목에서 터지고, 그 예외는 뒤따르는 파일 로드를 전부 막는다.
/// </summary>
[Serializable]
public struct NewTextTagData
{
    [JsonProperty("kind")] public string Kind { get; set; }
    [JsonProperty("value")] public object Value { get; set; }
}

/// <summary>
/// StreamingAssets/json/text_tags.json을 보유하는 ScriptableObject.
/// 루트가 태그 이름 -> 태그 정의의 딕셔너리다.
///
/// 지금 쓰는 것은 색 태그뿐이다. speed_ms(타이핑 속도)와 size_pct(글자 크기)도 파일에 있지만
/// 읽는 코드가 아직 없어서 ColorTags가 걸러 낸다 — 색이 아닌 값을 색으로 칠하면 대사에 엉뚱한
/// 색이 들어간다.
/// </summary>
[CreateAssetMenu(fileName = "NewTextTagDataSO", menuName = "Data/New/TextTagDataSO")]
public class NewTextTagDataSO : ScriptableObject
{
    public Dictionary<string, NewTextTagData> textTagData;

    /// <summary>
    /// 색 태그만 (태그 이름, #RRGGBB) 짝으로 훑는다.
    ///
    /// 데이터가 "#" 없이 적혀 있어도 받아 준다. TMP의 &lt;color&gt;는 "#"이 없으면 색 이름으로
    /// 읽으려 하고, 못 읽으면 태그째 글자로 찍힌다.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> ColorTags()
    {
        if (textTagData == null) yield break;

        foreach (KeyValuePair<string, NewTextTagData> tag in textTagData)
        {
            if (tag.Value.Kind != "color") continue;

            string hex = tag.Value.Value?.ToString();
            if (string.IsNullOrEmpty(hex)) continue;

            if (!hex.StartsWith("#")) hex = "#" + hex;

            yield return new KeyValuePair<string, string>(tag.Key, hex);
        }
    }
}
