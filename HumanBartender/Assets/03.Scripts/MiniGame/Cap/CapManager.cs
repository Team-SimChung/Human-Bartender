using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 병따기(Cap) 미니게임의 메인 매니저. IMiniGameController를 구현한다.
///
/// 병뚜껑 둘레에 목표 고리가 고정되어 있고, 바깥에서 조여드는 고리가 등속으로 좁혀 온다.
/// 두 고리가 겹치는 순간(반지름 차이가 judgeWindow 이내) 입력하면 성공이다. 빗나가거나
/// 목표를 지나쳐 버리면 실패로 치고, 잠깐 뒤 고리가 다시 바깥에서 조여든다 — 성공하거나
/// 제한 시간이 끝날 때까지 반복된다.
///
/// 인터페이스를 둘 구현한다. ICraftGimmick은 1부의 기믹 큐가 부르는 새 경로이고,
/// IMiniGameController는 2부 대화에서 컷씬과 함께 돌던 기존 경로다. 조작과 판정 코드는 양쪽이 공유한다.
///
/// 두 경로의 차이가 하나 있다. 기믹 큐가 돌릴 때는 자체 제한시간을 쓰지 않는다 —
/// 병따기는 성공해야만 끝나고, 시간은 한 잔 전체의 제조시간으로만 재기 때문이다.
/// </summary>
public class CapManager : MonoBehaviour, IMiniGameController, ICraftGimmick, ICraftGimmickProgress
{
    [SerializeField] bool isTest = false;

    [Header("Data")]
    [SerializeField] CraftStationData data;
    [SerializeField] NewCocktailDataSO cocktailDataSO;

    [Header("Ring")]
    [Tooltip("조여드는 고리. 바깥에서 목표 고리까지 좁혀 온다.")]
    [SerializeField] CapRing incomingRing;
    [Tooltip("목표 고리. 병뚜껑 둘레에 고정되어 크기가 변하지 않는다.")]
    [SerializeField] CapRing targetRing;

    [Tooltip("고리가 시작하는 반지름(월드 단위).")]
    [SerializeField] float startRadius = 2.2f;
    [Tooltip("목표 고리의 반지름. 병뚜껑 크기에 맞춰 잡는다.")]
    [SerializeField] float targetRadius = 0.6f;
    [Tooltip("판정 폭. 두 고리의 반지름 차이가 이 값 이내일 때 누르면 성공이다. " +
             "넓힐수록 쉬워지고, 좁힐수록 정확한 타이밍을 요구한다.")]
    [SerializeField] float judgeWindow = 0.14f;
    [Tooltip("고리가 시작 크기에서 목표까지 조여드는 데 걸리는 시간(초). 짧을수록 어렵다.")]
    [SerializeField] float approachSeconds = 1.6f;

    [Header("Timing")]
    [Tooltip("실패한 뒤 고리가 다시 바깥에서 조여들기까지의 텀. 연출을 볼 시간이자, " +
             "연타로 우연히 맞히는 걸 막는 장치다.")]
    [SerializeField] float failCooldown = 0.45f;
    [Tooltip("성공 연출을 보여준 뒤 결과로 넘어가기까지의 텀.")]
    [SerializeField] float successCooldown = 0.9f;
    [Tooltip("제조 제한 시간(초). 넘기면 실패로 끝난다.")]
    [SerializeField] float timeLimit = 25f;

    [Header("Judge")]
    [Tooltip("isTest일 때 쓸 허용 실패 횟수. 실제 모드에서는 " +
             "CraftStationData.targetCraft_tolerance에서 환산한다.\n" +
             "판정은 (실패 횟수 / 이 값)의 백분율이다 — 20% 이하 Perfect, 50% 이하 Normal, 초과 Failed.")]
    [SerializeField] int testLimitFailCount = 5;

    [Header("Visual")]
    [Tooltip("병뚜껑. 성공하면 튕겨 날아가고, 실패하면 살짝 꺾인다. 비워둬도 게임은 돌아간다.")]
    [SerializeField] Transform capPiece;
    [Tooltip("판정 구간에 들어왔을 때 조여드는 고리에 입힐 색. 이 신호가 없으면 " +
             "언제 눌러야 할지 눈으로 읽기 어렵다.")]
    [SerializeField] Color nearColor = new Color(0.22f, 0.83f, 0.48f);
    [SerializeField] Color incomingColor = new Color(0.95f, 0.76f, 0.31f);
    [SerializeField] Color targetColor = new Color(0.5f, 0.94f, 0.92f);

    [Header("UI")]
    [SerializeField] Canvas buttonCanvas;
    // 재료명·시도 횟수·전체 시간은 공통 HUD가 그린다. 여기서는 판정 순간의 피드백만 맡는다 —
    // PERFECT/FAIL은 고리 판정에 붙는 연출이라 공통 표시에 자리가 없다.
    [Tooltip("성공·실패 판정 텍스트. 고리 근처에 뜨는 기믹 고유 연출이다.")]
    [SerializeField] TMP_Text judgeText;

    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    /// <summary>고리 상태. 조여드는 중 → (성공/실패) 쿨다운 → 다시 조여드는 중.</summary>
    enum Phase { Approaching, Cooldown, Finished }

