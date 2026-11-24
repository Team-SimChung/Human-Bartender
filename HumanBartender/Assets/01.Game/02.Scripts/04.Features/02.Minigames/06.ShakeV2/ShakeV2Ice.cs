using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>얼음 조절 값. 기본값은 웹 game.js(stepIces·spillIce)와 같다. 반발·마찰은 Physics Material 2D 에셋에 있다.</summary>
[Serializable]
public sealed class ShakeV2IceTuning
{
    [Header("물에서")]
    [Tooltip("얼음 밀도(물 = 1). 잠긴 비율이 이 값보다 크면 떠오르고 작으면 가라앉는다. 웹 .92.")]
    [Range(.1f, 2f)] public float density = .92f;
    [Tooltip("물살에 끌리는 세기(1/초, 잠긴 비율을 곱한다). 올리면 얼음이 물과 같이 움직이고 벽에 덜 세게 부딪힌다. 웹 B.ICE_DRAG.")]
    [Min(0f)] public float waterDrag = 3f;
    [Tooltip("세로 움직임 감쇠(1/초). x = 물에 닿아 있을 때, y = 물 밖. 올리면 위아래로 덜 튄다. 웹 3.5 / .3.")]
    public Vector2 verticalDamping = new Vector2(3.5f, .3f);
    [Tooltip("물살이 얼음을 돌리는 세기. 웹 .008.")]
    [Min(0f)] public float spinFromFlow = .008f;

    [Header("한계")]
    [Tooltip("통에 대한 속도 상한(셰이커 단위/초). x = 가로, y = 세로. 웹 2200 / 650.")]
    public Vector2 maxSpeed = new Vector2(2200f, 650f);
    [Tooltip("회전 속도 상한(rad/초). 웹 5.")]
    [Min(0f)] public float maxSpin = 5f;
    [Tooltip("회전 감쇠(1/초). 웹 1.8.")]
    [Min(0f)] public float spinDamping = 1.8f;

    [Header("부딪힘")]
    [Tooltip("이보다 크게(셰이커 단위/초) 속도가 꺾이면 얼음이 잠깐 또렷해진다. 웹 chillMinV × .6.")]
    [Min(0f)] public float flashSpeed = 180f;
    [Tooltip("또렷해지는 시간(초). 웹 .18.")]
    [Min(.01f)] public float flashSec = .18f;
    [Tooltip("평소 얼음 불투명도. 부딪힌 직후 1로 오른다. 웹 .94.")]
    [Range(0f, 1f)] public float alpha = .94f;

    [Header("파손")]
    [Tooltip("파손 뒤 첫 조각이 나오기까지(초). 웹 50ms.")]
    [Min(0f)] public float spillFirstSec = .05f;
    [Tooltip("조각 사이 간격(초). 입구에 가까운 조각부터 나온다. 웹 46ms.")]
    [Min(0f)] public float spillIntervalSec = .046f;
    [Tooltip("입구(작은 틴이 빠진 자리)의 단면 x(셰이커 단위). 웹 −40.")]
    public float lipX = -40f;
    [Tooltip("나오는 속도(게임 좌표/초). x = 처음 조각의 가로, y = 마지막 조각까지 더하는 가로. 웹 −230 / +110.")]
    public Vector2 spillSpeed = new Vector2(-230f, 110f);
    [Tooltip("가로 속도의 무작위 폭(±). 웹 25.")]
    [Min(0f)] public float spillSpeedJitter = 25f;
    [Tooltip("세로 속도 범위(게임 좌표/초, 아래가 +). 마지막 조각일수록 아래 값(y)만큼 더한다. 웹 −55~40, +90.")]
    public Vector3 spillLift = new Vector3(-55f, 40f, 90f);
    [Tooltip("밖으로 나온 얼음의 중력(게임 좌표/초²). 웹 760.")]
    public float spillGravity = 760f;
    [Tooltip("밖으로 나온 얼음이 보이는 시간(초)과 마지막에 사라지는 시간. 웹 1.35 / .25.")]
    public Vector2 spillLife = new Vector2(1.35f, .25f);
    [Tooltip("나올 때 회전: x = 안에서 돌던 속도를 남기는 비율, y = 무작위 폭(±rad/초). 웹 .35 / 1.6.")]
    public Vector2 spillSpin = new Vector2(.35f, 1.6f);
}

