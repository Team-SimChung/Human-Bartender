using System;
using UnityEngine;

/// <summary>
/// 쉐이킹 V2 연출 수치. 기본값은 웹 V2(game.js·luna.js·session.css, 4d7b20d)의 값이다.
/// 길이는 오른쪽 게임 좌표(880×640) 또는 셰이커 단위(원본 1px = 4/3단위), 시간은 초다.
/// </summary>
[Serializable]
public sealed class ShakeV2MotionTuning
{
    [Header("결합 대기 · 위 틴 원호")]
    [Tooltip("편차 1당 작은 틴의 옆 이동량(셰이커 단위). 웹: 편차×4, 최대 ±64.")]
    public float sealLateralScale = 4f;
    [Tooltip("원호 반지름(셰이커 단위). 가운데서 가장 높고 양 끝에서 낮아진다.")]
    [Min(1f)] public float sealArcRadius = 140f;
    [Tooltip("가운데에 왔을 때 작은 틴의 높이(셰이커 단위, 큰 틴 기준).")]
    public float sealRestHeight = 88f;
    [Tooltip("작은 틴 기울기 = 이 값 × asin(옆 이동량 / 반지름). 약 ±15°.")]
    public float sealTiltScale = .55f;
    [Tooltip("결합된 작은 틴의 위치(셰이커 단위). 큰 틴 입구에 들어간 자리다.")]
    public float seatedOffset = 18f;

    [Header("결합 전환")]
    [Tooltip("Space 뒤 작은 틴이 가운데로 정렬하며 닫히는 시간(초).")]
    [Min(.01f)] public float alignSec = .16f;
    [Tooltip("세로에서 눕히기 시작하는 시각(초).")]
    [Min(0f)] public float layStartSec = .16f;
    [Tooltip("눕히는 데 걸리는 시간(초). 끝나면 작은 틴이 왼쪽에 온다.")]
    [Min(.01f)] public float layDurationSec = .84f;

    [Header("정상 분리")]
    [Tooltip("분리를 시작할 때 셰이커가 가운데로 모이는 시간(초).")]
    [Min(.01f)] public float unsealSettleSec = .24f;
    [Tooltip("사선 자세로 돌기 시작하는 시각(초).")]
    public float unsealTiltStartSec = .22f;
    [Tooltip("사선 자세로 도는 시간(초).")]
    [Min(.01f)] public float unsealTiltSec = .40f;
    [Tooltip("사선 자세의 각도(rad). 약 43°.")]
    public float unsealTiltAngle = .75f;
    [Tooltip("사선에서 수직으로 세우기 시작하는 시각(초).")]
    public float unsealRaiseStartSec = .87f;
    [Tooltip("수직으로 세우는 시간(초).")]
    [Min(.01f)] public float unsealRaiseSec = .39f;
    [Tooltip("위 틴이 빠지기 시작하는 시각(초).")]
    public float releaseSec = 1.42f;
    [Tooltip("위 틴을 들어 올리는 시간(초).")]
    [Min(.01f)] public float liftSec = .33f;
    [Tooltip("들어 올리는 거리(셰이커 단위).")]
    public float liftDistance = 68f;
    [Tooltip("옆으로 미끄러지기 시작하는 지연(초, 들어 올림 기준).")]
    public float slipDelaySec = .055f;
    [Tooltip("옆으로 미끄러지는 시간(초).")]
    [Min(.01f)] public float slipSec = .34f;
    [Tooltip("옆으로 미끄러지는 거리(셰이커 단위).")]
    public float slipDistance = 52f;
    [Tooltip("미끄러지며 기우는 각도(rad).")]
    public float slipAngle = .23f;
    [Tooltip("위 틴이 투명해지기 시작하는 시각(초).")]
    public float tinFadeStartSec = 1.76f;
    [Tooltip("위 틴이 투명해지는 시간(초).")]
    [Min(.01f)] public float tinFadeSec = .24f;
    [Tooltip("냉기가 이어지는 시간(초, 위 틴이 빠지는 순간부터).")]
    [Min(.01f)] public float vaporSec = .65f;

    [Header("파손")]
    [Tooltip("작은 틴이 튀어 나가는 초속도(게임 좌표/초). 오른쪽 위.")]
    public Vector2 popVelocity = new Vector2(560f, -560f);
    [Tooltip("튀어 나간 틴에 걸리는 중력(게임 좌표/초²).")]
    public float popGravity = 900f;
    [Tooltip("튀어 나간 틴의 회전 속도(rad/초).")]
    public float popSpin = 4f;
    [Tooltip("튀어 나간 거리를 셰이커 단위로 바꾸는 웹 기준 배율(B.K).")]
    [Min(.01f)] public float popNominalScale = 1.3f;
}

