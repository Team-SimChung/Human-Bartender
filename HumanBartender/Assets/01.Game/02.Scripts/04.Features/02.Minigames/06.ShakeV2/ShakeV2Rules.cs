using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>쉐이킹 V2의 입력 네 가지. Input Actions 맵 ShakingV2의 액션과 짝을 이룬다.</summary>
public enum EShakeV2Input
{
    /// <summary>Space. 두 틴을 결합한다.</summary>
    Seal,
    /// <summary>D·→. 오른쪽 아래로 뻗는다.</summary>
    Down,
    /// <summary>A·←. 왼쪽 중앙으로 당긴다.</summary>
    Back,
    /// <summary>E·↑. 오른쪽 위로 뻗는다.</summary>
    Up,
}

/// <summary>쉐이킹 V2 진행 단계. 웹 game.js의 phase(close → shake → unseal/pop → done)와 같은 순서다.</summary>
public enum EShakeV2Phase
{
    /// <summary>위 틴이 원호로 오가며 Space를 기다린다. 제조 시간이 흐른다.</summary>
    Seal,
    /// <summary>Space로 닫은 뒤의 전환. 방향 입력을 받지 않지만 제조 시간은 흐른다.</summary>
    Sealing,
    /// <summary>방향 입력으로 왕복한다.</summary>
    Shake,
    /// <summary>모든 회차를 마친 뒤의 분리 연출. 제조 시간은 이미 고정됐다.</summary>
    Unseal,
    /// <summary>내구도가 다해 틴이 빠진 연출. 제조 시간은 이미 고정됐다.</summary>
    Broken,
    /// <summary>결과를 넘겼다. 더 이상 아무것도 바뀌지 않는다.</summary>
    Done,
}

/// <summary>Space를 누른 위치. 시작 내구도와 점수 감점을 정한다.</summary>
public enum EShakeV2SealQuality
{
    /// <summary>아직 결합하지 않았다.</summary>
    None,
    /// <summary>금색 중앙.</summary>
    Center,
    /// <summary>청록 양옆.</summary>
    Side,
    /// <summary>나머지 양 끝. 약한 결합.</summary>
    Edge,
}

/// <summary>회차 하나의 결과. 진행 10칸의 색이 된다.</summary>
public enum EShakeV2RoundResult
{
    Success,
    Miss,
}

/// <summary>MISS가 난 이유. 오입력만 내구도를 깎는다.</summary>
public enum EShakeV2MissReason
{
    WrongKey,
    Timeout,
}

/// <summary>입력 한 번을 어떻게 처리했는지.</summary>
public enum EShakeV2PressResult
{
    /// <summary>지금은 받지 않는 입력이다. 아무것도 바뀌지 않았다.</summary>
    Ignored,
    /// <summary>두 틴을 결합했다.</summary>
    Sealed,
    /// <summary>요구한 키를 맞혔다.</summary>
    Accepted,
    /// <summary>정지 상태에서 다른 키를 눌렀다. 회차 MISS + 내구도 −1.</summary>
    Missed,
    /// <summary>셰이커가 아직 움직이는데 또 눌렀다. 회차는 그대로, 내구도 −1.</summary>
    Early,
}

/// <summary>화면·소리가 반응할 일. 규칙이 상태를 바꾼 그 순간에 한 번씩 알린다.</summary>
public enum EShakeV2EventKind
{
    /// <summary>결합했다. Strong이면 금색 중앙이다(웹: 80ms 순간 정지).</summary>
    Sealed,
    /// <summary>전환이 끝나 첫 키를 띄운다.</summary>
    ShakeStarted,
    /// <summary>요구한 키를 맞혔다. Strong이면 회차 시간 앞쪽에 친 빠른 입력이다(큰 링, 45ms 순간 정지).</summary>
    StrokeAccepted,
    /// <summary>회차의 두 키를 모두 맞혔다.</summary>
    RoundSucceeded,
    /// <summary>회차를 MISS로 끝냈다. Reason이 이유다.</summary>
    RoundMissed,
    /// <summary>이동 중에 눌러 내구도를 잃었다.</summary>
    EarlyInput,
    /// <summary>셰이커가 목표에 도착해 멈췄다.</summary>
    Arrived,
    /// <summary>내구도가 다해 틴이 빠졌다.</summary>
    Broken,
    /// <summary>정상 분리 연출을 시작했다.</summary>
    UnsealStarted,
    /// <summary>결과를 넘길 때가 됐다. 한 게임에 한 번뿐이다.</summary>
    Finished,
}

