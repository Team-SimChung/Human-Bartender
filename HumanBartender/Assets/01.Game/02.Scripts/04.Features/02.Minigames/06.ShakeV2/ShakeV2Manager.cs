using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>재료 한 층. 표시 이름, 색, 양.</summary>
[Serializable]
public struct ShakeV2Layer
{
    public string name;
    public Color color;
    [Min(0f)] public float amount;

    public ShakeV2Layer(string name, Color color, float amount)
    {
        this.name = name; this.color = color; this.amount = amount;
    }
}

/// <summary>
/// 쉐이킹 V2 매니저. 웹 V2(src/dev/shaking)를 옮긴 새 쉐이크 기믹이다.
///
/// 규칙은 ShakeV2Rules, 그리기는 Monitor/Control/Shaker 세 화면, 소리는 ShakeV2Audio가 맡는다. 이 클래스는 셋을 잇고
/// 게임 시간을 넣어 준다. 순간 정지(히트스톱)·일시정지·설정창 동안에는 규칙에 시간을 넣지 않는다 — 그래서 지연 실행과
/// 연출이 모두 같이 멈춘다. Time.timeScale은 건드리지 않는다(GamePause가 복귀값으로 저장한다).
///
/// 제조 시계(CraftTimer)는 읽기만 한다. 결합 대기·결합 전환·왕복·MISS 대기에서만 IsTimeCounting이 참이고,
/// 실행기가 그 동안만 시계를 더한다.
/// </summary>
public sealed class ShakeV2Manager : MonoBehaviour, ICraftGimmick, ICraftGimmickTiming, ICraftGimmickHudProvider
{
    [Serializable]
    public sealed class Feel
    {
        [Header("순간 정지")]
        [Tooltip("금색 중앙 결합 순간의 정지(초).")]
        [Min(0f)] public float sealHitstopSec = .08f;
        [Tooltip("빠른 입력(회차 시간 55% 전)의 정지(초).")]
        [Min(0f)] public float fastHitstopSec = .045f;

        [Header("화면 흔들림 세기(게임 좌표)")]
        [Tooltip("금색 중앙 결합 / 그 밖의 결합.")]
        public float sealCenterCam = 14f;
        public float sealCam = 9f;
        [Tooltip("빠른 입력 / 보통 입력. 정입력은 이 값과 지금 세기 중 큰 쪽.")]
        public float fastStrokeCam = 9f;
        public float strokeCam = 6f;
        [Tooltip("셰이커가 출발할 때의 최소 세기.")]
        public float throwCam = 4f;
        [Tooltip("내구도를 잃었을 때.")]
        public float durabilityCam = 6f;
        [Tooltip("틴이 빠졌을 때.")]
        public float brokenCam = 20f;
        [Tooltip("분리 중 위 틴이 빠지는 순간.")]
        public float releaseCam = 3f;

        [Header("글자")]
        public string missText = "MISS";
        public string earlyText = "너무 빨라..";
    }

    [Header("Dev")]
    [Tooltip("제조 흐름 없이 혼자 시작·재시작한다(Shaking_v2 Dev 씬). 기믹 큐가 PlayAsync를 부르면 무시한다.")]
    [SerializeField] bool standalone;
    [Tooltip("단독 실행에서 결과를 넘긴 뒤 다시 시작하기까지(초).")]
    [SerializeField, Min(0f)] float standaloneRestartSec = 2f;

    [Header("Rules")]
    [Tooltip("판정·움직임 수치. 기본값은 웹 V2와 같다.")]
    [SerializeField] ShakeV2Tuning tuning = new ShakeV2Tuning();
    [Tooltip("셰이커 연출 시간표. 기본값은 웹 V2와 같다.")]
    [SerializeField] ShakeV2MotionTuning motion = new ShakeV2MotionTuning();
    [SerializeField] Feel feel = new Feel();

