using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

/// <summary>
/// 말풍선 타이핑 연출(커스텀 색상 태그 치환, "&lt;숫자&gt;" 지연 태그 파싱, 한 글자씩 표시)을 담당하는 순수 로직.
/// MonoBehaviour에 속하지 않으므로 Play 씬(UIDialogueTextView)과 Outside 씬(OutsideElevatorRadio) 등
/// 어디서든 동일하게 호출해 쓴다.
/// </summary>
public static class DialogueTypingService
{
    /// <summary>
    /// NewTextTagDataSO에 등록된 색 태그(&lt;key&gt;...&lt;/key&gt;)를 TMP의 &lt;color&gt; 태그로 치환하고,
    /// "{cocktail}" 플레이스홀더를 손님별 현재 주문 칵테일 이름으로 치환한다.
    /// cocktailName은 호출부(예: 타이쿤 손님 주문 데이터)가 매번 넘겨줘야 하며, null이면 치환하지 않는다.
    /// </summary>
    public static string ApplyCustomTags(string raw, NewTextTagDataSO textTagData, string cocktailName = null)
    {
        string result = raw;

        if (cocktailName != null)
            result = result.Replace("{cocktail}", cocktailName);

        if (textTagData == null) return result;

        foreach (KeyValuePair<string, string> tag in textTagData.ColorTags())
        {
            // <key>...</key> 매칭 (내용은 비탐욕적으로)
            string pattern = $@"<{Regex.Escape(tag.Key)}>(.*?)</{Regex.Escape(tag.Key)}>";
            string replacement = $"<color={tag.Value}>$1</color>";

            result = Regex.Replace(result, pattern, replacement);
        }

        return result;
    }

    /// <summary>
    /// 말풍선(targetBubble)에 한 글자씩 순차 표시(타이핑 효과)한다. 문장 안의 "&lt;숫자&gt;" 태그는 해당 위치에서
    /// 지정 시간(ms)만큼 추가 딜레이를 주는 용도로 파싱되어 제거된다.
    /// </summary>
    public static async UniTask TypeSentenceTMP(
        TypingData data,
        DynamicSpeechBubble targetBubble,
        NewTextTagDataSO textTagData,
        float typingDelay = 0.025f,
        CancellationToken token = default,
        string cocktailName = null)
    {
        string rawSentence = data.str;
        if (!(rawSentence.Length > 0)) return;

        targetBubble.gameObject.SetActive(true);
        targetBubble.nameLabel.text = data.speaker;
        targetBubble.nameLabel.color = data.nameColor;

        string processed = ApplyCustomTags(rawSentence, textTagData, cocktailName);

        string cleanSentence = processed;
        Dictionary<int, float> delayDict = new Dictionary<int, float>();
        Regex tagRegex = new Regex(@"<(\d+)>");
        MatchCollection matches = tagRegex.Matches(processed);

        int offset = 0;
        foreach (Match match in matches)
        {
            int delayMs = int.Parse(match.Groups[1].Value);
            int targetIndex = match.Index - offset;
            delayDict[targetIndex] = delayMs / 1000f;
            cleanSentence = cleanSentence.Remove(targetIndex, match.Length);
            offset += match.Length;
        }

        if (targetBubble != null)
        {
            targetBubble.PrepareForText(cleanSentence);
        }

        targetBubble.textLabel.maxVisibleCharacters = 0;
        int totalVisibleChars = targetBubble.TotalVisibleCharacters;

        try
        {
            for (int i = 0; i <= totalVisibleChars; i++)
            {
                targetBubble.textLabel.maxVisibleCharacters = i;
                targetBubble.UpdateForVisible(i);

                if (delayDict.ContainsKey(i))
                    await UniTask.Delay(TimeSpan.FromSeconds(delayDict[i]), cancellationToken: token);

                if (i < totalVisibleChars)
                    await UniTask.Delay(TimeSpan.FromSeconds(typingDelay), cancellationToken: token);
            }
        }
        catch (Exception)
        {
            targetBubble.textLabel.maxVisibleCharacters = totalVisibleChars;
            targetBubble.UpdateForVisible(totalVisibleChars);
        }
    }
}