/// <summary>규칙이 알리는 일 하나.</summary>
public readonly struct ShakeV2Event
{
    public readonly EShakeV2EventKind Kind;
    /// <summary>StrokeAccepted·Arrived에서 어느 방향인지.</summary>
    public readonly EShakeV2Input Stroke;
    /// <summary>Sealed에서는 중앙 결합, StrokeAccepted에서는 빠른 입력.</summary>
    public readonly bool Strong;
    /// <summary>RoundMissed의 이유.</summary>
    public readonly EShakeV2MissReason Reason;
    /// <summary>규칙의 게임 시간(초).</summary>
    public readonly float Time;

    public ShakeV2Event(EShakeV2EventKind kind, float time, EShakeV2Input stroke = EShakeV2Input.Seal,
                        bool strong = false, EShakeV2MissReason reason = EShakeV2MissReason.WrongKey)
    {
        Kind = kind; Time = time; Stroke = stroke; Strong = strong; Reason = reason;
    }
}

/// <summary>
/// 쉐이킹 V2 판정·움직임 수치. 기본값은 웹 V2(game.js, 리비전 4d7b20d)의 값이다.
///
/// 위치·속도는 오른쪽 게임 좌표(880×640, 원점 왼쪽 위, 아래가 +Y) 기준이다. 화면 픽셀이나 월드 미터가 아니다.
/// CSV에는 넣지 않는다. 웹 CSV 계약에 없는 값이라 엔진만 열을 추가하면 다음 웹 반입이 막힌다.
/// </summary>
[Serializable]
public sealed class ShakeV2Tuning
{
    [Header("결합")]
    [Tooltip("위 틴 편차의 최대값. 편차 = −진폭 × cos(속도 × t), t는 결합 대기를 시작한 뒤의 시간. 웹 B.CLOSE.amp.")]
    [Min(0f)] public float sealAmplitude = 16f;
    [Tooltip("편차가 도는 각속도(rad/s). 2.55면 약 2.46초에 한 번 왕복한다. 웹 B.CLOSE.speed.")]
    [Min(0f)] public float sealSpeed = 2.55f;
    [Tooltip("|편차|가 이 값 이하에서 Space를 누르면 금색 중앙 결합.")]
    [Min(0f)] public float sealCenterRange = 3f;
    [Tooltip("|편차|가 이 값 이하에서 Space를 누르면 청록 양옆 결합. 넘으면 양 끝(약한 결합)이다.")]
    [Min(0f)] public float sealSideRange = 8f;
    [Tooltip("금색 중앙 결합의 시작 내구도.")]
    [Min(1)] public int centerDurability = 3;
    [Tooltip("청록 양옆 결합의 시작 내구도.")]
    [Min(1)] public int sideDurability = 2;
    [Tooltip("양 끝 결합의 시작 내구도.")]
    [Min(1)] public int edgeDurability = 1;
    [Tooltip("금색 중앙 결합의 점수 감점.")]
    [Min(0)] public int centerPenalty = 0;
    [Tooltip("청록 양옆 결합의 점수 감점.")]
    [Min(0)] public int sidePenalty = 3;
    [Tooltip("양 끝 결합의 점수 감점.")]
    [Min(0)] public int edgePenalty = 8;
    [Tooltip("Space 뒤 첫 방향 입력을 받기까지의 시간(초). 이 동안 눕히기·모니터 열림이 돈다.")]
    [Min(0f)] public float sealTransitionSec = 1f;