/// <summary>셰이커의 자세. 위치는 기준점(440,322)에서 잰 게임 좌표, 각도는 화면 아래가 +Y일 때의 시계 방향(rad)이다.</summary>
public readonly struct ShakeV2Pose
{
    public readonly Vector2 Offset;
    public readonly float Angle;

    public ShakeV2Pose(Vector2 offset, float angle)
    {
        Offset = offset; Angle = angle;
    }
}

/// <summary>분리된 작은 틴의 위치(셰이커 단위, 뒤집힌 틴 좌표)·각도·불투명도.</summary>
public readonly struct ShakeV2TinOffset
{
    public readonly Vector2 Position;
    public readonly float Angle;
    public readonly float Alpha;

    public ShakeV2TinOffset(Vector2 position, float angle, float alpha)
    {
        Position = position; Angle = angle; Alpha = alpha;
    }
}

/// <summary>
/// 쉐이킹 V2 화면 계산. 웹 game.js의 shakerView·closingTinOffset·unsealPresentation, session.css의
/// 모니터 열림 키프레임을 옮긴 순수 함수다. 화면 컴포넌트는 여기서 값을 받아 RectTransform에 넣기만 한다.
/// </summary>
public static class ShakeV2Presentation
{
    /// <summary>원본 1px당 셰이커 단위. 웹 shakerPixel.</summary>
    public const float ShakerUnitsPerArtPixel = 4f / 3f;

    /// <summary>웹 smoothProgress: 0~1로 자른 뒤 3t²−2t³.</summary>
    public static float Smooth(float elapsed, float duration)
    {
        float t = Mathf.Clamp01(duration <= 0f ? 1f : elapsed / duration);
        return t * t * (3f - 2f * t);
    }

    /// <summary>CSS ease-out(cubic-bezier(0,0,.58,1)).</summary>
    public static float CssEaseOut(float x)
    {
        x = Mathf.Clamp01(x);
        // x(t) = 3(1−t)²t·0 + 3(1−t)t²·.58 + t³, y(t) = 3(1−t)²t·0 + 3(1−t)t²·1 + t³ — 뉴턴법으로 t를 찾는다.
        float t = x;
        for (int i = 0; i < 8; i++)
        {
            float u = 1f - t;
            float bx = 3f * u * t * t * .58f + t * t * t - x;
            float dx = 3f * u * u * 0f + 6f * u * t * .58f - 3f * t * t * .58f + 3f * t * t;
            if (Mathf.Abs(dx) < 1e-5f) break;
            t = Mathf.Clamp01(t - bx / dx);
        }
        float v = 1f - t;
        return 3f * v * v * t * 0f + 3f * v * t * t + t * t * t;
    }

    // ── 셰이커 자세 ─────────────────────────────────────────────────────

    /// <summary>지금 셰이커가 놓일 자세. 웹 shakerView.</summary>
    public static ShakeV2Pose ShakerPose(ShakeV2Rules rules, ShakeV2MotionTuning motion, bool render = true)
    {
        switch (rules.Phase)
        {
            case EShakeV2Phase.Seal:
                return new ShakeV2Pose(Vector2.zero, Mathf.PI / 2f);

            case EShakeV2Phase.Sealing:
            {
                float lay = Smooth(rules.SealAge - motion.layStartSec, motion.layDurationSec);
                return new ShakeV2Pose(new Vector2(-rules.Tuning.travel * lay, 0f), Mathf.PI / 2f * (1f - lay));
            }

            case EShakeV2Phase.Unseal:
            case EShakeV2Phase.Done when !rules.WasBroken:
            {
                float age = rules.UnsealAge;
                float settle = 1f - Smooth(age, motion.unsealSettleSec);
                float angle = rules.UnsealTilt * settle
                              + motion.unsealTiltAngle * Smooth(age - motion.unsealTiltStartSec, motion.unsealTiltSec)
                              + (Mathf.PI / 2f - motion.unsealTiltAngle) * Smooth(age - motion.unsealRaiseStartSec, motion.unsealRaiseSec);
                return new ShakeV2Pose(rules.UnsealFrom * settle, angle);
            }

            default:
            {
                Vector2 position = render ? rules.RenderPosition : rules.Position;
                return new ShakeV2Pose(position, rules.TiltAt(position.y));
            }
        }
    }

