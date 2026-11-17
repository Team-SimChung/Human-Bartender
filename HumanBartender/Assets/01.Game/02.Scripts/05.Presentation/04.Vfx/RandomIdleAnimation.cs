using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>전담 Animator의 비반복 상태를 랜덤 대기 또는 고정 시작 간격으로 반복한다.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "CrowIdleAnimation")]
[AddComponentMenu("Human Bartender/Animation/Random Idle Animation")]
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class RandomIdleAnimation : MonoBehaviour
{
    [Header("애니메이션 연결")]
    [SerializeField, Tooltip("이 파츠가 전담하는 Animator입니다. 다른 연출이 사용하기 전에는 Stop(), 끝나면 Play()를 호출하세요.")] private Animator animator;
    [SerializeField, Tooltip("지정한 상태에 연결된 실제 클립입니다. Loop Time을 끄세요. 스프라이트 외의 Animator에도 사용할 수 있습니다.")] private AnimationClip idleClip;
    [SerializeField, Tooltip("Animator의 0번 레이어에서 재생할 상태 이름 또는 전체 경로입니다.")] private string stateName = "Idle";
    [SerializeField, Tooltip("활성화 시 자동 시작합니다. 비행 등 다른 연출 이후에 시작할 때는 끄고 Play()를 호출하세요.")] private bool playOnEnable = true;

    [Header("랜덤 시작 프레임 (1부터)")]
    [SerializeField, Min(1), Tooltip("매번 재생을 시작할 가장 이른 프레임입니다.")] private int minimumStartFrame = 1;
    [SerializeField, Min(1), Tooltip("매번 재생을 시작할 가장 늦은 프레임입니다. 이 프레임도 포함합니다.")] private int maximumStartFrame = 4;

    [Header("재생을 멈춘 뒤 랜덤 대기 (초)")]
    [SerializeField, Min(0), Tooltip("마지막 프레임을 유지하는 최소 시간입니다. 게임 일시정지 중에는 대기도 멈춥니다.")] private float minimumWaitSeconds = 1;
    [SerializeField, Min(0), Tooltip("마지막 프레임을 유지하는 최대 시간입니다.")] private float maximumWaitSeconds = 3;

    [Header("고정 재생 간격 · 선택 사항")]
    [SerializeField, Min(0), Tooltip("0이면 기존 랜덤 대기를 사용합니다. 양수이면 재생 시작부터 다음 재생 시작까지의 간격입니다. 일시정지 중에는 멈춥니다.")]
    private float repeatIntervalSeconds;

    private Coroutine repetition;
    private float originalSpeed;
    private bool controlsAnimator;
    private int stateHash;
    public bool IsWaiting { get; private set; }
    public bool IsRunning => controlsAnimator;
    public int CurrentStartFrame { get; private set; }
    public float CurrentWaitSeconds { get; private set; }

    private void OnValidate()
    {
        minimumStartFrame = Mathf.Max(1, minimumStartFrame);
        maximumStartFrame = Mathf.Max(minimumStartFrame, maximumStartFrame);
        minimumWaitSeconds = Mathf.Max(0, minimumWaitSeconds);
        maximumWaitSeconds = Mathf.Max(minimumWaitSeconds, maximumWaitSeconds);
        repeatIntervalSeconds = Mathf.Max(0, repeatIntervalSeconds);
    }

    private void Reset() => animator = GetComponent<Animator>();

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    /// <summary>설정한 랜덤 범위에서 새 반복을 시작한다. 대기 중 재호출해도 이전 반복은 정리한다.</summary>
    public void Play()
    {
        Stop();
        if (!isActiveAndEnabled) return;
        stateHash = Animator.StringToHash(stateName ?? "");
        if (animator == null || idleClip == null || idleClip.isLooping || idleClip.length <= 0 || idleClip.frameRate <= 0 ||
            animator.runtimeAnimatorController == null || !animator.HasState(0, stateHash) || animator.speed <= 0)
        {
            Debug.LogWarning("[RandomIdle] Animator와 반복하지 않는 클립·상태를 연결하고 재생 속도를 확인하세요.", this);
            return;
        }
        originalSpeed = animator.speed;
        controlsAnimator = true;
        repetition = StartCoroutine(Repeat());
    }

    private IEnumerator Repeat()
    {
        int frameCount = Mathf.Max(1, Mathf.RoundToInt(idleClip.length * idleClip.frameRate));
        while (true)
        {
            float cycleStart = Time.time;
            int first = Mathf.Clamp(minimumStartFrame, 1, frameCount);
            int last = Mathf.Clamp(maximumStartFrame, first, frameCount);
            CurrentStartFrame = Random.Range(first, last + 1);
            CurrentWaitSeconds = 0;
            IsWaiting = false;
            animator.speed = originalSpeed;
            animator.Play(stateHash, 0, (CurrentStartFrame - 1f) / frameCount);
            animator.Update(0);
            while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
                yield return null;

            // 비반복 클립이므로 끝을 지난 프레임에도 처음으로 튀지 않고 마지막 포즈를 유지한다.
            animator.speed = 0;
            IsWaiting = true;
            float minimum = Mathf.Max(0, minimumWaitSeconds);
            CurrentWaitSeconds = repeatIntervalSeconds > 0
                ? Mathf.Max(0, repeatIntervalSeconds - (Time.time - cycleStart))
                : Random.Range(minimum, Mathf.Max(minimum, maximumWaitSeconds));
            yield return new WaitForSeconds(CurrentWaitSeconds);
        }
    }

    /// <summary>재생·대기를 취소하고 Animator 속도를 돌려 다른 연출에 제어를 넘긴다.</summary>
    public void Stop()
    {
        if (repetition != null) StopCoroutine(repetition);
        repetition = null;
        if (controlsAnimator && animator != null) animator.speed = originalSpeed;
        controlsAnimator = false;
        IsWaiting = false;
        CurrentStartFrame = 0;
        CurrentWaitSeconds = 0;
    }

    private void OnDisable() => Stop();
}
