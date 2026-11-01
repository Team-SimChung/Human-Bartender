using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    internal static class ProjectUsageReferences
    {
        internal static void Collect(DeletePlan plan, List<ReferenceEntry> entries, List<string> errors)
        {
#if UNITY_6000_0_OR_NEWER
            AddScenes(EditorBuildSettings.globalScenes, "Build Settings / Global Scenes", "", plan, entries);
#else
            AddScenes(EditorBuildSettings.scenes, "Build Settings", "", plan, entries);
#endif
            foreach (var name in EditorBuildSettings.GetConfigObjectNames())
            {
                UnityEngine.Object obj;
                if (!EditorBuildSettings.TryGetConfigObject(name, out obj) || obj == null)
                {
                    errors.Add("Could not read registered build configuration: " + name);
                    continue;
                }
                var path = AssetDatabase.GetAssetPath(obj);
                if (plan.Targets.ContainsKey(path))
                    entries.Add(new ReferenceEntry
                    {
                        SourceId = "config:" + name, SourcePath = "", SourceLabel = "Build configuration: " + name,
                        TargetPath = path, PropertyPath = "configObjects", ProviderId = "project-usage",
                        Kind = ReferenceKind.ProjectSetting
                    });
                if (!plan.Contains(path))
                    OpenObjectReferences.Inspect(obj, "config:" + name, path, "Build configuration: " + name,
                        "project-usage", ReferenceKind.ProjectSetting, plan, entries, errors);
            }
            var preloaded = PlayerSettings.GetPreloadedAssets();
            for (var i = 0; i < preloaded.Length; i++)
            {
                if (preloaded[i] == null) continue;
                var path = AssetDatabase.GetAssetPath(preloaded[i]);
                if (plan.Targets.ContainsKey(path))
                    entries.Add(new ReferenceEntry
                    {
                        SourceId = "player-settings", SourcePath = "", SourceLabel = "Player Settings / Preloaded Assets",
                        TargetPath = path, PropertyPath = "preloadedAssets[" + i + "]", ProviderId = "project-usage",
                        Kind = ReferenceKind.ProjectSetting
                    });
            }
#if UNITY_6000_0_OR_NEWER
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (plan.Contains(path)) continue;
                var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(path);
                if (profile == null) { errors.Add("Could not read Build Profile: " + path); continue; }
                AddScenes(profile.scenes, "Build Profile: " + path +
                    (profile.overrideGlobalScenes ? " (override)" : " (override inactive)"), path, plan, entries);
            }
#endif
        }

        private static void AddScenes(EditorBuildSettingsScene[] scenes, string label, string source,
            DeletePlan plan, List<ReferenceEntry> entries)
        {
            if (scenes == null) return;
            for (var i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (!plan.Targets.ContainsKey(scene.path)) continue;
                entries.Add(new ReferenceEntry
                {
                    SourceId = "scenes:" + label, SourcePath = source,
                    SourceLabel = label + (scene.enabled ? " / enabled" : " / disabled"), TargetPath = scene.path,
                    PropertyPath = "scenes[" + i + "]", ProviderId = "project-usage", Kind = ReferenceKind.ProjectSetting
                });
            }
        }
    }
}