    [Header("왕복")]
    [Tooltip("왕복 횟수. 진행 칸 수이기도 하다.")]
    [Min(1)] public int rounds = 10;
    [Tooltip("1회차 제한 시간(초). 두 키가 이 시간을 함께 쓴다.")]
    [Min(.1f)] public float firstRoundSec = 3f;
    [Tooltip("마지막 회차 제한 시간(초). 사이 회차는 직선으로 줄어든다.")]
    [Min(.1f)] public float lastRoundSec = 1.5f;
    [Tooltip("회차 제한 시간 배율. 본편은 1. 웹 연습의 쉬움 1.25, 어려움 0.8.")]
    [Min(.1f)] public float roundTimeScale = 1f;
    [Tooltip("이번 회차 경과율이 이 값보다 작을 때 친 입력은 빠른 입력(큰 링·짧은 순간 정지)이다. 점수와는 무관하다.")]
    [Range(0f, 1f)] public float fastStrokeRatio = .55f;
    [Tooltip("MISS 뒤 다음 회차까지 입력을 받지 않는 시간(초).")]
    [Min(0f)] public float missWaitSec = .35f;
    [Tooltip("MISS 한 번마다 흘리는 액체 비율(처음 양 기준).")]
    [Range(0f, 1f)] public float missSpill = .09f;
    [Tooltip("시간 윤곽 색이 바뀌는 경과율. 순서대로 연두·주황·빨강이 시작하는 지점이다.")]
    public float[] gaugeBandStarts = { .25f, .5f, .75f };

    [Header("셰이커 움직임")]
    [Tooltip("D·E의 가로 목표, A의 가로 목표(−값). 웹 B.TRAVEL.")]
    [Min(0f)] public float travel = 62f;
    [Tooltip("D의 세로 목표(아래 +), E의 세로 목표(위 −). 웹 B.VTRAVEL.")]
    [Min(1f)] public float verticalTravel = 44f;
    [Tooltip("세로 목표 끝에서의 축 기울기(rad). 기울기 = 세로 위치 / 세로 목표 × 이 값.")]
    public float strokeTilt = .18f;
    [Tooltip("스프링 계수.")]
    [Min(0f)] public float springStiffness = 1400f;
    [Tooltip("스프링 감쇠.")]
    [Min(0f)] public float springDamping = 36f;
    [Tooltip("스프링 적분 간격(초). 프레임률과 상관없이 이 간격으로 나눠 계산한다. " +
             "웹은 프레임 간격(최대 17ms)으로 계산해 60Hz에서 약 0.22초, 120Hz에서 약 0.25초 만에 멈춘다.")]
    [Range(.001f, .017f)] public float springStepSec = 1f / 120f;
    [Tooltip("키를 맞힌 순간 셰이커에 주는 가로 초속도. A는 왼쪽, D·E는 오른쪽.")]
    [Min(0f)] public float strokeKickSpeed = 480f;
    [Tooltip("목표와의 거리가 이보다 작고")]
    [Min(0f)] public float arriveDistance = .75f;
    [Tooltip("속력이 이보다 작으면 도착으로 보고 위치를 목표에 맞춘 뒤 다음 키를 띄운다.")]
    [Min(0f)] public float arriveSpeed = 18f;

    [Header("마무리")]
    [Tooltip("분리 연출 길이(초). 분리에 들어간 순간부터 센다. 끝나면 결과를 넘긴다.")]
    [Min(0f)] public float unsealSec = 3f;
    [Tooltip("파손 연출 길이(초). 끝나면 0점을 넘긴다.")]
    [Min(0f)] public float brokenSec = 1.5f;
}

/// <summary>
/// 쉐이킹 V2 판정 규칙. 웹 V2(src/dev/shaking/game.js)의 결합·왕복·판정·종료를 옮긴 순수 C# 클래스다.
///
/// 화면도 입력 장치도 모른다. 입력(Press)과 게임 시간(Tick)만 받아 상태를 바꾸고, 그리는 쪽은 상태를 읽고
/// 이벤트(Raised)에 반응한다. 지연 실행(결합 전환·MISS 대기·분리·파손)도 모두 받은 게임 시간으로만 흐르므로,
/// Tick을 부르지 않으면 일시정지다. 순간 정지(히트스톱)는 이 클래스 밖에서 Tick을 쉬는 것으로 처리한다.
///
/// 웹 규칙 중 헷갈리기 쉬운 것:
/// - 키 순서는 인정된 입력만 전진한다. D를 맞히고 A를 놓쳐 회차가 끝나도 다음 회차는 A부터다.
/// - 이동 중 입력(조기 입력)은 회차를 소모하지 않고 내구도만 깎는다. 정지 상태의 오입력은 회차 MISS + 내구도 −1.
/// - 시간 초과는 회차 MISS지만 내구도는 그대로다.
/// - 마지막 회차의 두 번째 키까지 맞히면 그 뒤 방향 입력은 전부 무시한다(조기 입력도 아니다).
/// - 제조 시간은 마지막 정입력, 마지막 회차가 MISS면 그 대기가 끝난 순간, 파손이면 그 순간 고정된다.
/// </summary>
public sealed class ShakeV2Rules
{
    /// <summary>한 번의 Tick에 흐를 수 있는 최대 시간(초). 웹 루프의 50ms 상한과 같다.</summary>
    public const float MaxStep = .05f;

