using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    // GUI-only resources. No Texture2D allocations, project state, or deletion authority.
    internal sealed class ReviewTheme
    {
        internal readonly Color Background, Panel, Line, Accent, Muted, Selected;
        internal readonly GUIStyle Title, Heading, Number, Small, Body;
        internal ReviewTheme()
        {
            var dark = EditorGUIUtility.isProSkin;
            Background = dark ? new Color(.12f, .14f, .17f) : new Color(.92f, .94f, .96f);
            Panel = dark ? new Color(.17f, .19f, .22f) : Color.white;
            Line = dark ? new Color(.26f, .29f, .33f) : new Color(.79f, .83f, .87f);
            Accent = dark ? new Color(.42f, .80f, .91f) : new Color(.02f, .36f, .55f);
            Muted = dark ? new Color(.68f, .73f, .79f) : new Color(.33f, .39f, .45f);
            Selected = dark ? new Color(.19f, .31f, .38f) : new Color(.82f, .91f, .96f);
            Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 21 };
            Heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, clipping = TextClipping.Clip };
            Number = new GUIStyle(EditorStyles.boldLabel) { fontSize = 23 };
            Small = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            Small.normal.textColor = Muted;
            Body = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };
        }

        internal void Card(Rect rect)
        {
            EditorGUI.DrawRect(rect, Line);
            EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Panel);
        }

        internal static string DisplayName(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : Path.GetFileName(path);
        }
    }

    internal static class ReferenceLocator
    {
        internal static void Locate(ReferenceEntry entry)
        {
            UnityEngine.Object obj = null;
            int instanceId;
            if (entry.ProviderId == "open-objects" && entry.Kind == ReferenceKind.OpenObject &&
                entry.SourceId.StartsWith("session:", StringComparison.Ordinal) && int.TryParse(entry.SourceId.Substring(8), out instanceId))
#if UNITY_6000_3_OR_NEWER
                obj = EditorUtility.EntityIdToObject((EntityId)instanceId);
#else
                obj = EditorUtility.InstanceIDToObject(instanceId);
#endif
            if (obj == null && !string.IsNullOrEmpty(entry.SourcePath)) obj = AssetDatabase.LoadMainAssetAtPath(entry.SourcePath);
            if (obj == null) return;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        internal static void Locate(string path)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) return;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }

    internal static class ReviewReportExporter
    {
        [Serializable]
        private sealed class ExportData
        {
            public string unityVersion, state, completedUtc;
            public string[] roots, targets, diskPaths, warnings, errors, indirect;
            public ReferenceEntry[] references;
        }

        internal static void Write(AnalysisReport report, string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(new ExportData
            {
                unityVersion = report.UnityVersion, state = report.State.ToString(), completedUtc = report.CompletedUtc.ToString("o"),
                roots = report.Roots.ToArray(), targets = report.TargetPaths.ToArray(), diskPaths = report.DiskPaths.ToArray(),
                warnings = report.Warnings.ToArray(), errors = report.Errors.ToArray(), indirect = report.IndirectReferencers.ToArray(),
                references = report.References.ToArray()
            }, true));
        }
    }
}
