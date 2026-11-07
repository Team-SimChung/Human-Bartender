using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존에 유효했던 시작 방식·범위를 Inspector로 한 번 이전한다. 이후 작성된 설정은 덮어쓰지 않는다.</summary>
public static class WorldInteractionStartSettingsSetup
{
    [MenuItem("Tools/Story/Migrate Interaction Start Settings")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("시작 설정 이전은 편집 모드에서 실행하세요.");
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, CsvDataReader.Folder));
        var previousActive = SceneManager.GetActiveScene();
        foreach (string name in new[] { "Home", "OutSide" })
        {
            string path = "Assets/01.Game/01.Scenes/" + name + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
                throw new InvalidOperationException(name + " 씬의 편집 내용을 먼저 저장하세요.");
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var stories = StoryCsvCompiler.Compile(catalog.Read<NewStreetData>(name == "Home" ? "script/home" : "script/street"))
                    .Scenes ?? Array.Empty<NewSceneData>();
                var definitions = catalog.Read<NewInteractPointData[]>(name == "Home" ? "interact_points_home" : "interact_points_outside");
                bool changed = false;
                foreach (var target in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteractionTarget>(true)))
                    changed |= MigrateTarget(target, stories, definitions);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("시작 설정을 저장하지 못했습니다: " + name);
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }
        Debug.Log("INTERACTION_START_SETTINGS_OK: Home/OutSide의 시작 방식·범위를 Inspector 설정으로 이전했습니다.");
    }

    internal static bool MigrateTarget(WorldInteractionTarget target, NewSceneData[] stories, NewInteractPointData[] definitions)
    {
        if (target.HasAuthoredStartSettings) return false;
        float radius = target.InteractionRadius;
        var mode = WorldInteractionStartMode.Interact;
        var overrides = Array.Empty<WorldInteractionStartOverride>();
        if (target.Content != null)
        {
            var rows = (definitions ?? Array.Empty<NewInteractPointData>())
                .Where(row => row.SourceId == target.Content.SourceId).ToArray();
            if (rows.Length == 0)
                throw new InvalidOperationException("시작 설정을 이전할 Content의 CSV 행이 없습니다: " + target.Content.SourceId);
            // 일반 상호작용을 기본으로 두고, 방송 등 자동 행만 명시적인 Inspector 예외로 옮긴다.
            if (rows.All(row => row.ActivationMode == EActivationMode.Proximity)) mode = WorldInteractionStartMode.Ambient;
            else overrides = rows.Where(row => row.ActivationMode == EActivationMode.Proximity)
                .Select(row => new WorldInteractionStartOverride(row.Id, WorldInteractionStartMode.Ambient)).ToArray();
        }
        else if (!string.IsNullOrWhiteSpace(target.StorySceneId) && !OutsideStorySceneRules.IsNativeEntry(target.StorySceneId))
        {
            var found = (stories ?? Array.Empty<NewSceneData>()).Where(story => story.Id == target.StorySceneId).ToArray();
            if (found.Length == 0) mode = WorldInteractionStartMode.ScriptOnly; // 제거된 BD 판매상 등 기존 비활성 대상.
            else
            {
                if (found.Length != 1) throw new InvalidOperationException("중복 CSV scene ID: " + target.StorySceneId);
                mode = found[0].StartMode switch
                {
                    ENewSceneTrigger.Auto => WorldInteractionStartMode.Automatic,
                    ENewSceneTrigger.Interact => WorldInteractionStartMode.Interact,
                    _ => WorldInteractionStartMode.ScriptOnly
                };
                if (float.IsFinite(found[0].Radius) && found[0].Radius > 0) radius = found[0].Radius;
            }
        }
        target.ConfigureStart(mode, radius, overrides);
        EditorUtility.SetDirty(target);
        return true;
    }
}
