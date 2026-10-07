using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AssetDeleteGuard
{
    internal interface ITrashBackend
    {
        bool Move(string[] roots, List<string> failed);
    }

    internal sealed class UnityTrashBackend : ITrashBackend
    {
        public bool Move(string[] roots, List<string> failed)
        {
#if UNITY_2020_2_OR_NEWER
            return AssetDatabase.MoveAssetsToTrash(roots, failed);
#else
            for (var i = 0; i < roots.Length; i++)
            {
                if (AssetDatabase.MoveAssetToTrash(roots[i])) continue;
                failed.AddRange(roots.Skip(i));
                return false;
            }
            return true;
#endif
        }
    }

    internal static class DeleteExecutor
    {
        private static bool executing;

        // UI confirmation belongs to the coordinator. This entry is internal for fixture tests.
        internal static ExecutionResult Execute(AnalysisReport reviewed, ITrashBackend backend)
        {
            var result = new ExecutionResult();
            if (executing || reviewed == null || !reviewed.CanDelete || reviewed.Consumed)
            {
                result.Error = "This report is not valid for deletion. Scan again.";
                return result;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                result.Error = "Wait for compilation/importing and leave Play Mode.";
                return result;
            }
            executing = true;
            var attemptedDeletion = false;
            try
            {
                // Import pending external changes, then rebuild the entire report, including new referencers.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var fresh = ReferenceAnalyzer.Analyze(reviewed.Roots);
                if (!fresh.CanDelete || fresh.Signature != reviewed.Signature)
                {
                    result.UpdatedReport = fresh;
                    result.Error = "Project or references changed. Review the new report before deleting.";
                    return result;
                }
                if (fresh.Generation != ProjectChanges.Generation)
                {
                    result.Error = "Project changed immediately before deletion. Scan again.";
                    return result;
                }

                reviewed.Consumed = true;
                var failed = new List<string>();
                var success = false;
                attemptedDeletion = true;
                try
                {
                    using (ApprovedDeletionScope.Begin(fresh.DiskPaths))
                        success = backend.Move(fresh.Roots.ToArray(), failed);
                }
                catch (Exception ex) { result.Error = "Deletion interrupted: " + ex.Message; }

                foreach (var root in fresh.Roots)
                {
                    try
                    {
                        var remaining = fresh.DiskPaths.Where(p =>
                            (AssetPaths.IsSameOrChild(p, root) || p == root + ".meta") && ProjectFiles.Exists(p)).ToArray();
                        var rootPresent = ProjectFiles.Exists(root) || File.Exists(ProjectFiles.Absolute(root) + ".meta");
                        var removed = !rootPresent && remaining.Length == 0;
                        result.Items.Add(new DeletionItemResult
                        {
                            Path = root,
                            Status = removed ? "Deleted" : "Failed or partially deleted",
                            Detail = removed ? "Asset and metadata are absent. Trash retention depends on OS/VCS." :
                                "Remaining: " + string.Join(", ", remaining.Take(8).ToArray()) +
                                (failed.Contains(root) ? " (Unity rejected this path)" : "")
                        });
                    }
                    catch (Exception ex)
                    {
                        result.Items.Add(new DeletionItemResult { Path = root, Status = "Unknown", Detail = ex.Message });
                    }
                }
                if (!success && string.IsNullOrEmpty(result.Error))
                    result.Error = "Unity reported a deletion failure. Some files may have been removed; no automatic retry was performed.";
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                executing = false;
                if (attemptedDeletion) ProjectChanges.Invalidate();
            }
        }
    }
}
