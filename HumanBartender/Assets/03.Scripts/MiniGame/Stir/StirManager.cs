using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 스터(Stir) 미니게임의 메인 매니저.
///
/// 잔 중심의 바 스푼이 시침처럼 한 방위를 가리키고, 플레이어는 그 방위의 시계 방향 이웃 키만
/// 눌러야 한다. 기준 위치에서 4번 눌러 한 바퀴를 돌면 성공 판정 하나, 오답이나 제한시간 초과면
/// 실패 판정 하나다. 판정을 stackCount번 쌓으면 자동으로 끝난다.
///
/// 인터페이스를 둘 구현한다. ICraftGimmick은 1부의 기믹 큐가 부르는 새 경로이고,
/// IMiniGameController는 2부 대화에서 컷씬과 함께 돌던 기존 경로다. 2부를 옮기는 건 나중이라
/// 그때까지 둘이 함께 살아 있어야 한다 — 조작과 판정 코드는 양쪽이 그대로 공유한다.
/// </summary>
public class StirManager : MonoBehaviour, IMiniGameController, ICraftGimmick,
                           ICraftGimmickProgress, ICraftGimmickManualEnd
{
    [SerializeField] bool isTest = false;
    [Tooltip("isTest일 때 표시할 칵테일 id. 스터 대상은 현재 dry_martini 1종이다.")]
    [SerializeField] string testCocktailId = "dry_martini";

    [Header("Data")]
    [SerializeField] CraftStationData data;
    [SerializeField] NewCocktailDataSO cocktailDataSO;
    [Tooltip("stir_target_stacks(판정 횟수)와 stir_circle_limit_sec(한 바퀴 제한시간)을 읽어온다. " +
             "스터 수치는 코드에 고정하지 않고 balance.json을 정본으로 쓴다.")]
    [SerializeField] NewBalanceDataSO balanceData;

    [Header("Fallback")]
    [Tooltip("balanceData가 비었거나 아직 로드되지 않았을 때만 쓰는 값. " +
             "실제 플레이에서는 balance.json의 stir_target_stacks가 이긴다.")]
    [SerializeField] int fallbackStackCount = 10;
    [Tooltip("같은 이유의 폴백. 실제 플레이에서는 balance.json의 stir_circle_limit_sec가 이긴다.")]
    [SerializeField] float fallbackStackSeconds = 2f;

    [Header("View")]
    [Tooltip("탑다운 잔(아레나) — 스푼 시침, 키 노드, 제한시간 링. 비워둬도 판정은 그대로 돌아간다.")]
    [SerializeField] StirGlassView glass;
    [Tooltip("아레나 바깥 표시 — 상단 스탯, 라운드 타이머 카드, 사선 게이지, 시작 오버레이.")]
    [SerializeField] StirHudView hud;
    [Tooltip("잔 속 얼음. 정답마다 휘돌림을 한 번 밀어준다. 비워둬도 판정은 그대로 돌아간다.")]
    [SerializeField] StirIceSwirl ice;

    [Header("UI")]
    [SerializeField] Canvas buttonCanvas;

    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    /// <summary>판정이 모두 끝난 순간 한 번 발생한다. 입력 핸들러가 이걸 받아 입력을 해제한다.</summary>
    public event Action Completed;

    // ── 런타임 상태 (명세 §3의 목록과 1:1로 맞춘다) ──────────────────────
    bool isStarted;
    int pos;                    // 현재 방위 — 스푼이 가리키는 곳
    int attemptStartPos;        // 이번 시도의 기준 방위
    int step;                   // 시도 내 정답 수 (0~3)
    readonly List<bool> results = new();
    float stackTime;            // 남은 시도 시간
    float elapsedTime;          // 첫 W 입력부터의 경과 시간
    bool isCompleted;

    int successCount;
    int failCount;

    // 콤보는 판정에 쓰이지 않는 표시 전용 값이다. 명세 §4의 반환값에도 없다 —
    // 프로토타입 상단바에 COMBO / BEST 칸이 있어 화면 구성을 맞추려고 들고 있을 뿐이다.
    int combo;
    int bestCombo;

    int stackCount;
    float stackSeconds;

    // 창 포커스가 나가거나 앱이 멈추면 두 타이머를 함께 세운다 (명세 §7).
    bool hasFocus = true;
    bool isAppPaused;

    /// <summary>
    /// 아직 처리하지 않은 입력. 폴링이 아니라 큐를 쓰는 이유는 한 프레임에 여러 키가 들어와도
    /// '이벤트가 들어온 순서대로' 판정해야 하기 때문이다 (명세 §7).
    /// </summary>
    readonly Queue<int> inputQueue = new();

    UniTaskCompletionSource tcs;

    /// <summary>스터 완성도 0~1. 성공 판정 수 ÷ 전체 판정 수.</summary>
    public float Completion => results.Count == 0 ? 0f : successCount / (float)results.Count;

    public bool IsCompleted => isCompleted;

    /// <summary>지금 눌러야 하는 방위. 현재 방위의 시계 방향 이웃이다.</summary>
    int NextDirection => StirDirections.Next(pos);

    /// <summary>시작 전·완료 후·포커스 이탈 중에는 시간이 흐르지 않는다.</summary>
    bool IsTimerRunning => isStarted && !isCompleted && hasFocus && !isAppPaused;

    void Start()
    {
        if (isTest) SetupTestCocktail();

        LoadBalance();
        ResetState();

        if (hud != null)
        {
            hud.SetRoundLimit(stackSeconds);
            hud.ResetGauge();
            hud.SetStartOverlay(true);

            // 경과 시간·판정 수·콤보는 공통 표시가 그린다. 큐가 돌릴 때만 겹치므로 그때만 감춘다.
            if (drivenByRunner) hud.HideStatsSharedWithCommonHud();
        }

        RefreshView();
        SetJudgeText("W / ↑ 로 시작");
    }

    /// <summary>
    /// 독립 테스트 씬용 배선. 실제 흐름에서는 CraftContext가 고른 칵테일을 넘겨주지만
    /// 테스트 씬에는 그 단계가 없어서 직접 꽂는다.
    /// </summary>
    void SetupTestCocktail()
    {
        if (data == null || cocktailDataSO == null)
        {
            Debug.LogWarning("[Stir] isTest인데 data 또는 cocktailDataSO가 비어 있습니다. 칵테일 정보 없이 진행합니다.");
            return;
        }

        if (cocktailDataSO.TryGet(testCocktailId, out NewCocktailData cocktail))
        {
            data.targetCocktailData = cocktail;
            data.targetCocktailId = testCocktailId;
        }
        else
        {
            Debug.LogWarning($"[Stir] '{testCocktailId}'를 NewCocktailDataSO에서 찾지 못했습니다. 이름 표시만 비게 됩니다.");
        }
    }

    /// <summary>스터 수치를 balance.json에서 읽는다. 값이 비어 있으면 폴백으로 떨어진다.</summary>
    void LoadBalance()
    {
        NewBalanceConfig config = balanceData != null && balanceData.balanceData != null
            ? balanceData.balanceData.Config
            : default;

        // 0은 '아직 로드되지 않음'과 구분되지 않는 값이라 폴백으로 취급한다.
        // 판정 횟수가 0이면 완성도 계산에서 0으로 나누게 되므로 최소 1을 보장한다.
        stackCount = Mathf.Max(1, config.StirTargetStacks > 0 ? config.StirTargetStacks : fallbackStackCount);
        stackSeconds = config.StirCircleLimitSec > 0f ? config.StirCircleLimitSec : fallbackStackSeconds;
    }

    void ResetState()
    {
        isStarted = false;
        isCompleted = false;
        pos = (int)EStirDirection.Up;
        attemptStartPos = (int)EStirDirection.Up;
        step = 0;
        stackTime = stackSeconds;
        elapsedTime = 0f;
        successCount = 0;
        failCount = 0;
        combo = 0;
        bestCombo = 0;

        results.Clear();
        inputQueue.Clear();
    }

    void Update()
    {
        if (isCompleted) return;

        // 포커스가 없는 동안 들어온 입력은 없지만, 큐에 남아 있던 것도 돌아올 때까지 잡아둔다.
        if (!hasFocus || isAppPaused) return;

        // 입력을 타이머보다 먼저 소비한다. 제한시간 도달과 마지막 정답 입력이 같은 프레임에
        // 겹치면 입력이 이긴다는 규칙(명세 §7)이 이 순서 하나로 지켜진다.
        while (inputQueue.Count > 0 && !isCompleted)
        {
            HandleDirection(inputQueue.Dequeue());
        }

        if (isCompleted) return;

        if (IsTimerRunning)
        {
            float dt = Time.deltaTime;

            elapsedTime += dt;
            stackTime -= dt;

            if (stackTime <= 0f) CommitAttempt(false, "TIME OUT");
        }

        RefreshView();
    }

    // ── 입력 ────────────────────────────────────────────────────────────

    /// <summary>StirInputHandler가 키 이벤트마다 부른다. 판정은 다음 Update에서 순서대로 처리한다.</summary>
    public void EnqueueDirection(int direction)
    {
        if (isCompleted) return;
        if (direction < 0 || direction >= StirDirections.Count) return;

        inputQueue.Enqueue(direction);
    }

    void HandleDirection(int direction)
    {
        // 시작 대기 중에는 W만 받는다. 나머지는 실패로 치지 않고 그냥 무시한다 (명세 §3).
        if (!isStarted)
        {
            if (direction == (int)EStirDirection.Up) StartStir();
            return;
        }

        if (direction == NextDirection) HandleCorrect(direction);
        else CommitAttempt(false, "WRONG");
    }

    /// <summary>
    /// 첫 W 입력. 여기서부터 두 타이머가 흐른다. 이 W는 4입력에 포함되지 않으므로 step은 0에서
    /// 시작하고, 이후 D → S → A → W를 눌러야 첫 성공이 된다.
    /// </summary>
    void StartStir()
    {
        isStarted = true;
        pos = (int)EStirDirection.Up;
        attemptStartPos = pos;
        step = 0;
        stackTime = stackSeconds;
        elapsedTime = 0f;

        glass?.ResetSpoon(pos);
        ice?.StopSwirl();
        hud?.SetStartOverlay(false);
        SetJudgeText("START");
        RefreshView();
    }

    void HandleCorrect(int direction)
    {
        pos = direction;
        step++;

        // 스푼은 정답마다 시계 방향으로 90°씩 굴러간다. 방위로 되돌리지 않고 계속 더하는 이유는
        // W(0°)로 돌아올 때 역방향으로 세 칸 되감기는 그림이 나오지 않게 하기 위해서다.
        glass?.AdvanceSpoon();

        // 얼음은 여기서 한 번 밀어주기만 한다. 감쇠는 얼음 쪽에서 매 프레임 일어나므로,
        // 시도가 끊기면 저절로 잦아든다 — 실패 피드백이 따로 필요 없다.
        ice?.AddImpulse();

        if (step >= StirDirections.StepsPerAttempt)
        {
            CommitAttempt(true, "GOOD");
            return;
        }

        SetJudgeText($"다음 {StirDirections.KeyLabel(NextDirection)} / {StirDirections.ArrowLabel(NextDirection)}");
        RefreshView();
    }

    /// <summary>
    /// 판정 하나를 확정하고 곧바로 다음 시도를 시작한다. 시도 사이에 쿨다운이나 입력 잠금이 없어서
    /// 직후 입력도 새 시도의 첫 입력으로 즉시 판정된다 (명세 §3).
    /// </summary>
    void CommitAttempt(bool success, string label)
    {
        results.Add(success);

        if (success)
        {
            successCount++;
            combo++;
            bestCombo = Mathf.Max(bestCombo, combo);
        }
        else
        {
            failCount++;
            combo = 0;
        }

        hud?.SetGaugeResult(results.Count - 1, success);

        if (results.Count >= stackCount)
        {
            SetJudgeText(label);
            Complete();
            return;
        }

        // 실패 시점의 '현재 방위'가 다음 시도의 기준점이다. 오답 키 쪽으로는 옮기지 않으므로
        // W에서 출발해 D·S까지 맞힌 뒤 틀렸다면 기준점은 S가 된다 (명세 §3의 예시).
        attemptStartPos = pos;
        step = 0;
        stackTime = stackSeconds;

        SetJudgeText($"{label} — {StirDirections.KeyLabel(attemptStartPos)} / " +
                     $"{StirDirections.ArrowLabel(attemptStartPos)} 위치에서 다시 한 바퀴");

        RefreshView();
    }

    void Complete()
    {
        isCompleted = true;
        stackTime = 0f;
        inputQueue.Clear();

        glass?.SetIdle();
        RefreshView();

        Logger.Log($"[Stir] 완성도 {Completion:P0} / 성공 {successCount} / 실패 {failCount} / " +
                   $"전체 {results.Count} / 경과 {elapsedTime:0.00}s");

        // 입력 해제. 완료 이후 추가 입력은 처리하지 않는다 (명세 §7).
        Completed?.Invoke();

        CompleteMade();

        // 실제 흐름에서는 기믹 큐(GimmickRunner)가 결과를 받아 다음 스텝으로 넘긴다.
        // 독립 테스트 씬에는 그 흐름이 없어서 끝났다는 신호가 없으면 멈춘 것처럼 보인다.
        if (isTest) OnNextButton();
    }

    // ── 표시 ────────────────────────────────────────────────────────────

    void RefreshView()
    {
        float ratio = stackSeconds <= 0f ? 0f : Mathf.Clamp01(stackTime / stackSeconds);

        if (glass != null)
        {
            if (isCompleted) glass.SetIdle();
            else if (!isStarted) glass.SetBadges(-1, (int)EStirDirection.Up);
            else glass.SetBadges(pos, NextDirection);

            glass.SetTimer(ratio);
        }

        if (hud != null)
        {
            // 큐가 돌릴 때는 이 셋을 공통 표시가 맡는다. 기존 경로에서는 그대로 스터가 그린다.
            if (!drivenByRunner)
            {
                hud.SetElapsed(elapsedTime);
                hud.SetCircle(results.Count, stackCount);
                hud.SetCombo(combo, bestCombo);
            }

            // 잔 주변 2초 게이지는 스터 고유 표시라 언제나 스터가 그린다.
            // 시작 전에는 프로토타입처럼 제한시간이 가득 찬 상태로 보여준다(READY). 끝나면 0으로 비운다.
            hud.SetRoundTime(isCompleted ? 0f : stackTime, isCompleted ? 0f : ratio);
        }
    }

    void SetJudgeText(string message) => hud?.SetJudge(message);

    // ── 일시정지 ────────────────────────────────────────────────────────

    void OnApplicationFocus(bool focus) => hasFocus = focus;

    void OnApplicationPause(bool pause) => isAppPaused = pause;

    // ── IMiniGameController ─────────────────────────────────────────────

    public void InitGame(UniTaskCompletionSource tcs)
    {
        this.tcs = tcs;

        if (data != null) data.craftingResult.actionFailCount = 0;
    }

    public void CompleteMade()
    {
        if (data != null)
        {
            data.craftingResult.isResult = true;

            // 실패 비율(failCount / stackCount)이 그대로 판정에 쓰인다 — 성공 9회면 10%라 Perfect,
            // 7회면 30%라 Normal이 되어 명세 §4의 완성도 개념과 일치한다.
            data.craftingResult.actionFailCount = failCount;
            data.craftingResult.limitFailCount = stackCount;
        }

        tcs?.TrySetResult();
    }

    public void OnNextButton()
    {
        if (buttonCanvas != null) buttonCanvas.gameObject.SetActive(true);
    }

    public void Serve()
    {
        craftServe?.Raise(new Void());
    }

    public void Retry()
    {
        craftRetry?.Raise(new Void());
    }

    // ── ICraftGimmick ───────────────────────────────────────────────────

    /// <summary>기믹 큐가 이 기믹을 돌리고 있는지. 공통 표시와 겹치는 부분을 끄는 데 쓴다.</summary>
    bool drivenByRunner;

    /// <summary>기믹 큐가 이 기믹을 돌리는 동안 완료를 기다리는 신호.</summary>
    UniTaskCompletionSource runnerCompletion;

    /// <summary>큐에서 받은 전체 제조 시계. 결과에 확정 시점을 적는 데만 쓰고 건드리지 않는다.</summary>
    CraftTimer craftTimer;

    /// <summary>
    /// 이번 제조 시도의 사정(고른 잔·도구·재료). 실행기가 PlayAsync로 넘겨준다.
    /// 아직 그리는 데 쓰지는 않는다 — 리소스가 붙을 때 여기서 가져가면 된다.
    /// </summary>
    CraftContext craftContext;

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
                                                 CraftTimer timer, CancellationToken token)
    {
        // 스터는 재료를 다루지 않지만 잔은 다룬다 — 믹싱글라스와 옮겨 담을 잔이 context에 있다.
        craftContext = context;

        // 아직 그리는 데 쓰지 않으므로, 값이 제대로 도착했는지는 이 줄로만 확인한다.
        Debug.Log($"[Stir] 문맥 — 잔 {craftContext.GlassId ?? "없음"} / " +
                  $"도구 {craftContext.ToolId ?? "없음"} / 재료 {string.Join(", ", craftContext.IngredientIds)}");

        drivenByRunner = true;
        craftTimer = timer;
        runnerCompletion = new UniTaskCompletionSource();

        // 스터는 재료를 쓰지 않으므로 step에서 가져올 표시 정보가 없다. 잔 속 재료가 무엇이든
        // 젓는 조작은 같아서, 화면에는 칵테일 이름만 남는다.
        Completed += OnCompletedForRunner;

        try
        {
            // 제조가 중단되면(창을 닫는 등) 기다림을 풀어 준다. 그러지 않으면 끝나지 않는 대기가 남는다.
            using (token.Register(() => runnerCompletion.TrySetCanceled()))
            {
                await runnerCompletion.Task;
            }
        }
        finally
        {
            Completed -= OnCompletedForRunner;
        }

        // 스터는 목표 스택을 채우면 저절로 끝난다. 중간에 그만두는 다음 버튼은 아직 없다.
        return GimmickResult.Mix(ECraftGimmick.Stir, successCount, failCount, stackCount,
                                 ECraftEndType.AutoTarget,
                                 craftTimer != null ? craftTimer.ElapsedSec : elapsedTime);
    }

    void OnCompletedForRunner()
    {
        runnerCompletion?.TrySetResult();
    }

    /// <summary>
    /// 한 바퀴를 도는 동안. 스터가 자기 시도 게이지를 굴리는 조건과 같게 둔다 —
    /// 시작 대기 중이거나 창 포커스를 잃은 동안은 판정이 멈추므로 제조시간도 멈춰야 한다.
    /// </summary>
    public bool IsManualInputActive => IsTimerRunning;

    // ── ICraftGimmickProgress ───────────────────────────────────────────

    /// <summary>쌓인 스택과 목표. 스터는 수량이 아니라 판정 횟수를 센다.</summary>
    public string ProgressText => $"{results.Count} / {stackCount}";

    /// <summary>스터에는 도달을 알릴 목표 수량이 없다. 목표 스택을 채우면 그대로 끝난다.</summary>
    public bool ShowOkMark => false;

    // ── ICraftGimmickManualEnd ──────────────────────────────────────────

    public bool CanEndNow => isStarted && !isCompleted;

    /// <summary>
    /// 지금까지 쌓은 스택으로 끝낸다. 남은 스택을 성공으로 채워 주지 않으므로,
    /// 일찍 끝낼수록 완성도가 그만큼 낮게 남는다.
    /// </summary>
    public void EndNow()
    {
        if (!CanEndNow) return;

        SetJudgeText("STOP");
        Complete();
    }
}
