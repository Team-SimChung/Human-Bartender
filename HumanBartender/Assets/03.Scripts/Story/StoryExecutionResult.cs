using System;

public enum StoryExecutionStatus { Completed, Cancelled, Failed }

public readonly struct StoryExecutionResult
{
    public StoryExecutionStatus Status { get; }
    public string Error { get; }
    public bool Completed => Status == StoryExecutionStatus.Completed;

    public StoryExecutionResult(StoryExecutionStatus status, string error = null)
    {
        Status = status;
        Error = error;
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
