using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>소파에 배치된 수면 오브젝트를 켜고, 기상이 끝나면 기존 루나를 다시 표시함.</summary>
[Serializable]
public sealed class HomeSleepAnimation
{
    private static readonly int SleepState = Animator.StringToHash("Base Layer.Sleep");
    private static readonly int WakeUpState = Animator.StringToHash("Base Layer.WakeUp");

    [Header("배치된 수면 오브젝트")]
    [SerializeField, Tooltip("소파 위 LunaSleepAnimation의 Animator입니다. 이 오브젝트를 켜고 끄며, 위치는 씬의 Transform에서 직접 조절합니다.")]
    private Animator sleepAnimator;
    [SerializeField, Tooltip("평소 이동하는 루나의 SpriteRenderer입니다. 수면 오브젝트를 표시하는 동안 숨깁니다.")]
    private SpriteRenderer actorRenderer;
    [SerializeField, Tooltip("기상 후 기존 루나가 서 있을 HomeWakeUpSpawn입니다. 수면 오브젝트의 위치에는 영향을 주지 않습니다.")]
    private Transform wakeUpSpawn;
    [SerializeField, Tooltip("소파 화면을 비추는 CM_GamePlay입니다. 연결된 Confiner2D의 경계를 유지하며 검은 화면에서 구도를 갱신합니다.")]
    private CinemachineCamera roomCamera;
    [SerializeField, Tooltip("검은 화면에서 소파 카메라 상태를 갱신할 Cinemachine Brain입니다.")]
    private CinemachineBrain cameraBrain;

    [Header("수면·기상 클립")]
    [SerializeField, Tooltip("luna_sleep의 정방향, 반복하지 않는 수면 클립입니다.")]
    private AnimationClip sleepClip;
    [SerializeField, Tooltip("같은 프레임을 역순으로 배치한 반복하지 않는 기상 클립입니다.")]
    private AnimationClip wakeUpClip;
    [SerializeField, Min(.01f), Tooltip("두 클립의 재생 배율입니다. 1이면 클립에 저장한 원래 속도로 재생합니다.")]
    private float playbackSpeed = 1;
    [SerializeField, Min(0), Tooltip("서 있는 루나와 소파 위 수면 오브젝트를 전환할 때, 페이드 아웃·인 각각에 걸리는 시간(초)입니다. 수면 진입과 기상 완료에 함께 적용합니다.")]
    private float fadeDuration = .35f;
    [SerializeField, Min(0), Tooltip("잠자기 마지막 자세와, 화면이 밝아진 뒤 기상 첫 자세를 보여줄 시간(초)입니다.")]
    private float visiblePoseDuration = .5f;

    [NonSerialized] private bool active;
    [NonSerialized] private bool originalAnimatorEnabled;
    [NonSerialized] private float originalAnimatorSpeed;
    [NonSerialized] private bool originalRendererEnabled;
    [NonSerialized] private Vector3 originalPosition;

    public void HideVisual()
    {
        if (!active && sleepAnimator != null) sleepAnimator.gameObject.SetActive(false);
    }

    public async UniTask SleepAsync(ISceneFadeService fades, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Validate();
        if (fades == null) throw new InvalidOperationException("[HomeSleep] 페이드 서비스를 연결하세요.");
        if (active) throw new InvalidOperationException("[HomeSleep] 수면 연출이 이미 실행 중입니다.");
        originalAnimatorEnabled = sleepAnimator.enabled;
        originalAnimatorSpeed = sleepAnimator.speed;
        originalRendererEnabled = actorRenderer.enabled;
        originalPosition = actorRenderer.transform.position;
        active = true;
        bool revealed = false;
        try
        {
            await fades.FadeOutAsync(fadeDuration, token);
            actorRenderer.enabled = false;
            // 카메라가 추적하는 기존 루나만 소파 앞으로 옮기고, 배치된 수면 오브젝트는 움직이지 않는다.
            actorRenderer.transform.position = wakeUpSpawn.position;
            sleepAnimator.enabled = true;
            sleepAnimator.speed = 0;
            sleepAnimator.gameObject.SetActive(true);
            if (!sleepAnimator.HasState(0, SleepState) || !sleepAnimator.HasState(0, WakeUpState))
                throw new InvalidOperationException("[HomeSleep] 수면용 Animator에 Sleep / WakeUp 상태를 설정하세요.");
            ShowPose(SleepState, 0);
            PrepareCamera();
            // 소파 위 위치와 추적 카메라를 검은 화면에서 먼저 적용한다.
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            // 카메라가 활성화된 뒤 Pixel Perfect가 확정한 화면 크기로 경계를 다시 계산한다.
            PrepareCamera();
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            await fades.FadeInAsync(fadeDuration, token);
            revealed = true;
            await PlayAsync(sleepClip, SleepState, token);
            await HoldPoseAsync(token);
        }
        catch
        {
            Restore();
            if (!revealed && actorRenderer != null)
                await fades.FadeInAsync(0, CancellationToken.None);
            throw;
        }
    }

