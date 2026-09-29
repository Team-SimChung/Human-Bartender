using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
}

/// <summary>
/// BG / SE 2채널 사운드 매니저.
/// static Instance 없음 — VContainer 가 Lifetime.Singleton 으로 단일 인스턴스를 보장하고
/// ISoundManager 로 주입한다.
/// </summary>
public class SoundManager : MonoBehaviour, ISoundManager
{
    [Header("Audio Mixer (그룹: Master/BGM/SE)")]
    [SerializeField] private AudioMixer mixer;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource; // BG 전용 (Loop)

    [Header("SE Pool")]
    [Min(1)][SerializeField] private int sePoolSize = 12;
    private readonly List<AudioSource> sePool = new();
    private int seCursor;

    [Header("Sound Lists")]
    [SerializeField] private List<Sound> bgmList = new();
    [SerializeField] private List<Sound> seList = new();

    private Dictionary<string, Sound> _bgm;
    private Dictionary<string, Sound> _se;

    private const string MIXER_BGM = "BGMVolume";
    private const string MIXER_SE = "SEVolume";


    private const string PREF_BGM = "vol_bgm";
    private const string PREF_SE = "vol_se";
    private const float DEFAULT_VOLUME = 0.5f;

    private Coroutine _bgmFadeRoutine;

    // VContainer 가 프리팹을 Instantiate 하면 Awake 가 호출된다.
    private void Awake()
    {
        BuildDictionaries();
        BuildSePool();
    }

    // AudioMixer.SetFloat 은 Awake 프레임에 무시될 수 있어 Start 에서 로드한다.
    private void Start()
    {
        LoadVolumes();
    }

    private void BuildDictionaries()
    {
        _bgm = new Dictionary<string, Sound>();
        _se = new Dictionary<string, Sound>();

        IndexSounds(bgmList, _bgm);
        IndexSounds(seList, _se);
    }

    static void IndexSounds(List<Sound> sounds, Dictionary<string, Sound> index)
    {
        var duplicates = new HashSet<string>();
        foreach (var sound in sounds)
        {
            if (sound == null || string.IsNullOrWhiteSpace(sound.name)) continue;
            if (duplicates.Contains(sound.name)) continue;
            if (!index.TryAdd(sound.name, sound))
            {
                index.Remove(sound.name);
                duplicates.Add(sound.name);
                Debug.LogError($"[SoundManager] 중복된 사운드 이름은 재생하지 않습니다: {sound.name}");
            }
        }
    }

    void OnDisable()
    {
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;
        if (bgmSource != null) bgmSource.Stop();
        foreach (var source in sePool) if (source != null) source.Stop();
    }

    private void BuildSePool()
    {
        var seGroup = mixer != null ? mixer.FindMatchingGroups("SE") : null;
        for (int i = 0; i < Mathf.Max(1, sePoolSize); i++)
        {
            var go = new GameObject($"SE_Source_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            if (seGroup != null && seGroup.Length > 0) src.outputAudioMixerGroup = seGroup[0];
            sePool.Add(src);
        }
    }

    // ─── BG ─────────────────────────────────────────
    public void PlayBGM(string name, float fadeDuration = 1f, bool loop = true)
    {
        if (!isActiveAndEnabled) return;
        if (bgmSource == null || string.IsNullOrEmpty(name) || !_bgm.TryGetValue(name, out var sound) || sound.clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM '{name}' 없음");
            return;
        }

        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;

        if (fadeDuration <= 0f)
        {
            bgmSource.clip = sound.clip;
            bgmSource.volume = sound.volume;
            bgmSource.pitch = sound.pitch;
            bgmSource.loop = loop;
            bgmSource.Play();
        }
        else
        {
            _bgmFadeRoutine = StartCoroutine(CrossfadeBGM(sound, fadeDuration, loop));
        }
    }

    private IEnumerator CrossfadeBGM(Sound next, float duration, bool loop)
    {
        float half = duration * 0.5f;

        if (bgmSource.isPlaying)
        {
            float startVol = bgmSource.volume;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / half);
                yield return null;
            }
        }

        bgmSource.clip = next.clip;
        bgmSource.pitch = next.pitch;
        bgmSource.loop = loop;
        bgmSource.volume = 0f;
        bgmSource.Play();

        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, next.volume, t / half);
            yield return null;
        }
        bgmSource.volume = next.volume;
        _bgmFadeRoutine = null;
    }

    public void StopBGM(float fadeDuration = 1f)
    {
        if (bgmSource == null) return;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;
        if (!isActiveAndEnabled || fadeDuration <= 0f) { bgmSource.Stop(); return; }
        _bgmFadeRoutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    private IEnumerator FadeOutAndStop(float duration)
    {
        float startVol = bgmSource.volume;
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        bgmSource.Stop();
        _bgmFadeRoutine = null;
    }

    public void PauseBGM()
    {
        if (bgmSource != null) bgmSource.Pause();
    }

    public void ResumeBGM() { if (bgmSource != null) bgmSource.UnPause(); }

    // ─── SE (동시 재생) ─────────────────────────────
    public void PlaySE(string name)
    {
        if (!isActiveAndEnabled) return;
        if (string.IsNullOrEmpty(name) || !_se.TryGetValue(name, out var sound) || sound.clip == null)
        {
            Debug.LogWarning($"[SoundManager] SE '{name}' 없음");
            return;
        }

        AudioSource src = GetFreeSeSource();
        src.pitch = sound.pitch;
        src.PlayOneShot(sound.clip, sound.volume);
    }

    private AudioSource GetFreeSeSource()
    {
        foreach (var src in sePool)
            if (!src.isPlaying) return src;

        var reuse = sePool[seCursor];
        seCursor = (seCursor + 1) % sePool.Count;
        return reuse;
    }

    // ─── Volume ─────────────────────────────────────
    public void SetBGMVolume(float v) => ApplyAndSave(MIXER_BGM, PREF_BGM, v);
    public void SetSEVolume(float v) => ApplyAndSave(MIXER_SE, PREF_SE, v);

    private void ApplyAndSave(string mixerParam, string prefKey, float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);
        SetMixerVolume(mixerParam, linear01);
        PlayerPrefs.SetFloat(prefKey, linear01);
        PlayerPrefs.Save();
    }

    private void SetMixerVolume(string param, float linear01)
    {
        if (mixer == null) return;
        float dB = linear01 <= 0.0001f ? -80f : Mathf.Log10(linear01) * 20f;
        mixer.SetFloat(param, dB);
    }

    private void LoadVolumes()
    {
        SetMixerVolume(MIXER_BGM, PlayerPrefs.GetFloat(PREF_BGM, DEFAULT_VOLUME));
        SetMixerVolume(MIXER_SE, PlayerPrefs.GetFloat(PREF_SE, DEFAULT_VOLUME));
    }

    public float GetBGMVolume() => PlayerPrefs.GetFloat(PREF_BGM, DEFAULT_VOLUME);
    public float GetSEVolume() => PlayerPrefs.GetFloat(PREF_SE, DEFAULT_VOLUME);
}
