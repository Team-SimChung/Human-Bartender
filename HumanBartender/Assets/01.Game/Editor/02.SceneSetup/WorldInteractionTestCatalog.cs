using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장된 게임 씬의 실제 대상과 CSV 조건을 읽는다. 씬을 수정하거나 별도 대본을 만들지 않는다.</summary>
internal static class WorldInteractionTestCatalog
{
    internal const string MenuPath = "Tools/Human Bartender/Tests/Game Interaction Lab";
    internal const string ScenarioPrefix = "world|";
    internal static readonly string[] Scenes = { "Home", "OutSide" };

    [Serializable]
    internal sealed class Point
    {
        public string SceneName;
        public string Key;
        public string Name;
        public string HierarchyPath;
    }

    internal static string Key(WorldInteractionTarget target) => target.Content != null
        ? "source:" + target.Content.SourceId : "scene:" + target.StorySceneId;

    internal static WorldInteractionTarget[] Targets(Scene scene) => !scene.IsValid() || !scene.isLoaded
        ? Array.Empty<WorldInteractionTarget>()
        : scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true))
            .OrderBy(Key, StringComparer.Ordinal).ThenBy(value => value.name, StringComparer.Ordinal).ToArray();

    internal static WorldInteractionTarget FindTarget(Scene scene, string key)
    {
        var matches = Targets(scene).Where(target => Key(target) == key).ToArray();
        if (matches.Length > 1) throw new InvalidOperationException("[WorldTest] 중복된 지점 ID: " + key);
        return matches.SingleOrDefault();
    }

    internal static Point[] LoadPoints()
    {
        var points = new List<Point>();
        foreach (string sceneName in Scenes)
        {
            var preview = EditorSceneManager.OpenPreviewScene("Assets/01.Game/01.Scenes/" + sceneName + ".unity");
            try
            {
                foreach (var target in Targets(preview))
                    points.Add(new Point { SceneName = sceneName, Key = Key(target), Name = target.name,
                        HierarchyPath = HierarchyPath(target.transform) });
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        return points.ToArray();
    }

    private static string HierarchyPath(Transform target) => target.parent == null ? target.name
        : HierarchyPath(target.parent) + "/" + target.name;

    internal static string ScenarioId(string sceneName, int day, EGameFlow flow)
    {
        if (!Scenes.Contains(sceneName) || day < 0 || day > 3 ||
            flow != EGameFlow.CommuteIn && flow != EGameFlow.CommuteOut)
            throw new ArgumentException("[WorldTest] Home/OutSide, 0~3일차, 출근/퇴근을 선택하세요.");
        return $"{ScenarioPrefix}{sceneName}|{day}|{(int)flow}";
    }

    internal static HomeInteractionTestScenario ParseScenario(string id)
    {
        string[] parts = id.Split('|');
        if (parts.Length != 4 || parts[0] != "world" || !int.TryParse(parts[2], out int day) ||
            !int.TryParse(parts[3], out int phase))
            throw new ArgumentException("[WorldTest] 잘못된 테스트 상황: " + id);
        var flow = (EGameFlow)phase;
        ScenarioId(parts[1], day, flow);
        return new HomeInteractionTestScenario(id, parts[1] + " 지점 테스트",
            "실제 게임 지점을 날짜·출퇴근 조건으로 확인합니다.", day, flow, null, runtimeScene: parts[1]);
    }

    internal static NewInteractPointData[] LoadDefinitions() => CsvDataReader.LoadDirectory(
        Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder)).Read<NewInteractPointData[]>("interact_points");

    internal static NewSceneData[] LoadScripts(string sceneName)
    {
        if (!Scenes.Contains(sceneName)) throw new ArgumentException("[WorldTest] 지원하지 않는 씬: " + sceneName);
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        string source = sceneName == "Home" ? "script/home" : "script/street";
        return StoryCsvCompiler.Compile(catalog.Read<NewStreetData>(source)).Scenes
            .Where(story => !string.IsNullOrWhiteSpace(story.Id) && story.Steps?.Length > 0)
            .OrderBy(story => story.Id, StringComparer.Ordinal).ToArray();
    }

    internal static string[] LoadFlagNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Application.streamingAssetsPath,
            CsvDataReader.Folder), "*.csv", SearchOption.AllDirectories))
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\bflag\.([A-Za-z0-9_]+)"))
                names.Add(match.Groups[1].Value);
        return names.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
