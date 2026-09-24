using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// Play 씬의 데이터 준비, 1부와 2부 실행, 화면 전환, 입력 허용 순서를 소유한다.
/// 하루 실행은 한 번만 수락하며 성공, 취소, 실패 중 하나로 정확히 한 번 종료한다.
/// </summary>
public class PlayPhaseController : MonoBehaviour
{
    [SerializeField] TycoonFlow tycoonFlow;

    [Tooltip("2부 대본 국면. script/bar/dayN.json을 실행한다.")]
    [SerializeField] StoryFlow storyFlow;
    [SerializeField] OrderRequestController orderController;
    [SerializeField] CraftFlowController craftFlow;

    [Inject] IGameProgressionService progression;
    [Inject] PlayerSettlement settlement;

    [Header("국면별 화면")]
    [Tooltip("1부에만 보이는 것. 손님 자리(Tycoon Resource), 코스터 트레이 캔버스, 제조 슬라이드 패널 캔버스.")]
    [SerializeField] GameObject[] tycoonOnlyObjects;

    [Tooltip("2부에만 보이는 것. 좌석 인물(Story Resource).")]
    [SerializeField] GameObject[] storyOnlyObjects;

    [Header("Test")]
    [Tooltip("켜면 1부를 건너뛰고 곧장 2부를 연다. 2부 대본만 확인할 때 쓴다. " +
             "일차는 바꾸지 않는다. 아래 Test Day가 따로 정한다.")]
    [SerializeField] bool skipTycoonForTest;

    [Tooltip("테스트용 진행 일차. 음수면 실제 일차를 그대로 둔다. " +
             "0일차도 실제로 쓰는 값이므로 음수를 비활성 값으로 사용한다.")]
    [SerializeField] int testDay = -1;

    long nextOperationId;
    long currentOperationId;
    bool phaseInputEnabled;
    bool departurePending;
    string departureError;

    public EPlayPhase CurrentPhase { get; private set; } = EPlayPhase.None;
    public PlayPhaseRunState State { get; private set; } = PlayPhaseRunState.Idle;

    public bool IsRunning
    {
        get { return currentOperationId != 0; }
    }

    public long CurrentOperationId
    {
        get { return currentOperationId; }
    }

    void Awake()
    {
        if (testDay < 0) return;

        GameStateManager.Instance.CurrentDay = testDay;
        Debug.LogWarning($"[PlayPhase] 테스트 설정으로 진행 일차를 {testDay}일차로 바꿨습니다. " +
                         "실제 일차로 돌리려면 PlayPhaseController의 Test Day를 음수로 두세요.");
    }

    void Start()
    {
        StartDay();
    }

    /// <summary>기존 UnityEvent 호출부를 유지한다. 이미 시작한 하루는 다시 실행하지 않는다.</summary>
    public void OpenBar()
    {
        StartDay();
    }

    public PlayPhaseRunRequest RequestRunDay()
    {
        if (State != PlayPhaseRunState.Idle)
        {
            PlayPhaseRunResult rejectedResult = new(
                PlayPhaseRunOutcome.Rejected,
                currentOperationId,
                $"하루 실행은 이미 시작되었거나 종료되었습니다. 현재 상태: {State}");
            return new PlayPhaseRunRequest(rejectedResult);
        }

        if (!skipTycoonForTest && tycoonFlow == null)
        {
            PlayPhaseRunResult rejectedResult = new(
                PlayPhaseRunOutcome.Rejected,
                0,
                "TycoonFlow 참조가 없습니다.");
            return new PlayPhaseRunRequest(rejectedResult);
        }

        if (storyFlow == null)
        {
            PlayPhaseRunResult rejectedResult = new(
                PlayPhaseRunOutcome.Rejected,
                0,
                "StoryFlow 참조가 없습니다.");
            return new PlayPhaseRunRequest(rejectedResult);
        }

        if (progression == null)
        {
            PlayPhaseRunResult rejectedResult = new(
                PlayPhaseRunOutcome.Rejected,
                0,
                "하루 진행 서비스가 연결되지 않았습니다.");
            return new PlayPhaseRunRequest(rejectedResult);
        }

        long operationId = ++nextOperationId;
        currentOperationId = operationId;
        State = PlayPhaseRunState.Preparing;
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();
        UniTask<PlayPhaseRunResult> completion = RunDayAsync(operationId, destroyToken);
        return new PlayPhaseRunRequest(true, completion);
    }

