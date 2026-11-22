using System;
using UnityEngine;

/// <summary>씬에 저장된 반복 환경음을 날짜, 출퇴근 구간, 플레이어 거리에 맞춰 재생한다.</summary>
[DisallowMultipleComponent]
public sealed class DistanceAmbientAudio : MonoBehaviour
{
    public enum CommuteOutPlayback
    {
        [InspectorName("출근과 동일")] SameAsCommuteIn,
        [InspectorName("재생")] Play,
        [InspectorName("재생 안 함")] Mute
    }

    [Serializable]
    public struct DayPlayback
    {
        [Min(0), InspectorName("날짜"), Tooltip("게임의 CurrentDay와 비교할 날짜. 배열 순서와 관계없이 이 값으로 찾는다.")]
        public int day;
        [InspectorName("출근 재생"), Tooltip("켜면 해당 날짜에 출근 구간에서 재생한다.")]
        public bool play;
        [InspectorName("퇴근 재생"), Tooltip("퇴근 구간의 재생 여부. 출근과 동일을 선택하면 기존 출근 값과 같이 동작한다.")]
        public CommuteOutPlayback commuteOut;
    }

    [Header("음원 연결")]
    [SerializeField, Tooltip("같은 오브젝트에 미리 배치한 전용 AudioSource.")]
    AudioSource source;
    [SerializeField, Tooltip("반복 재생할 환경음. 비워 두면 재생하지 않는다.")]
    AudioClip clip;
    [SerializeField, Tooltip("거리 기준인 플레이어 Transform. 카메라 위치와 Z 거리는 음량에 영향을 주지 않는다.")]
    Transform listener;

