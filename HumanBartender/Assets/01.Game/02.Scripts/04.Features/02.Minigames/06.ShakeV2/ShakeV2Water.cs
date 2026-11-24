using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 셰이커 단면의 액체 부품. 셰이커 화면은 이 입구로만 물을 다루므로, 다른 방식의 물로 갈아 끼울 때 이것만 맞추면 된다.
///
/// 좌표는 단면 기준(웹 game.js와 같다): 셰이커 단위(원본 1px = 4/3), 원점 = 셰이커 중심, +X = 큰 틴 쪽, +Y = 아래.
/// 통은 움직이지 않는 것으로 보고, 셰이커가 움직인 만큼은 속도 변화(관성)와 돌린 중력으로 받는다.
/// </summary>
public interface IShakeV2Liquid
{
    /// <summary>재료 층(첫 항목이 바닥). 비어 있으면 기본 단일 층. target이 있으면 다 섞인 색으로 그 색을 쓴다.</summary>
    void SetLayers(IReadOnlyList<ShakeV2Layer> layers, Color? target = null);

    /// <summary>처음 채움(수평 수면)으로 되돌린다. 잃은 양·혼합·물보라도 처음으로.</summary>
    void Refill();

    /// <summary>처음 양 대비 잃은 비율(0~1). 다음 스텝부터 수위가 그만큼 낮아진다.</summary>
    float LostFraction { get; set; }

    /// <summary>x 자리의 수면 높이(단면 y, 아래가 +).</summary>
    float SurfaceAt(float x);

    /// <summary>x 자리의 유속(셰이커 단위/초, +X = 큰 틴 쪽).</summary>
    float FlowAt(float x);

    /// <summary>
    /// 한 부분 스텝. gravity = 통 기준 중력 가속도, velocityChange = 이 스텝 동안의 셰이커 속도 변화(통 기준, 관성 배율 적용 뒤).
    /// 물은 통에 대해 그만큼 반대로 밀린다.
    /// </summary>
    void Step(float dt, Vector2 gravity, Vector2 velocityChange);

    /// <summary>한 프레임의 끝. 물보라·거품·색 섞임을 진행한다. mix는 규칙의 혼합도(0~1).</summary>
    void EndFrame(float dt, Vector2 gravity, Vector2 velocityChange, float mix);

    /// <summary>단면 그림을 canvas에 칠한다(층·수면선·거품·물보라). chill은 규칙의 냉각도(0~100), time은 게임 시간(초).</summary>
    void Draw(ShakeV2DotCanvas canvas, float chill, float time);
}

/// <summary>수위 방식 물의 조절 값. 기본값은 웹 game.js와 같다.</summary>
[Serializable]
public sealed class ShakeV2WaterTuning
{
    [Header("칸")]
    [Tooltip("칸 하나의 폭(셰이커 단위). 단면 폭 453을 이 값으로 나눈 수가 칸 수다(3 → 151칸). 웹 B.PX.")]
    [Min(1f)] public float cellWidth = 3f;
    [Tooltip("처음 채움(단면 평균 깊이 대비). 수면은 수평이다. 웹 B.FILL.")]
    [Range(.05f, .95f)] public float fill = .52f;

    [Header("흐름")]
    [Tooltip("한 프레임에서 계산하는 최대 시간(초). 더 긴 프레임은 이만큼만 진행한다. 웹 1/30.")]
    [Range(.005f, .1f)] public float maxFrameSec = 1f / 30f;
    [Tooltip("한 프레임을 나누는 횟수. 웹 6.")]
    [Range(1, 24)] public int substeps = 6;
    [Tooltip("유속이 1/360초마다 남는 비율. 낮추면 출렁임이 빨리 가라앉는다. 웹 B.WDAMP.")]
    [Range(.9f, 1f)] public float damping = .993f;
    [Tooltip("유속 상한(셰이커 단위/초). 웹 900.")]
    [Min(1f)] public float maxSpeed = 900f;
    [Tooltip("수면을 평평하게 되돌리는 세로 중력의 하한(셰이커 단위/초²). 셰이커가 아래로 빨리 가속해도 이 밑으로 내려가지 않는다. 웹 120.")]
    [Min(1f)] public float minGravity = 120f;
    [Tooltip("이웃 칸 유속을 고르게 펴는 비율. 웹 .12.")]
    [Range(0f, .25f)] public float speedSmoothing = .12f;
    [Tooltip("이웃 칸 깊이를 고르게 펴는 비율(뾰족한 기둥 방지). 웹 .08.")]
    [Range(0f, .25f)] public float depthDiffusion = .08f;
    [Tooltip("한 스텝에 한 칸에서 옆으로 넘어갈 수 있는 깊이 비율. 웹 .45.")]
    [Range(.05f, 1f)] public float maxTransfer = .45f;

