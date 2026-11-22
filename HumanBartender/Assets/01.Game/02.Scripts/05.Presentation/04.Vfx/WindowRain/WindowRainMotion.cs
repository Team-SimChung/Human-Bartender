using UnityEngine;

/// <summary>
/// 카메라 위치에서 유리에 걸리는 바람·관성 신호를 만든다. 단위는 화면 높이/초.
/// 기준 높이를 고정해 줌만으로는 속도가 생기지 않고, 한 프레임에 크게 튄 위치(컷·순간 이동)는
/// 충격으로 읽지 않고 버린다. Pixel Perfect 격자 스냅 같은 계단 이동은 평활화로 흡수한다.
/// </summary>
public sealed class WindowRainMotion
{
    public float velocitySmoothing = 0.25f;
    public float accelerationSmoothing = 0.2f;
    /// <summary>한 프레임 이동이 이 값(화면 높이)을 넘으면 카메라 컷으로 본다.</summary>
    public float cutDistance = 0.25f;
    public float maxSpeed = 2.5f;
    public float maxAcceleration = 8f;

    Vector3 last;
    bool hasLast;

    public Vector2 Velocity { get; private set; }
    public Vector2 Acceleration { get; private set; }
    public int Cuts { get; private set; }
    public bool IsMoving { get; private set; }

    public void Reset()
    {
        hasLast = false;
        Velocity = Acceleration = Vector2.zero;
        IsMoving = false;
    }

    public void Sample(Vector3 position, float referenceHeight, float dt)
    {
        if (dt <= 0f) return; // 일시정지: 신호를 유지하고 적분하지 않는다.
        if (!hasLast || referenceHeight <= 0f)
        {
            last = position;
            hasLast = true;
            return;
        }
        Vector2 delta = (Vector2)(position - last) / referenceHeight;
        last = position;
        if (delta.magnitude > cutDistance)
        {
            // 컷 이후 새 위치를 기준으로 다시 시작한다. 남은 흐름은 방울 자체의 관성으로 이어진다.
            Velocity = Acceleration = Vector2.zero;
            IsMoving = false;
            Cuts++;
            return;
        }
        Vector2 raw = Vector2.ClampMagnitude(delta / dt, maxSpeed);
        Vector2 previous = Velocity;
        Velocity = Vector2.Lerp(Velocity, raw, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, velocitySmoothing)));
        // 픽셀 스냅이나 아주 작은 카메라 흔들림으로 정지/이동이 반복되지 않게 한다.
        IsMoving = Velocity.magnitude > (IsMoving ? 0.015f : 0.04f);
        Vector2 change = (Velocity - previous) / dt;
        Acceleration = Vector2.ClampMagnitude(
            Vector2.Lerp(Acceleration, change, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, accelerationSmoothing))),
            maxAcceleration);
    }
}
