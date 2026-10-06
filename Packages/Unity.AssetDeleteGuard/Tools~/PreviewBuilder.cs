using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard.Development
{
    // Copied only into the disposable staging project. Not included in customer archives.
    public static class PreviewBuilder
    {
        private const string Demo = "Assets/AssetDeleteGuard/Samples/ReferenceDemo/";

        public static void VerifyAndExport()
        {
            EditorApplication.delayCall += RunAfterImports;
        }

        private static void RunAfterImports()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunAfterImports;
                return;
            }
            try
            {
                var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (!File.Exists(Path.Combine(root, ".asset-delete-guard-preview")))
                    throw new InvalidOperationException("Requires the isolated preview project.");
                var report = ReferenceAnalyzer.Analyze(new[] { Demo + "Target.asset" });
                if (!report.CanDelete) throw new InvalidOperationException(string.Join("\n", report.Errors.ToArray()));
                if (!report.References.Any(r => r.SourcePath == Demo + "DirectReferencer.asset"))
                    throw new InvalidOperationException("Sample direct reference missing.");
                if (!report.IndirectReferencers.Contains(Demo + "IndirectReferencer.asset"))
                    throw new InvalidOperationException("Sample indirect reference missing.");
                var arguments = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(arguments, "-adgExportPath");
                if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("Missing export path.");
                var destination = Path.GetFullPath(arguments[index + 1]);
                if (File.Exists(destination)) throw new IOException("Refusing to overwrite an existing archive.");
                AssetDatabase.ExportPackage("Assets/AssetDeleteGuard", destination, ExportPackageOptions.Recurse);
                File.WriteAllText(Path.Combine(root, "preview-verification.json"),
                    "{\"unityVersion\":\"" + Application.unityVersion + "\",\"sampleDirect\":true,\"sampleIndirect\":true,\"exported\":true}");
                Debug.Log("Asset Delete Guard preview sample validation and export passed.");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void OpenDemo()
        {
            EditorApplication.delayCall += SelectDemo;
        }

        [MenuItem("Tools/Asset Delete Guard Preview/Select Demo Target")]
        private static void SelectDemo()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += SelectDemo;
                return;
            }
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(Demo + "Target.asset");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