    [Header("색")]
    [Tooltip("층 불투명도(본편 규칙: 섞인 색 = 양 가중 평균).")]
    [Range(0f, 1f)] public float opacity = .5f;
    [Tooltip("목표색을 따로 줄 때의 불투명도. x + y × 혼합률. 웹 .52 + .44m.")]
    public Vector2 targetOpacity = new Vector2(.52f, .44f);
    [Tooltip("다 섞이는 데 걸리는 시정수(초). 목표 혼합률 1 − (1 − 혼합도)²을 이 빠르기로 따라간다. 웹 .09.")]
    [Min(.001f)] public float blendSec = .09f;
    [Tooltip("냉각도 100일 때 층 색 파랑에 더하는 값(0~255). 수면선은 y. 웹 12 / 10.")]
    public Vector2 coldBlue = new Vector2(12f, 10f);
    [Tooltip("수면선: 맨 위 층 색을 흰색 쪽으로 옮기는 비율.")]
    [Range(0f, 1f)] public float surfaceWhite = .55f;
    [Range(0f, 1f)] public float surfaceAlpha = .85f;
    [Tooltip("이보다 얕은 곳(셰이커 단위)에는 수면선을 그리지 않는다.")]
    [Min(0f)] public float surfaceMinDepth = 1f;
    [Tooltip("층이 하나도 없을 때의 색.")]
    public Color emptyColor = new Color32(0xC8, 0xE6, 0xF0, 0xFF);

    [Header("거품")]
    [Tooltip("흔들림(평균 유속) 이만큼마다 거품 점 하나. 웹 25.")]
    [Min(1f)] public float foamPerTurbulence = 25f;
    [Min(0)] public int maxFoam = 14;
    [Tooltip("거품 자리가 바뀌는 간격(초). 웹 .06.")]
    [Min(.01f)] public float foamTickSec = .06f;
    [Tooltip("흔들림 값이 프레임마다 새 값으로 다가가는 비율. 웹 .15.")]
    [Range(0f, 1f)] public float turbulenceFollow = .15f;
    public Color foamColor = new Color(235f / 255f, 240f / 255f, 244f / 255f, .55f);

    [Header("물보라")]
    [Tooltip("벽 쪽으로 이보다 빠르게 밀려오면 벽에서 물보라가 튄다(셰이커 단위/초). 웹 260.")]
    [Min(0f)] public float wallSpraySpeed = 260f;
    [Tooltip("벽 물보라가 튀는 빈도. 초당 (유속 / 이 값)회. 웹 40.")]
    [Min(1f)] public float wallSprayPer = 40f;
    [Tooltip("수면 기울기가 이보다 가파르면(이웃 칸 깊이 차) 물보라가 튄다. 웹 7.")]
    [Min(0f)] public float slopeSpray = 7f;
    [Tooltip("기울기 물보라 빈도. 초당 (깊이 차 × 이 값)회. 웹 1.5.")]
    [Min(0f)] public float slopeSprayRate = 1.5f;
    [Tooltip("벽 / 기울기 물보라가 남는 시간(초). 웹 .9 / .7.")]
    public Vector2 sprayLife = new Vector2(.9f, .7f);
    [Tooltip("한 번에 남는 물보라 수. 웹 220.")]
    [Min(1)] public int maxDrops = 220;
    public Color sprayColor = new Color(220f / 255f, 228f / 255f, 234f / 255f, .85f);
}

