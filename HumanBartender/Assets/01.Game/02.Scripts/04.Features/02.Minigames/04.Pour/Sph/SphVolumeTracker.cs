using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 도착하거나, 이미 담긴 액체에 느린 속도로 합류한 입자만 수량으로 인정한다.
/// SPH가 만든 이웃 목록을 재사용한다. 외부로 나간 입자는 즉시 제외하며 재입장도 중복 누적하지 않는다.
/// </summary>
public sealed class SphVolumeTracker
{
    readonly List<SphParticle> received;
    readonly float joinSpeedSquared;

    public int ReceivedCount { get; private set; }
    public int PendingCount { get; private set; }

    public SphVolumeTracker(int capacity, float maxJoinSpeed = 0.75f)
    {
        received = new List<SphParticle>(Mathf.Max(1, capacity));
        joinSpeedSquared = maxJoinSpeed * maxJoinSpeed;
    }

    public void Measure(IReadOnlyList<SphParticle> particles, SphContainerBoundary boundary, float spacing)
    {
        received.Clear();
        PendingCount = 0;
        float floorTolerance = spacing * 0.15f;
        float contactDistanceSquared = spacing * spacing * 1.35f * 1.35f;

        for (int i = 0; i < particles.Count; i++)
        {
            SphParticle p = particles[i];
            bool insideBounds = boundary.TryGetContainedHeight(p.pos, out float heightAboveFloor);
            bool contained = p.insideContainer && insideBounds;
            p.volumeCanJoin = contained && p.velocity.sqrMagnitude <= joinSpeedSquared;
            if (!contained)
            {
                p.volumeReceived = false;
                continue;
            }

            if (p.volumeReceived || heightAboveFloor <= floorTolerance)
            {
                p.volumeReceived = true;
                received.Add(p);
            }
        }

        // 이미 도착한 입자는 잔 안에 있는 동안 유지하여 수면의 작은 흔들림으로 수량이 깜빡이지 않는다.
        // 빠르게 낙하하는 물줄기는 바닥까지 연결돼 있어도 한꺼번에 세지 않는다.
        for (int i = 0; i < received.Count; i++)
        {
            SphParticle p = received[i];
            for (int j = 0; j < p.neighbours.Count; j++)
            {
                SphParticle n = p.neighbours[j].particle;
                if (n.volumeReceived || !n.volumeCanJoin ||
                    (p.pos - n.pos).sqrMagnitude > contactDistanceSquared) continue;
                n.volumeReceived = true;
                received.Add(n);
            }
        }

        ReceivedCount = received.Count;
        for (int i = 0; i < particles.Count; i++)
        {
            SphParticle p = particles[i];
            if (!p.volumeReceived && boundary.CouldStillBeReceived(p.pos)) PendingCount++;
        }
    }
}
