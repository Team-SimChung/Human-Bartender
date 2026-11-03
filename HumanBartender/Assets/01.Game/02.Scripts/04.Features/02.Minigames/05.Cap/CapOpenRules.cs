using System;
using UnityEngine;

/// <summary>병따기 진행 단계. 웹 openPhase와 같은 이름·순서다.</summary>
public enum ECapOpenPhase
{
    /// <summary>시작 전. 첫 입력이 힘 조절을 시작한다.</summary>
    Ready,
    /// <summary>힘 게이지가 왕복하는 중. 초록 구간에서 누른다.</summary>
    Power,
    /// <summary>힘 조절 성공 직후의 짧은 대기. 입력을 받지 않는다.</summary>
    PowerDone,
    /// <summary>바깥 원이 목표 링으로 조여드는 중. 겹칠 때 누른다.</summary>
    Rhythm,
    /// <summary>병이 깨진 뒤 새 병을 준비하는 대기. 입력을 받지 않는다.</summary>
    Broken,
    /// <summary>뚜껑이 열렸다. 더 이상 판정하지 않는다.</summary>
    Pop,
}

/// <summary>
/// 병따기 판정에 쓰는 수치. 기본값은 웹 본편(gimmick-mechanics.js)에 하드코딩된 값이다.
///
/// CSV의 open_approach_sec(1.6)·open_judge_window_px·open_transition_delay_sec·open_input_enabled_after_sec는
/// 웹도 읽지 않는다. 웹과 같은 손맛을 내려면 그 값이 아니라 여기 값을 써야 한다.
/// </summary>
[Serializable]
public sealed class CapOpenTuning
{
    [Header("힘 조절")]
    [Tooltip("힘 게이지가 1초에 움직이는 양(0~1 왕복).")]
    public float powerSpeed = 1.1f;
    [Tooltip("초록 구간의 중심.")]
    public float powerTarget = .78f;
    [Tooltip("중심에서 이만큼 안이면 힘 조절 성공.")]
    public float powerWindow = .152f;
    [Tooltip("중심에서 이만큼 안이면 힘 조절 perfect. 최종 perfect의 조건 중 하나다.")]
    public float powerPerfectWindow = .08f;
    [Tooltip("힘 조절 성공 뒤 링 타이밍이 시작되기까지의 대기(초).")]
    public float powerDoneDelay = .45f;

    [Header("링 타이밍")]
    [Tooltip("바깥 원이 목표 링에 겹치는 시각(초).")]
    public float beatSec = 1.1f;
    [Tooltip("겹치는 시각 앞뒤로 이만큼 안에 누르면 성공(초).")]
    public float beatWindow = .14f;
    [Tooltip("이 시각을 넘기면 누르지 않아도 실패다(초).")]
    public float beatTimeout = 1.24f;
    [Tooltip("힘 조절이 perfect이고 타이밍 오차가 이 안이면 최종 perfect(초).")]
    public float perfectBeatWindow = .065f;

    [Header("실패")]
    [Tooltip("이 횟수만큼 연달아 실패하면 병이 깨진다. 힘·타이밍 실패를 합산한다.")]
    public int missesToBreak = 3;
    [Tooltip("병이 깨진 뒤 새 병으로 힘 조절을 다시 시작하기까지의 대기(초).")]
    public float brokenDelay = 1.2f;

    [Header("연출")]
    [Tooltip("성공 뒤 탄산이 올라오는 시간(초). 이 시간이 지나야 다음 버튼이 켜진다.")]
    public float carbonationSec = 2.05f;
    [Tooltip("판정 연출의 나이가 이 값에서 멈춘다(웹 minigameHoldSec).")]
    public float fxHoldSec = 4.1f;
}