/// <summary>
/// 코드로 칠하는 작은 도트 그림판(셰이커 외피와 같은 격자: 1칸 = 원본 1px). 단면 원점이 OriginX·OriginY 칸의 왼쪽 위 모서리다.
/// Pixels는 Texture2D.SetPixels32 순서(아래 줄부터)다.
/// </summary>
public sealed class ShakeV2DotCanvas
{
    /// <summary>셰이커 단위 1당 칸 수. 원본 1px = 셰이커 4/3.</summary>
    public const float DotsPerUnit = 1f / ShakeV2Presentation.ShakerUnitsPerArtPixel;

    public readonly int Width, Height, OriginX, OriginY;
    public readonly Color32[] Pixels;

    public ShakeV2DotCanvas(int width, int height)
    {
        Width = width; Height = height;
        OriginX = width / 2; OriginY = height / 2;
        Pixels = new Color32[width * height];
    }

    public void Clear() => Array.Clear(Pixels, 0, Pixels.Length);

    /// <summary>단면 x(셰이커 단위)를 칸 열로. 웹 snapArt처럼 가까운 도트 경계로 맞춘다.</summary>
    public int Column(float x) => Mathf.RoundToInt(x * DotsPerUnit) + OriginX;

    /// <summary>단면 y(셰이커 단위, 아래가 +)를 칸 줄(위에서부터)로.</summary>
    public int Row(float y) => Mathf.RoundToInt(y * DotsPerUnit) + OriginY;

    /// <summary>위에서부터 센 줄의 칸 색. 테스트·확인용.</summary>
    public Color32 Get(int column, int row) =>
        column < 0 || row < 0 || column >= Width || row >= Height ? default : Pixels[(Height - 1 - row) * Width + column];

    /// <summary>칸 하나에 색을 덮는다(투명 바탕 위 일반 합성). alpha는 color.a에 곱한다.</summary>
    public void Blend(int column, int row, Color color, float alpha = 1f)
    {
        if (column < 0 || row < 0 || column >= Width || row >= Height) return;
        float a = Mathf.Clamp01(color.a * alpha);
        if (a <= 0f) return;
        ref Color32 d = ref Pixels[(Height - 1 - row) * Width + column];
        float da = d.a / 255f, keep = da * (1f - a), oa = a + keep;
        d = new Color32(
            (byte)Mathf.RoundToInt((color.r * a + d.r / 255f * keep) / oa * 255f),
            (byte)Mathf.RoundToInt((color.g * a + d.g / 255f * keep) / oa * 255f),
            (byte)Mathf.RoundToInt((color.b * a + d.b / 255f * keep) / oa * 255f),
            (byte)Mathf.RoundToInt(oa * 255f));
    }
}

/// <summary>
/// 수위(칸) 방식 물. 웹 game.js의 stepWater·spray·drawWater를 옮겼다.
///
/// 단면 폭을 칸으로 나눠 칸마다 깊이 h, 칸 경계마다 유속 u를 둔다. 수면 기울기와 중력이 u를 바꾸고, u만큼 이웃 칸으로 물이
/// 넘어간다(천장까지 남은 자리만큼만). 양 끝 경계 유속은 0이고, 매 스텝 전체 부피를 (처음 부피 − 잃은 양)으로 맞춘다.
/// 셰이커가 움직인 만큼(관성)은 경계 유속에서 빼되, 양 끝 벽으로 갈수록 줄여 준다(웹 strokeProfile).
/// </summary>
public sealed class ShakeV2ColumnWater : IShakeV2Liquid
{
    struct Drop
    {
        public Vector2 Position, Velocity;
        public float Life;
    }

    readonly ShakeV2WaterTuning tuning;
    readonly int n;
    readonly float dx, meanDepth;
    readonly float[] floorY, strokeProfile, edgeFloor, edgeDepth, h, u, tmpU, tmpH;
    readonly List<Drop> drops = new List<Drop>();
    readonly System.Random random;
    readonly List<ShakeV2Layer> layers = new List<ShakeV2Layer>();
    Color average;
    Color? target;
    float volume, lost, turbulence, blend, stepTurbulence;
    int stepCount;