/// <summary>
/// 얼음 배치 계산(순수). 크기는 포즈 시트의 지름(원본 px)에서 고르고, 단면 넓이의 비율에 맞춘다. 배치는 웹 makeShakingIce처럼
/// 큰 것부터 왼쪽 아래에 모은다. 셋업 도구가 이 결과를 프리팹에 저장하고, 테스트가 같은 함수로 확인한다.
///
/// 단면 좌표(셰이커 단위, +Y 아래)와 물리 좌표(+Y 위, 1 = 셰이커 단위 × PhysicsPerShakerUnit)를 오간다.
/// 각도는 웹과 같은 rad(+ = 화면 시계 방향)이고, 물리 몸체 rotation(도, + = 반시계)과 부호가 반대다.
/// </summary>
public static class ShakeV2IceLayout
{
    /// <summary>얼음 수.</summary>
    public const int Count = 10;
    /// <summary>물리 공간 단위 / 셰이커 단위. 단면 폭 453 → 9.06, 얼음 한 변 0.3~0.85(Box2D가 다루기 좋은 크기).</summary>
    public const float PhysicsPerShakerUnit = .02f;
    /// <summary>얼음끼리 닿는 면을 한 변보다 조금 안쪽으로(모서리 깎인 자리). 웹 iceContact .96. 상자 콜라이더 크기에 곱한다.</summary>
    public const float ContactScale = .96f;
    /// <summary>얼음을 모아 두는 오른쪽 끝(단면 x). 웹 makeShakingIce 22.</summary>
    public const float PackRight = 22f;

    /// <summary>처음 각도(rad). 웹은 −0.5~0.5 무작위였고, 여기서는 매번 같은 모습이 되게 고정했다.</summary>
    public static readonly float[] Angles = { .12f, -.31f, .26f, -.08f, .38f, -.22f, .05f, -.44f, .17f, -.15f };

    public struct Piece
    {
        /// <summary>포즈 시트 지름(원본 px) = 한 변.</summary>
        public int Diameter;
        /// <summary>단면 좌표(셰이커 단위).</summary>
        public Vector2 Position;
        /// <summary>웹 각도(rad).</summary>
        public float Angle;
        /// <summary>한 변의 절반(셰이커 단위). 웹 cube.size.</summary>
        public float Half => Diameter * ShakeV2Presentation.ShakerUnitsPerArtPixel * .5f;
    }

    // ── 크기 ────────────────────────────────────────────────────────────

    /// <summary>지름(원본 px)을 한 변의 셰이커 단위로.</summary>
    public static float Side(int diameter) => diameter * ShakeV2Presentation.ShakerUnitsPerArtPixel;

    /// <summary>얼음 넓이 합 / 단면 넓이.</summary>
    public static float Coverage(IEnumerable<int> diameters)
    {
        float sum = 0f;
        foreach (int d in diameters) sum += Side(d) * Side(d);
        return sum / ShakeV2Presentation.ChamberArea;
    }

    /// <summary>
    /// 서로 다른 지름 count개를 큰 것부터. 시트에 있는 지름(min~max, step 간격) 중 이어진 count+1개에서 하나를 빼거나 이어진 count개를
    /// 골라, 넓이 합이 coverage에 가장 가까운 묶음을 쓴다.
    /// </summary>
    public static int[] Diameters(float coverage, int count = Count, int min = 4, int max = 40, int step = 2)
    {
        float target = coverage * ShakeV2Presentation.ChamberArea;
        int[] best = null;
        float bestError = float.MaxValue;
        var run = new List<int>();
        for (int start = min; start + (count - 1) * step <= max; start += step)
        {
            for (int length = count; length <= count + 1; length++)
            {
                if (start + (length - 1) * step > max) continue;
                for (int skip = length == count ? -1 : 0; skip < (length == count ? 0 : length); skip++)
                {
                    run.Clear();
                    for (int k = 0; k < length; k++)
                        if (k != skip) run.Add(start + k * step);
                    float sum = 0f;
                    foreach (int d in run) sum += Side(d) * Side(d);
                    float error = Mathf.Abs(sum - target);
                    if (error >= bestError - .001f) continue;
                    bestError = error;
                    best = run.ToArray();
                }
            }
        }
        best ??= new int[count];
        Array.Sort(best);
        Array.Reverse(best);
        return best;
    }

    // ── 맞닿음(웹 iceContact) ───────────────────────────────────────────

