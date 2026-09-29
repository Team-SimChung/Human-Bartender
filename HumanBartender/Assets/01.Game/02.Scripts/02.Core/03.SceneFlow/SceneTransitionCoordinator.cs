/// <summary>
/// 전환 상태의 단일 소유자. 작업 ID가 일치하는 호출만 상태를 바꾸거나 잠금을 해제할 수 있다.
/// </summary>
internal sealed class SceneTransitionCoordinator
{
    long nextOperationId;

    public long CurrentOperationId { get; private set; }
    public bool IsBusy
    {
        get { return CurrentOperationId != 0; }
    }
    public SceneTransitionState State { get; private set; } = SceneTransitionState.Idle;

    public bool TryBegin(out long operationId)
    {
        if (IsBusy)
        {
            operationId = 0;
            return false;
        }

        operationId = ++nextOperationId;
        CurrentOperationId = operationId;
        return true;
    }

    public bool TrySetState(long operationId, SceneTransitionState state)
    {
        if (operationId == 0 || CurrentOperationId != operationId) return false;
        State = state;
        return true;
    }

    public bool TryEnd(long operationId)
    {
        if (operationId == 0 || CurrentOperationId != operationId) return false;
        CurrentOperationId = 0;
        State = SceneTransitionState.Idle;
        return true;
    }
}
