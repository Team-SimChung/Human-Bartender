using UnityEngine;

/// <summary>
/// 거리 비 파티클을 일차·출퇴근 규칙대로 켜고 끈다. 규칙은 빗소리와 같은 GameAudioRules.IsRainy 한 곳이다
/// (0일차 출근길은 맑고, 퇴근길에 처음 나오면 비가 내린다). 창문 빗방울·물웅덩이 파문은 이 파티클을 따라간다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RainSchedule : MonoBehaviour
{
    [Tooltip("켜고 끌 비 파티클. 비우면 이 오브젝트와 자식의 파티클을 모두 쓴다.")]
    [SerializeField] ParticleSystem[] systems = System.Array.Empty<ParticleSystem>();

    bool? applied;

    public bool IsRaining => applied == true;

    void OnEnable()
    {
        if (systems == null || systems.Length == 0) systems = GetComponentsInChildren<ParticleSystem>(true);
        applied = null;
        Apply();
    }

    // QA 상황 불러오기처럼 씬을 다시 열지 않고 일차·흐름이 바뀌어도 따라간다.
    void LateUpdate() => Apply();

    public void Apply()
    {
        var state = GameStateManager.Instance;
        if (state == null) return;
        bool rain = GameAudioRules.IsRainy(state.CurrentDay, state.GameFlow);
        if (applied == rain) return;
        applied = rain;
        foreach (var system in systems)
        {
            if (system == null) continue;
            if (rain) system.Play(false);
            // 맑은 날에는 이미 떨어지던 빗줄기도 바로 지운다.
            else system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
