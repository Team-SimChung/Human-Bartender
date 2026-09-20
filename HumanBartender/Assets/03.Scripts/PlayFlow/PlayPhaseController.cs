using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Play 씬의 데이터 준비, 1부와 2부 실행, 화면 전환, 입력 허용 순서를 소유한다.
/// 하루 실행은 한 번만 수락하며 성공, 취소, 실패 중 하나로 정확히 한 번 종료한다.
/// </summary>
public class PlayPhaseController : MonoBehaviour
{
    [SerializeField] TycoonFlow tycoonFlow;

    [Tooltip("2부 대본 국면. script/bar/dayN.json을 실행한다.")]
    [SerializeField] StoryFlow storyFlow;

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

        long operationId = ++nextOperationId;
        currentOperationId = operationId;
        State = PlayPhaseRunState.Preparing;
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();
        UniTask<PlayPhaseRunResult> completion = RunDayAsync(operationId, destroyToken);
        return new PlayPhaseRunRequest(true, completion);
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

            await NewDataLoadManager.WaitUntilLoadedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

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

            State = PlayPhaseRunState.Completed;
            return new PlayPhaseRunResult(PlayPhaseRunOutcome.Succeeded, operationId);
        }
        catch (OperationCanceledException)
        {
            State = PlayPhaseRunState.Canceled;
            return new PlayPhaseRunResult(
                PlayPhaseRunOutcome.Canceled,
                operationId,
                "Play 씬의 하루 실행이 취소되었습니다.");
        }
        catch (Exception error)
        {
            State = PlayPhaseRunState.Failed;
            return new PlayPhaseRunResult(
                PlayPhaseRunOutcome.Failed,
                operationId,
                error.Message,
                error);
        }
        finally
        {
            if (currentOperationId == operationId)
            {
                phaseInputEnabled = false;
                CurrentPhase = EPlayPhase.None;
                HidePhaseObjects();
                currentOperationId = 0;
            }
        }
    }

    static async UniTask RunPhaseAsync(IPlayPhaseFlow flow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UniTask phaseTask = flow.RunAsync();
        await phaseTask.AttachExternalCancellation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
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