/// <summary>
/// 마지막 판정의 연출 상태. 웹 openFx와 같다 — 판정할 때마다 새로 만들고 serial이 하나씩 오른다.
/// age는 완료 뒤에도 계속 흘러 뚜껑 비행·탄산 같은 연출이 끝까지 재생된다.
/// </summary>
public readonly struct CapOpenFx
{
    public readonly bool Success;
    public readonly bool Perfect;
    /// <summary>판정 오차. 힘 조절은 게이지 위치 − 중심, 타이밍은 누른 시각 − 겹치는 시각.</summary>
    public readonly float Offset;
    public readonly float Age;
    public readonly int Serial;

    public CapOpenFx(bool success, bool perfect, float offset, float age, int serial)
    {
        Success = success; Perfect = perfect; Offset = offset; Age = age; Serial = serial;
    }

    public CapOpenFx WithAge(float age) => new CapOpenFx(Success, Perfect, Offset, age, Serial);
    public CapOpenFx WithPerfect(bool perfect) => new CapOpenFx(Success, perfect, Offset, Age, Serial);
}

/// <summary>
/// 병따기 판정 규칙. 웹 본편 OPEN(gimmick-mechanics.js)을 그대로 옮긴 순수 C# 클래스다.
///
/// 화면도 입력 장치도 모른다. 누름(Press)과 시간(Tick)만 받아 단계를 바꾸고, 그리는 쪽은
/// 이 상태를 읽기만 한다. 그래야 테스트에서 프레임 없이 판정 경계를 그대로 확인할 수 있다.
///
/// 웹의 특이한 규칙도 그대로 둔다. 실패 수(RoundMisses)는 힘 조절과 타이밍 실패를 합산하고 병이
/// 깨질 때만 0으로 돌아간다 — 힘 조절에 두 번 실패한 뒤 성공했다면 타이밍에서 한 번만 놓쳐도 병이
/// 깨진다. 게이지 방향(PowerDirection)도 깨진 뒤까지 이어진다.
/// </summary>
public sealed class CapOpenRules
{
    /// <summary>한 프레임에 흐를 수 있는 최대 시간. 웹 core.tick과 같다.</summary>
    public const float MaxStep = .2f;

    readonly CapOpenTuning tuning;

    public CapOpenRules(CapOpenTuning tuning = null)
    {
        this.tuning = tuning ?? new CapOpenTuning();
    }

    public CapOpenTuning Tuning => tuning;
    public ECapOpenPhase Phase { get; private set; } = ECapOpenPhase.Ready;
    public bool Started { get; private set; }
    public bool Completed { get; private set; }
    /// <summary>힘 게이지 위치 0~1.</summary>
    public float PowerPos { get; private set; }
    /// <summary>힘 게이지가 움직이는 방향. 1이면 위로, −1이면 아래로.</summary>
    public int PowerDirection { get; private set; } = 1;
    public bool PowerPerfect { get; private set; }
    /// <summary>병이 깨지기까지 쌓인 실패 수. 금이 가는 연출에도 쓴다.</summary>
    public int RoundMisses { get; private set; }
    /// <summary>이번 병따기 전체의 실패 수. 판정에 넘기는 값이다.</summary>
    public int Failures { get; private set; }
    /// <summary>링 타이밍이 시작된 뒤 흐른 시간(초).</summary>
    public float BeatTime { get; private set; }
    /// <summary>PowerDone·Broken 대기의 남은 시간(초).</summary>
    public float Delay { get; private set; }
    /// <summary>판정 연출. 한 번도 판정하지 않았다면 null이다.</summary>
    public CapOpenFx? Fx { get; private set; }

    /// <summary>성공했고 탄산이 다 올라와 다음으로 넘어갈 수 있는지.</summary>
    public bool CanFinish => Completed && Fx.HasValue && Fx.Value.Age >= tuning.carbonationSec;

    /// <summary>지금 판정 구간 안인지. 타이밍 힌트를 띄우는 데 쓴다.</summary>
    public bool InBeatWindow => Phase == ECapOpenPhase.Rhythm && !Completed &&
                                Mathf.Abs(BeatTime - tuning.beatSec) <= tuning.beatWindow;

