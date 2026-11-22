using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace AssetDeleteGuard
{
    internal static class OpenObjectReferences
    {
        internal static IEnumerable<int> Collect(DeletePlan plan, List<ReferenceEntry> entries,
            List<string> errors)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && plan.Contains(scene.path))
                    errors.Add("Save or close the scene before deleting it: " + scene.path);
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
#if UNITY_2020_1_OR_NEWER
            var stagePath = stage == null ? "" : stage.assetPath;
#else
            var stagePath = stage == null ? "" : stage.prefabAssetPath;
#endif
            if (stage != null && stage.scene.isDirty && plan.Contains(stagePath))
                errors.Add("Save or close the Prefab Stage before deleting it: " + stagePath);

            var objects = Resources.FindObjectsOfTypeAll<Object>();
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                var sourcePath = AssetDatabase.GetAssetPath(obj);
                var persistent = EditorUtility.IsPersistent(obj);
                var component = obj as Component;
                var go = obj as GameObject;
                var sceneSettings = obj is RenderSettings || obj is LightmapSettings;
                if (component != null) go = component.gameObject;
                if (persistent)
                {
                    if (!EditorUtility.IsDirty(obj)) continue;
                    if (plan.Contains(sourcePath))
                    {
                        errors.Add("Save or revert modified asset before deleting: " + sourcePath);
                        continue;
                    }
                    if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal) &&
                        !sourcePath.StartsWith("Packages/", StringComparison.Ordinal)) continue;
                }
                else
                {
                    if (!sceneSettings && (go == null || !go.scene.IsValid() || !go.scene.isLoaded)) continue;
                    sourcePath = sceneSettings ? "" : stage != null && go.scene == stage.scene ? stagePath : go.scene.path;
                    if (plan.Contains(sourcePath)) continue;
                    if (obj is GameObject)
                    {
                        foreach (var item in go.GetComponents<Component>())
                            if (item == null) errors.Add("Missing script in open scene: " + go.name);
                    }
                }
                var label = persistent ? sourcePath + " (modified)" : sceneSettings ? "Loaded scene settings / " + obj.GetType().Name :
                    (string.IsNullOrEmpty(sourcePath) ? "Unsaved scene" : sourcePath) + " / " +
                    HierarchyPath(go.transform) + " / " + obj.GetType().Name;
                Inspect(obj, "session:" + obj.GetInstanceID(), sourcePath, label,
                    "open-objects", ReferenceKind.OpenObject, plan, entries, errors);
                yield return 0;
            }
        }

        internal static void Inspect(Object obj, string id, string sourcePath, string label,
            string provider, ReferenceKind kind, DeletePlan plan, List<ReferenceEntry> entries, List<string> errors)
        {
            try
            {
                var pending = new Queue<KeyValuePair<Object, string>>();
                var visited = new HashSet<int>();
                pending.Enqueue(new KeyValuePair<Object, string>(obj, ""));
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    if (current.Key == null || !visited.Add(current.Key.GetInstanceID())) continue;
                    using (var serialized = new SerializedObject(current.Key))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var value = property.objectReferenceValue;
                            if (value == null) continue;
                            var path = AssetDatabase.GetAssetPath(value);
                            var location = current.Value + property.propertyPath;
                            if (plan.Targets.ContainsKey(path))
                                entries.Add(new ReferenceEntry
                                {
                                    SourceId = id, SourcePath = sourcePath, SourceLabel = label,
                                    TargetPath = path, PropertyPath = location,
                                    ProviderId = provider, Kind = kind
                                });
                            // Scene components are separate roots. Follow embedded objects (e.g. an
                            // unsaved ScriptableObject or instance Material) without walking editor UI.
                            else if (!EditorUtility.IsPersistent(value) && !(value is GameObject) &&
                                     !(value is Component) && !(value is EditorWindow) && !(value is UnityEditor.Editor))
                                pending.Enqueue(new KeyValuePair<Object, string>(value, location + " → "));
                        }
                    }
                }
            }
            catch (Exception ex) { errors.Add("Could not inspect " + label + ": " + ex.Message); }
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
