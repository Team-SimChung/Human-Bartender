using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 병따기(Cap) 미니게임의 메인 매니저. 웹 본편 병따기(gimmick-mechanics.js OPEN, open-view.js)와 같은 규칙이다.
///
/// 두 단계를 차례로 맞춘다. 먼저 위아래로 오가는 힘 게이지를 초록 구간에서 멈추고, 이어서 바깥 원이
/// 민트색 링에 겹치는 순간 누른다. 실패가 세 번 쌓이면 병이 깨지고 새 병으로 힘 조절부터 다시 한다.
/// 열린 뒤에는 탄산이 다 올라올 때까지(2.05초) 기다렸다가 플레이어가 "다음"을 눌러 넘어간다.
///
/// 판정은 CapOpenRules, 그리기는 CapOpenView, 소리는 CapOpenAudio가 맡는다. 이 클래스는 셋을 잇고
/// 기믹 큐(ICraftGimmick)와 예전 미니게임 경로(IMiniGameController)에 결과를 돌려준다.
///
/// 점수만 웹과 다르다. 웹은 열기만 하면 100점이지만, Unity는 실패 수를 결과에 담아 판정기가
/// open_penalty_per_failure만큼 깎는다(CraftJudge).
/// </summary>
public class CapManager : MonoBehaviour, IMiniGameController, ICraftGimmick, ICraftGimmickProgress,
    ICraftGimmickManualEnd, ICraftGimmickOwnHud
{
    [SerializeField] private bool isTest = false;

    [Header("Data")]
    [SerializeField] private CraftStationData data;
    [SerializeField] private NewCocktailDataSO cocktailDataSO;
    [Tooltip("링 크기(open_start_radius_px, open_target_radius_px)를 읽는다. 비었거나 아직 로드 전이면 웹 기본값을 쓴다.")]
    [SerializeField] private NewBalanceDataSO balanceData;

    [Header("Rules")]
    [Tooltip("판정 수치. 기본값은 웹 본편과 같다.")]
    [SerializeField] private CapOpenTuning tuning = new CapOpenTuning();
    [Tooltip("예전 미니게임 경로에서만 쓰는 제한 시간(초). 기믹 큐가 돌릴 때는 시간 제한이 없다.")]
    [SerializeField] private float timeLimit = 25f;
    [Tooltip("isTest일 때 쓸 허용 실패 횟수. 실제 모드에서는 CraftStationData.targetCraft_tolerance에서 환산한다.")]
    [SerializeField] private int testLimitFailCount = 5;

    [Header("Presentation")]
    [SerializeField] private CapOpenView view;
    [SerializeField] private CapOpenAudio openAudio;

    [Header("Legacy UI")]
    [Tooltip("예전 미니게임 경로의 제공하기·다시하기 버튼 묶음.")]
    [SerializeField] private Canvas buttonCanvas;

    [Header("Craft Event")]
    [SerializeField] private VoidEvent craftServe;
    [SerializeField] private VoidEvent craftRetry;

    const float DefaultStartRadius = 165f, DefaultTargetRadius = 44f;

    CapOpenRules rules;
    int heardFx;
    bool finished, timedOut, paused;
    float elapsed;
    int limitFailCount;
    string ingredientId = "beer";
    string lessonText;

    UniTaskCompletionSource tcs;

    /// <summary>지금 단계. 튜토리얼이 안내 문구를 고르는 데 쓴다.</summary>
    public ECapOpenPhase OpenPhase => Rules.Phase;
    /// <summary>뚜껑이 열렸는지.</summary>
    public bool Opened => Rules.Completed;
    /// <summary>누르기 버튼. 튜토리얼이 강조할 자리다.</summary>
    public RectTransform PressButton => view != null ? view.PressButton : null;
    /// <summary>"다음" 버튼. 튜토리얼이 강조할 자리다.</summary>
    public Transform NextButton => view != null && view.NextButton != null ? view.NextButton.transform : null;

    /// <summary>이 기믹이 끝났을 때. 튜토리얼이 병따기 수업을 마쳤다고 기록하는 데 쓴다.</summary>
    public event System.Action Finished;

    CapOpenRules Rules => rules ??= new CapOpenRules(tuning);

    void Awake()
    {
        if (view != null) view.NextClicked += OnNextClicked;
    }

    void Start()
    {
        if (isTest)
        {
            if (cocktailDataSO.TryGet(data.targetCocktailId, out NewCocktailData cocktail))
                data.targetCocktailData = cocktail;
            else
                Debug.LogWarning($"[Cap] 테스트 칵테일 '{data.targetCocktailId}'를 찾지 못했습니다.");

            data.targetCraft_tolerance = testLimitFailCount;
        }

        // 기존 경로에서는 이벤트가 정한 허용치에서 환산한다. 0이면 판정에서 0으로 나누게 되므로 최소 1을 보장한다.
        int tolerance = data != null ? data.targetCraft_tolerance : 0;
        limitFailCount = Mathf.Max(1, tolerance > 0 ? tolerance : testLimitFailCount);
    }

    void Update()
    {
        // 일시정지하면 판정과 연출을 모두 멈추고 소리는 끊는다. 웹 core.tick과 sync가 하는 일과 같다.
        bool nowPaused = UIDisplayOptions.IsOptionOpen;
        if (nowPaused && !paused && openAudio != null) openAudio.StopAll();
        paused = nowPaused;

        if (!paused && !finished)
        {
            float dt = Time.deltaTime;
            Rules.Tick(dt);
            Rules.TickFx(dt);
            elapsed += dt;

            if (!drivenByRunner && !Rules.Completed && elapsed >= timeLimit) TimeOut();
        }

        PlayNewFxSound();
        if (view != null)
        {
            view.SetLesson(lessonText, OpenPhase is ECapOpenPhase.Ready or ECapOpenPhase.Power or ECapOpenPhase.PowerDone);
            view.Render(Rules, ingredientId, StartRadius, TargetRadius, paused);
        }
    }

    float StartRadius => Config is { OpenStartRadiusPx: > 0f } config ? config.OpenStartRadiusPx : DefaultStartRadius;
    float TargetRadius => Config is { OpenTargetRadiusPx: > 0f } config ? config.OpenTargetRadiusPx : DefaultTargetRadius;
    NewBalanceConfig? Config => balanceData != null && balanceData.balanceData != null ? balanceData.balanceData.Config : null;

    /// <summary>새 판정이 나온 첫 순간에만 소리를 낸다. 웹: serial이 바뀌고 age가 0.12초 미만일 때.</summary>
    void PlayNewFxSound()
    {
        if (paused || !Rules.Fx.HasValue) return;
        CapOpenFx fx = Rules.Fx.Value;
        if (fx.Serial == heardFx) return;
        heardFx = fx.Serial;
        if (fx.Age < .12f && openAudio != null) openAudio.Play(fx.Success, fx.Perfect);
    }

    // ── 입력 ────────────────────────────────────────────────────────────

    /// <summary>CapInputHandler가 스페이스/클릭 어느 쪽이든 누른 순간에 부른다.</summary>
    public void OnPress()
    {
        if (paused || finished) return;
        Rules.Press();
    }

    void OnNextClicked()
    {
        if (CanEndNow) EndNow();
    }

    /// <summary>
    /// 튜토리얼 수업 문구. 비어 있으면 안내를 끈다. 웹은 1일차 tutorial_beer일 때만 이 안내를 띄운다.
    /// </summary>
    public void SetLesson(string text) => lessonText = text;

    // ── 종료 ────────────────────────────────────────────────────────────

    void TimeOut()
    {
        timedOut = true;
        Finish();
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        if (openAudio != null) openAudio.StopAll();

        // 자체 제한시간을 쓰는 기존 경로에서만 표시용으로 잘라 준다.
        if (!drivenByRunner) elapsed = Mathf.Min(elapsed, timeLimit);

        CompleteMade();
        Finished?.Invoke();
        runnerCompletion?.TrySetResult();

        // 독립 테스트 씬에는 결과를 받아 줄 흐름이 없어서, 끝났다는 신호가 없으면 그냥 멈춘 것처럼 보인다.
        if (isTest)
        {
            string how = timedOut ? "시간 초과" : $"실패 {Rules.Failures}회 뒤 개봉";
            Logger.Log($"[Cap] 판정 종료. {how} / 허용 {limitFailCount}");
            OnNextButton();
        }
    }

    void OnDisable()
    {
        if (openAudio != null) openAudio.StopAll();
    }

    void OnDestroy()
    {
        if (view != null) view.NextClicked -= OnNextClicked;
    }

    // ── IMiniGameController ─────────────────────────────────────────────

    public void InitGame(UniTaskCompletionSource tcs)
    {
        this.tcs = tcs;
        data.craftingResult.actionFailCount = 0;
    }

    public void CompleteMade()
    {
        // 기믹 큐가 돌릴 때는 결과를 GimmickResult로 돌려주므로 이 저장소를 쓰지 않는다.
        if (data != null)
        {
            data.craftingResult.isResult = true;

            // 시간 초과는 무조건 실패다. 허용치를 넘기는 값을 넣어 백분율이 100%를 넘게 만든다.
            data.craftingResult.actionFailCount = timedOut ? limitFailCount + 1 : Rules.Failures;
            data.craftingResult.limitFailCount = limitFailCount;
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

    /// <summary>기믹 큐가 이 기믹을 돌리고 있는지. 자체 제한시간을 끄는 데 쓴다.</summary>
    bool drivenByRunner;

    UniTaskCompletionSource runnerCompletion;
    CraftTimer craftTimer;

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
                                                 CraftTimer timer, CancellationToken token)
    {
        // 병 그림은 재료가 정한다. 탄산 연출도 맥주일 때만 나온다(웹 item_{재료}).
        ingredientId = string.IsNullOrEmpty(step.IngredientId) ? ingredientId : step.IngredientId;
        if (ingredientId != "beer")
            Debug.LogWarning($"[Cap] '{ingredientId}' 전용 병 그림이 없어 맥주병으로 그립니다.");

        drivenByRunner = true;
        craftTimer = timer;
        runnerCompletion = new UniTaskCompletionSource();

        // 여기서 "이미 끝났는지" 미리 보지 않는다. 이 함수는 프리팹을 띄운 직후, 아직 Start()가
        // 돌기 전에 불려서 상태가 초기값이다. 기다림을 푸는 신호는 Finish() 한 곳에서만 나온다.

        using (token.Register(CancelRunnerCompletion))
        {
            await runnerCompletion.Task;
        }

        return GimmickResult.Open(step.IngredientId, Rules.Failures + 1, Rules.Failures, Rules.Completed,
                                  craftTimer != null ? craftTimer.ElapsedSec : elapsed);
    }

    private void CancelRunnerCompletion()
    {
        runnerCompletion?.TrySetCanceled();
    }

    /// <summary>
    /// 병따기는 제조 시간에 넣지 않는다. 웹 본편도 병따기 동안은 한 잔의 경과 시간을 세지 않는다
    /// (craft-flow.js: g.type !== 'open'일 때만 elapsed를 더함). 뚜껑을 따는 손놀림은 판정 실패 수로
    /// 이미 점수에 반영되므로, 시간까지 세면 같은 실수를 두 번 깎게 된다.
    /// </summary>
    public bool IsManualInputActive => false;

    // ── ICraftGimmickProgress ───────────────────────────────────────────

    /// <summary>병따기는 단계 안내를 화면 위 가운데에 직접 그린다. 공통 진행 표시는 비워 둔다.</summary>
    public string ProgressText => string.Empty;

    /// <summary>병따기에는 도달을 알릴 목표가 없다.</summary>
    public bool ShowOkMark => false;

    // ── ICraftGimmickManualEnd / ICraftGimmickOwnHud ───────────────────

    /// <summary>뚜껑이 열리고 탄산이 다 올라온 뒤에만 끝낼 수 있다. 열리기 전에는 끝낼 방법이 없다.</summary>
    public bool CanEndNow => !finished && Rules.CanFinish;

    public void EndNow()
    {
        if (CanEndNow) Finish();
    }

    /// <summary>
    /// 단계 안내와 "다음" 버튼은 웹 위치·모양 그대로 직접 그린다. 공통 다음 버튼은 끝낼 수 있는지와
    /// 상관없이 늘 눌리는 모양이라, 탄산이 올라오는 동안 꺼져 있어야 하는 웹 동작을 낼 수 없다.
    /// </summary>
    public ECraftHudPart OwnedParts => ECraftHudPart.Progress | ECraftHudPart.NextButton;
}
