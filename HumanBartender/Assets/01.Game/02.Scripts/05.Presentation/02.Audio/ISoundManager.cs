/// <summary>
/// 사운드 매니저 추상화. 소비자(Presenter/Controller 등)는 이 인터페이스에만 의존한다.
/// 테스트 시 가짜(Mock) 구현을 주입할 수 있다.
/// </summary>
public interface ISoundManager
{
    // BG
    void PlayBGM(string name, float fadeDuration = 1f, bool loop = true);
    void StopBGM(float fadeDuration = 1f);
    void PauseBGM();
    void ResumeBGM();

    // SE (동시 재생)
    void PlaySE(string name);

    // Volume (0~1)
    void SetBGMVolume(float v);
    void SetSEVolume(float v);

    // 저장된 볼륨 조회 (옵션 슬라이더 초기값용)
    float GetBGMVolume();
    float GetSEVolume();
}