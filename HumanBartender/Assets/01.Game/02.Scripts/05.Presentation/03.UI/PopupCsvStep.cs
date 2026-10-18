using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>바와 거리 대본이 공유하는 CSV 팝업 명령 규칙.</summary>
public static class PopupCsvStep
{
    static readonly Dictionary<string, PopupType> Types = new Dictionary<string, PopupType>(StringComparer.OrdinalIgnoreCase)
    {
        { "ingredient_acquired", PopupType.IngredientAcquired },
        { "character_registered", PopupType.CharacterRegistered },
        { "affinity_increased", PopupType.AffinityIncreased },
        { "affinity_decreased", PopupType.AffinityDecreased },
        { "new_objective", PopupType.NewObjective },
        { "objective_completed", PopupType.ObjectiveCompleted },
        { "event_occurred", PopupType.EventOccurred }
    };

    public static bool TryParseType(string arg, out PopupType type)
    {
        type = default;
        return arg != null && Types.TryGetValue(arg.Trim(), out type);
    }

    public static string Validate(string arg, string sync, string data)
    {
        if (!TryParseType(arg, out var type)) return "Unknown popup kind: " + arg;
        if (!string.IsNullOrWhiteSpace(sync) && !string.Equals(sync, "nowait", StringComparison.OrdinalIgnoreCase))
            return "Popup sync must be nowait (or empty). Popup never waits for dismissal.";
        if ((type == PopupType.IngredientAcquired || type == PopupType.AffinityIncreased || type == PopupType.AffinityDecreased)
            && string.IsNullOrWhiteSpace(data)) return "Ingredient/affinity popup requires text data.";
        return null;
    }

    public static async UniTask ExecuteAsync(IPopupService service, string arg, string data, string sync,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        string error = Validate(arg, sync, data);
        if (error != null) throw new InvalidOperationException(error);
        if (service == null) throw new InvalidOperationException("Popup service is not connected.");
        TryParseType(arg, out var type);
        if (!await service.Popup(type, data ?? ""))
            throw new InvalidOperationException("Popup could not start: " + arg);
        token.ThrowIfCancellationRequested();
    }
}
