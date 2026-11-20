using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>공통 NPC 목록에서 호출하는 플레이어 배치·걷기·카메라 구도. 대사나 진행 플래그는 소유하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class NpcSceneStaging : MonoBehaviour
{
    [SerializeField] private CameraControllerNew sceneCamera;
    private Transform player;
    private PlayerMovement2D playerMovement;
    private PlayerAnimator2D playerAnimator;
    private CancellationTokenSource session, playerAction, cameraAction;
    private CameraControllerNew.FrameOverride cameraFrame;
    private Vector3 startPosition;
    private bool startFacingRight;
    private int generation;
    public bool IsInSession => session != null;
    public bool IsMovingPlayer => playerAction != null;
    public bool HasCameraOverride => cameraFrame?.IsValid == true;

    public void Initialize(Transform value)
    {
        if (session != null) throw new InvalidOperationException("NPC 연출 중에는 플레이어 연결을 바꿀 수 없습니다.");
        if (value == null || value.gameObject.scene != gameObject.scene)
            throw new InvalidOperationException("NPC 연출의 현재 씬 플레이어를 연결하세요.");
        player = value;
        playerMovement = value.GetComponent<PlayerMovement2D>();
        playerAnimator = value.GetComponent<PlayerAnimator2D>();
    }

    public void BeginSession()
    {
        if (session != null || player == null) throw new InvalidOperationException("NPC 연출 세션 또는 플레이어 연결을 확인하세요.");
        generation++;
        startPosition = player.position;
        startFacingRight = playerMovement != null && playerMovement.IsFacingRight;
        session = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
    }

    public void FacePlayer(Vector3 target)
    {
        RequireSession();
        ValidatePosition(target);
        float delta = target.x - player.position.x;
        if (Mathf.Abs(delta) > .0001f) FacePlayer(delta > 0);
    }

    public void FacePlayer(bool right)
    {
        RequireSession();
        if (playerMovement != null) playerMovement.Face(right);
    }

    public void SetPlayerPosition(Vector3 position)
    {
        RequireSession();
        ValidatePosition(position);
        if (playerAction != null) throw new InvalidOperationException("이동 중인 플레이어를 순간 배치할 수 없습니다.");
        player.position = position;
        playerAnimator?.Stop();
    }

    /// <summary>상호작용의 입력 잠금 아래에서 지정 시간에 정확한 위치로 이동한다. 충돌 없는 연출 경로는 작성자가 정한다.</summary>
    public async UniTask MovePlayerAsync(Vector3 destination, float duration, NpcMotionEase ease, CancellationToken token,
        bool keepFacing = false)
    {
        RequireSession();
        ValidatePosition(destination);
        ValidateDuration(duration);
        if (!Enum.IsDefined(typeof(NpcMotionEase), ease)) throw new ArgumentOutOfRangeException(nameof(ease));
        if (playerAction != null) throw new InvalidOperationException("플레이어 이동 연출은 동시에 하나만 실행할 수 있습니다.");
        token.ThrowIfCancellationRequested();
        int currentGeneration = generation;
        var from = player.position;
        bool facing = playerMovement != null && playerMovement.IsFacingRight;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, session.Token);
        playerAction = source;
        bool completed = false;
        try
        {
            float delta = destination.x - from.x;
            if (!keepFacing && Mathf.Abs(delta) > .0001f && playerMovement != null) playerMovement.Face(delta > 0);
            for (float elapsed = 0; elapsed < duration;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (UIDisplayOptions.IsOptionOpen) { playerAnimator?.Stop(); continue; }
                float dt = Time.deltaTime;
                elapsed = Mathf.Min(elapsed + dt, duration);
                float previousX = player.position.x;
                player.position = Vector3.LerpUnclamped(from, destination, NpcMotionCurves.Evaluate(ease, elapsed / duration));
                playerAnimator?.Handle(dt > 0 ? (player.position.x - previousX) / dt : 0);
            }
            source.Token.ThrowIfCancellationRequested();
            player.position = destination;
            completed = true;
        }
        finally
        {
            if (generation == currentGeneration && session != null && player != null)
            {
                if (!completed)
                {
                    player.position = from;
                    if (playerMovement != null) playerMovement.Face(facing);
                }
                playerAnimator?.Stop();
            }
            if (ReferenceEquals(playerAction, source)) playerAction = null;
        }
    }

    /// <summary>웹과 같은 지수 추적(초당 계수5)으로 구도를 맞추고 최소 대기 시간과 정착 기준을 모두 만족하면 끝난다.</summary>
    public UniTask FocusCameraAsync(Vector3 center, float width, float minimumDuration, CancellationToken token)
        => FrameCameraAsync(center, width, minimumDuration, null, false, token);

    /// <summary>정해진 시간·곡선으로 카메라를 이동한다. 집 안 크리스 등장 같은 고정 시간 팬에 사용한다.</summary>
    public UniTask PanCameraAsync(Vector3 center, float width, float duration, NpcMotionEase ease, CancellationToken token)
    {
        ValidateDuration(duration);
        if (!Enum.IsDefined(typeof(NpcMotionEase), ease)) throw new ArgumentOutOfRangeException(nameof(ease));
        return FrameCameraAsync(center, width, duration, ease, false, token);
    }

    public UniTask RestoreCameraAsync(float minimumDuration, CancellationToken token)
    {
        RequireSession();
        ValidateDuration(minimumDuration, true);
        token.ThrowIfCancellationRequested();
        if (cameraFrame == null) return UniTask.CompletedTask;
        return FrameCameraAsync(default, 1, minimumDuration, null, true, token);
    }

    /// <summary>암전 안에서 카메라의 팬 시작점을 바로 준비한다.</summary>
    public void SetCameraFrame(Vector3 center, float width)
    {
        RequireSession();
        ValidatePosition(center);
        ValidateWidth(width);
        if (cameraAction != null) throw new InvalidOperationException("이동 중인 카메라 구도를 덮어쓸 수 없습니다.");
        EnsureCameraFrame();
        cameraFrame.SetFrame(cameraFrame.Confine(center, width), width);
    }

    private async UniTask FrameCameraAsync(Vector3 center, float width, float duration, NpcMotionEase? ease,
        bool restore, CancellationToken token)
    {
        RequireSession();
        ValidateDuration(duration, true);
        ValidatePosition(center);
        ValidateWidth(width);
        if (cameraAction != null) throw new InvalidOperationException("카메라 연출은 동시에 하나만 실행할 수 있습니다.");
        token.ThrowIfCancellationRequested();
        EnsureCameraFrame();
        var frame = cameraFrame;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, session.Token);
        cameraAction = source;
        var from = frame.Center;
        float fromWidth = frame.Width;
        float elapsed = 0;
        bool completed = false;
        try
        {
            while (true)
            {
                source.Token.ThrowIfCancellationRequested();
                if (restore) { center = frame.FollowCenter; width = frame.FollowWidth; }
                center = frame.Confine(center, width);
                center.z = from.z;
                if (elapsed >= duration && (ease.HasValue || Settled(frame.Center, center, frame.Width, width))) break;
                await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                source.Token.ThrowIfCancellationRequested();
                if (UIDisplayOptions.IsOptionOpen) continue;
                float dt = Time.deltaTime;
                elapsed += dt;
                if (ease.HasValue)
                {
                    float p = NpcMotionCurves.Evaluate(ease.Value, elapsed / duration);
                    frame.SetFrame(Vector3.LerpUnclamped(from, center, p), Mathf.LerpUnclamped(fromWidth, width, p));
                }
                else
                {
                    float p = 1 - Mathf.Exp(-dt * 5);
                    frame.SetFrame(Vector3.LerpUnclamped(frame.Center, center, p), Mathf.LerpUnclamped(frame.Width, width, p));
                }
            }
            source.Token.ThrowIfCancellationRequested();
            frame.SetFrame(center, width);
            if (restore)
            {
                frame.Dispose();
                if (ReferenceEquals(cameraFrame, frame)) cameraFrame = null;
            }
            completed = true;
        }
        finally
        {
            if (!completed && frame.IsValid && ReferenceEquals(cameraFrame, frame)) frame.SetFrame(from, fromWidth);
            if (ReferenceEquals(cameraAction, source)) cameraAction = null;
        }
    }

    private static bool Settled(Vector3 current, Vector3 target, float width, float targetWidth) =>
        Mathf.Abs(current.x - target.x) < .015f && Mathf.Abs(current.y - target.y) < .015f && Mathf.Abs(width - targetWidth) < .015f;

    private void EnsureCameraFrame()
    {
        if (cameraFrame != null) return;
        if (sceneCamera == null)
            sceneCamera = FindObjectsByType<CameraControllerNew>(FindObjectsSortMode.None)
                .SingleOrDefault(value => value.gameObject.scene == gameObject.scene);
        if (sceneCamera == null) throw new InvalidOperationException("NPC 카메라 연출에 현재 씬 CameraControllerNew를 연결하세요.");
        cameraFrame = sceneCamera.BeginFrameOverride();
    }

    public void EndSession(bool restorePlayerPose)
    {
        var ended = session;
        session = null;
        generation++;
        ended?.Cancel();
        playerAction = null;
        cameraAction = null;
        cameraFrame?.Dispose();
        cameraFrame = null;
        if (player != null)
        {
            if (ended != null && restorePlayerPose)
            {
                player.position = startPosition;
                if (playerMovement != null) playerMovement.Face(startFacingRight);
            }
            playerAnimator?.Stop();
        }
        ended?.Dispose();
    }

    private void RequireSession()
    {
        if (session == null || player == null || !isActiveAndEnabled)
            throw new InvalidOperationException("NPC 연출 세션이 시작되지 않았습니다.");
        session.Token.ThrowIfCancellationRequested();
    }

    private static void ValidatePosition(Vector3 value)
    {
        if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z))
            throw new ArgumentOutOfRangeException(nameof(value));
    }
    private static void ValidateWidth(float value)
    {
        if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
    private static void ValidateDuration(float value, bool zero = false)
    {
        if (!float.IsFinite(value) || value < 0 || !zero && value == 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
    private void OnDisable() => EndSession(true);
}