    public ShakeV2ColumnWater(ShakeV2WaterTuning tuning, int seed = 1)
    {
        this.tuning = tuning ?? new ShakeV2WaterTuning();
        random = new System.Random(seed);
        float x0 = ShakeV2Presentation.ChamberX0, x1 = ShakeV2Presentation.ChamberX1;
        n = Mathf.Max(3, Mathf.RoundToInt((x1 - x0) / this.tuning.cellWidth));
        dx = this.tuning.cellWidth;
        floorY = new float[n];
        h = new float[n];
        tmpH = new float[n];
        strokeProfile = new float[n + 1];
        edgeFloor = new float[n + 1];
        edgeDepth = new float[n + 1];
        u = new float[n + 1];
        tmpU = new float[n + 1];
        float sum = 0f;
        for (int i = 0; i < n; i++)
        {
            floorY[i] = ShakeV2Presentation.ChamberHalfHeight(x0 + (i + .5f) * dx);
            sum += floorY[i];
        }
        for (int i = 0; i <= n; i++)
        {
            strokeProfile[i] = Mathf.Sin(Mathf.PI * i / n);
            edgeFloor[i] = ShakeV2Presentation.ChamberHalfHeight(x0 + i * dx);
        }
        meanDepth = 2f * sum / n;
        SetLayers(null);
        Refill();
    }

    // ── 조회 ────────────────────────────────────────────────────────────

    /// <summary>칸 수(웹 NC).</summary>
    public int Cells => n;
    /// <summary>단면 평균 깊이(셰이커 단위). 웹 meanDepth.</summary>
    public float MeanDepth => meanDepth;
    /// <summary>처음 부피(칸 깊이의 합).</summary>
    public float InitialVolume => volume;
    /// <summary>지금 부피(칸 깊이의 합).</summary>
    public float Volume { get { float s = 0f; for (int i = 0; i < n; i++) s += h[i]; return s; } }
    /// <summary>지금 섞인 정도(0~1). 목표 혼합률을 시정수로 따라간다.</summary>
    public float Blend => blend;
    /// <summary>흔들림(평균 유속).</summary>
    public float Turbulence => turbulence;
    public int DropCount => drops.Count;
    /// <summary>칸 경계 face의 유속. 0과 Cells가 양 끝 벽이다.</summary>
    public float FaceFlow(int face) => u[Mathf.Clamp(face, 0, n)];
    /// <summary>칸 i의 깊이.</summary>
    public float Depth(int cell) => h[Mathf.Clamp(cell, 0, n - 1)];

    public float LostFraction
    {
        get => volume > 0f ? lost / volume : 0f;
        set => lost = Mathf.Clamp01(value) * volume;
    }

    int CellAt(float x) => Mathf.Clamp((int)((x - ShakeV2Presentation.ChamberX0) / dx), 0, n - 1);

    public float SurfaceAt(float x)
    {
        int i = CellAt(x);
        return floorY[i] - h[i];
    }

    public float FlowAt(float x) => u[CellAt(x)];

    // ── 설정 ────────────────────────────────────────────────────────────

    public void SetLayers(IReadOnlyList<ShakeV2Layer> source, Color? targetColor = null)
    {
        layers.Clear();
        if (source != null)
            foreach (var layer in source)
                if (layer.amount > 0f) layers.Add(layer);
        if (layers.Count == 0) layers.Add(new ShakeV2Layer("", tuning.emptyColor, 1f));
        target = targetColor;
        average = WeightedAverage(layers);
    }

    /// <summary>재료 양 가중 평균 색(웹 mixedRGB에서 목표색이 없을 때).</summary>
    public static Color WeightedAverage(IReadOnlyList<ShakeV2Layer> source)
    {
        float total = 0f;
        Color sum = Color.black;
        if (source != null)
            foreach (var layer in source)
            {
                if (layer.amount <= 0f) continue;
                sum += layer.color * layer.amount;
                total += layer.amount;
            }
        if (total <= 0f) return new ShakeV2WaterTuning().emptyColor;
        Color mixed = sum / total;
        mixed.a = 1f;
        return mixed;
    }

    /// <summary>층의 위쪽 경계(깊이에 대한 비율, 바닥 0 ~ 수면 1). 층 두께는 양의 비율이다.</summary>
    public float LayerTop(int index)
    {
        float total = 0f, acc = 0f;
        foreach (var layer in layers) total += layer.amount;
        for (int i = 0; i <= index && i < layers.Count; i++) acc += layers[i].amount;
        return total > 0f ? acc / total : 1f;
    }