    Phase phase = Phase.Approaching;

    float radius;
    float cooldown;
    float elapsed;

    /// <summary>시도 횟수. 1회째부터 시작한다(캡처 UI의 "시도 n회째"와 같은 뜻).</summary>
    int attempts = 1;
    int limitFailCount;
    bool opened;
    bool timedOut;

    UniTaskCompletionSource tcs;

    /// <summary>실패 횟수. 판정에 넘기는 값이다 — 첫 시도에 성공하면 0이다.</summary>
    int FailCount => attempts - 1;

    /// <summary>지금 반지름에서 고리가 조여드는 속도. 시작~목표 구간을 approachSeconds에 지나도록 잡는다.</summary>
    float ShrinkSpeed => (startRadius - targetRadius) / Mathf.Max(0.1f, approachSeconds);

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
        // 기믹 큐가 돌릴 때는 실패 횟수를 그대로 결과에 담고 감점은 Config가 정하므로 허용치를 쓰지 않는다.
        int tolerance = data != null ? data.targetCraft_tolerance : 0;
        limitFailCount = Mathf.Max(1, tolerance > 0 ? tolerance : testLimitFailCount);

        radius = startRadius;
        attempts = 1;
        elapsed = 0f;
        opened = false;
        timedOut = false;
        phase = Phase.Approaching;

        if (targetRing != null)
        {
            targetRing.SetColor(targetColor);
            targetRing.SetRadius(targetRadius);
        }

        if (incomingRing != null) incomingRing.SetColor(incomingColor);

