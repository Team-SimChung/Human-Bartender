using System;
using UnityEngine;

/// <summary>
/// 설정의 '효과' 탭 값. PlayerPrefs에 저장되며, 씬의 효과 컴포넌트(웅덩이 반사, 캐릭터 림라이트, 빛줄기)가
/// Changed를 구독해 즉시 켜고 끈다. 블룸 세기는 DisplaySettings가 런타임 볼륨으로 반영한다.
/// </summary>
public static class GraphicsEffectOptions
{
    public enum BloomLevel { Off, Low, Normal, High }
    public static readonly string[] BloomLabels = { "끄기", "약하게", "보통", "강하게" };
    public static readonly string[] Keys = { "EffectBloom", "EffectLightShafts", "EffectCharacterLighting", "EffectPuddleReflections" };

    [Serializable]
    public struct Values
    {
        public BloomLevel Bloom;
        public bool LightShafts, CharacterLighting, PuddleReflections;
        public static Values Default => new Values { Bloom = BloomLevel.Normal, LightShafts = true, CharacterLighting = true, PuddleReflections = true };
    }

    public static event Action Changed;
    public static Values Current => new Values
    {
        Bloom = (BloomLevel)Mathf.Clamp(PlayerPrefs.GetInt(Keys[0], (int)BloomLevel.Normal), 0, BloomLabels.Length - 1),
        LightShafts = PlayerPrefs.GetInt(Keys[1], 1) == 1,
        CharacterLighting = PlayerPrefs.GetInt(Keys[2], 1) == 1,
        PuddleReflections = PlayerPrefs.GetInt(Keys[3], 1) == 1,
    };
    public static bool LightShafts => Current.LightShafts;
    public static bool CharacterLighting => Current.CharacterLighting;
    public static bool PuddleReflections => Current.PuddleReflections;

    public static void Save(Values values)
    {
        PlayerPrefs.SetInt(Keys[0], (int)values.Bloom);
        PlayerPrefs.SetInt(Keys[1], values.LightShafts ? 1 : 0);
        PlayerPrefs.SetInt(Keys[2], values.CharacterLighting ? 1 : 0);
        PlayerPrefs.SetInt(Keys[3], values.PuddleReflections ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>씬에 배치된 블룸 세기에 곱할 배율. 보통은 원래 값 그대로다.</summary>
    public static float BloomScale(BloomLevel level) => level switch
    {
        BloomLevel.Off => 0,
        BloomLevel.Low => .5f,
        BloomLevel.High => 1.7f,
        _ => 1,
    };
}
