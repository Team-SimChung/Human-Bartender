using System;
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
public class SoundManager : MonoBehaviour, ISoundEffectPlayer, IBgmPlayer, ILoopingSoundManager, IAudioCuePlayer
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
    readonly Dictionary<AudioSource, Coroutine> envelopes = new();
    readonly Dictionary<string, AudioSource> exclusiveVoices = new();
    readonly Dictionary<string, float> lastCueTimes = new();
    string requestedGroup, playingGroup;
    string[] playlist;
    int trackIndex, shakerPitchIndex;
    bool repeatPlaylist = true, musicPaused;
    float placeGain = 1f, musicEnvelope = 1f, musicVisibility = 1f, visibilityTarget = 1f, visibilitySeconds = .6f;
    Sound currentMusic;

    void Update()
    {
        musicVisibility = Mathf.MoveTowards(musicVisibility, visibilityTarget,
            Time.unscaledDeltaTime / Mathf.Max(.001f, visibilitySeconds));
        if (bgmSource == null) return;
        if (_bgmFadeRoutine == null && !musicPaused && currentMusic != null &&
            !bgmSource.isPlaying && playlist != null && playlist.Length > 0 && repeatPlaylist)
        {
            trackIndex = (trackIndex + 1) % playlist.Length;
            StartTrack(trackIndex);
        }
        bgmSource.volume = (currentMusic?.volume ?? 0f) * placeGain * musicEnvelope * musicVisibility;
    }

    // VContainer 가 프리팹을 Instantiate 하면 Awake 가 호출된다.
    private void Awake()
    {
        if (output == null) output = AudioOutputSettings.Shared;
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
        foreach (var envelope in envelopes.Values) if (envelope != null) StopCoroutine(envelope);
        envelopes.Clear();
        exclusiveVoices.Clear();
        foreach (var loop in new List<Loop>(loops)) loop.StopImmediately();
        currentMusic = null;
        requestedGroup = playingGroup = null;
        playlist = null;
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
        PlayPlaylist(name, new[] { name }, 1f, fadeDuration, loop);
    }

    public void PlayPlaylist(string group, string[] names, float gain = 1f, float fadeDuration = 1f, bool repeat = true)
    {
        if (!isActiveAndEnabled || bgmSource == null || names == null || names.Length == 0) return;
        foreach (string key in names)
            if (!_bgm.TryGetValue(key, out var sound) || sound.clip == null)
            { Debug.LogWarning($"[SoundManager] BGM '{key}' 없음"); return; }
        if (requestedGroup == group) return;
        requestedGroup = group;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        // 교체 전 원래 그룹으로 돌아오면 현재 곡과 재생 위치를 유지한다.
        bool restore = playingGroup == group && currentMusic != null;
        if (fadeDuration <= 0f)
        {
            _bgmFadeRoutine = null;
            musicEnvelope = 1f;
            if (!restore)
            {
                playlist = (string[])names.Clone();
                repeatPlaylist = repeat;
                playingGroup = group;
                placeGain = Mathf.Max(0f, gain);
                trackIndex = 0;
                StartTrack(0);
            }
            else bgmSource.volume = currentMusic.volume * placeGain * musicVisibility;
            return;
        }
        _bgmFadeRoutine = StartCoroutine(ChangePlaylist(group, (string[])names.Clone(), gain, fadeDuration, repeat, restore));
    }

    IEnumerator ChangePlaylist(string group, string[] names, float gain, float duration, bool repeat, bool restore)
    {
        if (!restore && currentMusic != null) yield return FadeMusic(0f, duration * .5f);
        if (!restore)
        {
            playlist = names;
            repeatPlaylist = repeat;
            playingGroup = group;
            placeGain = Mathf.Max(0f, gain);
            trackIndex = 0;
            musicEnvelope = 0f;
            StartTrack(0);
        }
        yield return FadeMusic(1f, duration * .5f);
        _bgmFadeRoutine = null;
    }

    void StartTrack(int index)
    {
        currentMusic = _bgm[playlist[index]];
        bgmSource.clip = currentMusic.clip;
        bgmSource.pitch = currentMusic.pitch;
        bgmSource.loop = false;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = currentMusic.volume * placeGain * musicEnvelope * musicVisibility;
        bgmSource.Play();
        if (musicPaused) bgmSource.Pause();
    }

    IEnumerator FadeMusic(float target, float seconds)
    {
        float start = musicEnvelope;
        for (float time = 0f; time < seconds; time += Time.unscaledDeltaTime)
        {
            musicEnvelope = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, time / seconds));
            yield return null;
        }
        musicEnvelope = target;
    }

    public void SetMusicSuppressed(bool suppressed, float seconds = .6f)
    {
        visibilityTarget = suppressed ? 0f : 1f;
        visibilitySeconds = Mathf.Max(.001f, seconds);
    }

    public void StopBGM(float fadeDuration = 1f)
    {
        if (bgmSource == null) return;
        requestedGroup = null;
        playlist = null;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = null;
        if (!isActiveAndEnabled || fadeDuration <= 0f) { bgmSource.Stop(); currentMusic = null; return; }
        _bgmFadeRoutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    private IEnumerator FadeOutAndStop(float duration)
    {
        yield return FadeMusic(0f, duration);
        bgmSource.Stop();
        currentMusic = null;
        playingGroup = null;
        _bgmFadeRoutine = null;
    }

    public void PauseBGM()
    {
        musicPaused = true;
        if (bgmSource != null) bgmSource.Pause();
    }

    public void ResumeBGM() { musicPaused = false; if (bgmSource != null) bgmSource.UnPause(); }

    // ─── SE (동시 재생) ─────────────────────────────
    public void PlaySE(string name)
        => PlayCue(name);

    public void PlayCue(string name, float gain = 1f, float pitch = 1f,
        float fadeIn = 0f, float fadeOut = 0f, bool exclusive = false)
    {
        if (!isActiveAndEnabled) return;
        if (string.IsNullOrEmpty(name) || !_se.TryGetValue(name, out var sound))
        {
            Debug.LogWarning($"[SoundManager] SE '{name}' 없음");
            return;
        }

        AudioClip clip = NextSeClip(sound);
        if (clip == null) return;
        bool notification = name == "SFX_notification" || name == "SFX_goal_completed";
        string channel = name;
        if (notification && lastCueTimes.TryGetValue(channel, out float last) && Time.unscaledTime - last < .12f) return;
        lastCueTimes[channel] = Time.unscaledTime;
        if (name == "SFX_ui_hover") exclusive = true;
        if (exclusive && exclusiveVoices.TryGetValue(channel, out var previous) && previous != null && previous.isPlaying)
            FadeVoice(previous, .045f);
        AudioSource src = GetFreeSeSource();
        var staleChannels = new List<string>();
        foreach (var pair in exclusiveVoices) if (pair.Value == src) staleChannels.Add(pair.Key);
        foreach (string key in staleChannels) exclusiveVoices.Remove(key);
        CancelEnvelope(src);
        src.Stop();
        src.clip = clip;
        src.loop = false;
        src.spatialBlend = 0f;
        float variation = name == "SFX_shaker_hit" ? ((shakerPitchIndex++ % 3) switch { 0 => .98f, 1 => 1f, _ => 1.02f }) : 1f;
        src.pitch = Mathf.Clamp(sound.pitch * pitch * variation, .1f, 3f);
        src.volume = fadeIn > 0f ? 0f : Mathf.Clamp01(sound.volume * gain);
        src.Play();
        if (exclusive) exclusiveVoices[channel] = src;
        if (fadeIn > 0f || fadeOut > 0f)
            envelopes[src] = StartCoroutine(Envelope(src, Mathf.Clamp01(sound.volume * gain), fadeIn, fadeOut));
    }

    void CancelEnvelope(AudioSource source)
    {
        if (envelopes.Remove(source, out var routine) && routine != null) StopCoroutine(routine);
    }

    void FadeVoice(AudioSource source, float seconds)
    {
        CancelEnvelope(source);
        envelopes[source] = StartCoroutine(FadeVoiceOut(source, seconds));
    }

    IEnumerator FadeVoiceOut(AudioSource source, float seconds)
    {
        float start = source.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        { source.volume = Mathf.Lerp(start, 0f, t / seconds); yield return null; }
        source.Stop();
        envelopes.Remove(source);
    }

    IEnumerator Envelope(AudioSource source, float gain, float fadeIn, float fadeOut)
    {
        float length = source.clip.length / Mathf.Max(.1f, source.pitch);
        for (float t = 0f; t < length && source.isPlaying; t += Time.unscaledDeltaTime)
        {
            float enter = fadeIn <= 0f ? 1f : Mathf.Clamp01(t / fadeIn);
            float exit = fadeOut <= 0f ? 1f : Mathf.Clamp01((length - t) / fadeOut);
            source.volume = gain * Mathf.SmoothStep(0f, 1f, Mathf.Min(enter, exit));
            yield return null;
        }
        envelopes.Remove(source);
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
        => StartLoop(name, 0f);

    public IAudioLoop StartLoop(string name, float fadeSeconds = .12f)
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
        source.spatialBlend = 0f;
        var handle = new Loop(this, source, sound.volume, fadeSeconds);
        loops.Add(handle);
        source.Play();
        return handle;
    }

    sealed class Loop : IAudioLoop
    {
        readonly SoundManager owner;
        AudioSource source;
        bool disposed;
        readonly float baseVolume, fadeSeconds;
        float gain = 1f;
        public Loop(SoundManager owner, AudioSource source, float baseVolume, float fadeSeconds)
        {
            this.owner = owner; this.source = source;
            this.baseVolume = baseVolume; this.fadeSeconds = Mathf.Max(0f, fadeSeconds);
            if (fadeSeconds > 0f) { source.volume = 0f; owner.StartCoroutine(Animate()); }
        }
        public void SetGain(float value)
        {
            gain = Mathf.Max(0f, value);
            if (fadeSeconds <= 0f && source != null && !disposed) source.volume = Mathf.Clamp01(baseVolume * gain);
        }
        IEnumerator Animate()
        {
            while (source != null)
            {
                float target = disposed ? 0f : Mathf.Clamp01(baseVolume * gain);
                source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime / Mathf.Max(.001f, fadeSeconds));
                if (disposed && source.volume <= 0f) { StopImmediately(); yield break; }
                yield return null;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (fadeSeconds <= 0f || !owner.isActiveAndEnabled) StopImmediately();
        }
        public void StopImmediately()
        {
            disposed = true;
            owner.loops.Remove(this);
            if (source == null) return;
            source.Stop();
            var go = source.gameObject;
            source = null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
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