    public int LayerCount => layers.Count;

    public void Refill()
    {
        float lift = meanDepth * (tuning.fill - .5f);
        volume = 0f;
        for (int i = 0; i < n; i++)
        {
            h[i] = floorY[i] + lift;
            volume += h[i];
        }
        Array.Clear(u, 0, u.Length);
        lost = 0f;
        turbulence = 0f;
        stepTurbulence = 0f;
        stepCount = 0;
        blend = 0f;
        drops.Clear();
    }

    // ── 진행 ────────────────────────────────────────────────────────────

    public void Step(float dt, Vector2 gravity, Vector2 velocityChange)
    {
        if (dt <= 0f) return;
        // 셰이커의 세로 속도 변화는 칸 방식에서 중력의 변화로 받는다(아래로 가속하면 가벼워지고, 멈추면 무거워진다).
        float gy = Mathf.Max(tuning.minGravity, gravity.y - velocityChange.y / dt);
        float gx = gravity.x;
        float damp = Mathf.Pow(tuning.damping, dt * 360f), cap = tuning.maxSpeed;
        for (int i = 1; i < n; i++)
        {
            float eta = (h[i] - floorY[i]) - (h[i - 1] - floorY[i - 1]);
            u[i] += (-gy * eta / dx + gx * strokeProfile[i]) * dt - velocityChange.x * strokeProfile[i];
            u[i] = Mathf.Clamp(u[i] * damp, -cap, cap);
        }
        u[0] = 0f; u[n] = 0f;
        float smoothing = tuning.speedSmoothing;
        for (int i = 1; i < n; i++) tmpU[i] = u[i] + smoothing * (u[i - 1] + u[i + 1] - 2f * u[i]);
        for (int i = 1; i < n; i++) u[i] = tmpU[i];
        u[0] = 0f; u[n] = 0f;

        // 경계마다 물을 주고받는다. 한 번에 한 칸을 통째로 밀어 넣지 않고, 받는 칸의 천장까지만 채운다.
        float turb = 0f, share = tuning.maxTransfer;
        for (int i = 1; i < n; i++)
        {
            int from = u[i] > 0f ? i - 1 : i, to = u[i] > 0f ? i : i - 1;
            float free = Mathf.Max(0f, 2f * floorY[to] - h[to]);
            float flow = Mathf.Min(h[from] * share, Mathf.Min(Mathf.Abs(u[i]) * h[from] * dt / dx, free));
            h[from] -= flow;
            h[to] += flow;
            turb += Mathf.Abs(u[i]);
        }

        // 수치 확산(뾰족한 기둥 방지). 양 끝 칸은 바깥을 자기 깊이로 본다.
        float diffusion = tuning.depthDiffusion;
        for (int i = 0; i < n; i++)
        {
            float left = i > 0 ? h[i - 1] : h[i], right = i < n - 1 ? h[i + 1] : h[i];
            tmpH[i] = h[i] + diffusion * (left + right - 2f * h[i]);
        }
        Array.Copy(tmpH, h, n);

        // 부피 보정: 처음 부피 − 잃은 양.
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += h[i];
        float k = (volume - lost) / Mathf.Max(1f, sum);
        for (int i = 0; i < n; i++) h[i] *= k;

        stepTurbulence += turb / n;
        stepCount++;
    }

    public void EndFrame(float dt, Vector2 gravity, Vector2 velocityChange, float mix)
    {
        if (stepCount > 0)
        {
            turbulence += (stepTurbulence / stepCount - turbulence) * tuning.turbulenceFollow;
            stepTurbulence = 0f;
            stepCount = 0;
        }
        if (dt <= 0f) return;
        float m = Mathf.Clamp01(mix);
        float goal = 1f - (1f - m) * (1f - m);
        blend += (goal - blend) * (1f - Mathf.Exp(-dt / tuning.blendSec));
        Spray(dt, gravity, velocityChange);
    }

