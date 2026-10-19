using UnityEngine;

/// <summary>윗면이 열린 잔. 입구를 통과한 입자를 추적해 깊이 밀려나도 바닥/벽 안으로 되돌린다.</summary>
public readonly struct SphContainerBoundary
{
    readonly Matrix4x4 toLocal;
    readonly Matrix4x4 toWorld;
    readonly Vector2 xAxis;
    readonly Vector2 yAxis;
    readonly float width;
    readonly float height;
    readonly float innerWidth;
    readonly float floor;

    public SphContainerBoundary(Transform container, Vector2 halfExtents, float radius)
    {
        toLocal = container.worldToLocalMatrix;
        toWorld = container.localToWorldMatrix;
        width = Mathf.Abs(halfExtents.x);
        height = Mathf.Abs(halfExtents.y);
        Vector2 worldX = toWorld.MultiplyVector(Vector3.right);
        Vector2 worldY = toWorld.MultiplyVector(Vector3.up);
        float scaleX = Mathf.Max(worldX.magnitude, 0.0001f);
        float scaleY = Mathf.Max(worldY.magnitude, 0.0001f);
        xAxis = worldX / scaleX;
        yAxis = worldY / scaleY;
        innerWidth = Mathf.Max(0f, width - radius / scaleX);
        floor = -height + Mathf.Min(radius / scaleY, height);
    }

    public static void Constrain(SphParticle particle, Transform container, Vector2 halfExtents, float radius)
        => new SphContainerBoundary(container, halfExtents, radius).Constrain(particle);

    public void Constrain(SphParticle particle)
    {
        Vector3 local = toLocal.MultiplyPoint3x4(particle.pos);

        if (!particle.insideContainer)
        {
            Vector3 previous = toLocal.MultiplyPoint3x4(particle.previousPos);
            bool startedInside = Mathf.Abs(previous.x) <= width && Mathf.Abs(previous.y) <= height;
            bool crossedMouth = false;
            if (previous.y > height && local.y <= height)
            {
                float t = (previous.y - height) / (previous.y - local.y);
                crossedMouth = Mathf.Abs(Mathf.Lerp(previous.x, local.x, t)) <= width;
            }
            if (!startedInside && !crossedMouth) return;
            particle.insideContainer = true;
        }

        // 위로 넘친 액체는 자유롭게 흘러나간다. 잔 옆으로 빗나간 액체도 안으로 끌어오지 않는다.
        if (local.y > height)
        {
            particle.insideContainer = false;
            return;
        }

        float clampedX = Mathf.Clamp(local.x, -innerWidth, innerWidth);
        float clampedY = Mathf.Max(local.y, floor);
        // 경계 안의 좌표를 매번 월드→로컬→월드로 왕복시키면 비균일 스케일과
        // 무대 오프셋의 반올림 오차가 속도로 누적된다. 실제 침투량만 보정한다.
        Vector3 correction = new Vector3(clampedX-local.x,clampedY-local.y,0f);
        if (correction.sqrMagnitude > 0f)
            particle.pos += (Vector2)toWorld.MultiplyVector(correction);
        local.x = clampedX;
        local.y = clampedY;

        float vx = Vector2.Dot(particle.velocity, xAxis);
        float vy = Vector2.Dot(particle.velocity, yAxis);
        const float epsilon = 0.00001f;
        if ((local.x <= -innerWidth + epsilon && vx < 0f) ||
            (local.x >= innerWidth - epsilon && vx > 0f))
            particle.velocity -= vx * xAxis;
        if (local.y <= floor + epsilon && vy < 0f)
            particle.velocity -= vy * yAxis;
    }
}
