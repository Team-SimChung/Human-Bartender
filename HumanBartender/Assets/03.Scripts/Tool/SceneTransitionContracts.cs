using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.SceneManagement;

public enum SceneTransitionOutcome
{
    Succeeded,
    Rejected,
    Canceled,
    Failed,
}

public enum SceneTransitionState
{
    Idle,
    FadingOut,
    Loading,
    Unloading,
    FadingIn,
    Fading,
}

/// <summary>씬 전환 요청이 끝난 이유. 실패 예외는 삼키지 않고 Error로 전달한다.</summary>
public readonly struct SceneTransitionResult
{
    public SceneTransitionOutcome Outcome { get; }
    public string SceneName { get; }
    public string Message { get; }
    public Exception Error { get; }
    public bool Succeeded { get; }
    public bool SceneActivated { get; }

    internal SceneTransitionResult(
        SceneTransitionOutcome outcome,
        string sceneName,
        string message = null,
        Exception error = null) : this(outcome, sceneName, message, error, false) { }

    internal SceneTransitionResult(
        SceneTransitionOutcome outcome,
        string sceneName,
        string message,
        Exception error,
        bool sceneActivated)
    {
        Outcome = outcome;
        SceneName = sceneName;
        Message = message;
        Error = error;
        Succeeded = outcome == SceneTransitionOutcome.Succeeded;
        SceneActivated = sceneActivated || Succeeded;
    }
}

/// <summary>
/// 요청 수락 여부는 즉시 확인하고, 수락된 작업의 최종 결과는 Completion으로 기다린다.
/// </summary>
public readonly struct SceneTransitionRequest
{
    public bool Accepted { get; }
    public UniTask<SceneTransitionResult> Completion { get; }

    internal SceneTransitionRequest(bool accepted, UniTask<SceneTransitionResult> completion)
    {
        Accepted = accepted;
        Completion = completion;
    }

    internal SceneTransitionRequest(SceneTransitionResult completedResult)
    {
        Accepted = false;
        Completion = UniTask.FromResult(completedResult);
    }
}

public interface ISceneTransitionService
{
    bool IsBusy { get; }
    SceneTransitionState CurrentState { get; }
    long CurrentOperationId { get; }
    SceneTransitionRequest RequestLoadScene(string sceneName, LoadSceneMode sceneMode = LoadSceneMode.Single,
        CancellationToken cancellationToken = default);
    SceneTransitionRequest RequestUnloadScene(string sceneName, CancellationToken cancellationToken = default);
}