    /// <summary>회전한 두 정사각형의 겹침(분리축). 겹치지 않으면 false. normal은 a에서 b 쪽.</summary>
    public static bool Contact(Vector2 a, float halfA, float angleA, Vector2 b, float halfB, float angleB,
        out Vector2 normal, out float depth, float scale = ContactScale)
    {
        normal = Vector2.zero;
        depth = float.MaxValue;
        Vector2 d = b - a;
        if (Mathf.Abs(d.x) + Mathf.Abs(d.y) > 2f * (halfA + halfB)) return false;
        float ca = Mathf.Cos(angleA), sa = Mathf.Sin(angleA), cb = Mathf.Cos(angleB), sb = Mathf.Sin(angleB);
        float spread = Mathf.Abs(ca * cb + sa * sb) + Mathf.Abs(sa * cb - ca * sb);
        Span<Vector3> axes = stackalloc Vector3[4]
        {
            new Vector3(ca, sa, halfA + halfB * spread), new Vector3(-sa, ca, halfA + halfB * spread),
            new Vector3(cb, sb, halfB + halfA * spread), new Vector3(-sb, cb, halfB + halfA * spread),
        };
        foreach (var axis in axes)
        {
            float offset = d.x * axis.x + d.y * axis.y, overlap = scale * axis.z - Mathf.Abs(offset);
            if (overlap <= 0f) return false;
            if (overlap >= depth) continue;
            depth = overlap;
            float sign = offset < 0f ? -1f : 1f;
            normal = new Vector2(sign * axis.x, sign * axis.y);
        }
        return true;
    }

    /// <summary>회전한 정사각형의 가로·세로 반폭. 웹 extent.</summary>
    public static float Extent(float half, float angle) => half * (Mathf.Abs(Mathf.Cos(angle)) + Mathf.Abs(Mathf.Sin(angle)));

    /// <summary>x에 놓인 얼음(반폭 extent)이 위아래로 갈 수 있는 한계. 웹 iceLimit.</summary>
    public static float Limit(float x, float extent) =>
        Mathf.Min(ShakeV2Presentation.ChamberHalfHeight(x - extent), ShakeV2Presentation.ChamberHalfHeight(x + extent)) - extent;

    /// <summary>
    /// 큰 것부터 왼쪽 아래에 모은다(웹 makeShakingIce의 비용: 바닥에서 뜬 만큼 + 왼쪽 벽에서 떨어진 만큼 × .65).
    /// 웹은 무작위 120번 중 고르고 조금 겹치는 것도 허용했지만, 여기서는 격자를 모두 보고 겹치지 않는 자리만 쓴다.
    /// </summary>
    public static Piece[] Pack(IReadOnlyList<int> diameters, IReadOnlyList<float> angles = null, float grid = .5f)
    {
        angles ??= Angles;
        var pieces = new Piece[diameters.Count];
        float x0 = ShakeV2Presentation.ChamberX0;
        for (int i = 0; i < pieces.Length; i++)
        {
            var piece = new Piece { Diameter = diameters[i], Angle = angles.Count > 0 ? angles[i % angles.Count] : 0f };
            float half = piece.Half, extent = Extent(half, piece.Angle);
            float bestCost = float.MaxValue;
            Vector2 best = new Vector2(x0 + extent, 0f);
            for (float x = x0 + extent; x <= PackRight - extent; x += grid)
            {
                float limit = Limit(x, extent);
                if (limit < 0f) continue;
                for (float y = limit; y >= -limit; y -= grid)
                {
                    float cost = (x - x0) * .65f + limit - y;
                    if (cost >= bestCost) break;
                    var at = new Vector2(x, y);
                    bool free = true;
                    for (int j = 0; j < i && free; j++)
                        free = !Contact(at, half, piece.Angle, pieces[j].Position, pieces[j].Half, pieces[j].Angle, out _, out _);
                    if (!free) continue;
                    bestCost = cost;
                    best = at;
                    break;
                }
            }
            piece.Position = best;
            pieces[i] = piece;
        }
        return pieces;
    }

    // ── 통 ──────────────────────────────────────────────────────────────

