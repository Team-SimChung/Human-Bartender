using System;
using UnityEngine;

/// <summary>음량 저장 계약. 출력·UI 소비자는 이 저장소에 의존하지 않는다.</summary>
public interface IAudioVolumeStore
{
    float Load(AudioBus bus);
    void Save(AudioBus bus, float value);
    bool LoadMuted(AudioBus bus);
    void SaveMuted(AudioBus bus, bool muted);
}

/// <summary>기존 저장 키와 기본값을 유지하는 사용자 음량 저장소.</summary>
public sealed class PlayerPrefsAudioVolumeStore : IAudioVolumeStore
{

    static string Key(AudioBus bus) => bus switch
    {
        AudioBus.Music => "vol_bgm",
        AudioBus.SoundEffects => "vol_se",
        _ => throw new ArgumentOutOfRangeException(nameof(bus))
    };

    public float Load(AudioBus bus) => PlayerPrefs.GetFloat(Key(bus), bus == AudioBus.Music ? .30f : .80f);

    public bool LoadMuted(AudioBus bus) => PlayerPrefs.GetInt(Key(bus) + "_muted", 0) == 1;

    public void SaveMuted(AudioBus bus, bool muted)
    {
        PlayerPrefs.SetInt(Key(bus) + "_muted", muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void Save(AudioBus bus, float value)
    {
        PlayerPrefs.SetFloat(Key(bus), Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }
}