    /// <summary>결과의 목표 스택 수. 점수를 정수 분자로 넘겨 97점이 그대로 0.97이 되게 한다.</summary>
    public const int ResultScale = 100;

    /// <summary>회차당 키 수. 웹 본편은 D→A / E→A 두 키로 고정이다.</summary>
    public const int StrokesPerRound = 2;

    /// <summary>방향의 순환. 웹 strokeCycle 'DAEA'.</summary>
    static readonly EShakeV2Input[] Cycle = { EShakeV2Input.Down, EShakeV2Input.Back, EShakeV2Input.Up, EShakeV2Input.Back };

    readonly ShakeV2Tuning tuning;
    readonly List<EShakeV2RoundResult> results = new();
    readonly EShakeV2Input[] sequence = new EShakeV2Input[StrokesPerRound];

    float sealAt, roundStart, readySince, pendingUntil, unsealAt, brokenAt, frozenAt, lastStrokeAt;
    float springCarry;
    Vector2 previousPosition;

    public ShakeV2Rules(ShakeV2Tuning tuning = null)
    {
        this.tuning = tuning ?? new ShakeV2Tuning();
        SealOffset = SealOffsetAt(0f);
        sequence[0] = Cycle[0];
        sequence[1] = Cycle[1];
    }

    /// <summary>상태가 바뀐 순간마다 한 번씩 알린다.</summary>
    public event Action<ShakeV2Event> Raised;

    public ShakeV2Tuning Tuning => tuning;
    public EShakeV2Phase Phase { get; private set; } = EShakeV2Phase.Seal;
    /// <summary>이 게임이 받은 게임 시간의 합(초).</summary>
    public float GameTime { get; private set; }

    // ── 결합 ────────────────────────────────────────────────────────────

    /// <summary>결합 대기를 한 시간(초). 편차 계산의 t다.</summary>
    public float SealTime { get; private set; }
    /// <summary>위 틴의 편차(−진폭~+진폭). 결합한 순간 그 값에서 멈춘다. 바늘과 틴이 같은 값을 쓴다.</summary>
    public float SealOffset { get; private set; }
    public EShakeV2SealQuality SealQuality { get; private set; }
    /// <summary>결합한 뒤 흐른 시간(초). 결합 전에는 0이다. 눕히기·모니터 열림 연출의 기준이다.</summary>
    public float SealAge => SealQuality == EShakeV2SealQuality.None ? 0f : GameTime - sealAt;
    public int StartDurability { get; private set; }
    public int DurabilityLost => WrongInputs + EarlyInputs;
    public int DurabilityLeft => Mathf.Max(0, StartDurability - DurabilityLost);
    /// <summary>결합 품질에 따른 점수 감점.</summary>
    public int SealPenalty => SealQuality switch
    {
        EShakeV2SealQuality.Center => tuning.centerPenalty,
        EShakeV2SealQuality.Side => tuning.sidePenalty,
        EShakeV2SealQuality.Edge => tuning.edgePenalty,
        _ => 0,
    };

    // ── 왕복 ────────────────────────────────────────────────────────────

