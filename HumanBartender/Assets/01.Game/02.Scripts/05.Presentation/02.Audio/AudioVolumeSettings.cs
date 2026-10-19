using UnityEngine;
using VContainer.Unity;

/// <summary>사용자 음량을 믹서에 적용한다. 재생 소스와 개별 클립 음량은 소유하지 않는다.</summary>
public sealed class AudioVolumeSettings : IAudioVolumeSettings, IStartable
{
    readonly AudioOutputSettings output;
    readonly IAudioVolumeStore store;

    public AudioVolumeSettings(AudioOutputSettings output, IAudioVolumeStore store)
    {
        this.output = output;
        this.store = store;
    }

    // AudioMixer.SetFloat이 가능한 Start 시점에 저장된 설정을 적용한다.
    public void Start()
    {
        output.ApplyVolume(AudioBus.Music, GetBGMVolume());
        output.ApplyVolume(AudioBus.SoundEffects, GetSEVolume());
    }

    public float GetBGMVolume() => Mathf.Clamp01(store.Load(AudioBus.Music));
    public float GetSEVolume() => Mathf.Clamp01(store.Load(AudioBus.SoundEffects));
    public void SetBGMVolume(float value) => ApplyAndSave(AudioBus.Music, value);
    public void SetSEVolume(float value) => ApplyAndSave(AudioBus.SoundEffects, value);

    void ApplyAndSave(AudioBus bus, float value)
    {
        value = Mathf.Clamp01(value);
        output.ApplyVolume(bus, value);
        store.Save(bus, value);
    }
}
