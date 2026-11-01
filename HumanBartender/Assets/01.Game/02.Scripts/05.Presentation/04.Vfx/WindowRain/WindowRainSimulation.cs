using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>화면 앞 유리의 물방울 동작 값. 길이는 가상 픽셀(화면 높이 / virtualRows) 단위다.</summary>
[Serializable]
public sealed class WindowRainSettings
{
    [Tooltip("화면 높이를 몇 칸의 가상 픽셀로 볼지. 해상도·종횡비·카메라 줌과 무관하게 방울의 크기와 픽셀 밀도를 고정한다.")]
    [Min(90)] public int virtualRows = 270;
    [Tooltip("비가 최대일 때 초당 새로 맺히는 작은 방울 수.")]
    [Min(0f)] public float spawnPerSecond = 55f;
    [Tooltip("동시에 유지할 최대 방울 수.")]
    [Min(16)] public int maxDrops = 650;
    [Tooltip("새 방울의 반지름 범위(가상 픽셀). 대부분은 작은 쪽에 몰린다.")]
    public Vector2 spawnRadius = new Vector2(0.55f, 1.9f);
    [Tooltip("큰 빗방울이 유리에 직접 닿는 비율. 작은 방울의 합체와 별개로 드문 물줄기를 만든다.")]
    [Range(0f, 0.2f)] public float largeDropChance = 0.03f;
    [Tooltip("이 반지름(가상 픽셀)을 넘으면 표면 장력을 이기고 흐르기 시작한다.")]
    [Min(0.5f)] public float slideRadius = 3.2f;
    [Tooltip("방울마다, 유리 위치마다 달라지는 붙잡힘 정도. 흐르다 멈칫하는 움직임을 만든다.")]
    [Range(0f, 0.6f)] public float pinVariation = 0.3f;
    [Tooltip("중력 가속도(가상 픽셀/초²).")]
    [Min(0f)] public float gravity = 330f;
    [Tooltip("유리면 마찰. 클수록 종단 속도가 낮다.")]
    [Min(0.1f)] public float drag = 4f;
    [Tooltip("가장 큰 방울의 최대 속도(가상 픽셀/초).")]
    [Min(1f)] public float maxSpeed = 150f;
    [Tooltip("흐르는 방울이 좌우로 구불거리는 정도.")]
    [Range(0f, 1f)] public float meander = 0.35f;
    [Tooltip("흐르는 방울이 뒤에 작은 방울을 남기는 간격(가상 픽셀).")]
    [Min(1f)] public float trailSpacing = 4f;
    [Range(0f, 1f)] public float trailChance = 0.7f;
    [Tooltip("비가 그친 뒤 작은 방울이 마르는 속도(가상 픽셀/초).")]
    [Min(0f)] public float evaporation = 0.12f;
    [Tooltip("비 시작/멈춤에 따라 생성량이 따라가는 시간(초).")]
    public Vector2 intensityResponse = new Vector2(1.2f, 2.5f);
    [Tooltip("맺히는 방울이 차오르는 시간(초). 방울마다 약간 다르다.")]
    [Range(0.1f, 1f)] public float formationTime = 0.4f;
    [Tooltip("합쳐진 두 방울의 연결부가 한 덩어리로 가라앉는 시간(초).")]
    [Range(0.1f, 1f)] public float mergeTime = 0.38f;
    [Tooltip("유리면에 걸려 잠깐 멈췄다 다시 흐르는 정도. 0이면 연속 흐름.")]
    [Range(0f, 1f)] public float stickSlip = 0.65f;
}

/// <summary>
/// 유리 위 물방울. 좌표는 가상 픽셀이고 (0,0)이 화면 왼쪽 아래다.
/// 움직임은 연속 좌표로 계산하고 격자 정렬은 렌더링 마지막 단계에서만 한다.
/// </summary>
public struct WindowDrop
{
    public Vector2 position;
    public Vector2 previous;
    public Vector2 velocity;
    /// <summary>진행 방향. 정지하면 아래쪽으로 천천히 돌아간다.</summary>
    public Vector2 heading;
    public float radius;
    public float pin;
    public float phase;
    public float travelled;
    public bool sliding;
    public float age;
    // 합체 후에도 흡수된 방울의 형태를 잠깐 남긴다. 물리 면적에는 중복해서 더하지 않는다.
    public Vector2 mergeOffset;
    public float mergeFraction;
    public float mergeRemaining;
    public float adhesionTravel;
    public float adhesionPause;
    public int adhesionStops;
}

