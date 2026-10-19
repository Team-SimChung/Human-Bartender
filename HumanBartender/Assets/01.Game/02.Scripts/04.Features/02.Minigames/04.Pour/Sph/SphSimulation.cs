using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SPH 밀도/압력/점성 계산 + 공간 분할 그리드. AlexandreSajus/Unity-Fluid-Simulation의 Simulation.cs를
/// 최대한 그대로 이식했다(밀도·압력힘·점성힘을 "쌍을 이루는 두 파티클 모두에서" 누적하는 구조 포함 —
/// 이 이중 누적은 원본 K 계수 튜닝에 이미 녹아있는 특성이라 그대로 유지, 임의로 "정리"하면 검증된
/// 레퍼런스 거동과 달라질 위험이 있어 그대로 둔다).
///
/// PourManager가 파티클들의 Integrate()가 끝난 뒤 시점에 맞춰 매 프레임 Tick()을 명시적으로 호출한다.
/// </summary>
public class SphSimulation
{
    public SphConfig config;

    /// <summary>CalibrateRestDensity()가 실측해 채우는 기준 밀도. 보정 전에는 config 값을 쓴다.</summary>
    float restDensity;

    readonly List<SphParticle> activeParticles = new List<SphParticle>();
    public IReadOnlyList<SphParticle> ActiveParticles => activeParticles;

    readonly List<SphParticle>[,] grid;
    readonly int gridSizeX;
    readonly int gridSizeY;
    readonly Vector2 gridMin;
    readonly Vector2 gridMax;

    public SphSimulation(SphConfig config, Vector2 gridMin, Vector2 gridMax, int gridSizeX = 24, int gridSizeY = 24)
    {
        this.config = config;
        this.restDensity = config.restDensity;
        this.gridMin = gridMin;
        this.gridMax = gridMax;
        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;

        grid = new List<SphParticle>[gridSizeX, gridSizeY];
        for (int x = 0; x < gridSizeX; x++)
            for (int y = 0; y < gridSizeY; y++)
                grid[x, y] = new List<SphParticle>();
    }

    public void Register(SphParticle particle)
    {
        if (!activeParticles.Contains(particle))
            activeParticles.Add(particle);
    }

    public void Unregister(SphParticle particle)
    {
        activeParticles.Remove(particle);
    }

    /// <summary>
    /// 스폰 직후 격자 상태의 실제 밀도를 재서, 그보다 살짝 낮은 값을 기준 밀도로 삼는다.
    /// 기준 밀도를 손으로 적어두면 spacing/neighborRadius를 조금만 바꿔도 "스폰 간격에서의 밀도"가
    /// 기준보다 낮아지고, 그러면 압력이 밀어내는 게 아니라 끌어당기는 방향이 돼서 액체가 바닥으로
    /// 짓눌려버린다(파티클을 늘려도 병이 안 차던 원인). 재서 잡으면 어떤 설정에서도 항상 밀어낸다.
    /// </summary>
    public void CalibrateRestDensity(float slack = 0.9f)
    {
        foreach (var p in activeParticles)
            p.ResetForFrame();

        AssignToGrid();
        CalculateDensity();

        float maxDensity = 0f;
        foreach (var p in activeParticles)
            maxDensity = Mathf.Max(maxDensity, p.density);

        if (maxDensity > 0f)
            restDensity = maxDensity * slack;
    }

    public void Tick(float dt)
    {
        foreach (var p in activeParticles)
            p.ResetForFrame();

        AssignToGrid();
        CalculateDensity();

        foreach (var p in activeParticles)
            p.CalculatePressure(restDensity);

        ApplyPressureForce();
        ApplyViscosity();
        SmoothVelocities();

        foreach (var p in activeParticles)
            p.Integrate(dt);
    }

