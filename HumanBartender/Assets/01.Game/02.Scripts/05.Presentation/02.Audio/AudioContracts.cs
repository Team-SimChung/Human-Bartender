/// <summary>단발 효과음만 필요한 소비자의 계약.</summary>
public interface ISoundEffectPlayer
{
    void PlaySE(string name);
}

/// <summary>BGM 재생을 제어하는 소비자의 계약.</summary>
public interface IBgmPlayer
{
    // BG
    void PlayBGM(string name, float fadeDuration = 1f, bool loop = true);
    void StopBGM(float fadeDuration = 1f);
    void PauseBGM();
    void ResumeBGM();

}

/// <summary>옵션 UI에 필요한 사용자 음량 계약.</summary>
public interface IAudioVolumeSettings
{
    // Volume (0~1)
    void SetBGMVolume(float v);
    void SetSEVolume(float v);

    // 저장된 볼륨 조회 (옵션 슬라이더 초기값용)
    float GetBGMVolume();
    float GetSEVolume();
}

/// <summary>재생을 요청한 작업이 자신의 반복 효과음만 종료한다.</summary>
public interface ILoopingSoundManager
{
    System.IDisposable PlayLoop(string name);
}

public interface IAudioLoop : System.IDisposable
{
    void SetGain(float gain);
    void StopImmediately();
}

/// <summary>큐별 음량·피치·페이드. 사용자 음량은 공용 믹서에서 한 번만 적용한다.</summary>
public interface IAudioCuePlayer
{
    void PlayCue(string name, float gain = 1f, float pitch = 1f,
        float fadeIn = 0f, float fadeOut = 0f, bool exclusive = false);
    IAudioLoop StartLoop(string name, float fadeSeconds = 0.12f);
}

public static class AudioCueExtensions
{
    public static void PlayCue(this ISoundEffectPlayer player, string name, float gain = 1f,
        float pitch = 1f, float fadeIn = 0f, float fadeOut = 0f, bool exclusive = false)
    {
        if (player is IAudioCuePlayer cues) cues.PlayCue(name, gain, pitch, fadeIn, fadeOut, exclusive);
        else player?.PlaySE(name);
    }

    public static IAudioLoop StartLoop(this ILoopingSoundManager player, string name, float fadeSeconds = 0.12f)
    {
        if (player is IAudioCuePlayer cues) return cues.StartLoop(name, fadeSeconds);
        var loop = player?.PlayLoop(name);
        return loop == null ? null : new BasicLoop(loop);
    }

    sealed class BasicLoop : IAudioLoop
    {
        readonly System.IDisposable loop;
        public BasicLoop(System.IDisposable loop) => this.loop = loop;
        public void SetGain(float gain) { }
        public void StopImmediately() => loop.Dispose();
        public void Dispose() => loop.Dispose();
    }
}
