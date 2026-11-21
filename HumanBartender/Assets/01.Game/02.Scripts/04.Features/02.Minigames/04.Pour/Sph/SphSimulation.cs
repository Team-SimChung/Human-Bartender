using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 위치 기반 밀도 보정으로 압축을 제한한다. 중력은 모든 입자에 계속 적용한다.
/// 기존 밀도 커널의 두 배 가중치는 유지하고, 위치 보정의 미분에도 같은 가중치를 쓴다.
/// PourManager가 고정 시간 간격으로 Tick을 호출한다.
/// </summary>
public class SphSimulation
{
    public SphConfig config;
    public bool SynchronizeTransforms { get; set; } = true;

    /// <summary>CalibrateRestDensity()가 실측해 채우는 기준 밀도. 보정 전에는 config 값을 쓴다.</summary>
    float restDensity;

    readonly List<SphParticle> activeParticles = new List<SphParticle>();
    public IReadOnlyList<SphParticle> ActiveParticles => activeParticles;
    public int LastDensityIterations { get; private set; }

    readonly List<SphParticle>[] grid;
    readonly List<int> occupiedCells = new List<int>();
    readonly Stack<List<SphParticle>> emptyCells = new Stack<List<SphParticle>>();
    readonly int gridSizeX;
    readonly int gridSizeY;
    readonly float cellSize;
    readonly Vector2 gridMin;
    readonly Action<SphParticle> constrainParticle;