    /// <summary>
    /// 각 파티클의 속도를 이웃 평균 쪽으로 조금 당긴다(XSPH 방식의 속도 평활화).
    /// 압력 강성이 높으면 정지 상태에서도 파티클들이 서로 밀고 당기며 계속 떨리는데, 이걸로 그
    /// 상대 진동만 걷어낸다. 전체가 같은 속도로 움직이는 경우(자유낙하)에는 이웃과의 차이가 없어
    /// 아무 영향이 없으므로, 떨어지는 속도는 느려지지 않는다.
    /// </summary>
    void SmoothVelocities()
    {
        float k = config.velocitySmoothing;
        if (k <= 0f) return;

        float r = config.NeighborRadius;

        foreach (var p in activeParticles)
        {
            Vector2 weightedSum = Vector2.zero;
            float weightTotal = 0f;

            foreach (var n in p.neighbours)
            {
                if (n == p) continue;

                float distance = Vector2.Distance(p.pos, n.pos);
                if (distance >= r) continue;

                float weight = 1f - distance / r;
                weightedSum += n.velocity * weight;
                weightTotal += weight;
            }

            p.smoothedVelocity = weightTotal > 0f
                ? Vector2.Lerp(p.velocity, weightedSum / weightTotal, k)
                : p.velocity;
        }

        foreach (var p in activeParticles)
            p.velocity = p.smoothedVelocity;
    }

    void AssignToGrid()
    {
        for (int x = 0; x < gridSizeX; x++)
            for (int y = 0; y < gridSizeY; y++)
                grid[x, y].Clear();

        Vector2 size = gridMax - gridMin;

        foreach (var p in activeParticles)
        {
            p.gridX = Mathf.Clamp(Mathf.FloorToInt((p.pos.x - gridMin.x) / size.x * gridSizeX), 0, gridSizeX - 1);
            p.gridY = Mathf.Clamp(Mathf.FloorToInt((p.pos.y - gridMin.y) / size.y * gridSizeY), 0, gridSizeY - 1);

            grid[p.gridX, p.gridY].Add(p);
        }
    }

    void CalculateDensity()
    {
        float r = config.NeighborRadius;

        foreach (var p in activeParticles)
        {
            for (int gx = p.gridX - 1; gx <= p.gridX + 1; gx++)
            {
                for (int gy = p.gridY - 1; gy <= p.gridY + 1; gy++)
                {
                    if (gx < 0 || gx >= gridSizeX || gy < 0 || gy >= gridSizeY) continue;

                    foreach (var n in grid[gx, gy])
                    {
                        float dist = Vector2.Distance(p.pos, n.pos);
                        if (dist >= r) continue;

                        float normalDistance = 1f - dist / r;
                        p.density += normalDistance * normalDistance;
                        p.densityNear += normalDistance * normalDistance * normalDistance;
                        n.density += normalDistance * normalDistance;
                        n.densityNear += normalDistance * normalDistance * normalDistance;

                        p.neighbours.Add(n);
                    }
                }
            }
        }
    }

    void ApplyPressureForce()
    {
        float r = config.NeighborRadius;

        foreach (var p in activeParticles)
        {
            Vector2 pressureForce = Vector2.zero;

            foreach (var n in p.neighbours)
            {
                Vector2 toNeighbour = n.pos - p.pos;
                float distance = toNeighbour.magnitude;
                if (distance <= 0.0001f) continue;

                float normalDistance = 1f - distance / r;
                float totalPressure =
                    (p.pressure + n.pressure) * normalDistance * normalDistance +
                    (p.pressureNear + n.pressureNear) * normalDistance * normalDistance * normalDistance;

                Vector2 pressureVector = totalPressure * (toNeighbour / distance);
                n.force += pressureVector;
                pressureForce += pressureVector;
            }

            p.force -= pressureForce;
        }
    }

    void ApplyViscosity()
    {
        float r = config.NeighborRadius;
        float sigma = config.viscosity;

        foreach (var p in activeParticles)
        {
            foreach (var n in p.neighbours)
            {
                Vector2 toNeighbour = n.pos - p.pos;
                float distance = toNeighbour.magnitude;
                if (distance <= 0.0001f) continue;

                Vector2 normalDir = toNeighbour / distance;
                float relativeDistance = distance / r;
                float velocityDifference = Vector2.Dot(p.velocity - n.velocity, normalDir);

                if (velocityDifference > 0f)
                {
                    Vector2 viscosityForce = (1f - relativeDistance) * velocityDifference * sigma * normalDir;
                    p.velocity -= viscosityForce * 0.5f;
                    n.velocity += viscosityForce * 0.5f;
                }
            }
        }
    }
}
