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