    public SphSimulation(SphConfig config, Vector2 gridMin, Vector2 gridMax,
        Action<SphParticle> constrainParticle = null)
    {
        this.config = config;
        this.restDensity = config.restDensity;
        this.gridMin = gridMin;
        this.constrainParticle = constrainParticle;
        Vector2 area = gridMax - gridMin;
        float radius = Mathf.Max(config.NeighborRadius, 0.0001f);
        // 셀 크기를 이웃 반경에 맞춰 불필요한 입자 비교를 줄인다.
        // 비어 있는 셀은 목록을 만들지 않고, 사용했던 목록도 재사용한다.
        cellSize = Mathf.Max(radius, Mathf.Max(area.x, area.y) / 512f);
        gridSizeX = Mathf.Max(1, Mathf.CeilToInt(area.x / cellSize));
        gridSizeY = Mathf.Max(1, Mathf.CeilToInt(area.y / cellSize));
        grid = new List<SphParticle>[gridSizeX * gridSizeY];
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

    void TickPositionBased(float dt)
    {
        // 압력 힘과 위치 보정을 동시에 적용하면 같은 압축을 두 번 밀어내며 튀게 된다.
        // 위치 보정 모드에서는 중력으로 예측하고 밀도 제약 한 가지로 압축을 푼다.
        foreach (var p in activeParticles)
        {
            p.Integrate(dt);
            constrainParticle?.Invoke(p);
        }
        CorrectDensity();

        foreach (var p in activeParticles)
        {
            // 공중의 겹침 정리를 발사 속도로 바꾸지 않는다. 중력/방출 속도는 계속 유지한다.
            // 잔 안에서는 보정된 이동량을 속도에 반영해 압력에 따른 옆 흐름을 유지한다.
            // 아직 고인 액체에 합류하지 않은 입자는 수면에 부딪혀 밀려난 거리를 위로 튀는 속도로 바꾸지 않는다.
            if (!p.insideContainer) continue;
            if (p.volumeReceived) p.UpdateVelocityFromPosition(dt);
            else p.UpdateVelocityWithoutRebound(dt);
        }
        ApplyFluidViscosity(dt);
        LimitPooledSpeed();
        foreach (var p in activeParticles)
        {
            constrainParticle?.Invoke(p);
            p.CaptureRenderImpact(dt);
            if (SynchronizeTransforms) p.transform.position = p.pos;
        }
    }

    /// <summary>
    /// 굵은 물줄기가 고인 액체에 꽂히면 밀도 보정이 밀어낸 거리가 그대로 속도가 되어(UpdateVelocityFromPosition)
    /// 액체가 옆으로 밀려나고 줄기 아래에 V자 구덩이가 파인다. 고인 액체의 속도를 제한해 충격을 흡수한다.
    /// 고인 액체는 수량으로 인정된 입자와 그 입자에 닿은 입자다. 밀도로 고르면 벽에 붙어 떨어지는 3줄 줄기도
    /// 기준 밀도의 약 92%라 걸리기 쉽다. 걸리면 입구 바로 아래에서 줄기가 멈춰 막히고, 뒤 입자가 쌓여 갈라진다.
    /// 마지막 밀도 계산의 이웃 목록을 재사용하고, 아직 합류하지 않은 입자의 이웃만 확인하므로 비용이 거의 없다.
    /// </summary>
    void LimitPooledSpeed()
    {
        float limit = config.poolSpeedLimit;
        if (limit <= 0f) return;
        float limitSquared = limit * limit;
        foreach (var p in activeParticles)
        {
            if (!p.insideContainer || p.velocity.sqrMagnitude <= limitSquared) continue;
            bool pooled = p.volumeReceived;
            for (int i = 0; !pooled && i < p.neighbours.Count; i++)
                pooled = p.neighbours[i].particle.volumeReceived;
            if (pooled) p.velocity = p.velocity.normalized * limit;
        }
    }

    void ApplyFluidViscosity(float dt)
    {
        // 매 스텝 속도를 크게 깎으면 액체가 모래처럼 경사진 상태로 멈춘다.
        // 이웃 수로 정규화하고 초당 감쇠율로 적용해 수평 이동을 과하게 죽이지 않는다.
        float rate = Mathf.Max(0f, config.viscosity) * 4f + Mathf.Clamp01(config.velocitySmoothing) * 8f;
        float blend = Mathf.Min(0.5f, 1f - Mathf.Exp(-rate * dt));
        if (blend <= 0f) return;
        foreach (var p in activeParticles) p.smoothedVelocity = p.velocity;
        foreach (var p in activeParticles)
        {
            foreach (var neighbour in p.neighbours)
            {
                var n = neighbour.particle;
                if (p.solverIndex >= n.solverIndex) continue;
                float weight = neighbour.weight / Mathf.Max(1f, Mathf.Max(p.neighbourWeight, n.neighbourWeight));
                Vector2 exchange = (n.velocity - p.velocity) * (blend * weight);
                p.smoothedVelocity += exchange;
                n.smoothedVelocity -= exchange;
            }
        }
        foreach (var p in activeParticles) p.velocity = p.smoothedVelocity;
    }

    public void Tick(float dt)
    {
        LastDensityIterations = 0;
        if (dt <= 0f || activeParticles.Count == 0) return;
        foreach (var p in activeParticles)
            p.ResetForFrame();

        if (config.densityIterations > 0)
        {
            TickPositionBased(dt);
            return;
        }

        AssignToGrid();
        CalculateDensity();

        foreach (var p in activeParticles)
            p.CalculatePressure(restDensity);

        ApplyPressureForce();
        ApplyViscosity();
        SmoothVelocities();

        foreach (var p in activeParticles)
        {
            p.Integrate(dt);
            constrainParticle?.Invoke(p);
        }

        CorrectDensity();

        foreach (var p in activeParticles)
        {
            p.UpdateVelocityFromPosition(dt);
            // 보정된 위치로 속도를 다시 구한 뒤에도 벽을 향하는 속도 성분은 제거한다.
            constrainParticle?.Invoke(p);
            p.CaptureRenderImpact(dt);
            if (SynchronizeTransforms) p.transform.position = p.pos;
        }
    }

    void CorrectDensity()
    {
        float radius = Mathf.Max(config.NeighborRadius, 0.0001f);
        float targetDensity = Mathf.Max(restDensity, 2.0001f); // 자기 자신만의 밀도 기여는 2다.
        float tolerance = Mathf.Clamp(config.densityTolerance, 0f, 0.2f);
        float regularizer = 0.0001f / Mathf.Max(config.spacing * config.spacing, 0.000001f);
        float maxCorrection = config.spacing * 0.2f;
        int iterations = Mathf.Clamp(config.densityIterations, 0, 8);
        float gradientScale = -4f / (radius * targetDensity);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            // 이웃은 스텝의 첫 보정에서만 격자로 찾는다. 이후 보정은 그 쌍의 거리만 다시 잰다.
            if (iteration == 0)
            {
                AssignToGrid();
                CalculateDensity();
            }
            else RefreshDensity();
            float maxError = 0f;
            foreach (var p in activeParticles)
            {
                float error = Mathf.Max(0f, p.density / targetDensity - 1f - tolerance);
                maxError = Mathf.Max(maxError, error);
                if (error <= 0f)
                {
                    p.densityLambda = 0f;
                    continue;
                }
                float gradientX = 0f;
                float gradientY = 0f;
                float gradientSquares = 0f;
                for (int i = 0; i < p.neighbours.Count; i++)
                {
                    var neighbour = p.neighbours[i];
                    float scale = gradientScale * neighbour.weight;
                    float x = neighbour.direction.x * scale;
                    float y = neighbour.direction.y * scale;
                    gradientX += x;
                    gradientY += y;
                    gradientSquares += x * x + y * y;
                }
                p.densityLambda = -error / (gradientSquares + gradientX * gradientX + gradientY * gradientY + regularizer);
            }
            if (maxError <= 0f) break;

            // Jacobi 보정: 모두 계산한 다음 함께 적용해 입자 순서에 따른 편향을 막는다.
            foreach (var p in activeParticles)
            {
                float x = 0f;
                float y = 0f;
                for (int i = 0; i < p.neighbours.Count; i++)
                {
                    var neighbour = p.neighbours[i];
                    float scale = (p.densityLambda + neighbour.particle.densityLambda) * gradientScale * neighbour.weight;
                    x += neighbour.direction.x * scale;
                    y += neighbour.direction.y * scale;
                }
                p.densityCorrection = Vector2.ClampMagnitude(new Vector2(x, y), maxCorrection);
            }
            foreach (var p in activeParticles)
            {
                p.pos += p.densityCorrection;
                constrainParticle?.Invoke(p);
            }
            LastDensityIterations++;
        }
    }