    // 물보라 도트: 벽 가까이에서 빠르게 밀려오거나, 수면이 가파를 때. 웹 spray.
    void Spray(float dt, Vector2 gravity, Vector2 velocityChange)
    {
        float x0 = ShakeV2Presentation.ChamberX0, x1 = ShakeV2Presentation.ChamberX1;
        for (int side = 0; side < 2; side++)
        {
            int i = side == 0 ? 6 : n - 6, dir = side == 0 ? -1 : 1;
            float v = u[i] * dir;
            if (v <= tuning.wallSpraySpeed || Range(0f, 1f) >= dt * v / tuning.wallSprayPer) continue;
            int ci = dir < 0 ? 0 : n - 1;
            for (int k = 0; k < 2; k++)
                drops.Add(new Drop
                {
                    Position = new Vector2(dir < 0 ? x0 + 3f : x1 - 3f, floorY[ci] - h[ci]),
                    Velocity = new Vector2(-dir * Range(40f, 160f), -Range(120f, 320f) * (v / 500f)),
                    Life = tuning.sprayLife.x,
                });
        }
        for (int i = 2; i < n - 2; i += 3)
        {
            float s = Mathf.Abs(h[i + 1] - h[i - 1]);
            if (s <= tuning.slopeSpray || Range(0f, 1f) >= dt * s * tuning.slopeSprayRate) continue;
            drops.Add(new Drop
            {
                Position = new Vector2(x0 + i * dx, floorY[i] - h[i]),
                Velocity = new Vector2(u[i] * .6f + Range(-40f, 40f), -Range(60f, 200f)),
                Life = tuning.sprayLife.y,
            });
        }
        for (int d = drops.Count - 1; d >= 0; d--)
        {
            var drop = drops[d];
            drop.Velocity += gravity * dt - velocityChange;
            drop.Position += drop.Velocity * dt;
            drop.Life -= dt;
            if (drop.Position.x < x0 + 2f || drop.Position.x > x1 - 2f) drop.Velocity.x *= -.4f;
            float ceiling = 2f - ShakeV2Presentation.ChamberHalfHeight(Mathf.Clamp(drop.Position.x, x0, x1));
            if (drop.Position.y < ceiling)
            {
                drop.Position.y = ceiling;
                drop.Velocity.y = Mathf.Abs(drop.Velocity.y) * .3f;
            }
            int ci = CellAt(drop.Position.x);
            if (drop.Velocity.y > 0f && drop.Position.y >= floorY[ci] - h[ci]) drop.Life = 0f;
            if (drop.Life <= 0f) drops.RemoveAt(d);
            else drops[d] = drop;
        }
        int extra = drops.Count - tuning.maxDrops;
        if (extra > 0) drops.RemoveRange(0, extra);
    }

    float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    // ── 그리기 ──────────────────────────────────────────────────────────

    /// <summary>x 자리의 깊이. 칸 경계에서 이웃 칸을 고르게 섞은 값을 직선으로 잇는다(웹 drawWater의 edgeDepth).</summary>
    public float DepthAt(float x)
    {
        float pos = (x - ShakeV2Presentation.ChamberX0) / dx;
        int i = Mathf.Clamp(Mathf.FloorToInt(pos), 0, n - 1);
        float t = Mathf.Clamp01(pos - i);
        return Mathf.Lerp(EdgeDepth(i), EdgeDepth(i + 1), t);
    }

    float EdgeDepth(int face)
    {
        int left = Mathf.Max(0, face - 1), right = Mathf.Min(n - 1, face);
        float depth = (h[Mathf.Max(0, left - 1)] + 3f * h[left] + 3f * h[right] + h[Mathf.Min(n - 1, right + 1)]) / 8f;
        return Mathf.Min(depth, 2f * edgeFloor[face]);
    }

    float FloorAt(float x)
    {
        float pos = (x - ShakeV2Presentation.ChamberX0) / dx;
        int i = Mathf.Clamp(Mathf.FloorToInt(pos), 0, n - 1);
        return Mathf.Lerp(edgeFloor[i], edgeFloor[i + 1], Mathf.Clamp01(pos - i));
    }

    /// <summary>층 색(섞인 정도와 냉각을 반영). 0~1 색.</summary>
    public Color LayerColor(int index, float chill)
    {
        Color mixTo = target ?? average;
        Color c = Color.Lerp(layers[Mathf.Clamp(index, 0, layers.Count - 1)].color, mixTo, blend);
        c.b = Mathf.Min(1f, c.b + chill / 100f * tuning.coldBlue.x / 255f);
        c.a = target.HasValue ? tuning.targetOpacity.x + tuning.targetOpacity.y * blend : tuning.opacity;
        return c;
    }