    [Header("음량")]
    [SerializeField, Range(0f, 1f), Tooltip("가까이 있을 때의 최대 음량. 사용자 효과음 음량은 공용 믹서에서 적용한다.")]
    float baseVolume = .3f;
    [SerializeField, Tooltip("켜면 플레이어와 음원 사이의 XY 거리에 따라 음량을 조절한다. 끄면 위치와 무관하게 최대 음량을 사용한다.")]
    bool useDistanceAttenuation = true;
    [SerializeField, Min(0f), Tooltip("이 거리 이내에서는 최대 음량을 유지한다. 월드 XY 거리 기준.")]
    float fullVolumeDistance = .75f;
    [SerializeField, Min(0f), InspectorName("최저 음량 거리"), Tooltip("이 거리 이상에서는 최저 음량을 유지한다. 최대 음량 거리보다 크게 설정한다.")]
    float silentDistance = 4f;
    [SerializeField, Range(0f, 1f), Tooltip("멀리서도 남겨 둘 음량의 비율. 0이면 최저 음량 거리부터 들리지 않는다.")]
    float distantGain;
    [SerializeField, Tooltip("가로 0은 최대 음량 거리, 1은 최저 음량 거리. 세로는 최저 음량과 최대 음량 사이의 비율이다.")]
    AnimationCurve distanceCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField, Tooltip("켜면 대화 중 음량을 낮춘다. 옵션 화면의 음량 감소는 항상 적용한다.")]
    bool duckDuringDialogue = true;
    [SerializeField, Min(0f), Tooltip("최대 음량까지 변하는 시간(초). 거리·날짜·장소 변화에 부드럽게 반응한다. 0이면 즉시 변경한다.")]
    float fadeSeconds = .5f;

    [Header("날짜별 재생")]
    [SerializeField, Tooltip("날짜별로 출근·퇴근의 재생 여부를 정한다. 등록하지 않은 날짜와 중복 등록한 날짜에는 재생하지 않는다.")]
    DayPlayback[] days = Array.Empty<DayPlayback>();

    bool playbackAllowed = true;
    bool applicationPaused;
    bool configured;
    float currentVolume;
    int? dayOverride;
    EGameFlow? flowOverride;

    public bool PlaybackAllowed => playbackAllowed;
    public void SetPlaybackAllowed(bool allowed, int? scheduledDay = null, EGameFlow? scheduledFlow = null)
    {
        playbackAllowed = allowed;
        dayOverride = scheduledDay;
        flowOverride = scheduledFlow;
    }

    public bool IsScheduledForDay(int day, EGameFlow flow = EGameFlow.CommuteIn)
    {
        if (day < 0 || days == null) return false;
        bool found = false;
        bool play = false;
        foreach (var entry in days)
        {
            if (entry.day != day) continue;
            if (found) return false;
            found = true;
            play = flow == EGameFlow.CommuteOut && entry.commuteOut != CommuteOutPlayback.SameAsCommuteIn
                ? entry.commuteOut == CommuteOutPlayback.Play
                : entry.play;
        }
        return found && play;
    }

    public float EvaluateVolume(Vector3 listenerPosition, int day, EGameFlow flow = EGameFlow.CommuteIn)
    {
        if (!playbackAllowed || !IsScheduledForDay(dayOverride ?? day, flowOverride ?? flow)) return 0f;
        if (!useDistanceAttenuation) return baseVolume;
        float distance = Vector2.Distance(listenerPosition, transform.position);
        float near = Mathf.Max(0f, fullVolumeDistance);
        float far = Mathf.Max(near, silentDistance);
        if (distance <= near) return baseVolume;
        if (distance >= far) return baseVolume * distantGain;
        float t = Mathf.InverseLerp(near, far, distance);
        float gain = distanceCurve != null ? distanceCurve.Evaluate(t) : 1f - t;
        return baseVolume * Mathf.Lerp(distantGain, 1f, gain);
    }

    void Awake() => ConfigureSource();

    void ConfigureSource()
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.clip = clip;
        AudioOutputSettings.Shared.Route(source, AudioBus.SoundEffects);
        configured = true;
    }

    void Update()
    {
        var state = GameStateManager.Instance;
        bool paused = Time.timeScale <= 0f || UIDisplayOptions.IsOptionOpen;
        bool cinematic = GameAudioContext.Cinematic || state.CurrentGameState == GameState.Effect;
        float gain = cinematic ? 0f : GameAudioRules.AmbienceGain(duckDuringDialogue && GameAudioContext.AnyDialogue, paused);
        UpdatePlayback(Time.unscaledDeltaTime, state.CurrentDay, state.GameFlow,
            Application.isFocused && !applicationPaused && !AudioListener.pause, gain);
    }

    void UpdatePlayback(float deltaTime, int day, EGameFlow flow, bool focused, float contextGain)
    {
        if (!configured) ConfigureSource();
        if (source == null) return;
        if (!focused || clip == null || (useDistanceAttenuation && listener == null) || baseVolume <= 0f)
        {
            StopPlayback();
            return;
        }
        if (source.clip != clip)
        {
            StopPlayback();
            source.clip = clip;
        }
        Vector3 listenerPosition = useDistanceAttenuation ? listener.position : default;
        float target = EvaluateVolume(listenerPosition, day, flow) * Mathf.Clamp01(contextGain);
        currentVolume = fadeSeconds <= 0f ? target : Mathf.MoveTowards(currentVolume, target,
            Mathf.Max(0f, deltaTime) * baseVolume / fadeSeconds);
        source.volume = currentVolume;
        if (currentVolume > 0f || target > 0f)
        {
            if (!source.isPlaying && source.isActiveAndEnabled) source.Play();
        }
        else if (source.isPlaying) source.Stop();
    }

    void StopPlayback()
    {
        currentVolume = 0f;
        if (source == null) return;
        source.Stop();
        source.volume = 0f;
    }

    void OnDisable() => StopPlayback();
    void OnApplicationFocus(bool focused) { if (!focused) StopPlayback(); }
    void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) StopPlayback(); }

    void OnDrawGizmosSelected()
    {
        if (!useDistanceAttenuation) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, fullVolumeDistance));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(fullVolumeDistance, silentDistance));
    }
}