    /// <summary>다음 날 페이드 인 전에 역순 클립의 첫 프레임(잠든 자세)을 준비한다.</summary>
    public void PrepareWakeUp()
    {
        if (!active) return;
        actorRenderer.transform.position = wakeUpSpawn.position;
        ShowPose(WakeUpState, 0);
        PrepareCamera();
    }

    public async UniTask WakeUpAsync(ISceneFadeService fades, CancellationToken token)
    {
        if (!active) return;
        if (fades == null) throw new InvalidOperationException("[HomeSleep] 페이드 서비스를 연결하세요.");
        bool fadeStarted = false;
        try
        {
            await HoldPoseAsync(token);
            await PlayAsync(wakeUpClip, WakeUpState, token);
            fadeStarted = true;
            await fades.FadeOutAsync(fadeDuration, token);
            // 서 있는 루나로 바뀌는 순간과 카메라 갱신을 검은 화면 안에서 처리한다.
            Restore(wakingUp: true);
            PrepareCamera();
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            await fades.FadeInAsync(fadeDuration, token);
            fadeStarted = false;
        }
        finally
        {
            Restore(wakingUp: true);
            if (fadeStarted && actorRenderer != null)
                await fades.FadeInAsync(0, CancellationToken.None);
        }
    }

    public void Restore(bool wakingUp = false)
    {
        if (!active) return;
        active = false;
        if (actorRenderer != null)
        {
            actorRenderer.transform.position = wakingUp && wakeUpSpawn != null ? wakeUpSpawn.position : originalPosition;
            actorRenderer.enabled = originalRendererEnabled;
        }
        if (sleepAnimator != null)
        {
            sleepAnimator.gameObject.SetActive(false);
            sleepAnimator.speed = originalAnimatorSpeed;
            sleepAnimator.enabled = originalAnimatorEnabled;
        }
    }

    private async UniTask PlayAsync(AnimationClip clip, int state, CancellationToken token)
    {
        float elapsed = 0;
        float frameDuration = 1f / clip.frameRate;
        while (elapsed < clip.length)
        {
            token.ThrowIfCancellationRequested();
            ShowPose(state, elapsed / clip.length);
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, token);
            // 첫 스프라이트 업로드 등으로 한 프레임이 길어져도 클립 전체를 건너뛰지 않는다.
            if (!UIDisplayOptions.IsOptionOpen) elapsed += Mathf.Min(Time.unscaledDeltaTime, frameDuration) * playbackSpeed;
        }
        token.ThrowIfCancellationRequested();
        // 클립 마지막 프레임을 유지해 다음 날 전환 중 Idle로 튀지 않게 한다.
        ShowPose(state, 1);
    }

    private void ShowPose(int state, float normalizedTime)
    {
        sleepAnimator.Play(state, 0, normalizedTime);
        sleepAnimator.Update(0);
    }

    private UniTask HoldPoseAsync(CancellationToken token) => visiblePoseDuration > 0
        ? UniTask.Delay(TimeSpan.FromSeconds(visiblePoseDuration), ignoreTimeScale: true, cancellationToken: token)
        : UniTask.CompletedTask;

    private void PrepareCamera()
    {
        // 추적 위치만 초기화하면 Confiner가 이전 화면 크기로 구도를 제한할 수 있다.
        // 기존 Bounding Shape를 그대로 사용하고 렌즈 캐시만 갱신한다.
        if (roomCamera.TryGetComponent<CinemachineConfiner2D>(out var confiner) && confiner.isActiveAndEnabled)
            confiner.InvalidateLensCache();
        roomCamera.PreviousStateIsValid = false;
        roomCamera.UpdateCameraState(cameraBrain.DefaultWorldUp, -1);
        cameraBrain.ResetState();
    }

    private void Validate()
    {
        if (sleepAnimator == null || actorRenderer == null || wakeUpSpawn == null || roomCamera == null || cameraBrain == null ||
            sleepClip == null || wakeUpClip == null)
            throw new InvalidOperationException("[HomeSleep] 수면 오브젝트 Animator·기존 루나 Renderer·기상 위치·실내 카메라·수면/기상 클립을 연결하세요.");
        if (sleepAnimator.gameObject == actorRenderer.gameObject || sleepAnimator.transform.IsChildOf(actorRenderer.transform) ||
            sleepAnimator.runtimeAnimatorController == null || sleepClip.length <= 0 || wakeUpClip.length <= 0 ||
            sleepClip.frameRate <= 0 || wakeUpClip.frameRate <= 0 ||
            !float.IsFinite(playbackSpeed) || playbackSpeed <= 0)
            throw new InvalidOperationException("[HomeSleep] 별도로 배치한 수면 Animator와 유효한 클립·재생 속도를 설정하세요.");
    }
}