    public void Draw(ShakeV2DotCanvas canvas, float chill, float time)
    {
        if (canvas == null) return;
        canvas.Clear();
        float x0 = ShakeV2Presentation.ChamberX0, x1 = ShakeV2Presentation.ChamberX1, dots = ShakeV2DotCanvas.DotsPerUnit;
        int count = layers.Count;
        var colors = new Color[count];
        var tops = new float[count];
        for (int k = 0; k < count; k++)
        {
            colors[k] = LayerColor(k, chill);
            tops[k] = LayerTop(k);
        }
        Color surface = Color.Lerp(layers[count - 1].color, target ?? average, blend);
        surface = Color.Lerp(surface, Color.white, tuning.surfaceWhite);
        surface.b = Mathf.Min(1f, surface.b + chill / 100f * tuning.coldBlue.y / 255f);
        surface.a = tuning.surfaceAlpha;

        // 층: 칸 열마다 바닥에서 수면까지를 양의 비율로 나눈다. 경계는 도트에 걸친 만큼만 칠해 매끈하게 잇는다.
        for (int c = 0; c < canvas.Width; c++)
        {
            float x = (c - canvas.OriginX + .5f) / dots;
            if (x < x0 || x > x1) continue;
            float depth = DepthAt(x), bottom = FloorAt(x);
            if (depth <= 0f) continue;
            float baseFraction = 0f;
            for (int k = 0; k < count; k++)
            {
                float top = (bottom - depth * tops[k]) * dots, low = (bottom - depth * baseFraction) * dots;
                baseFraction = tops[k];
                int first = Mathf.FloorToInt(top), last = Mathf.CeilToInt(low) - 1;
                for (int y = first; y <= last; y++)
                {
                    float cover = Mathf.Min(y + 1f, low) - Mathf.Max(y, top);
                    if (cover > 0f) canvas.Blend(c, y + canvas.OriginY, colors[k], cover);
                }
            }
            if (depth >= tuning.surfaceMinDepth) canvas.Blend(c, canvas.Row(bottom - depth), surface);
        }

        // 거품: 흔들림이 셀수록 수면 아래에 반짝이는 도트.
        int foam = Mathf.Min(tuning.maxFoam, Mathf.FloorToInt(turbulence / tuning.foamPerTurbulence));
        int tick = Mathf.FloorToInt(time / tuning.foamTickSec);
        for (int k = 0; k < foam; k++)
        {
            int i = ((k * 37 + tick * 13) % n + n) % n;
            if (h[i] < dx * 3f) continue;
            canvas.Blend(canvas.Column(x0 + i * dx), canvas.Row(floorY[i] - h[i]) + 1 + k % 3, tuning.foamColor);
        }

        // 물보라.
        foreach (var drop in drops) canvas.Blend(canvas.Column(drop.Position.x), canvas.Row(drop.Position.y), tuning.sprayColor);
    }

    /// <summary>
    /// 단면 위아래 벽에 맺히는 성에(냉각도 10 초과부터). 웹 drawContents. 얼음 위에 그려야 하므로 물과 다른 그림판에 칠한다.
    /// </summary>
    public static void DrawFrost(ShakeV2DotCanvas canvas, float chill, Color color, float threshold = 10f, float perChill = 1.2f)
    {
        if (canvas == null) return;
        canvas.Clear();
        if (chill <= threshold) return;
        float x0 = ShakeV2Presentation.ChamberX0, width = ShakeV2Presentation.ChamberX1 - x0;
        float inset = 2f * ShakeV2Presentation.ShakerUnitsPerArtPixel;
        color.a *= chill / 100f;
        for (int i = 0; i < chill * perChill; i++)
        {
            float x = x0 + (i * 37) % width;
            float rim = ShakeV2Presentation.ChamberHalfHeight(x) - inset;
            canvas.Blend(canvas.Column(x), canvas.Row(i % 2 == 1 ? -rim : rim), color);
        }
    }
}