    /// <summary>분리된 작은 틴의 자리(셰이커 단위). 파손 비행은 화면이 따로 더한다. 웹 closingTinOffset·unsealPresentation.</summary>
    public static ShakeV2TinOffset TinOffset(ShakeV2Rules rules, ShakeV2MotionTuning motion)
    {
        if (rules.Phase is EShakeV2Phase.Unseal || rules.Phase == EShakeV2Phase.Done && !rules.WasBroken)
        {
            float age = rules.UnsealAge;
            float lift = Smooth(age - motion.releaseSec, motion.liftSec);
            float slip = Smooth(age - motion.releaseSec - motion.slipDelaySec, motion.slipSec);
            return new ShakeV2TinOffset(new Vector2(motion.seatedOffset + motion.liftDistance * lift, motion.slipDistance * slip),
                motion.slipAngle * slip, 1f - Smooth(age - motion.tinFadeStartSec, motion.tinFadeSec));
        }

        if (rules.Phase is EShakeV2Phase.Seal or EShakeV2Phase.Sealing)
        {
            float lateral = rules.SealOffset * motion.sealLateralScale;
            float radius = motion.sealArcRadius;
            float height = motion.sealRestHeight - (radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - lateral * lateral)));
            float angle = Mathf.Asin(Mathf.Clamp(lateral / radius, -1f, 1f)) * motion.sealTiltScale;
            if (rules.Phase == EShakeV2Phase.Seal) return new ShakeV2TinOffset(new Vector2(height, lateral), angle, 1f);

            float e = Smooth(rules.SealAge, motion.alignSec);
            return new ShakeV2TinOffset(new Vector2(motion.seatedOffset + (height - motion.seatedOffset) * (1f - e), lateral * (1f - e)),
                angle * (1f - e), 1f);
        }

        return new ShakeV2TinOffset(new Vector2(motion.seatedOffset, 0f), 0f, 1f);
    }

    // ── 단면 ────────────────────────────────────────────────────────────

    /// <summary>단면 안쪽 꼭짓점(셰이커 단위). 웹 B.CH: 작은 틴 끝 x0, 큰 틴 끝 x1, 틴이 만나는 자리 seam과 각 자리의 반높이.</summary>
    public const float ChamberX0 = -226f, ChamberX1 = 227f, ChamberSeam = 12f,
        ChamberLeft = 32f, ChamberMiddle = 47f, ChamberRight = 35f;

    /// <summary>
    /// 단면 안쪽의 반높이(셰이커 단위). 웹 halfHeight. 꼭짓점 (−226,±32), (12,±47), (227,±35)을 직선으로 잇는다.
    /// </summary>
    public static float ChamberHalfHeight(float x) =>
        x < ChamberSeam
            ? ChamberLeft + (ChamberMiddle - ChamberLeft) * (x - ChamberX0) / (ChamberSeam - ChamberX0)
            : ChamberMiddle + (ChamberRight - ChamberMiddle) * (x - ChamberSeam) / (ChamberX1 - ChamberSeam);

    /// <summary>단면 넓이(셰이커 단위²). 위아래 대칭인 여섯 꼭짓점 다각형.</summary>
    public static float ChamberArea =>
        (ChamberSeam - ChamberX0) * (ChamberLeft + ChamberMiddle) + (ChamberX1 - ChamberSeam) * (ChamberMiddle + ChamberRight);

    // ── 모니터 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 모니터가 열리는 동안 보이는 영역. session.css monitor-open 키프레임(0 / 19 / 43 / 100%)을 구간마다 ease-out으로 잇는다.
    /// x = 보이는 폭 비율, y = 보이는 높이 비율, z = 높이에 더하는 웹 픽셀(가운데 가는 선의 두께).
    /// </summary>
    public static Vector3 MonitorOpening(float progress)
    {
        // (가로 inset 비율, 세로 inset 비율, 세로 inset 픽셀)
        Vector3 k0 = new Vector3(.43f, .5f, -1f), k1 = new Vector3(.01f, .5f, -2f), k2 = new Vector3(0f, .37f, 0f), k3 = Vector3.zero;
        progress = Mathf.Clamp01(progress);
        Vector3 inset = progress < .19f ? Vector3.Lerp(k0, k1, CssEaseOut(progress / .19f))
            : progress < .43f ? Vector3.Lerp(k1, k2, CssEaseOut((progress - .19f) / .24f))
            : Vector3.Lerp(k2, k3, CssEaseOut((progress - .43f) / .57f));
        return new Vector3(1f - 2f * inset.x, 1f - 2f * inset.y, -2f * inset.z);
    }

    /// <summary>모니터 빛 줄기의 불투명도. session.css monitor-beam(0~18% .88 → 48% .1 → 100% 0).</summary>
    public static float MonitorBeam(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress <= .18f) return .88f;
        if (progress <= .48f) return Mathf.Lerp(.88f, .1f, CssEaseOut((progress - .18f) / .3f));
        return Mathf.Lerp(.1f, 0f, CssEaseOut((progress - .48f) / .52f));
    }

    // ── 떠오르는 글자 · 링 · 완료 문구 ───────────────────────────────────

    /// <summary>떠오르는 글자의 크기 배율. 웹: 0.14까지 1.2로 커졌다가 천천히 줄어든다.</summary>
    public static float TextPop(float k) => k < .14f ? k / .14f * 1.2f : 1.2f - (k - .14f) * .25f;

    /// <summary>떠오르는 글자의 불투명도. fadeStart(SHAKE!는 .25, 나머지 .7)부터 사라진다.</summary>
    public static float TextAlpha(float k, float fadeStart) => Mathf.Clamp01((1f - k) / (1f - fadeStart));

    /// <summary>"쉐이킹 완료!" 크기 배율. 웹 drawShakeCompletion.</summary>
    public static float CompletionScale(float age) =>
        1f + .27f * (1f - Smooth(age, .13f)) - .055f * (1f - Smooth(age - .13f, .12f));

    /// <summary>"쉐이킹 완료!" 불투명도.</summary>
    public static float CompletionAlpha(float age) => Mathf.Min(1f, age / .065f) * (1f - Smooth(age - 1.74f, .46f));
}

