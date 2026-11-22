using UnityEngine;

/// <summary>
/// 병따기 소리. 웹 본편 open-view.js의 Web Audio 구성을 그대로 옮겼다.
///
/// 공용 SoundManager의 큐를 쓰지 않고 자기 AudioSource를 둔다. 탄산 꼬리는 0.45초 늦게 시작해
/// 정해진 곡선으로 줄어들어야 하고, 일시정지·기믹 종료 때 모든 소리를 즉시 끊어야 하는데,
/// 공용 큐는 재생한 뒤 손잡이를 돌려주지 않아 둘 다 할 수 없다. 출력은 공용 효과음 버스로 보내므로
/// 사용자 효과음 음량은 그대로 적용된다.
///
/// 웹의 highshelf 필터(2200Hz, +1.5dB)는 옮기지 않았다. AudioSource 하나로는 걸 수 없고 차이가 작다.
/// </summary>
public sealed class CapOpenAudio : MonoBehaviour
{
    [Tooltip("뚜껑이 열리는 소리. 웹 beercan_open.wav와 같은 파일.")]
    [SerializeField] AudioClip openClip;
    [Tooltip("탄산 꼬리. 웹 beer_fizz_tail.wav와 같은 파일.")]
    [SerializeField] AudioClip fizzClip;

    [Header("Level")]
    [Tooltip("뚜껑 소리 음량. 웹 level.open(0.881) × 0.88.")]
    [SerializeField, Range(0f, 1f)] float openGain = .775f;
    [Tooltip("탄산 꼬리의 최대 음량. 웹 level.openFizz.")]
    [SerializeField, Range(0f, 1f)] float fizzGain = .14f;
    [Tooltip("실패음 전체 음량. 웹 실패음 버스.")]
    [SerializeField, Range(0f, 1f)] float failGain = .35f;

    const float FizzDelay = .45f, FizzRise = .4f, FizzHold = 2.2f;

    AudioSource openSource, fizzSource, failSource;
    AudioClip failClip;
    double fizzStartDsp = -1;

    void Awake()
    {
        openSource = CreateSource("Open");
        fizzSource = CreateSource("Fizz");
        failSource = CreateSource("Fail");
        failClip = CreateFailClip(AudioSettings.outputSampleRate);
    }

    AudioSource CreateSource(string label)
    {
        var go = new GameObject("Cap Audio " + label);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        AudioOutputSettings.Shared?.Route(source, AudioBus.SoundEffects);
        return source;
    }

    /// <summary>판정 한 번에 맞는 소리를 낸다. 성공이면 뚜껑 소리와 탄산 꼬리, 실패면 합성 실패음이다.</summary>
    public void Play(bool success, bool perfect)
    {
        if (!success)
        {
            failSource.PlayOneShot(failClip, failGain);
            return;
        }

        StopAll();
        if (openClip != null)
        {
            openSource.clip = openClip;
            openSource.pitch = perfect ? 1.16f : 1.08f;
            openSource.volume = openGain;
            openSource.Play();
        }

        if (fizzClip != null)
        {
            // 뚜껑 소리가 먼저 나오고, 탄산은 뒤늦게 조용히 올라온다.
            fizzSource.clip = fizzClip;
            fizzSource.pitch = 1f;
            fizzSource.volume = 0f;
            fizzStartDsp = AudioSettings.dspTime + FizzDelay;
            fizzSource.PlayScheduled(fizzStartDsp);
        }
    }

    /// <summary>일시정지·기믹 종료 때 모든 소리를 끊는다. 웹 stopSound와 같다.</summary>
    public void StopAll()
    {
        if (openSource != null) openSource.Stop();
        if (fizzSource != null) fizzSource.Stop();
        if (failSource != null) failSource.Stop();
        fizzStartDsp = -1;
    }

    void Update()
    {
        if (fizzStartDsp < 0 || fizzSource == null || fizzClip == null) return;
        float t = (float)(AudioSettings.dspTime - fizzStartDsp);
        float length = fizzClip.length;
        // 웹 곡선: 0 → (0.4초) 최대 → (2.2초) 최대의 80% → (끝) 0, 모두 직선 보간.
        float level = t < 0f ? 0f
            : t < FizzRise ? Mathf.Lerp(0f, fizzGain, t / FizzRise)
            : t < FizzHold ? Mathf.Lerp(fizzGain, fizzGain * .8f, (t - FizzRise) / (FizzHold - FizzRise))
            : Mathf.Lerp(fizzGain * .8f, 0f, (t - FizzHold) / Mathf.Max(.01f, length - FizzHold));
        fizzSource.volume = level;
        if (t >= length) fizzStartDsp = -1;
    }

    /// <summary>
    /// 웹 실패음을 미리 합성해 둔다: 0.12초 감쇠 노이즈(2800Hz 하이패스) + 480→140Hz로 내려가는 삼각파.
    /// 버스 음량(failGain)은 재생할 때 곱한다.
    /// </summary>
    public static AudioClip CreateFailClip(int sampleRate)
    {
        sampleRate = Mathf.Max(8000, sampleRate);
        int length = Mathf.CeilToInt(sampleRate * .23f);
        var data = new float[length];

        // 노이즈: (rand·2 − 1)·e^(−12·i/N), 1차 RC 하이패스 2800Hz.
        int noiseLength = Mathf.CeilToInt(sampleRate * .12f);
        var random = new System.Random(2800);
        float rc = 1f / (2f * Mathf.PI * 2800f), dt = 1f / sampleRate, alpha = rc / (rc + dt);
        float previousIn = 0f, previousOut = 0f;
        for (int i = 0; i < noiseLength; i++)
        {
            float input = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-i / (float)noiseLength * 12f);
            float output = alpha * (previousOut + input - previousIn);
            previousIn = input; previousOut = output;
            data[i] += output;
        }

        // 핑: 주파수 480 → 140Hz(0.16초 지수), 음량 0.13 → 0.0001(0.22초 지수), 0.23초에 정지.
        double phase = 0;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float frequency = t < .16f ? 480f * Mathf.Pow(140f / 480f, t / .16f) : 140f;
            float envelope = t < .22f ? .13f * Mathf.Pow(.0001f / .13f, t / .22f) : .0001f;
            phase += frequency / sampleRate;
            float p = (float)(phase - System.Math.Floor(phase));
            float triangle = 4f * Mathf.Abs(p - .5f) - 1f;
            data[i] += triangle * envelope;
        }

        var clip = AudioClip.Create("CapFail", length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDisable() => StopAll();

    void OnDestroy()
    {
        if (failClip != null) Destroy(failClip);
    }
}
