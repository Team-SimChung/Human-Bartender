using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameProgressionDestination
{
    Bar,
    Home,
    OutsideFromHome,
}

public enum GameProgressionOutcome
{
    Succeeded,
    Rejected,
    Canceled,
    Failed,
}

public readonly struct GameProgressionResult
{
    public GameProgressionOutcome Outcome { get; }
    public string Message { get; }
    public Exception Error { get; }
    public bool Succeeded { get { return Outcome == GameProgressionOutcome.Succeeded; } }

    public GameProgressionResult(GameProgressionOutcome outcome, string message = null, Exception error = null)
    {
        Outcome = outcome;
        Message = message;
        Error = error;
    }
}

public interface ISceneFadeService
{
    UniTask FadeOutAsync(float duration = -1f, CancellationToken cancellationToken = default);
    UniTask FadeInAsync(float duration = -1f, CancellationToken cancellationToken = default);
}

public interface IGameProgressionService
{
    UniTask<GameProgressionResult> WaitForBarEntryAsync(CancellationToken cancellationToken = default);
    UniTask<GameProgressionResult> CompleteBarAsync(CancellationToken cancellationToken = default);
    UniTask<GameProgressionResult> EnterAsync(GameProgressionDestination destination,
        CancellationToken cancellationToken = default);
    UniTask<GameProgressionResult> SleepAsync(Action refreshConditions,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 일차와 출퇴근 문맥을 변경하는 운영 경계. 씬 이동과 수면 연출은 기존 서비스에 위임한다.
/// </summary>
public sealed class GameProgressionService : IGameProgressionService
{
    readonly GameStateManager gameState;
    readonly ISceneTransitionService sceneTransitions;
    readonly ISceneFadeService sceneFades;
    bool isRunning;
    UniTaskCompletionSource<GameProgressionResult> barEntryCompletion;

    public GameProgressionService(GameStateManager gameState, ISceneTransitionService sceneTransitions,
        ISceneFadeService sceneFades)
    {
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.sceneTransitions = sceneTransitions ?? throw new ArgumentNullException(nameof(sceneTransitions));
        this.sceneFades = sceneFades ?? throw new ArgumentNullException(nameof(sceneFades));
    }

    public UniTask<GameProgressionResult> CompleteBarAsync(CancellationToken cancellationToken = default)
    {
        return MoveAsync("Play", "OutSide", EGameFlow.Bar, EGameFlow.CommuteOut, cancellationToken);
    }

    public UniTask<GameProgressionResult> WaitForBarEntryAsync(CancellationToken cancellationToken = default)
    {
        return barEntryCompletion == null
            ? UniTask.FromResult(new GameProgressionResult(GameProgressionOutcome.Succeeded))
            : barEntryCompletion.Task.AttachExternalCancellation(cancellationToken);
    }

    public UniTask<GameProgressionResult> EnterAsync(GameProgressionDestination destination,
        CancellationToken cancellationToken = default)
    {
        switch (destination)
        {
            case GameProgressionDestination.Bar:
                return MoveAsync("OutSide", "Play", EGameFlow.CommuteIn, EGameFlow.Bar, cancellationToken);
            case GameProgressionDestination.Home:
                return MoveAsync("OutSide", "Home", EGameFlow.CommuteOut, EGameFlow.CommuteOut,
                    cancellationToken);
            case GameProgressionDestination.OutsideFromHome:
                return MoveAsync("Home", "OutSide", EGameFlow.CommuteIn, EGameFlow.CommuteIn,
                    cancellationToken);
            default:
                return UniTask.FromResult(new GameProgressionResult(GameProgressionOutcome.Rejected,
                    "알 수 없는 이동 요청입니다."));
        }
    }

    async UniTask<GameProgressionResult> MoveAsync(string sourceScene, string destinationScene,
        EGameFlow requiredFlow, EGameFlow destinationFlow, CancellationToken cancellationToken)
    {
        if (isRunning || sceneTransitions.IsBusy)
            return new GameProgressionResult(GameProgressionOutcome.Rejected, "다른 진행 작업이 실행 중입니다.");
        if (SceneManager.GetActiveScene().name != sourceScene || gameState.GameFlow != requiredFlow)
            return new GameProgressionResult(GameProgressionOutcome.Rejected,
                $"현재 위치 또는 출퇴근 상태에서 이동할 수 없습니다: {sourceScene}/{requiredFlow}");
        if (cancellationToken.IsCancellationRequested)
            return new GameProgressionResult(GameProgressionOutcome.Canceled);

        isRunning = true;
        EGameFlow previousFlow = gameState.GameFlow;
        UniTaskCompletionSource<GameProgressionResult> barEntry = null;
        GameProgressionResult outcome = default;
        try
        {
            SceneTransitionRequest request = sceneTransitions.RequestLoadScene(destinationScene,
                cancellationToken: cancellationToken);
            if (!request.Accepted)
                return outcome = ConvertSceneResult(await request.Completion);

            if (destinationScene == "Play")
                barEntryCompletion = barEntry = new UniTaskCompletionSource<GameProgressionResult>();

            // 새 씬의 Start가 전환 Completion보다 먼저 실행되므로, 수락된 요청에 한해서
            // 목적지의 출퇴근 문맥을 준비한다. 씬이 바뀌지 않은 실패는 아래에서 되돌린다.
            gameState.GameFlow = destinationFlow;
            SceneTransitionResult result = await request.Completion;
            if (!result.Succeeded && SceneManager.GetActiveScene().name != destinationScene)
                gameState.GameFlow = previousFlow;
            if (result.Succeeded)
                Debug.Log($"[Progression] {sourceScene} → {destinationScene}, Day {gameState.CurrentDay}, {gameState.GameFlow}");
            return outcome = ConvertSceneResult(result);
        }
        catch (OperationCanceledException error)
        {
            if (SceneManager.GetActiveScene().name != destinationScene)
                gameState.GameFlow = previousFlow;
            return outcome = new GameProgressionResult(GameProgressionOutcome.Canceled, error.Message, error);
        }
        catch (Exception error)
        {
            if (SceneManager.GetActiveScene().name != destinationScene)
                gameState.GameFlow = previousFlow;
            return outcome = new GameProgressionResult(GameProgressionOutcome.Failed, error.Message, error);
        }
        finally
        {
            isRunning = false;
            if (barEntry != null)
            {
                if (ReferenceEquals(barEntryCompletion, barEntry)) barEntryCompletion = null;
                barEntry.TrySetResult(outcome);
            }
        }
    }

    public async UniTask<GameProgressionResult> SleepAsync(Action refreshConditions,
        CancellationToken cancellationToken = default)
    {
        if (isRunning || sceneTransitions.IsBusy)
            return new GameProgressionResult(GameProgressionOutcome.Rejected, "다른 진행 작업이 실행 중입니다.");
        if (SceneManager.GetActiveScene().name != "Home" || gameState.GameFlow != EGameFlow.CommuteOut)
            return new GameProgressionResult(GameProgressionOutcome.Rejected, "귀가 후 집에서만 취침할 수 있습니다.");
        if (cancellationToken.IsCancellationRequested)
            return new GameProgressionResult(GameProgressionOutcome.Canceled);
        if (gameState.CurrentDay == int.MaxValue)
            return new GameProgressionResult(GameProgressionOutcome.Rejected, "다음 일차를 만들 수 없습니다.");

        isRunning = true;
        int previousDay = gameState.CurrentDay;
        EGameFlow previousFlow = gameState.GameFlow;
        bool changed = false;
        try
        {
            await NewDataLoadManager.WaitUntilLoadedAsync(cancellationToken);
            int nextDay = previousDay + 1;
            if (!NewDataLoadManager.TryGetDayInfo(nextDay, out NewDayInfoData next))
                return new GameProgressionResult(GameProgressionOutcome.Rejected,
                    $"등록되지 않은 다음 일차입니다: Day {nextDay}");

            if (next.StartPhase == "commute_out")
            {
                SceneTransitionRequest request = sceneTransitions.RequestLoadScene("OutSide",
                    cancellationToken: cancellationToken);
                if (!request.Accepted)
                    return ConvertSceneResult(await request.Completion);

                // OutSide.Start보다 먼저 다음 일차 문맥을 준비한다. 씬이 바뀐 뒤의 실패는 되돌리지 않는다.
                gameState.CurrentDay = nextDay;
                gameState.GameFlow = EGameFlow.CommuteOut;
                changed = true;
                SceneTransitionResult result = await request.Completion;
                if (!result.Succeeded && SceneManager.GetActiveScene().name == "Home")
                {
                    gameState.CurrentDay = previousDay;
                    gameState.GameFlow = previousFlow;
                }
                return ConvertSceneResult(result);
            }

            if (next.StartPhase != "home")
                return new GameProgressionResult(GameProgressionOutcome.Rejected,
                    $"알 수 없는 시작 국면입니다: Day {nextDay}/{next.StartPhase}");

            await sceneFades.FadeOutAsync(0.5f, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            gameState.CurrentDay = nextDay;
            gameState.GameFlow = EGameFlow.CommuteIn;
            changed = true;
            refreshConditions?.Invoke();
            await sceneFades.FadeInAsync(0.5f, CancellationToken.None);
            Debug.Log($"[Progression] 취침 완료, Day {previousDay} → {gameState.CurrentDay}, {gameState.GameFlow}");
            return new GameProgressionResult(GameProgressionOutcome.Succeeded);
        }
        catch (OperationCanceledException error)
        {
            if (SceneManager.GetActiveScene().name == "Home")
                await RestoreFailedSleepAsync(previousDay, previousFlow, changed, refreshConditions);
            return new GameProgressionResult(GameProgressionOutcome.Canceled, error.Message, error);
        }
        catch (Exception error)
        {
            if (SceneManager.GetActiveScene().name != "Home")
                return new GameProgressionResult(GameProgressionOutcome.Failed, error.Message, error);
            await RestoreFailedSleepAsync(previousDay, previousFlow, changed, refreshConditions);
            return new GameProgressionResult(GameProgressionOutcome.Failed, error.Message, error);
        }
        finally
        {
            isRunning = false;
        }
    }

    async UniTask RestoreFailedSleepAsync(int previousDay, EGameFlow previousFlow, bool changed,
        Action refreshConditions)
    {
        if (changed)
        {
            gameState.CurrentDay = previousDay;
            gameState.GameFlow = previousFlow;
            try { refreshConditions?.Invoke(); }
            catch (Exception refreshError) { Debug.LogException(refreshError); }
        }
        try { await sceneFades.FadeInAsync(0f, CancellationToken.None); }
        catch (Exception fadeError) { Debug.LogException(fadeError); }
    }

    static GameProgressionResult ConvertSceneResult(SceneTransitionResult result)
    {
        switch (result.Outcome)
        {
            case SceneTransitionOutcome.Succeeded:
                return new GameProgressionResult(GameProgressionOutcome.Succeeded);
            case SceneTransitionOutcome.Rejected:
                return new GameProgressionResult(GameProgressionOutcome.Rejected, result.Message, result.Error);
            case SceneTransitionOutcome.Canceled:
                return new GameProgressionResult(GameProgressionOutcome.Canceled, result.Message, result.Error);
            default:
                return new GameProgressionResult(GameProgressionOutcome.Failed, result.Message, result.Error);
        }
    }
}
