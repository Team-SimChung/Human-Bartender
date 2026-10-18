using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>저장된 컷신은 기본값 0을 효과음으로 사용한다.</summary>
public enum AudioBus { SoundEffects = 0, Music = 1 }

/// <summary>모든 런타임 출력의 믹서 그룹과 음량 파라미터를 정의하는 공용 설정.</summary>
[CreateAssetMenu(fileName = "AudioOutputSettings", menuName = "Audio/Output Settings")]
public sealed class AudioOutputSettings : ScriptableObject
{
    [SerializeField] AudioMixerGroup effectsGroup;
    [SerializeField] AudioMixerGroup musicGroup;

    public const string EffectsVolumeParameter = "SEVolume";
    public const string MusicVolumeParameter = "BGMVolume";

    static AudioOutputSettings shared;
    public static AudioOutputSettings Shared
    {
        get
        {
            if (shared == null) shared = Resources.Load<AudioOutputSettings>("AudioOutputSettings");
            if (shared == null) throw new InvalidOperationException("Resources/AudioOutputSettings가 없습니다.");
            return shared;
        }
    }

    public AudioMixerGroup GetGroup(AudioBus bus)
    {
        var group = bus switch
        {
            AudioBus.SoundEffects => effectsGroup,
            AudioBus.Music => musicGroup,
            _ => throw new ArgumentOutOfRangeException(nameof(bus))
        };
        if (group == null) throw new InvalidOperationException($"{bus} 오디오 믹서 그룹이 없습니다.");
        return group;
    }

    public static string VolumeParameter(AudioBus bus) => bus switch
    {
        AudioBus.SoundEffects => EffectsVolumeParameter,
        AudioBus.Music => MusicVolumeParameter,
        _ => throw new ArgumentOutOfRangeException(nameof(bus))
    };

    public void Route(AudioSource source, AudioBus bus)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.outputAudioMixerGroup = GetGroup(bus);
    }

    public void ApplyVolume(AudioBus bus, float linear)
    {
        float value = Mathf.Clamp01(linear);
        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        if (!GetGroup(bus).audioMixer.SetFloat(VolumeParameter(bus), db))
            throw new InvalidOperationException($"{bus} 음량 파라미터를 적용하지 못했습니다.");
    }
}
