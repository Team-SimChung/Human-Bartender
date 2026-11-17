using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>씬에 미리 배치한 비둘기를 지정한 날짜·대본 스텝에서 한 번 도착시킨다.</summary>
[DisallowMultipleComponent]
public sealed class TerraceBirdArrival : MonoBehaviour, ISerializationCallbackReceiver
{
    [Serializable]
    public sealed class DayTiming
    {
        [Min(0), InspectorName("날짜"), Tooltip("이 설정을 적용할 날짜입니다. 배열 순서와 관계없이 날짜 값으로 찾습니다.")]
        public int day;
        [FormerlySerializedAs("step"), Min(0), InspectorName("시작 스텝"), Tooltip("0이면 테라스 진입부터 착지 위치에 Idle로 있습니다. 1 이상이면 해당 CSV seq 시작에 한 번 날아옵니다.")]
        public int startStep = 1;
        [FormerlySerializedAs("enabled"), InspectorName("생성 여부"), Tooltip("끄면 해당 날짜에는 비둘기를 표시하지 않습니다.")]
        public bool spawn;
    }

    [Header("씬 연결")]
    [SerializeField, Tooltip("날짜별 테라스 대본 ID를 가진 기존 화면 담당자입니다.")]
    private HomeTerracePresentation terrace;
    [SerializeField, Tooltip("미리 배치한 비둘기 프리팹의 Transform입니다. 이 스크립트와 별도의 자식이어야 합니다.")]
    private Transform bird;
    [SerializeField, Tooltip("IsFlying Bool로 Idle/Fly를 전환하는 비둘기 Animator입니다.")]
    private Animator birdAnimator;
    [SerializeField, Tooltip("비둘기 프리팹의 공통 랜덤 Idle 파츠입니다. 비행 중에는 멈추고 착지 뒤에 시작합니다.")]
    private RandomIdleAnimation birdIdle;
    [SerializeField, Tooltip("날아오기 시작할 씬 기준점입니다.")]
    private Transform flightStart;
    [SerializeField, Tooltip("착지할 씬 기준점입니다.")]
    private Transform landingPoint;

    [Header("날아오기")]
    [SerializeField, Min(0), Tooltip("시작점에서 착지점까지 이동할 시간(초)입니다. 일시정지 중에는 멈춥니다.")]
    private float flightDuration = 1.5f;
    [SerializeField, Tooltip("착지까지 이동하는 가속·감속 곡선입니다.")]
    private Ease flightEase = Ease.OutSine;
    [Header("날짜별 설정")]
    [SerializeField, Tooltip("날짜·시작 스텝·생성 여부를 항목별로 설정합니다. 같은 날짜는 한 번만 등록하세요. 등록하지 않은 날짜에는 나타나지 않습니다.")]
    private DayTiming[] days = { new() { day = 0 }, new() { day = 1 }, new() { day = 2 }, new() { day = 3 } };
    [SerializeField, HideInInspector] private int daySettingsVersion;

    private static readonly int IsFlyingParameter = Animator.StringToHash("IsFlying");
    private Tween movement;
    private DayTiming activeTiming;
    public bool HasTriggered { get; private set; }
    public bool IsFlying { get; private set; }
    public bool HasArrived { get; private set; }
    public HomeTerracePresentation Terrace => terrace;

    private void Awake()
    {
        UpgradeLegacyDays();
        ResetArrival();
    }

    private void UpgradeLegacyDays()
    {
        if (daySettingsVersion >= 1) return;
        // 기존 배열은 인덱스가 날짜였다. 저장된 생성 여부와 스텝을 유지하며 날짜를 명시한다.
        if (days != null)
            for (int i = 0; i < days.Length; i++)
                if (days[i] != null) days[i].day = i;
        daySettingsVersion = 1;
    }

    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() => UpgradeLegacyDays();
    private void OnValidate() => UpgradeLegacyDays();

    public void PrepareForDay(int day)
    {
        ResetArrival();
        if (day < 0 || days == null) return;
        foreach (var timing in days)
        {
            if (timing == null || timing.day != day) continue;
            if (activeTiming != null)
            {
                activeTiming = null;
                Debug.LogWarning($"[TerraceBird] {day}일차 설정이 중복되어 비둘기를 표시하지 않습니다.", this);
                return;
            }
            activeTiming = timing;
        }
        TryStartAtStep(0);
    }

    public bool TryStartAtStep(int step)
    {
        if (!isActiveAndEnabled || HasTriggered || step < 0 || activeTiming == null)
            return false;
        if (!activeTiming.spawn || activeTiming.startStep != step) return false;
        if (bird == null || bird == transform || birdAnimator == null || landingPoint == null || (step > 0 && flightStart == null))
        {
            Debug.LogWarning("[TerraceBird] 비둘기·Animator·출발점·착지점을 연결하세요.", this);
            return false;
        }
        HasTriggered = true;
        if (birdIdle != null) birdIdle.Stop();
        if (step == 0)
        {
            bird.position = landingPoint.position;
            bird.gameObject.SetActive(true);
            Land();
            return true;
        }
        IsFlying = true;
        bird.position = flightStart.position;
        bird.gameObject.SetActive(true);
        birdAnimator.SetBool(IsFlyingParameter, true);
        birdAnimator.Play("Fly", 0, 0);
        birdAnimator.Update(0);
        if (flightDuration <= 0)
        {
            bird.position = landingPoint.position;
            Land();
        }
        else
            movement = bird.DOMove(landingPoint.position, flightDuration).SetEase(flightEase).OnComplete(Land);
        return true;
    }

    /// <summary>원래 앉아 있던 새는 대사 전 테라스 진입 이동에도 난간을 따라간다.</summary>
    public void SyncTerraceEntrance(Transform foreground, Vector3 offset)
    {
        if (HasArrived && bird != null && landingPoint != null)
        {
            // 착지점이 Terrace 아래에 있으면 부모 이동이 이미 적용되어 있다.
            if (foreground != null && landingPoint.IsChildOf(foreground)) offset = Vector3.zero;
            bird.position = landingPoint.position + offset;
        }
    }

    private void Land()
    {
        movement = null;
        IsFlying = false;
        HasArrived = true;
        if (birdAnimator != null)
        {
            birdAnimator.SetBool(IsFlyingParameter, false);
            if (birdIdle != null)
            {
                birdIdle.Play();
                if (birdIdle.IsRunning) return;
            }
            // 공통 파츠가 재생을 시작하지 못한 경우에만 기본 착지 포즈를 적용한다.
            birdAnimator.Play("Idle", 0, 0);
            birdAnimator.Update(0);
        }
    }

    public void ResetArrival()
    {
        movement?.Kill();
        movement = null;
        if (birdIdle != null) birdIdle.Stop();
        activeTiming = null;
        HasTriggered = IsFlying = HasArrived = false;
        if (bird != null && bird != transform)
        {
            if (bird.gameObject.activeInHierarchy && birdAnimator != null)
                birdAnimator.SetBool(IsFlyingParameter, false);
            bird.gameObject.SetActive(false);
            if (landingPoint != null) bird.position = landingPoint.position;
        }
    }

    private void OnDisable() => ResetArrival();
    private void OnDestroy() => ResetArrival();

    private void OnDrawGizmosSelected()
    {
        if (flightStart == null || landingPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(flightStart.position, landingPoint.position);
        Gizmos.DrawWireSphere(flightStart.position, .05f);
        Gizmos.DrawWireSphere(landingPoint.position, .05f);
    }
}
