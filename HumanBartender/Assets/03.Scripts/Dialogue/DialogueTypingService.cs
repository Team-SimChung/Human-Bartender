using Cysharp.Threading.Tasks;
using System;
using System.Threading;

/// <summary>
/// 기존 호출부의 얇은 호환 경계. 해석은 DialogueTextCompiler, 대사 수명은 말풍선별
/// DialogueTextPlayer가 소유한다.
/// </summary>
public static class DialogueTypingService
{
    /// <summary>
    /// 데이터 별칭과 자리표시자를 TMP 본문으로 변환한다. 재생 계획은 Compile 결과를 사용한다.
    /// </summary>
    public static string ApplyCustomTags(string raw, NewTextTagDataSO textTagData, string cocktailName = null)
    {
        return DialogueTextCompiler.Compile(raw, textTagData,
            DialoguePresentationSettings.Shared, cocktailName).Text;
    }

    /// <summary>
    /// 기존 타입 API를 유지하면서 대상 말풍선의 세션으로 위임한다.
    /// </summary>
    public static async UniTask TypeSentenceTMP(
        TypingData data,
        DynamicSpeechBubble targetBubble,
        NewTextTagDataSO textTagData,
        float typingDelay = 0.025f,
        CancellationToken token = default,
        string cocktailName = null)
    {
        if (targetBubble == null) throw new ArgumentNullException(nameof(targetBubble));
        await targetBubble.TextPlayer.PlayAsync(data, textTagData, typingDelay, token, cocktailName);
    }
}