/// <summary>
/// 루나 손동작 프레임. 웹 luna.js를 옮겼다. 자동 반복 영상이 아니라 입력마다 현재 셀에서 이어서 움직이고
/// 마지막 셀을 유지한다(예: 정상 출발 D는 0→5→6, 이어서 A는 6→7→0).
/// </summary>
public sealed class ShakeV2LunaMotion
{
    // 시트 순서: 가슴 → 위로 뻗기 → 가슴 → 아래로 뻗기 → 가슴. 반대로 갈 때는 지금 셀에서 시작한다.
    static readonly int[][] Returns =
    {
        new[] { 0 }, new[] { 1, 0 }, new[] { 2, 3, 4 }, new[] { 3, 4 }, new[] { 4 }, new[] { 5, 4 }, new[] { 6, 7, 0 }, new[] { 7, 0 },
    };

    int[] frames = { 0 };
    float startedAt = float.NegativeInfinity;

    /// <summary>한 셀의 길이(초). 12fps.</summary>
    public float FrameSec { get; set; } = 1f / 12f;
    public EShakeV2Input Input { get; private set; } = EShakeV2Input.Back;
    public float StartedAt => startedAt;
    public int[] Frames => (int[])frames.Clone();

    public void Reset()
    {
        frames = new[] { 0 };
        startedAt = float.NegativeInfinity;
        Input = EShakeV2Input.Back;
    }

    /// <summary>t초에 보이는 셀.</summary>
    public int FrameAt(float time)
    {
        if (float.IsNegativeInfinity(startedAt)) return 0;
        int index = Mathf.Max(0, Mathf.FloorToInt((time - startedAt) / FrameSec));
        return frames[Mathf.Min(index, frames.Length - 1)];
    }

    /// <summary>방향 입력을 받아 새 동작을 시작한다. 웹 beginLunaStroke.</summary>
    public void Begin(EShakeV2Input stroke, float time)
    {
        int current = FrameAt(time);
        int[] next = Returns[current];
        if (stroke != EShakeV2Input.Back)
        {
            int end = stroke == EShakeV2Input.Up ? 2 : 6;
            if (current == end) next = new[] { end };
            else if (Mathf.Abs(current - end) == 1) next = new[] { current, end };
            else
            {
                var list = new int[next.Length + 2];
                next.CopyTo(list, 0);
                list[next.Length] = end - 1;
                list[next.Length + 1] = end;
                next = list;
            }
        }
        else if (next.Length == 1)
        {
            // 쉬고 있다가 당길 때도 짧게 예비 동작을 하고 가슴 앞에 멈춘다.
            next = new[] { current, current == 0 ? 7 : 3, current };
        }

        frames = next;
        startedAt = time;
        Input = stroke;
    }

    /// <summary>
    /// 손 강조광의 세기 0~1. 키를 누른 순간이 아니라 도착 셀에서 160ms 동안 맺힌다. A는 약하게(.4), D·E는 .75.
    /// </summary>
    public float PulseAt(float time, float pulseSec = .16f, float backStrength = .4f, float reachStrength = .75f)
    {
        if (float.IsNegativeInfinity(startedAt)) return 0f;
        float elapsed = time - startedAt - (frames.Length - 1) * FrameSec;
        if (elapsed < 0f || elapsed >= pulseSec) return 0f;
        return Mathf.Sin(elapsed / pulseSec * Mathf.PI) * (Input == EShakeV2Input.Back ? backStrength : reachStrength);
    }
}