    [Header("Liquid")]
    [Tooltip("시험용 재료 층(첫 항목이 바닥, 색 + 양). 본편에서는 SetLayers로 앞 제조 결과가 들어온다. 기본은 문서의 코스모폴리탄 3층. 비우면 #C8E6F0 단일 층.")]
    [SerializeField] ShakeV2Layer[] layers =
    {
        new ShakeV2Layer("크랜베리주스", new Color32(0xC2, 0x3A, 0x66, 0xFF), 90f),
        new ShakeV2Layer("트리플섹", new Color32(0xFF, 0xEB, 0xD8, 0xFF), 45f),
        new ShakeV2Layer("보드카", new Color32(0xE7, 0xEF, 0xF5, 0xFF), 120f),
    };
    [Tooltip("다 섞인 뒤의 색을 따로 정한다(웹 연습 레시피의 mixedColor). 끄면 본편 규칙: 재료 양의 가중 평균, 층 불투명도 .5.")]
    [SerializeField] bool useMixedColor;
    [SerializeField] Color mixedColor = new Color32(0xFF, 0x81, 0x96, 0xFF);
    [Tooltip("본편에서 앞 단계에 부은 재료의 색(shelf_items.color)을 읽는다. 따르기와 같은 데이터(NewShelfItemDataSO).")]
    [SerializeField] NewShelfItemDataSO shelfData;
    [Tooltip("색이 없는 재료의 색. 웹 shake-v2.js layers()의 기본값 200,230,240.")]
    [SerializeField] Color unknownIngredientColor = new Color32(200, 230, 240, 255);

    [Header("Presentation")]
    [Tooltip("왼쪽 모니터(04.Gameplay/Monitor).")]
    [SerializeField] ShakeV2MonitorView monitor;
    [Tooltip("HUD(05.UI/Canvas_ShakeV2).")]
    [SerializeField] ShakeV2ControlView control;
    [Tooltip("셰이커(04.Gameplay/Shaker).")]
    [SerializeField] ShakeV2ShakerView shaker;
    [Tooltip("소리(04.Gameplay/ShakingV2Sound).")]
    [SerializeField] ShakeV2Audio sound;
    [Tooltip("시간 표시. 본편에서는 공통 HUD가 같은 제조 시계를 넘겨 준다(ICraftGimmickHudProvider).")]
    [SerializeField] CraftGimmickHud timeHud;

    Camera viewCamera;

    /// <summary>
    /// 도트 배율을 정할 때 읽는 카메라. 본편은 기믹 무대 카메라, Dev 씬은 메인 카메라다(CraftGimmickStage.Find).
    /// 미리보기·테스트는 직접 넘긴다.
    /// </summary>
    internal Camera ViewCamera
    {
        get => viewCamera != null ? viewCamera : viewCamera = CraftGimmickStage.Find(this);
        set { viewCamera = value; lastPixelsPerWorld = -1f; }
    }

    ShakeV2Rules rules;
    readonly ShakeV2LunaMotion luna = new ShakeV2LunaMotion();
    UniTaskCompletionSource completion;
    CraftTimer craftTimer, standaloneTimer;
    bool drivenByRunner, started, paused, resultSent;
    float hitstopLeft, lastMissAt = float.NegativeInfinity, restartAt = -1f, lastPixelsPerWorld = -1f;
    bool releaseCamDone;

    ShakeV2Rules Rules => rules ??= CreateRules();

    /// <summary>지금 게임의 규칙. 화면·테스트가 읽기만 한다.</summary>
    public ShakeV2Rules CurrentRules => Rules;
    public CraftGimmickHud GimmickHud => timeHud;

    ShakeV2Rules CreateRules()
    {
        var created = new ShakeV2Rules(tuning);
        created.Raised += OnRuleEvent;
        return created;
    }

    // ── 시작 ────────────────────────────────────────────────────────────

    void Start()
    {
        if (drivenByRunner || !standalone) return;
        standaloneTimer = new CraftTimer();
        standaloneTimer.Start();
        if (timeHud != null) timeHud.BeginCraft(standaloneTimer, 0f, false);
        Begin();
    }

    void Begin()
    {
        if (rules != null) rules.Raised -= OnRuleEvent;
        rules = CreateRules();
        luna.Reset();
        hitstopLeft = 0f;
        lastMissAt = float.NegativeInfinity;
        restartAt = -1f;
        resultSent = false;
        releaseCamDone = false;
        monitor?.ResetState();
        control?.ResetState();
        shaker?.ResetState();
        shaker?.SetLiquid(CurrentLayers, useMixedColor ? mixedColor : (Color?)null);
        sound?.StopAll();
        started = true;
        Render(0f);
    }

    // ── 매 프레임 ───────────────────────────────────────────────────────

    void Update() => Step(Time.deltaTime);

    /// <summary>테스트·미리보기에서 프레임을 직접 넘길 때 쓴다. Update와 같은 경로다.</summary>
    internal void Restart() => Begin();

    /// <summary>남은 순간 정지(초).</summary>
    internal float HitstopLeft => hitstopLeft;