    static Vector2 SeparationDirection(SphParticle p, SphParticle n, float distance)
    {
        if (distance > 0.000001f) return (p.pos - n.pos) / distance;

        // 완전히 같은 위치도 분리한다. 두 입자에 반대 방향을 써 전체를 한쪽으로 밀지 않는다.
        int low = Mathf.Min(p.solverIndex, n.solverIndex);
        int high = Mathf.Max(p.solverIndex, n.solverIndex);
        uint hash = unchecked((uint)(low * 73856093 ^ high * 19349663));
        float angle = (hash & 1023) * (2f * Mathf.PI / 1024f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return p.solverIndex < n.solverIndex ? direction : -direction;
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

        foreach (var p in activeParticles)
        {
            Vector2 weightedSum = Vector2.zero;
            float weightTotal = 0f;

            foreach (var neighbour in p.neighbours)
            {
                weightedSum += neighbour.particle.velocity * neighbour.weight;
                weightTotal += neighbour.weight;
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
        foreach (int index in occupiedCells)
        {
            var cell = grid[index];
            cell.Clear();
            emptyCells.Push(cell);
            grid[index] = null;
        }
        occupiedCells.Clear();
        float inverseCellSize = 1f / cellSize;

        for (int i = 0; i < activeParticles.Count; i++)
        {
            var p = activeParticles[i];
            p.solverIndex = i;
            p.gridX = Mathf.Clamp(Mathf.FloorToInt((p.pos.x - gridMin.x) * inverseCellSize), 0, gridSizeX - 1);
            p.gridY = Mathf.Clamp(Mathf.FloorToInt((p.pos.y - gridMin.y) * inverseCellSize), 0, gridSizeY - 1);
            int index = p.gridY * gridSizeX + p.gridX;
            var cell = grid[index];
            if (cell == null)
            {
                cell = emptyCells.Count > 0 ? emptyCells.Pop() : new List<SphParticle>(8);
                grid[index] = cell;
                occupiedCells.Add(index);
            }
            cell.Add(p);
        }
    }

    void CalculateDensity()
    {
        float r = config.NeighborRadius;
        float radiusSquared = r * r;
        int searchRadius = Mathf.Max(1, Mathf.CeilToInt(r / cellSize));

        // 이웃 쌍마다 도는 가장 뜨거운 루프다. Mono는 작은 함수도 잘 인라인하지 않으므로 커널 계산을
        // RefreshDensity와 같은 식으로 직접 적고, 밀도 합은 지역 변수에 모아 입자당 한 번만 쓴다.
        foreach (var p in activeParticles)
        {
            float density = 2f, densityNear = 2f, neighbourWeight = 0f;
            float px = p.pos.x, py = p.pos.y;
            var neighbours = p.neighbours;
            neighbours.Clear();
            int minX = Mathf.Max(0, p.gridX - searchRadius);
            int maxX = Mathf.Min(gridSizeX - 1, p.gridX + searchRadius);
            int minY = Mathf.Max(0, p.gridY - searchRadius);
            int maxY = Mathf.Min(gridSizeY - 1, p.gridY + searchRadius);
            for (int gy = minY; gy <= maxY; gy++)
            {
                int row = gy * gridSizeX;
                for (int gx = minX; gx <= maxX; gx++)
                {
                    var cell = grid[row + gx];
                    if (cell == null) continue;
                    for (int c = 0; c < cell.Count; c++)
                    {
                        var n = cell[c];
                        if (ReferenceEquals(n, p)) continue;
                        float dx = px - n.pos.x;
                        float dy = py - n.pos.y;
                        float distanceSquared = dx * dx + dy * dy;
                        if (distanceSquared >= radiusSquared) continue;
                        float dist = Mathf.Sqrt(distanceSquared);
                        float normalDistance = 1f - dist / r;
                        float weightSquared = normalDistance * normalDistance;
                        density += 2f * weightSquared;
                        densityNear += 2f * weightSquared * normalDistance;
                        neighbourWeight += normalDistance;
                        Vector2 direction = dist > 0.000001f ? new Vector2(dx / dist, dy / dist) : SeparationDirection(p, n, dist);
                        neighbours.Add(new SphNeighbour(n, normalDistance, direction));
                    }
                }
            }
            p.density = density;
            p.densityNear = densityNear;
            p.neighbourWeight = neighbourWeight;
        }
    }

    /// <summary>
    /// 같은 물리 스텝의 두 번째 밀도 보정부터는 첫 탐색에서 찾은 이웃 쌍만 다시 잰다.
    /// 보정 한 번은 입자를 spacing×0.2 이하로만 옮기므로, 그사이 반경 안으로 새로 들어오는 이웃은
    /// 커널 끝(가중치 ≈ 0)뿐이다. 격자 재구성과 주변 셀 전체 비교를 반복마다 하지 않아도 결과가 거의 같다.
    /// 커널 식은 CalculateDensity와 같다(인라인 이유도 같다).
    /// </summary>
    void RefreshDensity()
    {
        float r = config.NeighborRadius;
        float radiusSquared = r * r;

        foreach (var p in activeParticles)
        {
            float density = 2f, densityNear = 2f, neighbourWeight = 0f;
            float px = p.pos.x, py = p.pos.y;
            var neighbours = p.neighbours;
            for (int i = neighbours.Count - 1; i >= 0; i--)
            {
                SphParticle n = neighbours[i].particle;
                float dx = px - n.pos.x;
                float dy = py - n.pos.y;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared >= radiusSquared)
                {
                    // 반경 밖으로 나간 쌍은 끝 항목과 바꿔 지운다. 뒤에서부터 도므로 옮겨 온 항목은 이미 계산했다.
                    int last = neighbours.Count - 1;
                    neighbours[i] = neighbours[last];
                    neighbours.RemoveAt(last);
                    continue;
                }
                float dist = Mathf.Sqrt(distanceSquared);
                float normalDistance = 1f - dist / r;
                float weightSquared = normalDistance * normalDistance;
                density += 2f * weightSquared;
                densityNear += 2f * weightSquared * normalDistance;
                neighbourWeight += normalDistance;
                Vector2 direction = dist > 0.000001f ? new Vector2(dx / dist, dy / dist) : SeparationDirection(p, n, dist);
                neighbours[i] = new SphNeighbour(n, normalDistance, direction);
            }
            p.density = density;
            p.densityNear = densityNear;
            p.neighbourWeight = neighbourWeight;
        }
    }

    void ApplyPressureForce()
    {
        foreach (var p in activeParticles)
        {
            Vector2 pressureForce = Vector2.zero;

            foreach (var neighbour in p.neighbours)
            {
                SphParticle n = neighbour.particle;
                float normalDistance = neighbour.weight;
                float totalPressure =
                    (p.pressure + n.pressure) * normalDistance * normalDistance +
                    (p.pressureNear + n.pressureNear) * normalDistance * normalDistance * normalDistance;

                Vector2 pressureVector = -totalPressure * neighbour.direction;
                n.force += pressureVector;
                pressureForce += pressureVector;
            }

            p.force -= pressureForce;
        }
    }

    void ApplyViscosity()
    {
        float sigma = config.viscosity;

        foreach (var p in activeParticles)
        {
            foreach (var neighbour in p.neighbours)
            {
                SphParticle n = neighbour.particle;
                Vector2 normalDir = -neighbour.direction;
                float velocityDifference = Vector2.Dot(p.velocity - n.velocity, normalDir);

                if (velocityDifference > 0f)
                {
                    Vector2 viscosityForce = neighbour.weight * velocityDifference * sigma * normalDir;
                    p.velocity -= viscosityForce * 0.5f;
                    n.velocity += viscosityForce * 0.5f;
                }
            }
        }
    }
}
