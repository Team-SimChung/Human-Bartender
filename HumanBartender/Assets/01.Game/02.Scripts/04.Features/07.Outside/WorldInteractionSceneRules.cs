using System.Collections.Generic;

/// <summary>외부·홈의 CSV 진입 조건을 한곳에서 판단함.</summary>
internal static class WorldInteractionSceneRules
{
    internal static bool IsEligible(NewSceneData scene, string runtimeScene, int day,
        EGameFlow flow, IConditionUtil conditions)
    {
        if (day < 0 || day > 3 || scene.RuntimeScene != runtimeScene) return false;
        if (scene.Day.HasValue && scene.Day.Value != day) return false;
        string route = flow == EGameFlow.CommuteIn ? "in" : "out";
        if (scene.Route != "both" && scene.Route != route) return false;
        if (!conditions.CheckRequired(scene.When)) return false;
        return string.IsNullOrEmpty(scene.CompletionFlag) ||
            !conditions.CheckRequired("flag." + scene.CompletionFlag);
    }

    /// <summary>
    /// 취침 때 이어 갈 대본을 고른다. 웹(day-flow.js playSleepScenes)처럼 그날의 테라스 대본 하나만 재생하고,
    /// 다른 수동 Home 대본은 취침 흐름에서 재생하지 않는다. 테라스 대본도 진입 조건(날짜·퇴근·완료 플래그)을 만족해야 한다.
    /// </summary>
    internal static bool TrySelectSleepStory(IReadOnlyDictionary<string, NewSceneData> stories, string terraceStoryId,
        string runtimeScene, int day, EGameFlow flow, IConditionUtil conditions, out NewSceneData story)
    {
        story = default;
        if (stories == null || string.IsNullOrEmpty(terraceStoryId) || !stories.TryGetValue(terraceStoryId, out var candidate))
            return false;
        if (candidate.StartMode != ENewSceneTrigger.Manual || !IsEligible(candidate, runtimeScene, day, flow, conditions))
            return false;
        story = candidate;
        return true;
    }
}