    internal void Step(float frameSec)
    {
        bool nowPaused = UIDisplayOptions.IsOptionOpen || GamePause.IsPaused;
        if (nowPaused && !paused) sound?.StopAll();
        paused = nowPaused;
        if (!started) return;

        float dt = paused ? 0f : frameSec;
        if (dt > 0f)
        {
            bool counting = IsTimeCounting;
            if (hitstopLeft > 0f) hitstopLeft = Mathf.Max(0f, hitstopLeft - dt);
            else Rules.Tick(dt);
            if (counting && standaloneTimer != null) standaloneTimer.Tick(dt);
        }

        if (!releaseCamDone && Rules.Phase == EShakeV2Phase.Unseal && Rules.UnsealAge >= motion.releaseSec)
        {
            releaseCamDone = true;
            shaker?.SetCam(feel.releaseCam);
        }

        if (standalone && !drivenByRunner && restartAt >= 0f && Time.time >= restartAt) Begin();
        Render(dt);
    }

    void Render(float dt)
    {
        // 화면 크기·카메라가 바뀌면 도트 배율을 다시 정한다(웹 Surface.fit).
        float pixelsPerWorld = ShakeV2ShakerView.PixelsPerWorldUnit(ViewCamera);
        if (!Mathf.Approximately(pixelsPerWorld, lastPixelsPerWorld))
        {
            lastPixelsPerWorld = pixelsPerWorld;
            shaker?.UpdatePixelScale(pixelsPerWorld);
        }
        shaker?.Render(Rules, motion);
        control?.Render(Rules);
        monitor?.Render(Rules, luna, lastMissAt, dt);
    }

    // ── 입력 ────────────────────────────────────────────────────────────

    /// <summary>ShakeV2InputHandler가 누른 순간 부른다.</summary>
    public void Press(EShakeV2Input input)
    {
        if (!started || paused) return;
        Rules.Press(input);
    }

    // ── 규칙 이벤트 → 화면·소리 ─────────────────────────────────────────

    void OnRuleEvent(ShakeV2Event e)
    {
        var r = Rules;
        switch (e.Kind)
        {
            case EShakeV2EventKind.Sealed:
                sound?.PlayHit();
                shaker?.SetCam(e.Strong ? feel.sealCenterCam : feel.sealCam);
                shaker?.AddSealRing(r, motion, e.Strong);
                if (e.Strong) hitstopLeft = feel.sealHitstopSec;
                break;

            case EShakeV2EventKind.StrokeAccepted:
                sound?.PlayHit();
                luna.Begin(e.Stroke, e.Time);
                shaker?.RaiseCam(feel.throwCam);
                shaker?.RaiseCam(e.Strong ? feel.fastStrokeCam : feel.strokeCam);
                shaker?.AddStrokeRing(r, e.Stroke, e.Strong);
                control?.ShowShakeWord(r, ShakeV2Presentation.ShakerPose(r, motion, false));
                if (e.Strong) hitstopLeft = feel.fastHitstopSec;
                break;

            case EShakeV2EventKind.RoundMissed:
                control?.ShowAlert(r, feel.missText);
                if (e.Reason == EShakeV2MissReason.WrongKey) MarkDurabilityLoss(e.Time);
                break;

            case EShakeV2EventKind.EarlyInput:
                control?.ShowAlert(r, feel.earlyText);
                MarkDurabilityLoss(e.Time);
                break;

            case EShakeV2EventKind.Broken:
                sound?.PlayHit();
                shaker?.SetCam(feel.brokenCam);
                shaker?.BeginBreak(r, motion, LiquidColor());
                control?.ShowPop(r);
                break;

            case EShakeV2EventKind.UnsealStarted:
                sound?.PlayComplete();
                shaker?.SetCam(0f);
                break;

            case EShakeV2EventKind.Finished:
                Finish();
                break;
        }
    }

    void MarkDurabilityLoss(float time)
    {
        lastMissAt = time;
        shaker?.SetCam(feel.durabilityCam);
    }

    /// <summary>쏟아지는 액체 색. 목표색이 있으면 그것, 없으면 재료 양의 가중 평균(웹 avgCol).</summary>
    public Color LiquidColor() => useMixedColor ? mixedColor : ShakeV2ColumnWater.WeightedAverage(CurrentLayers);

    // ── 재료 층 ─────────────────────────────────────────────────────────

    IReadOnlyList<ShakeV2Layer> layerOverride;

    /// <summary>이번 판의 재료 층(첫 항목이 바닥). 넣은 값이 없으면 Inspector의 시험용 층.</summary>
    public IReadOnlyList<ShakeV2Layer> CurrentLayers => layerOverride ?? layers ?? Array.Empty<ShakeV2Layer>();