    /// <summary>단면 안쪽 둘레(단면 좌표, 닫힌 고리 7점): 작은 틴 끝 위 → 이음 위 → 큰 틴 끝 위 → 아래로 돌아온다.</summary>
    public static Vector2[] ChamberLoop()
    {
        float x0 = ShakeV2Presentation.ChamberX0, x1 = ShakeV2Presentation.ChamberX1, seam = ShakeV2Presentation.ChamberSeam;
        float left = ShakeV2Presentation.ChamberLeft, middle = ShakeV2Presentation.ChamberMiddle, right = ShakeV2Presentation.ChamberRight;
        return new[]
        {
            new Vector2(x0, -left), new Vector2(seam, -middle), new Vector2(x1, -right),
            new Vector2(x1, right), new Vector2(seam, middle), new Vector2(x0, left), new Vector2(x0, -left),
        };
    }

    // ── 좌표 변환 ───────────────────────────────────────────────────────

    public static Vector2 ToPhysics(Vector2 chamber) => new Vector2(chamber.x, -chamber.y) * PhysicsPerShakerUnit;
    public static Vector2 ToChamber(Vector2 physics) => new Vector2(physics.x, -physics.y) / PhysicsPerShakerUnit;
    public static float ToPhysicsDegrees(float angle) => -angle * Mathf.Rad2Deg;
    public static float ToChamberAngle(float degrees) => -degrees * Mathf.Deg2Rad;
}

/// <summary>
/// 통 안 얼음의 Unity 2D 물리 구동. 통 콜라이더와 얼음 몸체는 움직이지 않는 물리 공간(통 기준)에 있다.
///
/// 매 부분 스텝마다 셰이커의 속도 변화(관성)를 얼음 속도에서 빼고, 돌린 중력·부력·물살 끌림을 코드로 준 뒤 직접 한 스텝 진행한다.
/// 몸체의 중력 배율은 0이라 프로젝트 Physics 2D 중력을 쓰지 않는다. 물리 씬은 셰이커 화면이 따로 만들어 넘긴다.
/// </summary>
public sealed class ShakeV2IceSimulation
{
    readonly Rigidbody2D[] bodies;
    readonly Collider2D[] colliders;
    readonly EdgeCollider2D chamber;
    readonly float[] half;
    readonly Vector2[] startPosition, before;
    readonly float[] startRotation, hitAt;
    readonly bool[] active;
    PhysicsScene2D physics;

