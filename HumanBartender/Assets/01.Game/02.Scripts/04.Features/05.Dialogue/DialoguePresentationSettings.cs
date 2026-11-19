using System;
using UnityEngine;

public enum DialogueFxMotion { None, Shake, Wave }

[Serializable]
public sealed class DialogueFxPreset
{
    public string Id;
    public DialogueFxMotion Motion;
    [Min(1)] public float SizePercent = 100;
    [Tooltip("Optional #RRGGBB color. Leave empty to inherit the dialogue color.")]
    public string ColorHex;
    public bool OverrideSpeed;
    [Min(0)] public float TypingIntervalMs;
    [Min(0)] public float PauseBeforeMs;
    [Min(0)] public float PauseAfterMs;
    [Min(0)] public float Amplitude;
    [Min(0)] public float Frequency;
}

/// <summary>Shared defaults for inline vertex effects. CSV remains the owner of legacy aliases.</summary>
[CreateAssetMenu(fileName = "DialoguePresentationSettings", menuName = "Dialogue/Presentation Settings")]
public sealed class DialoguePresentationSettings : ScriptableObject
{
    [Min(0)] public float ShakeAmplitude = 0.7f;
    [Min(0)] public float ShakeFrequency = 18f;
    [Min(0)] public float WaveAmplitude = 2f;
    [Min(0)] public float WaveFrequency = 2f;
    [Min(0)] public float WavePhase = 0.65f;
    [Min(1)] public float PopPeak = 1.2f;
    [Min(0.001f)] public float PopDuration = 0.12f;
    public bool ReducedMotion;
    [Tooltip("Optional registered SoundManager SE key. Empty means silent typing.")]
    public string TypingSoundKey;
    [Min(0f)] public float TypingSoundInterval = 0.06f;
    [Header("Exterior Period Rhythm")]
    [Tooltip("거리·집·테라스 대사에서 단독 마침표 다음 글자(또는 줄 끝)까지 기다리는 시간(초). 웹 dialogue-rhythm.js 기준 0.35. 바 내부 대사에는 적용하지 않는다.")]
    [Min(0f)] public float LonePeriodPause = 0.35f;
    [Tooltip("'...'처럼 이어진 마침표 사이의 간격(초). 웹 기준 0.3. 마지막 점 다음은 평소 속도로 이어진다.")]
    [Min(0f)] public float ConsecutivePeriodInterval = 0.3f;
    [Tooltip("SE keys for accepted dialogue input and choice interactions. Empty disables that cue.")]
    public string AdvanceSoundKey;
    public string ChoiceHoverSoundKey;
    public string ChoiceConfirmSoundKey;
    [Tooltip("Reusable effects referenced by <fx=id>...</fx> in dialogue CSV.")]
    public DialogueFxPreset[] Presets;

    public DialogueFxPreset GetPreset(string id)
    {
        DialogueFxPreset found = null;
        if (Presets != null)
            foreach (var preset in Presets)
                if (preset != null && preset.Id == id)
                {
                    if (found != null) throw new ArgumentException($"Duplicate dialogue FX preset '{id}'.");
                    found = preset;
                }
        if (found == null) throw new ArgumentException($"Unknown dialogue FX preset '{id}'.");
        return found;
    }

    static DialoguePresentationSettings shared;
    public static DialoguePresentationSettings Shared => shared != null
        ? shared : shared = Resources.Load<DialoguePresentationSettings>("DialoguePresentationSettings");
}
