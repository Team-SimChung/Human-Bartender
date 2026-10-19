using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;
    [Tooltip("SE 전용. 기본 clip과 순서대로 번갈아 재생하며 게임의 난수 상태를 사용하지 않습니다.")]
    public AudioClip[] alternatives;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
}

/// <summary>
/// BG / SE 2채널 사운드 매니저.
/// static Instance 없음 — VContainer 가 Lifetime.Singleton 으로 단일 인스턴스를 보장하고
/// 소비자는 효과음/BGM/반복음 계약 중 필요한 기능만 주입받는다.
/// </summary>
public class SoundManager : MonoBehaviour, ISoundEffectPlayer, IBgmPlayer, ILoopingSoundManager
{
    [Header("Shared Output")]
    [SerializeField] private AudioOutputSettings output;

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
    readonly Dictionary<Sound, int> variationIndices = new();
    readonly HashSet<Loop> loops = new();

    private Coroutine _bgmFadeRoutine;

    // VContainer 가 프리팹을 Instantiate 하면 Awake 가 호출된다.
    private void Awake()
    {
        BuildDictionaries();
        BuildSePool();
        if (bgmSource != null) output.Route(bgmSource, AudioBus.Music);
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
        foreach (var loop in new List<Loop>(loops)) loop.Dispose();
    }

    private void BuildSePool()
    {
        for (int i = 0; i < Mathf.Max(1, sePoolSize); i++)
        {
            var go = new GameObject($"SE_Source_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            output.Route(src, AudioBus.SoundEffects);
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
        if (string.IsNullOrEmpty(name) || !_se.TryGetValue(name, out var sound))
        {
            Debug.LogWarning($"[SoundManager] SE '{name}' 없음");
            return;
        }

        AudioClip clip = NextSeClip(sound);
        if (clip == null) return;

        AudioSource src = GetFreeSeSource();
        src.pitch = sound.pitch;
        src.PlayOneShot(clip, sound.volume);
    }

    AudioClip NextSeClip(Sound sound)
    {
        int count = 1 + (sound.alternatives?.Length ?? 0);
        variationIndices.TryGetValue(sound, out int next);
        for (int i = 0; i < count; i++)
        {
            int index = (next + i) % count;
            AudioClip clip = index == 0 ? sound.clip : sound.alternatives[index - 1];
            if (clip == null) continue;
            variationIndices[sound] = (index + 1) % count;
            return clip;
        }
        Debug.LogWarning($"[SoundManager] SE '{sound.name}'에 재생 가능한 클립이 없습니다.");
        return null;
    }

    public System.IDisposable PlayLoop(string name)
    {
        if (!isActiveAndEnabled || string.IsNullOrEmpty(name)) return null;
        if (!_se.TryGetValue(name, out var sound))
        {
            Debug.LogWarning($"[SoundManager] SE '{name}' 없음");
            return null;
        }
        AudioClip clip = NextSeClip(sound);
        if (clip == null) return null;
        var go = new GameObject($"SE_Loop_{name}");
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = true;
        source.volume = sound.volume;
        source.pitch = sound.pitch;
        output.Route(source, AudioBus.SoundEffects);
        var handle = new Loop(this, source);
        loops.Add(handle);
        source.Play();
        return handle;
    }

    sealed class Loop : System.IDisposable
    {
        readonly SoundManager owner;
        AudioSource source;
        bool disposed;
        public Loop(SoundManager owner, AudioSource source) { this.owner = owner; this.source = source; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.loops.Remove(this);
            if (source == null) return;
            source.Stop();
            var go = source.gameObject;
            source = null;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }

    private AudioSource GetFreeSeSource()
    {
        foreach (var src in sePool)
            if (!src.isPlaying) return src;

        var reuse = sePool[seCursor];
        seCursor = (seCursor + 1) % sePool.Count;
        return reuse;
    }

}