    /// <summary>끝낸 회차 수. 지금 진행 중인 회차의 번호(0부터)이기도 하다.</summary>
    public int Round { get; private set; }
    /// <summary>이번 회차에서 맞힌 키 수.</summary>
    public int KeyIndex { get; private set; }
    /// <summary>순환에서 다음에 요구할 위치. 인정된 입력만 전진한다.</summary>
    public int StrokeIndex { get; private set; }
    /// <summary>이번 회차에 요구하는 키 두 개.</summary>
    public IReadOnlyList<EShakeV2Input> Sequence => sequence;
    public bool StrokeMoving { get; private set; }
    /// <summary>MISS 뒤 다음 회차를 기다리는 중.</summary>
    public bool MissPending { get; private set; }
    /// <summary>셰이커 중심의 위치. 기준점(440,322)에서 잰 거리다.</summary>
    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public Vector2 Target { get; private set; }
    /// <summary>적분 간격 사이를 보간한 위치. 화면에 그릴 때 쓴다.</summary>
    public Vector2 RenderPosition => Vector2.Lerp(previousPosition, Position, Mathf.Clamp01(springCarry / tuning.springStepSec));
    /// <summary>셰이커 축 기울기(rad). 아래로 갈수록 +.</summary>
    public float Tilt => TiltAt(Position.y);
    public float RenderTilt => TiltAt(RenderPosition.y);
    /// <summary>마지막으로 맞힌 방향과 그 시각.</summary>
    public EShakeV2Input LastStroke { get; private set; } = EShakeV2Input.Back;
    public float LastStrokeAge => GameTime - lastStrokeAt;

