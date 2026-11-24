using UnityEngine;

/// <summary>
/// 쉐이킹 V2 소리. 웹 session-audio.js처럼 소리 하나만 낸다 — 새 소리가 나면 이전 소리를 끊는다.
///
/// 병따기(CapOpenAudio)처럼 공용 SoundManager 큐 대신 프리팹의 AudioSource를 쓴다. 일시정지·기믹 종료 때
/// 즉시 끊어야 하는데 공용 큐는 재생한 소리를 돌려주지 않기 때문이다. 출력은 공용 효과음 버스로 보내므로
/// 사용자 효과음 음량·음소거는 그대로 적용된다.
/// </summary>
public sealed class ShakeV2Audio : MonoBehaviour
{
    [Tooltip("소리를 낼 AudioSource. 프리팹 루트에 둔다.")]
    [SerializeField] AudioSource source;
    [Tooltip("결합·정입력·파손 순간의 타격음. 웹 shaker_02_hit.wav와 같은 파일(shaker_hit.wav).")]
    [SerializeField] AudioClip hitClip;
    [Tooltip("정상 분리를 시작할 때의 완료음(shaker_complete.wav). 파손 때는 내지 않는다.")]
    [SerializeField] AudioClip completeClip;

    [Header("Level")]
    [Tooltip("타격음 음량. 웹: 효과음 기본 70에서 재생 게인 0.416.")]
    [SerializeField, Range(0f, 1f)] float hitGain = .416f;
    [Tooltip("완료음 음량. 웹: 0.85.")]
    [SerializeField, Range(0f, 1f)] float completeGain = .85f;

    void Awake()
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioOutputSettings.Shared?.Route(source, AudioBus.SoundEffects);
    }

    public void PlayHit() => Play(hitClip, hitGain);
    public void PlayComplete() => Play(completeClip, completeGain);

    /// <summary>일시정지·기믹 종료·재시작 때 소리를 끊는다.</summary>
    public void StopAll()
    {
        if (source != null) source.Stop();
    }

    void Play(AudioClip clip, float gain)
    {
        if (source == null || clip == null) return;
        source.Stop();
        source.clip = clip;
        source.volume = gain;
        source.Play();
    }

    void OnDisable() => StopAll();
}