    /// <summary>
    /// 실제 부은 재료로 층을 정하는 입구. 본편에서는 PlayAsync가 앞 단계 결과로 부른다. 다음 시작부터 반영되고,
    /// 이미 쉐이킹 중이면 색만 바뀐다. null을 넣으면 Inspector의 시험용 층으로 돌아간다(Dev 단독 실행).
    /// </summary>
    public void SetLayers(IReadOnlyList<ShakeV2Layer> value)
    {
        layerOverride = value != null ? new List<ShakeV2Layer>(value) : null;
        shaker?.SetLiquid(CurrentLayers, useMixedColor ? mixedColor : (Color?)null);
    }

    /// <summary>
    /// 앞 단계 결과에서 층을 만든다. 웹 shake-v2.js layers()와 같다: 재료가 있고 값이 0보다 큰 결과를 순서대로(첫 항목이 바닥),
    /// 색은 그 재료의 shelf_items.color, 없으면 unknown, 양은 결과 값 그대로(단위를 바꾸지 않는다 — 웹도 ml·oz·개수를 섞어 그대로 쓴다).
    /// </summary>
    public static List<ShakeV2Layer> LayersFrom(IReadOnlyList<GimmickResult> results, NewShelfItemDataSO shelf, Color unknown)
    {
        var list = new List<ShakeV2Layer>();
        if (results == null) return list;
        foreach (var result in results)
        {
            if (result == null || string.IsNullOrEmpty(result.IngredientId) || !(result.ActualValue > 0f)) continue;
            Color color = unknown;
            if (shelf != null && shelf.TryGet(result.IngredientId, out var item) && item.TryGetLiquidColor(out Color32 liquid)) color = liquid;
            color.a = 1f;
            list.Add(new ShakeV2Layer(result.IngredientId, color, result.ActualValue));
        }
        return list;
    }

    // ── 종료 ────────────────────────────────────────────────────────────

    void Finish()
    {
        if (resultSent) return;
        resultSent = true;
        sound?.StopAll();
        var r = Rules;
        if (!drivenByRunner)
        {
            Debug.Log($"[ShakeV2] 결과 {r.Score}점 / 성공 {r.Successes}/{tuning.rounds} / 실패 {r.Failures} " +
                      $"(오입력 {r.WrongInputs}, 조기 {r.EarlyInputs}, 시간 초과 {r.Timeouts}) / 시간 {r.ClockSec:0.00}s" +
                      (r.WasBroken ? " / 파손" : ""));
            if (standalone) restartAt = Time.time + standaloneRestartSec;
        }
        completion?.TrySetResult();
    }

    void OnDisable() => sound?.StopAll();

    void OnDestroy()
    {
        if (rules != null) rules.Raised -= OnRuleEvent;
        completion?.TrySetCanceled();
    }

    // ── ICraftGimmick ───────────────────────────────────────────────────

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context, CraftTimer timer, CancellationToken token)
    {
        // Start()보다 먼저 불린다. 결과를 기다리는 신호는 Finish() 한 곳에서만 나온다.
        drivenByRunner = true;
        craftTimer = timer;
        completion = new UniTaskCompletionSource();
        // 이 잔에 앞서 부은 재료가 단면의 층이 된다. 하나도 없으면 물 부품이 #C8E6F0 단일 층으로 그린다(웹 '음료').
        layerOverride = LayersFrom(context.Results, shelfData, unknownIngredientColor);
        Begin();

        // 제조가 취소되면 분리·파손 대기도 함께 풀린다. 이전 게임의 결과가 새 잔에 들어가지 않는다.
        using (token.Register(() => completion?.TrySetCanceled()))
        {
            await completion.Task;
        }

        return Rules.ToResult(craftTimer != null ? craftTimer.ElapsedSec : Rules.ClockSec);
    }

    /// <summary>게임이 진행 중인 동안 참이다. 실제로 시간을 셀지는 IsTimeCounting이 정한다.</summary>
    public bool IsManualInputActive => started && !Rules.Finished;

    /// <summary>
    /// 결합 대기·결합 전환·왕복·MISS 대기에서만 참. 순간 정지·종료 연출·파손 연출·일시정지·설정창에서는 거짓이다.
    /// 마지막 정입력(마지막 회차가 MISS면 그 대기 뒤) 또는 파손 순간에 고정된다.
    /// </summary>
    public bool IsTimeCounting => started && !paused && hitstopLeft <= 0f && Rules.IsTimeCounting;

#if UNITY_EDITOR
    [ContextMenu("다시 시작")]
    void RestartFromMenu()
    {
        if (Application.isPlaying) Begin();
    }
#endif
}
