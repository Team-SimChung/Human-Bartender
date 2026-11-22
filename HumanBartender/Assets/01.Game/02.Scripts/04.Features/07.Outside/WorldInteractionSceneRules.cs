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
}