    /// <summary>1부와 2부가 이미 끝난 경우 퇴근 이동만 다시 요청한다.</summary>
    public PlayPhaseRunRequest RequestRetryDeparture()
    {
        if (!departurePending || currentOperationId != 0 ||
            (State != PlayPhaseRunState.Failed && State != PlayPhaseRunState.Canceled) ||
            SceneManager.GetActiveScene().name != "Play" ||
            GameStateManager.Instance.GameFlow != EGameFlow.Bar)
            return new PlayPhaseRunRequest(new PlayPhaseRunResult(
                PlayPhaseRunOutcome.Rejected, currentOperationId, "재시도할 퇴근 이동이 없습니다."));

        long operationId = ++nextOperationId;
        currentOperationId = operationId;
        State = PlayPhaseRunState.Transitioning;
        return new PlayPhaseRunRequest(true, RetryDepartureAsync(operationId, this.GetCancellationTokenOnDestroy()));
    }

    async UniTask<PlayPhaseRunResult> RetryDepartureAsync(long operationId, CancellationToken cancellationToken)
    {
        try
        {
            GameProgressionResult result = await progression.CompleteBarAsync(cancellationToken);
            if (result.Outcome == GameProgressionOutcome.Canceled)
            {
                State = PlayPhaseRunState.Canceled;
                departureError = result.Message;
                return new PlayPhaseRunResult(PlayPhaseRunOutcome.Canceled, operationId, result.Message);
            }
            if (!result.Succeeded)
                throw new InvalidOperationException(result.Message ?? "퇴근 이동에 실패했습니다.", result.Error);

            departurePending = false;
            departureError = null;
            State = PlayPhaseRunState.Completed;
            return new PlayPhaseRunResult(PlayPhaseRunOutcome.Succeeded, operationId);
        }
        catch (Exception error)
        {
            State = PlayPhaseRunState.Failed;
            departureError = error.Message;
            return new PlayPhaseRunResult(PlayPhaseRunOutcome.Failed, operationId, error.Message, error);
        }
        finally
        {
            if (SceneManager.GetActiveScene().name != "Play") departurePending = false;
            if (currentOperationId == operationId) currentOperationId = 0;
        }
    }

    void OnGUI()
    {
        if (!departurePending && currentOperationId == 0 && State == PlayPhaseRunState.Failed &&
            SceneManager.GetActiveScene().name == "Play")
        {
            var recovery = new Rect((Screen.width - 360f) / 2f, (Screen.height - 115f) / 2f, 360f, 115f);
            GUI.Box(recovery, "입장 또는 하루 실행에 실패했습니다.");
            if (GUI.Button(new Rect(recovery.x + 80f, recovery.y + 65f, 200f, 35f), "타이틀로 돌아가기"))
                SceneTransitionManager.Instance?.LoadScene("Main");
            return;
        }
        if (!departurePending || currentOperationId != 0 ||
            (State != PlayPhaseRunState.Failed && State != PlayPhaseRunState.Canceled) ||
            SceneManager.GetActiveScene().name != "Play") return;

        var area = new Rect((Screen.width - 360f) / 2f, (Screen.height - 120f) / 2f, 360f, 120f);
        GUI.Box(area, "퇴근 이동에 실패했습니다.");
        GUI.Label(new Rect(area.x + 15f, area.y + 28f, 330f, 36f), departureError ?? "이동을 다시 시도하세요.");
        if (GUI.Button(new Rect(area.x + 80f, area.y + 72f, 200f, 35f), "퇴근 이동 재시도"))
            ObserveAsync(RequestRetryDeparture()).Forget();
    }

    public bool CanReceiveInput(EPlayPhase phase)
    {
        if (!phaseInputEnabled) return false;
        return CurrentPhase == phase;
    }

    void StartDay()
    {
        PlayPhaseRunRequest request = RequestRunDay();
        ObserveAsync(request).Forget();
    }

