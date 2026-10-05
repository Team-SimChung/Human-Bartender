using Cysharp.Threading.Tasks;
using System;

public enum PlayPhaseRunOutcome
{
    Succeeded,
    Rejected,
    Canceled,
    Failed,
}

public enum PlayPhaseRunState
{
    Idle,
    Preparing,
    RunningTycoon,
    Transitioning,
    RunningDialogue,
    Completed,
    Canceled,
    Failed,
}

/// <summary>Play 씬의 하루 실행이 끝난 이유와 실패 정보를 전달한다.</summary>
public readonly struct PlayPhaseRunResult
{
    public PlayPhaseRunOutcome Outcome { get; }
    public long OperationId { get; }
    public string Message { get; }
    public Exception Error { get; }
    public bool Succeeded { get; }

    internal PlayPhaseRunResult(
        PlayPhaseRunOutcome outcome,
        long operationId,
        string message = null,
        Exception error = null)
    {
        Outcome = outcome;
        OperationId = operationId;
        Message = message;
        Error = error;
        Succeeded = outcome == PlayPhaseRunOutcome.Succeeded;
    }
}

/// <summary>하루 실행의 즉시 수락 여부와 최종 완료 결과를 함께 제공한다.</summary>
public readonly struct PlayPhaseRunRequest
{
    public bool Accepted { get; }
    public UniTask<PlayPhaseRunResult> Completion { get; }

    internal PlayPhaseRunRequest(bool accepted, UniTask<PlayPhaseRunResult> completion)
    {
        Accepted = accepted;
        Completion = completion;
    }

    internal PlayPhaseRunRequest(PlayPhaseRunResult completedResult)
    {
        Accepted = false;
        Completion = UniTask.FromResult(completedResult);
    }
}