        if (judgeText != null) judgeText.text = string.Empty;
    }

    void Update()
    {
        if (phase == Phase.Finished) return;

        float dt = Time.deltaTime;

        elapsed += dt;

        // 기믹 큐가 돌리는 동안에는 병따기에 자체 제한시간이 없다. 병뚜껑은 열림과 닫힘의 중간
        // 상태로 제조를 이어갈 수 없어서, 성공할 때까지 계속 시도하는 게 규칙이다. 시간은 한 잔
        // 전체의 제조시간에만 누적되고 그 초과는 마지막에 한 번 판정한다.
        if (!drivenByRunner && elapsed >= timeLimit)
        {
            TimeOut();
            return;
        }

        if (phase == Phase.Cooldown)
        {
            cooldown -= dt;
            if (cooldown <= 0f) EndCooldown();
        }
        else
        {
            radius -= ShrinkSpeed * dt;

            // 목표를 지나쳐 버리면 누를 기회를 놓친 것이므로 그대로 실패다.
            if (radius < targetRadius - judgeWindow) Fail();
        }

        RefreshRing();
    }

    // ── 판정 ────────────────────────────────────────────────────────────

    /// <summary>CapInputHandler가 스페이스/클릭 어느 쪽이든 누른 순간에 부른다.</summary>
    public void OnPress()
    {
        // 쿨다운 중에는 받지 않는다. 안 그러면 실패 직후 연타로 다음 고리가 시작하기도 전에 또 실패가 쌓인다.
        if (phase != Phase.Approaching) return;

        if (Mathf.Abs(radius - targetRadius) <= judgeWindow) Succeed();
        else Fail();
    }

    void Succeed()
    {
        opened = true;
        phase = Phase.Cooldown;
        cooldown = successCooldown;

        if (incomingRing != null) incomingRing.SetVisible(false);
        if (judgeText != null) judgeText.text = "PERFECT";

        PopCap();
    }

    void Fail()
    {
        attempts++;
        phase = Phase.Cooldown;
        cooldown = failCooldown;

        if (judgeText != null) judgeText.text = "FAIL";

        BendCap();
    }

    void EndCooldown()
    {
        cooldown = 0f;

        if (opened)
        {
            Finish();
            return;
        }

        // 실패했으면 고리를 다시 바깥에서 시작시킨다. 시도 횟수만 남고 조건은 그대로다.
        radius = startRadius;
        phase = Phase.Approaching;

        if (incomingRing != null) incomingRing.SetVisible(true);
        if (judgeText != null) judgeText.text = string.Empty;
    }

    void TimeOut()
    {
        timedOut = true;

        if (judgeText != null) judgeText.text = "TIME OVER";
        if (incomingRing != null) incomingRing.SetVisible(false);

        Finish();
    }

    void Finish()
    {
        if (phase == Phase.Finished) return;

        phase = Phase.Finished;

        // 자체 제한시간을 쓰는 기존 경로에서만 표시용으로 잘라 준다. 큐가 돌릴 때는 제한이 없어서
        // 잘라 버리면 실제로 걸린 시간이 사라진다.
        if (!drivenByRunner) elapsed = Mathf.Min(elapsed, timeLimit);

        CompleteMade();

        runnerCompletion?.TrySetResult();

        // 실제 게임 흐름에서는 기믹 큐(GimmickRunner)가 결과를 받아 다음 스텝으로 넘긴다.
        // 독립 테스트 씬에는 그 흐름이 없어서, 끝났다는 신호가 없으면 그냥 멈춘 것처럼 보인다.
        if (isTest)
        {
            string how = timedOut ? "시간 초과" : $"{attempts}회 만에 개봉";
            Logger.Log($"[Cap] 판정 종료. {how} / 실패 {FailCount} / 허용 {limitFailCount}");
            OnNextButton();
        }
    }

    // ── 표시 ────────────────────────────────────────────────────────────

    void RefreshRing()
    {
        if (incomingRing == null) return;

        incomingRing.SetRadius(radius);

        // 판정 구간에 들어오면 색으로 알린다. 반지름만으로는 겹치는 순간을 눈으로 잡기 어렵다.
        bool near = phase == Phase.Approaching && Mathf.Abs(radius - targetRadius) <= judgeWindow;
        incomingRing.SetColor(near ? nearColor : incomingColor);
    }

    void PopCap()
    {
        if (capPiece == null) return;

        // 실제 연출(튕겨 날아감)은 아트가 들어온 뒤에 애니메이션으로 붙인다. 지금은 위치만 옮겨
        // 열렸다는 걸 알아볼 수 있게 한다.
        capPiece.localPosition += new Vector3(0.25f, 0.9f, 0f);
        capPiece.localRotation = Quaternion.Euler(0f, 0f, -40f);
    }

    void BendCap()
    {
        if (capPiece == null) return;

        capPiece.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
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
            data.craftingResult.actionFailCount = timedOut ? limitFailCount + 1 : FailCount;
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

    /// <summary>
    /// 이번 제조 시도의 사정(고른 잔·도구·재료). 실행기가 PlayAsync로 넘겨준다.
    /// 아직 그리는 데 쓰지는 않는다 — 리소스가 붙을 때 여기서 가져가면 된다.
    /// </summary>
    CraftContext craftContext;

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
                                                 CraftTimer timer, CancellationToken token)
    {
        // 병따기는 병마다 뚜껑 모양이 다르다. 어느 병인지는 step.IngredientId가 알려 주고,
        // 그 병이 어떤 잔 앞에 놓이는지는 context.GlassId가 알려 준다. 리소스가 나오면 여기서 가져간다.
        craftContext = context;

        // 아직 그리는 데 쓰지 않으므로, 값이 제대로 도착했는지는 이 줄로만 확인한다.
        Debug.Log($"[Cap] 문맥 — 잔 {craftContext.GlassId ?? "없음"} / " +
                  $"도구 {craftContext.ToolId ?? "없음"} / 재료 {string.Join(", ", craftContext.IngredientIds)}");

        drivenByRunner = true;
        craftTimer = timer;
        runnerCompletion = new UniTaskCompletionSource();

        // 여기서 "이미 끝났는지" 미리 보지 않는다. 이 함수는 프리팹을 띄운 직후, 아직 Start()가
        // 돌기 전에 불려서 상태가 초기값이다. 기다림을 푸는 신호는 Finish() 한 곳에서만 나온다.

        using (token.Register(() => runnerCompletion.TrySetCanceled()))
        {
            await runnerCompletion.Task;
        }

        // 병따기는 다음 버튼이 없다. 열려야만 끝나므로 종료 방식이 하나뿐이다.
        return GimmickResult.Open(step.IngredientId, attempts, FailCount, opened,
                                  craftTimer != null ? craftTimer.ElapsedSec : elapsed);
    }

    /// <summary>
    /// 고리가 조여드는 동안에만 입력을 받는다. 성공·실패 연출을 보여주는 쿨다운 구간에서는
    /// 눌러도 판정하지 않으므로 시간에서도 뺀다.
    /// </summary>
    public bool IsManualInputActive => phase == Phase.Approaching;

    // ── ICraftGimmickProgress ───────────────────────────────────────────

    /// <summary>몇 번째 시도인지. 병따기는 수량을 재지 않고 성공까지 걸린 횟수로 판정한다.</summary>
    public string ProgressText => $"시도 {attempts}회째";

    /// <summary>병따기에는 도달을 알릴 목표가 없다. 열리는 순간이 곧 끝이다.</summary>
    public bool ShowOkMark => false;

    // ICraftGimmickManualEnd는 구현하지 않는다. 병뚜껑은 열림과 닫힘의 중간 상태로 제조를 이어갈 수
    // 없어서, 성공하기 전에 끝낼 방법이 없어야 한다. HUD는 이걸 보고 다음 버튼을 감춘다.
}