    /// <summary>한 번 누름. 받아들였으면 true, 무시했으면 false다.</summary>
    public bool Press()
    {
        if (Completed || Phase is ECapOpenPhase.Broken or ECapOpenPhase.PowerDone) return false;

        if (!Started || Phase == ECapOpenPhase.Ready)
        {
            Started = true;
            Phase = ECapOpenPhase.Power;
            PowerPos = 0f;
            return true;
        }

        if (Phase == ECapOpenPhase.Power)
        {
            float distance = Mathf.Abs(PowerPos - tuning.powerTarget);
            if (distance <= tuning.powerWindow)
            {
                PowerPerfect = distance <= tuning.powerPerfectWindow;
                Phase = ECapOpenPhase.PowerDone;
                Delay = tuning.powerDoneDelay;
            }
            else Fail(PowerPos - tuning.powerTarget);
            return true;
        }

        float error = BeatTime - tuning.beatSec;
        if (Mathf.Abs(error) <= tuning.beatWindow)
        {
            Completed = true;
            Phase = ECapOpenPhase.Pop;
            Hit(true, error, .2167f);
            Fx = Fx.Value.WithPerfect(PowerPerfect && Mathf.Abs(error) <= tuning.perfectBeatWindow);
        }
        else Fail(error);
        return true;
    }

    /// <summary>판정 시간을 흘린다. 시작 전이거나 끝났으면 아무것도 하지 않는다.</summary>
    public void Tick(float dt)
    {
        if (!Started || Completed) return;
        dt = Mathf.Min(MaxStep, Mathf.Max(0f, dt));

        switch (Phase)
        {
            case ECapOpenPhase.Power:
                PowerPos += PowerDirection * tuning.powerSpeed * dt;
                if (PowerPos >= 1f) { PowerPos = 1f; PowerDirection = -1; }
                if (PowerPos <= 0f) { PowerPos = 0f; PowerDirection = 1; }
                break;

            case ECapOpenPhase.PowerDone:
            case ECapOpenPhase.Broken:
                Delay -= dt;
                if (Delay > 0f) break;
                if (Phase == ECapOpenPhase.Broken)
                {
                    RoundMisses = 0;
                    Phase = ECapOpenPhase.Power;
                    PowerPos = 0f;
                }
                else
                {
                    Phase = ECapOpenPhase.Rhythm;
                    BeatTime = 0f;
                }
                break;

            case ECapOpenPhase.Rhythm:
                BeatTime += dt;
                if (BeatTime > tuning.beatTimeout) Fail(BeatTime - tuning.beatSec);
                break;
        }
    }

    /// <summary>
    /// 연출 시간을 흘린다. 판정과 달리 완료 뒤에도 흐르고, 일시정지 중에는 부르지 않는다.
    /// 웹에서 판정(tick)과 연출(visual)이 나뉘어 있는 것과 같다.
    /// </summary>
    public void TickFx(float dt)
    {
        if (!Fx.HasValue) return;
        dt = Mathf.Min(MaxStep, Mathf.Max(0f, dt));
        Fx = Fx.Value.WithAge(Mathf.Min(tuning.fxHoldSec, Fx.Value.Age + dt));
    }

    void Fail(float offset)
    {
        Failures++;
        RoundMisses++;
        BeatTime = 0f;
        Hit(false, offset);

        if (RoundMisses >= tuning.missesToBreak)
        {
            Phase = ECapOpenPhase.Broken;
            Delay = tuning.brokenDelay;
        }
        else if (Phase == ECapOpenPhase.Power) PowerPos = 0f;
    }

    void Hit(bool ok, float offset, float window = 10f)
    {
        int serial = (Fx?.Serial ?? 0) + 1;
        Fx = new CapOpenFx(ok, ok && Mathf.Abs(offset) <= window * .3f + 1e-8f, offset, 0f, serial);
    }
}