    public ShakeV2IceSimulation(Rigidbody2D[] bodies, EdgeCollider2D chamber, PhysicsScene2D physics)
    {
        this.bodies = bodies ?? Array.Empty<Rigidbody2D>();
        this.chamber = chamber;
        this.physics = physics;
        int count = this.bodies.Length;
        colliders = new Collider2D[count];
        half = new float[count];
        startPosition = new Vector2[count];
        before = new Vector2[count];
        startRotation = new float[count];
        hitAt = new float[count];
        active = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var body = this.bodies[i];
            if (body == null) continue;
            var box = body.GetComponent<BoxCollider2D>();
            colliders[i] = box;
            half[i] = box != null ? box.size.x / (ShakeV2IceLayout.ContactScale * ShakeV2IceLayout.PhysicsPerShakerUnit) * .5f : 10f;
            startPosition[i] = body.transform.localPosition;
            startRotation[i] = body.transform.localEulerAngles.z;
        }
    }

    public int Count => bodies.Length;
    public PhysicsScene2D Physics => physics;

    /// <summary>i번 얼음의 단면 좌표(셰이커 단위).</summary>
    public Vector2 Position(int i) => bodies[i] != null ? ShakeV2IceLayout.ToChamber(bodies[i].position) : Vector2.zero;
    /// <summary>i번 얼음의 웹 각도(rad).</summary>
    public float Angle(int i) => bodies[i] != null ? ShakeV2IceLayout.ToChamberAngle(bodies[i].rotation) : 0f;
    /// <summary>i번 얼음의 통 기준 속도(셰이커 단위/초, +Y 아래).</summary>
    public Vector2 Velocity(int i) => bodies[i] != null ? ShakeV2IceLayout.ToChamber(bodies[i].linearVelocity) : Vector2.zero;
    /// <summary>i번 얼음의 웹 회전 속도(rad/초).</summary>
    public float Spin(int i) => bodies[i] != null ? -bodies[i].angularVelocity * Mathf.Deg2Rad : 0f;
    /// <summary>한 변의 절반(셰이커 단위).</summary>
    public float Half(int i) => half[i];
    public bool Active(int i) => active[i];
    /// <summary>마지막으로 세게 부딪힌 게임 시각.</summary>
    public float HitAt(int i) => hitAt[i];

    /// <summary>처음 배치(프리팹에 저장된 자리)로 되돌리고 통 안에서 움직이게 한다.</summary>
    public void Reset(ShakeV2IceTuning tuning)
    {
        if (chamber != null) chamber.enabled = true;
        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            if (body == null) continue;
            body.simulated = true;
            if (colliders[i] != null) colliders[i].enabled = true;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = tuning.spinDamping;
            body.position = startPosition[i];
            body.rotation = startRotation[i];
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            hitAt[i] = float.NegativeInfinity;
            active[i] = true;
        }
    }

    /// <summary>모두 멈추고 숨긴다(쉐이킹 밖, 파손 직후).</summary>
    public void Hide()
    {
        if (chamber != null) chamber.enabled = false;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null) bodies[i].simulated = false;
            active[i] = false;
        }
    }

    /// <summary>
    /// 통 안에서 한 부분 스텝. gravity·velocityChange는 단면 좌표(셰이커 단위)이고 velocityChange는 관성 배율을 곱한 이 스텝의 몫이다.
    /// </summary>
    public void Step(float dt, Vector2 gravity, Vector2 velocityChange, IShakeV2Liquid liquid, ShakeV2IceTuning tuning, float now)
    {
        if (dt <= 0f) return;
        float u = ShakeV2IceLayout.PhysicsPerShakerUnit;
        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            if (body == null || !active[i]) continue;
            Vector2 p = Position(i), v = Velocity(i);
            float r = half[i];
            float flow = liquid?.FlowAt(p.x) ?? 0f;
            float sub = liquid != null ? Mathf.Clamp(p.y + r - liquid.SurfaceAt(p.x), 0f, 2f * r) : 0f;
            float wet = sub / (2f * r);

            v -= velocityChange;
            v.x += (flow - v.x) * tuning.waterDrag * wet * dt;
            v += gravity * ((1f - wet / tuning.density) * dt);
            v.y *= Mathf.Exp(-(sub > 0f ? tuning.verticalDamping.x : tuning.verticalDamping.y) * dt);
            v.x = Mathf.Clamp(v.x, -tuning.maxSpeed.x, tuning.maxSpeed.x);
            v.y = Mathf.Clamp(v.y, -tuning.maxSpeed.y, tuning.maxSpeed.y);
            float spin = Mathf.Clamp(Spin(i) + (flow - v.x) * tuning.spinFromFlow * dt, -tuning.maxSpin, tuning.maxSpin);

            body.linearVelocity = ShakeV2IceLayout.ToPhysics(v);
            body.angularVelocity = -spin * Mathf.Rad2Deg;
            before[i] = body.linearVelocity;
        }
        physics.Simulate(dt);
        float flash = tuning.flashSpeed * u;
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null && active[i] && (bodies[i].linearVelocity - before[i]).magnitude > flash) hitAt[i] = now;
    }

    /// <summary>파손: 통을 없애고 모두 멈춰 둔다. 조각은 Launch로 하나씩 나온다.</summary>
    public void BeginSpill() => Hide();

    /// <summary>i번 조각을 입구 밖으로 내보낸다. 크기·각도는 그대로이고, 다른 조각·통과는 부딪히지 않는다(웹 spillIce).</summary>
    public void Launch(int i, Vector2 position, Vector2 velocity, float angle, float spin)
    {
        var body = bodies[i];
        if (body == null) return;
        if (colliders[i] != null) colliders[i].enabled = false;
        body.simulated = true;
        body.angularDamping = 0f;
        body.position = ShakeV2IceLayout.ToPhysics(position);
        body.rotation = ShakeV2IceLayout.ToPhysicsDegrees(angle);
        body.linearVelocity = ShakeV2IceLayout.ToPhysics(velocity);
        body.angularVelocity = -spin * Mathf.Rad2Deg;
        active[i] = true;
    }

    /// <summary>나온 조각만 중력으로 한 스텝(단면 좌표의 중력).</summary>
    public void StepSpill(float dt, Vector2 gravity)
    {
        if (dt <= 0f) return;
        Vector2 dv = ShakeV2IceLayout.ToPhysics(gravity * dt);
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null && active[i]) bodies[i].linearVelocity += dv;
        physics.Simulate(dt);
    }

    /// <summary>i번 조각을 다시 숨긴다.</summary>
    public void Retire(int i)
    {
        if (bodies[i] != null) bodies[i].simulated = false;
        active[i] = false;
    }
}
