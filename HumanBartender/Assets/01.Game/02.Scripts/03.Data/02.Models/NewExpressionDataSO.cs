using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public struct NewExpressionPartClip
{
    [field: SerializeField][JsonProperty("clip")] public string Clip { get; set; }
    [field: SerializeField][JsonProperty("loop")] public ENewAnimLoopMode Loop { get; set; }
}

[Serializable]
public struct NewExpressionEntry
{
    [field: SerializeField][JsonProperty("mode")] public ENewExpressionMode Mode { get; set; }
    [field: SerializeField][JsonProperty("sprite")] public string Sprite { get; set; }
    [field: SerializeField][JsonProperty("talk_anim")] public bool TalkAnim { get; set; }
    [JsonProperty("parts")] public Dictionary<string, NewExpressionPartClip> Parts { get; set; }
}

/// <summary>
/// StreamingAssets/json/expressions.json을 보유하는 ScriptableObject.
/// 루트가 캐릭터 id -> 표정 이름 -> 표정 데이터의 2단 딕셔너리 구조다.
/// </summary>
[CreateAssetMenu(fileName = "NewExpressionDataSO", menuName = "Data/New/ExpressionDataSO")]
public class NewExpressionDataSO : ScriptableObject
{
    public Dictionary<string, Dictionary<string, NewExpressionEntry>> expressionData;

    /// <summary>표정이 파츠 조립이 아니라 통짜 그림 하나인지.</summary>
    public bool IsPortraitSprite(string characterId, string expression)
        => TryGetEntry(characterId, expression, out NewExpressionEntry entry)
           && entry.Mode == ENewExpressionMode.Sprite;

    /// <summary>통짜 그림 표정의 스프라이트 경로. 없으면 null.</summary>
    public string GetSpritePath(string characterId, string expression)
        => TryGetEntry(characterId, expression, out NewExpressionEntry entry) ? entry.Sprite : null;

    /// <summary>
    /// 그 표정에서 한 부위가 쓸 클립. 그 부위가 표정에 안 적혀 있으면 null이다.
    ///
    /// 부르는 쪽(CharacterLoader)이 null을 받으면 기본 표정 것으로 물러서므로, 없는 부위를 빈
    /// 클립으로 메우지 않는다 — 그러면 눈이 감긴 채로 굳는 식으로 조용히 어긋난다.
    /// </summary>
    public PartAnimData GetPartData(string characterId, string expression, EAnimationPart part)
    {
        if (!TryGetEntry(characterId, expression, out NewExpressionEntry entry)) return null;
        if (entry.Parts == null) return null;

        string key = PartKey(part);
        if (key == null) return null;
        if (!entry.Parts.TryGetValue(key, out NewExpressionPartClip clip)) return null;

        return new PartAnimData { Clip = clip.Clip, Loop = ToLoopMode(clip.Loop) };
    }

    /// <summary>기본 표정("default")에서 그 부위가 쓸 클립.</summary>
    public PartAnimData GetDefaultPartData(string characterId, EAnimationPart part)
        => GetPartData(characterId, "default", part);

    /// <summary>
    /// 캐릭터 + 표정으로 항목 하나를 찾는다. 그 표정이 없으면 "default"로 물러선다.
    ///
    /// 물러설 때 경고를 남기는 이유는, 없는 표정을 부르는 것이 보통 대본 오타이기 때문이다.
    /// 조용히 default를 쓰면 인물이 계속 무표정인데 왜 그런지는 알 수 없다.
    /// </summary>
    bool TryGetEntry(string characterId, string expression, out NewExpressionEntry entry)
    {
        entry = default;

        if (expressionData == null) return false;

        if (!expressionData.TryGetValue(characterId, out Dictionary<string, NewExpressionEntry> byExpression)
            || byExpression == null)
        {
            Logger.LogWarning($"[Expression] 캐릭터 없음: {characterId}");
            return false;
        }

        if (byExpression.TryGetValue(expression, out entry)) return true;

        Logger.LogWarning($"[Expression] '{characterId}'에 '{expression}' 없음 → default 사용");

        return byExpression.TryGetValue("default", out entry);
    }

    /// <summary>리그의 부위 이름과 json의 parts 키를 잇는다.</summary>
    static string PartKey(EAnimationPart part) => part switch
    {
        EAnimationPart.Eyes => "eyes",
        EAnimationPart.Eyeblows => "eyebrows",
        EAnimationPart.Upper_Face => "upper_face",
        EAnimationPart.Lower_Face => "lower_face",
        EAnimationPart.Body => "body",
        EAnimationPart.Extra => "extra",
        EAnimationPart.Etc => "etc",
        _ => null,
    };

    /// <summary>
    /// json의 반복 표기를 리그가 쓰는 것으로 옮긴다.
    ///
    /// on_dialogue와 special_on_dialogue가 같은 곳으로 간다. 리그에는 그 둘을 가르는 자리가 없고,
    /// 없는 구분을 지어내는 것보다 한쪽으로 모으는 편이 낫다.
    /// </summary>
    static EAnimLoopMode ToLoopMode(ENewAnimLoopMode loop) => loop switch
    {
        ENewAnimLoopMode.Always => EAnimLoopMode.Always,
        ENewAnimLoopMode.AlwaysOnDialogue => EAnimLoopMode.Always_OnDialogue,
        ENewAnimLoopMode.SpecialOnDialogue => EAnimLoopMode.Special_OnDialogue,
        ENewAnimLoopMode.OnDialogue => EAnimLoopMode.Special_OnDialogue,
        _ => EAnimLoopMode.None,
    };
}
