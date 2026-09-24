using UnityEngine;

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

    static DialoguePresentationSettings shared;
    public static DialoguePresentationSettings Shared => shared != null
        ? shared : shared = Resources.Load<DialoguePresentationSettings>("DialoguePresentationSettings");
}
