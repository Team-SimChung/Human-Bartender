using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>연출에서 지정한 지점까지 NPC를 이동한다. 대본·재화·게임 진행 상태는 다루지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class NpcMovement2D : MonoBehaviour
{
    [Header("이동")]
    [SerializeField, Min(.01f)] private float moveSpeed = 1.2f;
    [SerializeField] private bool facingRight = true;

    private CancellationTokenSource movement;
    public Vector2 Velocity { get; private set; }
    public float Speed => Velocity.magnitude;
    public bool FacingRight => facingRight;
    public bool IsMoving => movement != null;

    internal void Face(bool right)
    {
        if (movement != null) throw new InvalidOperationException("이동 중에는 방향을 직접 바꿀 수 없습니다.");
        facingRight = right;
    }

    internal void Configure(float speed)
    {
        if (movement != null) throw new InvalidOperationException("NPC 이동 중에는 이동 설정을 바꿀 수 없습니다.");
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        moveSpeed = speed;
    }

    /// <summary>성공하면 도착 위치를 유지한다. 취소되면 이 이동의 시작 위치로 복구한다.</summary>
    public UniTask MoveToAsync(Vector3 destination, CancellationToken token)
        => MoveAsync(destination, null, NpcMotionEase.Linear, null, token);

    /// <summary>작성된 이동 속도를 바꾸지 않고 지정 시간 동안 월드 좌표를 선형 이동한다.</summary>
    public UniTask MoveToAsync(Vector3 destination, float duration, CancellationToken token)
        => MoveAsync(destination, duration, NpcMotionEase.Linear, null, token);

    public UniTask MoveToAsync(Vector3 destination, float duration, NpcMotionEase ease, CancellationToken token)
        => MoveAsync(destination, duration, ease, null, token);

    // 이동과 페이드가 같은 프레임·경과 시간을 사용하도록 Actor에 진행 시간을 전달한다.
    internal UniTask MoveToAsync(Vector3 destination, float duration, NpcMotionEase ease,
        Action<float> onElapsed, CancellationToken token)
        => MoveAsync(destination, duration, ease, onElapsed, token);

    internal UniTask MoveArcToAsync(Vector3 destination, float duration, float height, CancellationToken token)
        => MoveAsync(destination, duration, NpcMotionEase.Linear, null, token, height);

    private async UniTask MoveAsync(Vector3 destination, float? duration, NpcMotionEase ease,
        Action<float> onElapsed, CancellationToken token, float arcHeight = 0)
    {
        if (!isActiveAndEnabled || movement != null)
            throw new InvalidOperationException("NPC 이동체가 비활성 상태이거나 이미 이동 중입니다.");
        if (!float.IsFinite(moveSpeed) || moveSpeed <= 0 ||
            !float.IsFinite(destination.x) || !float.IsFinite(destination.y) || !float.IsFinite(destination.z))
            throw new InvalidOperationException("NPC의 이동 속도와 목적지를 확인하세요.");
        if (duration.HasValue && (!float.IsFinite(duration.Value) || duration.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (!Enum.IsDefined(typeof(NpcMotionEase), ease)) throw new ArgumentOutOfRangeException(nameof(ease));
        if (!float.IsFinite(arcHeight) || arcHeight < 0) throw new ArgumentOutOfRangeException(nameof(arcHeight));
        token.ThrowIfCancellationRequested();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        movement = source;
        var start = transform.position;
        bool startFacingRight = facingRight;
        bool completed = false;
        try
        {
            if (duration.HasValue)
            {
                // 기존 월드 연출은 수직 이동에서도 오른쪽을 보도록 반전을 해제했다.
                facingRight = destination.x >= start.x;
                float elapsed = 0;
                onElapsed?.Invoke(0);
                while (elapsed < duration.Value)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                    source.Token.ThrowIfCancellationRequested();
                    var previous = transform.position;
                    float deltaTime = Time.deltaTime;
                    elapsed = Mathf.Min(elapsed + deltaTime, duration.Value);
                    transform.position = Vector3.LerpUnclamped(start, destination,
                        NpcMotionCurves.Evaluate(ease, elapsed / duration.Value)) +
                        Vector3.up * (Mathf.Sin(Mathf.PI * elapsed / duration.Value) * arcHeight);
                    Velocity = deltaTime > 0 ? (Vector2)(transform.position - previous) / deltaTime : Vector2.zero;
                    onElapsed?.Invoke(elapsed);
                }
            }
            else
            {
                while ((transform.position - destination).sqrMagnitude > .000001f)
                {
                    source.Token.ThrowIfCancellationRequested();
                    var previous = transform.position;
                    float deltaTime = Time.deltaTime;
                    transform.position = Vector3.MoveTowards(previous, destination, moveSpeed * deltaTime);
                    Velocity = deltaTime > 0 ? (Vector2)(transform.position - previous) / deltaTime : Vector2.zero;
                    if (Mathf.Abs(Velocity.x) > .0001f) facingRight = Velocity.x > 0;
                    await UniTask.Yield(PlayerLoopTiming.Update, source.Token);
                }
            }
            source.Token.ThrowIfCancellationRequested();
            transform.position = destination;
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                if (this != null) transform.position = start;
                facingRight = startFacingRight;
            }
            Velocity = Vector2.zero;
            if (ReferenceEquals(movement, source)) movement = null;
        }
    }

    public void Stop()
    {
        movement?.Cancel();
        Velocity = Vector2.zero;
    }

    private void OnDisable() => Stop();
}
