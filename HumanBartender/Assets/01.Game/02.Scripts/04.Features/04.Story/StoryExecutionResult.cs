using System;

public enum StoryExecutionStatus { Completed, Cancelled, Failed }

public readonly struct StoryExecutionResult
{
    public StoryExecutionStatus Status { get; }
    public string Error { get; }
    public bool Completed => Status == StoryExecutionStatus.Completed;
    public GameProgressionDestination? Destination { get; }
    public StorySceneContinuation Continuation { get; }

    public StoryExecutionResult(StoryExecutionStatus status, string error = null, GameProgressionDestination? destination = null,
        StorySceneContinuation continuation = null)
    {
        Status = status;
        Error = error;
        Destination = status == StoryExecutionStatus.Completed ? destination : null;
        Continuation = status == StoryExecutionStatus.Completed && destination.HasValue ? continuation : null;
    }
}

/// <summary>씬 이동 직후에 이어 읽을 대본. 원본 CSV 배열과 진행 저장 형식은 변경하지 않는다.</summary>
public sealed class StorySceneContinuation
{
    public Step[] Steps { get; }
    public string DepartureEffects { get; }
    public string RollbackEffects { get; }
    public StorySceneContinuation(Step[] steps, string departureEffects = null, string rollbackEffects = null)
    {
        Steps = steps == null ? Array.Empty<Step>() : (Step[])steps.Clone();
        DepartureEffects = departureEffects;
        RollbackEffects = rollbackEffects;
    }
}

public readonly struct ConditionResult
{
    public bool Value { get; }
    public string Error { get; }
    public bool IsValid => Error == null;
    public ConditionResult(bool value, string error = null) { Value = value; Error = error; }
}

public enum EffectApplyStatus { NoOp, Applied, AlreadyApplied, Failed, PartialFailure }

public readonly struct EffectApplyResult
{
    public EffectApplyStatus Status { get; }
    public int AppliedCount { get; }
    public string Error { get; }
    public bool Succeeded => Status == EffectApplyStatus.NoOp || Status == EffectApplyStatus.Applied ||
                             Status == EffectApplyStatus.AlreadyApplied;
    public EffectApplyResult(EffectApplyStatus status, int appliedCount = 0, string error = null)
    { Status = status; AppliedCount = appliedCount; Error = error; }
}

public static class StoryConditionExtensions
{
    public static bool CheckRequired(this IConditionUtil conditions, string expression)
    {
        var result = conditions.Evaluate(expression);
        if (!result.IsValid) throw new InvalidOperationException(result.Error);
        return result.Value;
    }

    public static void ApplyRequired(this IConditionUtil conditions, string effects, string token = null)
    {
        var result = conditions.ApplyEffects(effects, token);
        if (!result.Succeeded) throw new InvalidOperationException(result.Error);
    }
}
