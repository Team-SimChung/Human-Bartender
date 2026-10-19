using System;
using UnityEngine;

/// <summary>겹치는 대사·컷씬의 수명을 세어 종료·스킵·취소 시 원래 음량으로 복구한다.</summary>
public static class GameAudioContext
{
    static int direct, background, cinematics, mutedMusic;
    public static bool DirectDialogue => direct > 0;
    public static bool AnyDialogue => direct > 0 || background > 0;
    public static bool Cinematic => cinematics > 0;
    public static bool MusicSuppressed => mutedMusic > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { direct = background = cinematics = mutedMusic = 0; }

    public static IDisposable Dialogue(bool ambient = false)
    {
        if (ambient) background++; else direct++;
        return new Lease(() => { if (ambient) background = Mathf.Max(0, background - 1); else direct = Mathf.Max(0, direct - 1); });
    }

    public static IDisposable Cutscene(bool suppressMusic = true)
    {
        cinematics++;
        if (suppressMusic) mutedMusic++;
        return new Lease(() =>
        {
            cinematics = Mathf.Max(0, cinematics - 1);
            if (suppressMusic) mutedMusic = Mathf.Max(0, mutedMusic - 1);
        });
    }

    sealed class Lease : IDisposable
    {
        Action release;
        public Lease(Action release) => this.release = release;
        public void Dispose() { var action = release; release = null; action?.Invoke(); }
    }
}

public static class GameAudioRules
{
    public static string MusicGroup(string scene) => scene switch
    {
        "Home" => "home", "OutSide" => "street", "Play" => "bar", _ => null
    };

    public static bool IsRainy(int day, EGameFlow route) =>
        ((day == 0 || day == 1) && route == EGameFlow.CommuteOut) ||
        (day == 2 && route == EGameFlow.CommuteIn);

    public static float AmbienceGain(bool dialogue, bool paused) => paused ? .45f : dialogue ? .65f : 1f;
    public static float PourGain(float flowRatio) => Mathf.Sqrt(Mathf.Min(1.4f, Mathf.Max(0f, flowRatio)));
    public static bool SuppressesMusic(string id) => !string.IsNullOrEmpty(id) &&
        (id.IndexOf("dream", StringComparison.OrdinalIgnoreCase) >= 0 ||
         id.IndexOf("intro", StringComparison.OrdinalIgnoreCase) >= 0 ||
         id.IndexOf("tv", StringComparison.OrdinalIgnoreCase) >= 0 ||
         id.IndexOf("bd", StringComparison.OrdinalIgnoreCase) >= 0 ||
         id.IndexOf("memory", StringComparison.OrdinalIgnoreCase) >= 0);
}
