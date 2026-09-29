using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Read-only scene/prefab reference snapshot for verifying asset relocations.</summary>
public static class ProjectAssetConnectionSnapshot
{
    static readonly string[] VendorRoots = {
        "Assets/Plugins/", "Assets/Spine/", "Assets/Spine Examples/", "Assets/TextMesh Pro/",
        "Assets/ConsolePro/", "Assets/Wingman/", "Assets/Hierarchy Designer/", "Assets/SpriteGlow/"
    };

    public static void Capture()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-assetSnapshot");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("Pass -assetSnapshot followed by an output JSON path.");
        string output = args[index + 1];
        var assetRecords = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var objectRecords = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var errors = new List<string>();
        string[] paths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                && !VendorRoots.Any(v => p.StartsWith(v, StringComparison.Ordinal))
                && !AssetDatabase.IsValidFolder(p))
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();

        foreach (string path in paths)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;
            string[] dependencies = AssetDatabase.GetDependencies(path, true)
                .Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g) && g != guid)
                .Distinct().OrderBy(g => g, StringComparer.Ordinal).ToArray();
            assetRecords[guid] = new { path, dependencies };
        }

        foreach (string path in paths.Where(p => p.EndsWith(".prefab", StringComparison.Ordinal)))
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                CaptureHierarchy(root, AssetDatabase.AssetPathToGUID(path), objectRecords);
            }
            catch (Exception ex) { errors.Add(path + ": " + ex.GetType().Name + ": " + ex.Message); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }

        foreach (string path in paths.Where(p => p.EndsWith(".unity", StringComparison.Ordinal)))
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string guid = AssetDatabase.AssetPathToGUID(path);
                foreach (GameObject root in scene.GetRootGameObjects())
                    CaptureHierarchy(root, guid, objectRecords);
            }
            catch (Exception ex) { errors.Add(path + ": " + ex.GetType().Name + ": " + ex.Message); }
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonConvert.SerializeObject(new {
            unityVersion = Application.unityVersion, assets = assetRecords, objects = objectRecords, errors
        }, Formatting.Indented));
        Debug.Log($"[AssetSnapshot] {assetRecords.Count} assets, {objectRecords.Count} objects, {errors.Count} errors -> {output}");
        if (errors.Count != 0) throw new InvalidOperationException("Asset snapshot could not inspect every scene/prefab; see report.");
    }

    static void CaptureHierarchy(GameObject root, string ownerGuid, IDictionary<string, object> output)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            string hierarchy = Hierarchy(transform);
            Component[] components = transform.GetComponents<Component>();
            output[ownerGuid + "/" + hierarchy] = new {
                active = transform.gameObject.activeSelf,
                missingScripts = components.Count(c => c == null),
                components = components.Where(c => c != null).Select(c => c.GetType().FullName).ToArray()
            };
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;
                var references = new SortedDictionary<string, string>(StringComparer.Ordinal);
                using (var serialized = new SerializedObject(component))
                {
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object value = property.objectReferenceValue;
                        references[property.propertyPath] = value == null
                            ? (property.objectReferenceInstanceIDValue == 0 ? "null" : "missing")
                            : ReferenceIdentity(value);
                    }
                }
                output[ownerGuid + "/" + hierarchy + "/component:" + i] = references;
            }
        }
    }

    static string ReferenceIdentity(Object value)
    {
        if (EditorUtility.IsPersistent(value)
            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id))
            return guid + ":" + id + ":" + value.GetType().FullName;
        if (value is GameObject go) return "local:" + Hierarchy(go.transform) + ":GameObject";
        if (value is Component component)
            return "local:" + Hierarchy(component.transform) + ":" + component.GetType().FullName;
        return "transient:" + value.GetType().FullName + ":" + value.name;
    }

    static string Hierarchy(Transform transform)
    {
        string path = transform.GetSiblingIndex() + ":" + transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.GetSiblingIndex() + ":" + transform.name + "/" + path;
        }
        return path;
    }
}