    async UniTask<PlayPhaseRunResult> RunDayAsync(long operationId, CancellationToken cancellationToken)
    {
        try
        {
            HidePhaseObjects();

            GameProgressionResult entry = await progression.WaitForBarEntryAsync(cancellationToken);
            if (entry.Outcome == GameProgressionOutcome.Canceled)
                throw new OperationCanceledException(entry.Message, cancellationToken);
            if (!entry.Succeeded && Application.isEditor && testDay >= 0 &&
                entry.Outcome == GameProgressionOutcome.Rejected)
                Debug.LogWarning("[PlayPhase] 명시적 Test Day로 Play 씬을 직접 실행합니다.");
            else if (!entry.Succeeded)
                throw new InvalidOperationException(entry.Message ?? "바 입장에 실패했습니다.", entry.Error);

            await NewDataLoadManager.WaitUntilLoadedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            settlement?.Init();

            if (skipTycoonForTest)
            {
                Debug.LogWarning("[PlayPhase] 테스트 설정으로 1부를 건너뜁니다. " +
                                 "PlayPhaseController의 Skip Tycoon For Test를 끄면 원래대로 돌아옵니다.");
            }
            else
            {
                BeginPhase(EPlayPhase.Tycoon);
                await RunPhaseAsync(tycoonFlow, cancellationToken);
                EndCurrentPhase();
            }

            BeginPhase(EPlayPhase.Dialogue);
            await RunPhaseAsync(storyFlow, cancellationToken);
            EndCurrentPhase();

            departurePending = true;
            GameProgressionResult departure = await progression.CompleteBarAsync(cancellationToken);
            if (departure.Outcome == GameProgressionOutcome.Canceled)
                throw new OperationCanceledException(departure.Message, cancellationToken);
            if (!departure.Succeeded)
                throw new InvalidOperationException(departure.Message ?? "퇴근 이동에 실패했습니다.", departure.Error);

            departurePending = false;
            State = PlayPhaseRunState.Completed;
            return new PlayPhaseRunResult(PlayPhaseRunOutcome.Succeeded, operationId);
        }
        catch (OperationCanceledException)
        {
            if (departurePending) departureError = "퇴근 이동이 취소되었습니다.";
            State = PlayPhaseRunState.Canceled;
            return new PlayPhaseRunResult(
                PlayPhaseRunOutcome.Canceled,
                operationId,
                "Play 씬의 하루 실행이 취소되었습니다.");
        }
        catch (Exception error)
        {
            if (departurePending) departureError = error.Message;
            State = PlayPhaseRunState.Failed;
            return new PlayPhaseRunResult(
                PlayPhaseRunOutcome.Failed,
                operationId,
                error.Message,
                error);
        }
        finally
        {
            if (SceneManager.GetActiveScene().name != "Play") departurePending = false;
            if (currentOperationId == operationId)
            {
                phaseInputEnabled = false;
                CurrentPhase = EPlayPhase.None;
                HidePhaseObjects();
                currentOperationId = 0;
            }
        }
    }

    async UniTask RunPhaseAsync(IPlayPhaseFlow flow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await flow.RunAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            if (orderController != null) await orderController.CancelAllAndWaitAsync();
            if (craftFlow != null && craftFlow.IsBusy && craftFlow.Current != null)
            {
                var completion = craftFlow.Completion;
                if (!await craftFlow.CancelCraftAsync(craftFlow.Current.JobId)) await completion;
            }
            orderController?.CloseIdleView();
        }
    }

    void BeginPhase(EPlayPhase phase)
    {
        phaseInputEnabled = false;
        CurrentPhase = EPlayPhase.None;
        State = PlayPhaseRunState.Transitioning;

        ShowPhase(phase);

        CurrentPhase = phase;
        if (phase == EPlayPhase.Tycoon)
        {
            State = PlayPhaseRunState.RunningTycoon;
        }
        else
        {
            State = PlayPhaseRunState.RunningDialogue;
        }
        phaseInputEnabled = true;
    }

    void EndCurrentPhase()
    {
        phaseInputEnabled = false;
        CurrentPhase = EPlayPhase.None;
        State = PlayPhaseRunState.Transitioning;
        HidePhaseObjects();
    }

    void ShowPhase(EPlayPhase phase)
    {
        bool showTycoon = phase == EPlayPhase.Tycoon;
        bool showStory = phase == EPlayPhase.Dialogue;
        SetActive(tycoonOnlyObjects, showTycoon);
        SetActive(storyOnlyObjects, showStory);
    }

    void HidePhaseObjects()
    {
        SetActive(tycoonOnlyObjects, false);
        SetActive(storyOnlyObjects, false);
    }

    static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject target in objects)
        {
            if (target != null) target.SetActive(active);
        }
    }

    static async UniTask ObserveAsync(PlayPhaseRunRequest request)
    {
        PlayPhaseRunResult result = await request.Completion;
        if (result.Outcome == PlayPhaseRunOutcome.Rejected)
        {
            Debug.LogWarning($"[PlayPhase] 실행 요청 거절: {result.Message}");
        }
        else if (result.Outcome == PlayPhaseRunOutcome.Failed)
        {
            Debug.LogException(result.Error);
        }
    }
}