public sealed class WindowRainSimulation
{
    readonly WindowRainSettings settings;
    readonly System.Random random;
    readonly List<WindowDrop> drops = new List<WindowDrop>(256);
    readonly List<WindowDrop> spawned = new List<WindowDrop>(32);
    readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
    readonly Stack<List<int>> cellPool = new Stack<List<int>>();
    bool[] merged = Array.Empty<bool>();
    float spawnDebt;
    float time;

    public WindowRainSimulation(WindowRainSettings settings, int seed = 1234)
    {
        this.settings = settings ?? new WindowRainSettings();
        random = new System.Random(seed);
    }

    public IReadOnlyList<WindowDrop> Drops => drops;
    /// <summary>현재 생성 세기(0~1). 비가 그치면 서서히 0이 된다.</summary>
    public float Intensity { get; private set; }
    public Vector2 Size { get; private set; } = new Vector2(480, 270);
    public bool IsDry => drops.Count == 0;
    public float ElapsedTime => time;

    public float FormationScale(WindowDrop drop) => Mathf.SmoothStep(0.2f, 1f,
        drop.age / Mathf.Max(0.05f, settings.formationTime * (0.8f + Mathf.Repeat(drop.phase, 1f) * 0.4f)));

    /// <summary>종횡비가 바뀌면 가로 좌표만 비율대로 옮긴다. 방울 크기는 그대로다.</summary>
    public void Resize(float aspect)
    {
        var size = new Vector2(Mathf.Max(1f, settings.virtualRows * aspect), settings.virtualRows);
        if (size == Size) return;
        float scale = size.x / Size.x;
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            d.position.x *= scale; d.previous.x *= scale;
            d.mergeOffset.x *= scale;
            drops[i] = d;
        }
        Size = size;
    }

    public void Clear()
    {
        drops.Clear();
        spawnDebt = 0f;
        Intensity = 0f;
    }

    /// <summary>씬에 들어왔을 때 이미 비가 오고 있었다면 유리가 젖어 있는 상태로 시작한다.</summary>
    public void Prewarm(float seconds)
    {
        Intensity = 1f;
        for (float t = 0; t < seconds; t += 1f / 30f) Step(1f / 30f, 1f, Vector2.zero);
    }

    /// <param name="rain">비의 목표 세기(0~1).</param>
    /// <param name="externalAcceleration">유리에 대한 바람·관성 가속도(가상 픽셀/초²). 중력은 포함하지 않는다.</param>
    public void Step(float dt, float rain, Vector2 externalAcceleration, bool cameraMoving = false)
    {
        if (dt <= 0f) return;
        // 물자국은 마지막 서브스텝만이 아니라 이번 프레임 전체 이동 구간을 연결한다.
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            d.previous = d.position;
            drops[i] = d;
        }
        // 긴 프레임도 같은 결과가 나오도록 쪼갠다.
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / (1f / 30f)), 1, 4);
        float h = Mathf.Min(dt, 4f / 30f) / steps;
        for (int s = 0; s < steps; s++) Advance(h, Mathf.Clamp01(rain), externalAcceleration, cameraMoving);
    }

    void Advance(float dt, float rain, Vector2 external, bool cameraMoving)
    {
        time += dt;
        float response = rain > Intensity ? settings.intensityResponse.x : settings.intensityResponse.y;
        Intensity = Mathf.MoveTowards(Intensity, rain, dt / Mathf.Max(0.05f, response));

        Spawn(dt);
        Vector2 force = new Vector2(0f, -settings.gravity) + external;
        float forceMagnitude = force.magnitude;
        Vector2 forceDirection = forceMagnitude > 0.001f ? force / forceMagnitude : Vector2.down;
        // 중력보다 센 바람·관성이 걸리면 더 작은 방울도 미끄러진다.
        float forceRatio = Mathf.Sqrt(Mathf.Max(0.05f, settings.gravity) / Mathf.Max(0.05f, forceMagnitude));

        spawned.Clear();
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            d.age += dt;
            d.mergeRemaining = Mathf.Max(0f, d.mergeRemaining - dt / Mathf.Max(0.05f, settings.mergeTime));
            float pinNoise = Mathf.PerlinNoise(d.position.x * 0.09f + 17.3f, d.position.y * 0.09f);
            float threshold = settings.slideRadius * forceRatio *
                (d.pin + (pinNoise - 0.5f) * settings.pinVariation * 2f);
            // 이동 중에는 이미 흐르던 방울의 접촉선이 다시 붙지 않도록 이력을 적용한다.
            // 아직 맺혀 있는 작은 방울을 한꺼번에 흘려보내지는 않는다.
            if (cameraMoving && (d.sliding || d.adhesionPause > 0f)) threshold *= 0.7f;
            d.adhesionPause = Mathf.Max(0f, d.adhesionPause - dt);
            if (cameraMoving)
            {
                d.adhesionPause = 0f;
                d.adhesionTravel = 0f;
            }
            float nextContact = 8f + 16f * Mathf.PerlinNoise(d.phase + 31f, d.adhesionStops * 0.73f);
            // 접촉선이 유리의 거친 부분에 잠깐 걸렸다 풀린다. 큰 물줄기는 관성으로 통과한다.
            // 낮은 부착력의 방울은 계속 흐른다. 생성 시 정한 pin으로 성향을 유지한다.
            if (!cameraMoving && d.pin >= 1f && settings.stickSlip > 0f && d.adhesionPause <= 0f && d.radius > threshold &&
                d.radius < threshold * 2.2f && d.adhesionTravel >= nextContact)
            {
                d.adhesionPause = (0.2f + 0.3f * Mathf.PerlinNoise(d.phase + 73f, d.adhesionStops * 1.13f)) * settings.stickSlip;
                d.adhesionTravel = 0f;
                d.adhesionStops++;
            }
            d.sliding = d.radius > threshold && d.adhesionPause <= 0f;
            if (d.sliding)
            {
                float excess = 1f - threshold / d.radius;
                // 진행 방향은 힘 방향으로 천천히 돌아간다(관성). 표면 장력 탓에 갑자기 꺾이지 않는다.
                d.heading = Vector2.Lerp(d.heading.sqrMagnitude > 0.01f ? d.heading : forceDirection,
                                         forceDirection, 1f - Mathf.Exp(-dt * 3f)).normalized;
                Vector2 side = new Vector2(-d.heading.y, d.heading.x);
                float wobble = (Mathf.PerlinNoise(d.phase, time * 0.8f) - 0.5f) * 2f * settings.meander;
                Vector2 acceleration = (d.heading + side * wobble) * forceMagnitude * Mathf.Clamp01(excess * 1.5f);
                d.velocity += acceleration * dt;
                d.velocity *= Mathf.Exp(-settings.drag * dt);
                float limit = settings.maxSpeed * Mathf.Clamp01(d.radius / (settings.slideRadius * 3f));
                if (d.velocity.magnitude > limit) d.velocity = d.velocity.normalized * limit;
                d.position += d.velocity * dt;
                d.travelled += d.velocity.magnitude * dt;
                d.adhesionTravel += d.velocity.magnitude * dt;
                if (d.travelled > settings.trailSpacing)
                {
                    d.travelled = 0f;
                    if (drops.Count + spawned.Count < settings.maxDrops && Next() < settings.trailChance && d.radius > 1.6f)
                    {
                        float r = d.radius * Range(0.22f, 0.36f);
                        d.radius = Mathf.Sqrt(Mathf.Max(0.01f, d.radius * d.radius - r * r));
                        // 본체의 합체 반경 바깥에 남겨야 바로 다시 흡수되지 않는다.
                        spawned.Add(NewDrop(d.position - d.heading * (d.radius + r + 0.5f), r));
                    }
                }
            }
            else
            {
                // 멈춘 방울은 붙어 있다. 방향만 서서히 아래로 되돌린다.
                d.velocity *= Mathf.Exp(-dt * (d.adhesionPause > 0f ? 40f : 12f));
                if (d.velocity.sqrMagnitude < 1f) d.velocity = Vector2.zero;
                d.position += d.velocity * dt;
                d.heading = Vector2.Lerp(d.heading, Vector2.down, 1f - Mathf.Exp(-dt * 1.5f));
                if (Intensity < 0.5f)
                    d.radius -= settings.evaporation * (1f - Intensity * 2f) * dt * (d.radius < 1.5f ? 1f : 0.4f);
            }
            drops[i] = d;
        }
        drops.AddRange(spawned);
        Merge();
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            if (d.radius < 0.35f || d.position.y < -d.radius * 4f ||
                d.position.x < -8f || d.position.x > Size.x + 8f)
                drops.RemoveAt(i);
        }
    }

    void Spawn(float dt)
    {
        spawnDebt += settings.spawnPerSecond * Intensity * dt;
        while (spawnDebt >= 1f)
        {
            spawnDebt -= 1f;
            var position = new Vector2(Range(0f, Size.x), Range(0f, Size.y * 1.03f));
            // 대부분 작은 방울, 가끔 큰 방울.
            float radius = Mathf.Lerp(settings.spawnRadius.x, settings.spawnRadius.y, Mathf.Pow(Next(), 2.4f));
            if (Next() < settings.largeDropChance)
                radius = settings.slideRadius * Range(1.15f, 1.6f);
            if (drops.Count >= settings.maxDrops)
            {
                // 가득 차면 새 비는 기존 방울을 키운다.
                int target = random.Next(drops.Count);
                var d = drops[target];
                d.radius = Mathf.Sqrt(d.radius * d.radius + radius * radius * 0.5f);
                drops[target] = d;
                continue;
            }
            drops.Add(NewDrop(position, radius));
        }
    }

    WindowDrop NewDrop(Vector2 position, float radius) => new WindowDrop
    {
        position = position, previous = position, radius = radius, heading = Vector2.down,
        pin = Range(0.85f, 1.25f), phase = Range(0f, 100f)
    };

    /// <summary>겹친 방울을 면적을 보존하며 합친다. 흐르는 방울이 지나가며 작은 방울을 쓸어 담는다.</summary>
    void Merge()
    {
        foreach (var list in grid.Values) { list.Clear(); cellPool.Push(list); }
        grid.Clear();
        const float cell = 8f;
        for (int i = 0; i < drops.Count; i++)
        {
            long key = Key(drops[i].position, cell);
            if (!grid.TryGetValue(key, out var list))
                grid[key] = list = cellPool.Count > 0 ? cellPool.Pop() : new List<int>(4);
            list.Add(i);
        }
        if (merged.Length < drops.Count) merged = new bool[Mathf.NextPowerOfTwo(drops.Count)];
        Array.Clear(merged, 0, drops.Count);
        var dead = merged;
        for (int i = 0; i < drops.Count; i++)
        {
            if (dead[i]) continue;
            var a = drops[i];
            int cx = Mathf.FloorToInt(a.position.x / cell), cy = Mathf.FloorToInt(a.position.y / cell);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                {
                    if (!grid.TryGetValue(Pack(cx + ox, cy + oy), out var list)) continue;
                    foreach (int j in list)
                    {
                        if (j == i || dead[j]) continue;
                        var b = drops[j];
                        float reach = (a.radius + b.radius) * 0.8f;
                        if ((a.position - b.position).sqrMagnitude > reach * reach) continue;
                        float areaA = a.radius * a.radius, areaB = b.radius * b.radius, total = areaA + areaB;
                        // 큰 방울(또는 흐르는 방울) 쪽으로 합친다.
                        bool keepA = a.sliding == b.sliding ? areaA >= areaB : a.sliding;
                        var keep = keepA ? a : b;
                        var other = keepA ? b : a;
                        keep.mergeOffset = other.position - keep.position;
                        keep.mergeFraction = (other.radius * other.radius) / total;
                        keep.mergeRemaining = 1f;
                        keep.age = Mathf.Max(keep.age, other.age);
                        keep.position = Vector2.Lerp(keep.position, other.position, (other.radius * other.radius) / total * 0.5f);
                        keep.velocity = (keep.velocity * (keep.radius * keep.radius) + other.velocity * (other.radius * other.radius)) / total;
                        keep.radius = Mathf.Sqrt(total);
                        a = keep;
                        dead[j] = true;
                    }
                }
            drops[i] = a;
        }
        for (int i = drops.Count - 1; i >= 0; i--) if (dead[i]) drops.RemoveAt(i);
    }

    static long Key(Vector2 position, float cell) =>
        Pack(Mathf.FloorToInt(position.x / cell), Mathf.FloorToInt(position.y / cell));
    static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;

    float Next() => (float)random.NextDouble();
    float Range(float min, float max) => min + (max - min) * Next();
}
