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
    [Min(0)] public float ShakeAmplitude = 1.5f;
    [Min(0)] public float ShakeFrequency = 18f;
    [Min(0)] public float WaveAmplitude = 2f;
    [Min(0)] public float WaveFrequency = 2f;
    [Min(0)] public float WavePhase = 0.65f;
    [Min(1)] public float PopPeak = 1.2f;
    [Min(0.001f)] public float PopDuration = 0.12f;
    public bool ReducedMotion;
    [Tooltip("Optional registered SoundManager SE key. Empty means silent typing.")]
    public string TypingSoundKey;
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
