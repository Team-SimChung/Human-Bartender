using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>바 내부의 날짜별 실제 대사를 ID보다 본문으로 찾을 수 있는 목록.</summary>
internal static class BarDialogueTestCatalog
{
    internal const string MenuPath = "Tools/Human Bartender/Tests/Bar Dialogue Lab";
    internal const string Prefix = "bar-dialogue-test-";

    internal sealed class Line
    {
        internal int Day;
        internal string SceneId;
        internal string SceneTitle;
        internal ENewScenePhase Phase;
        internal NewDialogueStepData Step;
        internal string Key => SceneId + "#" + Step.Seq;
        internal string Text => Step.Text?.Ko ?? "";
    }

    internal static NewDayScriptBase LoadScript(int day)
    {
        if (day < 0 || day > 3) throw new ArgumentOutOfRangeException(nameof(day), "[BarTest] 0~3일차를 선택하세요.");
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        return StoryCsvCompiler.Compile(catalog.Read<NewDayScriptBase>("script/bar/day" + day));
    }

    internal static Line[] LoadLines(int day) => (LoadScript(day).Scenes ?? Array.Empty<NewScriptSceneData>())
        .OrderBy(scene => scene.Phase).ThenBy(scene => scene.Seq).SelectMany(scene =>
            (scene.Steps ?? Array.Empty<NewDialogueStepData>()).Where(step =>
                step.Type is ENewStepType.Say or ENewStepType.Order && !string.IsNullOrWhiteSpace(step.Text?.Ko))
            .Select(step => new Line { Day = day, SceneId = scene.Id, SceneTitle = scene.Title, Phase = scene.Phase, Step = step }))
        .ToArray();

    internal static HomeInteractionTestScenario Scenario(int day)
    {
        if (day < 0 || day > 3) throw new ArgumentOutOfRangeException(nameof(day), "[BarTest] 0~3일차를 선택하세요.");
        // 실제 3일차 진입에 필요한 전날 재료·기억 조건. 각설탕 수령은 대본의 앞 단계에서 준비한다.
        var flags = day == 3 ? new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["gang_wild_dog_received"] = true,
            ["shiba_bought_bitters"] = true,
            ["campaign_observation"] = true
        } : null;
        return new HomeInteractionTestScenario(Prefix + day, "바 내부 대사", "선택한 대사부터 실제 바 대본을 진행합니다.",
            day, EGameFlow.Bar, null, flags: flags, runtimeScene: "Play");
    }

    internal static HomeInteractionTestScenario Parse(string id) =>
        id.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(Prefix.Length), out int day)
            ? Scenario(day) : throw new ArgumentException("[BarTest] 잘못된 시작 상황: " + id);
}