    public IReadOnlyList<EShakeV2RoundResult> Results => results;
    public int Successes { get; private set; }
    public int WrongInputs { get; private set; }
    public int EarlyInputs { get; private set; }
    public int Timeouts { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    /// <summary>판정에 넘기는 실패 수 = 오입력 + 조기 입력 + 시간 초과.</summary>
    public int Failures => WrongInputs + EarlyInputs + Timeouts;
    /// <summary>MISS 회차 수. 액체를 흘린 횟수다.</summary>
    public int MissCount => WrongInputs + Timeouts;
    /// <summary>지금까지 흘린 액체 비율(처음 양 기준).</summary>
    public float SpilledFraction => Mathf.Min(1f, MissCount * tuning.missSpill);
    /// <summary>혼합도 0~100. 성공 회차마다 오른다.</summary>
    public float Mix { get; private set; }
    /// <summary>냉각도 0~100. 성공 회차마다 오른다.</summary>
    public float Chill { get; private set; }

    /// <summary>이번 회차의 제한 시간(초).</summary>
    public float RoundLimit => RoundLimitFor(Round);
    /// <summary>이번 회차가 시작된 뒤 흐른 시간(초).</summary>
    public float RoundElapsed => GameTime - roundStart;

    /// <summary>
    /// 시간 윤곽의 경과율 0~1. 웹 drawGauge와 같다 — 1이면 윤곽이 모두 꺼진 상태다.
    /// 성공 뒤 돌아오는 동안은 0(전부 빛남), MISS 대기·마무리 중에는 1이다.
    /// </summary>
    public float GaugeElapsed
    {
        get
        {
            if (Phase != EShakeV2Phase.Shake || MissPending || Round >= tuning.rounds) return 1f;
            return StrokeMoving && KeyIndex == 0 ? 0f : Mathf.Clamp01(RoundElapsed / RoundLimit);
        }
    }

    /// <summary>지금 띄울 키. 이동 중·MISS 대기·마지막 회차 뒤에는 없다.</summary>
    public EShakeV2Input? ReadyKey =>
        Phase == EShakeV2Phase.Shake && !StrokeMoving && !MissPending && Round < tuning.rounds
            ? sequence[KeyIndex]
            : null;
    /// <summary>지금 키를 띄운 뒤 흐른 시간(초). 키 상자가 커졌다 가라앉는 연출의 기준이다.</summary>
    public float ReadyAge => GameTime - readySince;

    // ── 마무리 ──────────────────────────────────────────────────────────

    public bool WasBroken { get; private set; }
    /// <summary>분리를 시작할 때의 셰이커 위치·기울기. 분리 연출이 여기서 중앙으로 모인다.</summary>
    public Vector2 UnsealFrom { get; private set; }
    public float UnsealTilt { get; private set; }
    public float UnsealAge => Phase is EShakeV2Phase.Unseal or EShakeV2Phase.Done && !WasBroken ? GameTime - unsealAt : 0f;
    public float BrokenAge => WasBroken ? GameTime - brokenAt : 0f;
    public bool Finished => Phase == EShakeV2Phase.Done;

    /// <summary>제조 시간이 고정됐는지.</summary>
    public bool ClockFrozen { get; private set; }
    /// <summary>이 게임이 제조 시간에 더한 시간(초). 결합 대기부터 고정될 때까지다.</summary>
    public float ClockSec => ClockFrozen ? frozenAt : GameTime;
    /// <summary>
    /// 지금 제조 시간을 세는 구간인지. 결합 대기·결합 전환·왕복·MISS 대기만 참이다.
    /// 안내·일시정지·순간 정지는 이 클래스가 Tick을 받지 않으므로 바깥에서 따로 거짓으로 만든다.
    /// </summary>
    public bool IsTimeCounting => !ClockFrozen &&
        Phase is EShakeV2Phase.Seal or EShakeV2Phase.Sealing or EShakeV2Phase.Shake;

    /// <summary>
    /// 쉐이킹 점수 0~100. 성공 회차 ÷ 회차 × 100을 반올림한 뒤 결합 감점을 뺀다. 파손이면 0이다.
    /// 콤보·조기 입력은 점수에 들어가지 않는다(내구도에서 이미 처리했다).
    /// </summary>
    public int Score
    {
        get
        {
            if (WasBroken) return 0;
            int raw = (int)Math.Round(Successes * 100.0 / tuning.rounds, MidpointRounding.AwayFromZero);
            return Mathf.Clamp(raw - SealPenalty, 0, 100);
        }
    }

    /// <summary>
    /// 제조 결과. 점수를 100분의 정수로 넘겨 완성도가 점수/100 그대로 되게 한다(97점 → 0.97).
    /// 파손도 제조 단계를 끝낸 것으로 보고 0점으로 넘긴다.
    /// </summary>
    public GimmickResult ToResult(float completedAtSec) =>
        GimmickResult.Mix(ECraftGimmick.Shake, Score, Failures, ResultScale, ECraftEndType.AutoTarget, completedAtSec);

    // ── 계산 도우미 ─────────────────────────────────────────────────────

    /// <summary>결합 대기 t초의 편차. 왼쪽 끝(−진폭)에서 시작한다.</summary>
    public float SealOffsetAt(float sealTime) => -tuning.sealAmplitude * Mathf.Cos(tuning.sealSpeed * sealTime);

    /// <summary>편차로 결합 품질을 정한다.</summary>
    public EShakeV2SealQuality QualityAt(float offset)
    {
        float distance = Mathf.Abs(offset);
        if (distance <= tuning.sealCenterRange) return EShakeV2SealQuality.Center;
        return distance <= tuning.sealSideRange ? EShakeV2SealQuality.Side : EShakeV2SealQuality.Edge;
    }

    /// <summary>n번째(0부터) 회차의 제한 시간. 처음에서 마지막까지 직선으로 줄어든다.</summary>
    public float RoundLimitFor(int roundIndex)
    {
        float progress = Mathf.Min(1f, roundIndex / (float)Mathf.Max(1, tuning.rounds - 1));
        return Mathf.Lerp(tuning.firstRoundSec, tuning.lastRoundSec, progress) * tuning.roundTimeScale;
    }

    /// <summary>시간 윤곽 색 구간 0~3(청백·연두·주황·빨강).</summary>
    public int GaugeBand(float elapsed)
    {
        int band = 0;
        float[] starts = tuning.gaugeBandStarts ?? Array.Empty<float>();
        for (int i = 0; i < starts.Length; i++)
            if (elapsed >= starts[i]) band = i + 1;
        return band;
    }

    /// <summary>방향의 좌우. A는 −1(작은 틴 왼쪽 끝), D·E는 +1(큰 틴 오른쪽 끝). 링·내용물·루나가 같은 쪽을 쓴다.</summary>
    public static int Side(EShakeV2Input stroke) => stroke switch
    {
        EShakeV2Input.Back => -1,
        EShakeV2Input.Down or EShakeV2Input.Up => 1,
        _ => 0,
    };

    /// <summary>방향별 도착 위치. D(+62,+44), A(−62,0), E(+62,−44).</summary>
    public Vector2 TargetOf(EShakeV2Input stroke) => stroke switch
    {
        EShakeV2Input.Down => new Vector2(tuning.travel, tuning.verticalTravel),
        EShakeV2Input.Up => new Vector2(tuning.travel, -tuning.verticalTravel),
        _ => new Vector2(-tuning.travel, 0f),
    };

    public float TiltAt(float y) => y / tuning.verticalTravel * tuning.strokeTilt;

    // ── 입력 ────────────────────────────────────────────────────────────

    /// <summary>입력 한 번. 누른 순간에만 부른다(뗌·자동 반복은 넘기지 않는다).</summary>
    public EShakeV2PressResult Press(EShakeV2Input input)
    {
        if (Phase == EShakeV2Phase.Seal)
        {
            if (input != EShakeV2Input.Seal) return EShakeV2PressResult.Ignored;
            Seal();
            return EShakeV2PressResult.Sealed;
        }

        // 결합 전환·마무리 중의 입력, 왕복 중의 Space, MISS 대기 중의 입력, 마지막 회차를 끝낸 뒤의 입력은 받지 않는다.
        if (Phase != EShakeV2Phase.Shake || input == EShakeV2Input.Seal) return EShakeV2PressResult.Ignored;
        if (MissPending || Round >= tuning.rounds) return EShakeV2PressResult.Ignored;

        if (StrokeMoving)
        {
            EarlyInputs++;
            Raise(new ShakeV2Event(EShakeV2EventKind.EarlyInput, GameTime));
            BreakIfSpent();
            return EShakeV2PressResult.Early;
        }

        if (input != sequence[KeyIndex])
        {
            Miss(EShakeV2MissReason.WrongKey);
            return EShakeV2PressResult.Missed;
        }

        Accept(input);
        return EShakeV2PressResult.Accepted;
    }

    // ── 시간 ────────────────────────────────────────────────────────────

    /// <summary>게임 시간을 흘린다. 일시정지·순간 정지 중에는 부르지 않는다.</summary>
    public void Tick(float dt)
    {
        if (Phase == EShakeV2Phase.Done) return;
        dt = Mathf.Min(MaxStep, Mathf.Max(0f, dt));
        if (dt <= 0f) return;
        GameTime += dt;

        switch (Phase)
        {
            case EShakeV2Phase.Seal:
                SealTime += dt;
                SealOffset = SealOffsetAt(SealTime);
                break;

            case EShakeV2Phase.Sealing:
                if (GameTime - sealAt >= tuning.sealTransitionSec) StartShake();
                break;

            case EShakeV2Phase.Shake:
                TickShake(dt);
                break;

            case EShakeV2Phase.Unseal:
                if (GameTime - unsealAt >= tuning.unsealSec) Finish();
                break;

            case EShakeV2Phase.Broken:
                if (GameTime - brokenAt >= tuning.brokenSec) Finish();
                break;
        }
    }

    void TickShake(float dt)
    {
        // 웹 순서: 지연 실행(MISS 대기 끝) → 시간 초과 → 스프링.
        if (MissPending && GameTime >= pendingUntil)
        {
            MissPending = false;
            NextRound();
            if (Phase != EShakeV2Phase.Shake) return;
        }

        if (!MissPending && Round < tuning.rounds && RoundElapsed > RoundLimit)
            Miss(EShakeV2MissReason.Timeout);

        IntegrateSpring(dt);
    }

    void IntegrateSpring(float dt)
    {
        float h = tuning.springStepSec;
        springCarry += dt;
        while (springCarry >= h && Phase == EShakeV2Phase.Shake)
        {
            springCarry -= h;
            previousPosition = Position;
            Velocity += (tuning.springStiffness * (Target - Position) - tuning.springDamping * Velocity) * h;
            Position += Velocity * h;

            if (!StrokeMoving || (Target - Position).magnitude >= tuning.arriveDistance ||
                Velocity.magnitude >= tuning.arriveSpeed) continue;

            // 한 디자인 픽셀 안에 멈추면 위치를 정확히 맞추고 다음 키를 띄운다.
            Position = previousPosition = Target;
            Velocity = Vector2.zero;
            StrokeMoving = false;
            readySince = GameTime;
            Raise(new ShakeV2Event(EShakeV2EventKind.Arrived, GameTime, LastStroke));

            if (Round >= tuning.rounds && !MissPending) StartUnseal();
            else if (KeyIndex == 0 && !MissPending) roundStart = GameTime;
        }
    }

    // ── 단계 전환 ───────────────────────────────────────────────────────

    void Seal()
    {
        SealQuality = QualityAt(SealOffset);
        StartDurability = SealQuality switch
        {
            EShakeV2SealQuality.Center => tuning.centerDurability,
            EShakeV2SealQuality.Side => tuning.sideDurability,
            _ => tuning.edgeDurability,
        };
        sealAt = GameTime;
        Phase = EShakeV2Phase.Sealing;
        Raise(new ShakeV2Event(EShakeV2EventKind.Sealed, GameTime, strong: SealQuality == EShakeV2SealQuality.Center));
    }

    void StartShake()
    {
        Phase = EShakeV2Phase.Shake;
        Target = Position = previousPosition = TargetOf(EShakeV2Input.Back);
        Velocity = Vector2.zero;
        springCarry = 0f;
        NextRound();
        Raise(new ShakeV2Event(EShakeV2EventKind.ShakeStarted, GameTime));
    }

    void Accept(EShakeV2Input stroke)
    {
        bool fast = RoundElapsed / RoundLimit < tuning.fastStrokeRatio;
        StrokeIndex = (StrokeIndex + 1) % Cycle.Length;
        Target = TargetOf(stroke);
        Velocity = new Vector2(Side(stroke) * tuning.strokeKickSpeed, Velocity.y);
        StrokeMoving = true;
        LastStroke = stroke;
        lastStrokeAt = GameTime;
        Raise(new ShakeV2Event(EShakeV2EventKind.StrokeAccepted, GameTime, stroke, fast));

        KeyIndex++;
        if (KeyIndex >= StrokesPerRound) Succeed();
    }

    void Succeed()
    {
        results.Add(EShakeV2RoundResult.Success);
        Round++;
        Successes++;
        Combo++;
        BestCombo = Mathf.Max(BestCombo, Combo);
        Mix = Mathf.Min(100f, Mix + 100f / tuning.rounds);
        Chill = Mathf.Min(100f, Chill + 100f / tuning.rounds);
        Raise(new ShakeV2Event(EShakeV2EventKind.RoundSucceeded, GameTime));

        // 마지막 정입력에서 시간을 고정한다. 분리는 마지막 동작이 도착한 뒤에 시작한다.
        if (Round >= tuning.rounds) FreezeClock();
        else NextRound();
    }

    void Miss(EShakeV2MissReason reason)
    {
        results.Add(EShakeV2RoundResult.Miss);
        Round++;
        Combo = 0;
        if (reason == EShakeV2MissReason.Timeout) Timeouts++;
        else WrongInputs++;
        Raise(new ShakeV2Event(EShakeV2EventKind.RoundMissed, GameTime, reason: reason));

        if (reason == EShakeV2MissReason.WrongKey && BreakIfSpent()) return;
        MissPending = true;
        pendingUntil = GameTime + tuning.missWaitSec;
    }

    void NextRound()
    {
        if (Round >= tuning.rounds)
        {
            StartUnseal();
            return;
        }

        for (int i = 0; i < StrokesPerRound; i++)
            sequence[i] = Cycle[(StrokeIndex + i) % Cycle.Length];
        KeyIndex = 0;
        roundStart = readySince = GameTime;
    }

    bool BreakIfSpent()
    {
        if (DurabilityLost < StartDurability) return false;
        Phase = EShakeV2Phase.Broken;
        WasBroken = true;
        brokenAt = GameTime;
        FreezeClock();
        Raise(new ShakeV2Event(EShakeV2EventKind.Broken, GameTime));
        return true;
    }

    void StartUnseal()
    {
        Phase = EShakeV2Phase.Unseal;
        FreezeClock();
        unsealAt = GameTime;
        UnsealFrom = Position;
        UnsealTilt = Tilt;
        Raise(new ShakeV2Event(EShakeV2EventKind.UnsealStarted, GameTime));
    }

    void FreezeClock()
    {
        if (ClockFrozen) return;
        ClockFrozen = true;
        frozenAt = GameTime;
    }

    void Finish()
    {
        Phase = EShakeV2Phase.Done;
        Raise(new ShakeV2Event(EShakeV2EventKind.Finished, GameTime));
    }

    void Raise(ShakeV2Event e) => Raised?.Invoke(e);
}
