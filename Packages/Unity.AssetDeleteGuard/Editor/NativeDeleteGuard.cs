using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    // One setting owner. User/project local: importing the package never edits ProjectSettings.
    internal static class NativeDeleteSettings
    {
        private static readonly string Key = "AssetDeleteGuard.NativeDelete." + Application.dataPath;
        private static bool enabled = EditorPrefs.GetBool(Key, true);
        internal static bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                EditorPrefs.SetBool(Key, value);
                if (!value) NativeDeleteBridge.CancelPending();
            }
        }
    }

    // Exact reviewed disk inventory, never a global bypass or a path-prefix permit.
    // Owned only by DeleteExecutor for the synchronous backend call, including exceptions.
    internal sealed class ApprovedDeletionScope : IDisposable
    {
        private static ApprovedDeletionScope current;
        private readonly HashSet<string> paths;
        private bool disposed;

        private ApprovedDeletionScope(IEnumerable<string> inventory)
        {
            paths = new HashSet<string>(inventory.Select(AssetPaths.Normalize), StringComparer.Ordinal);
        }

        internal static ApprovedDeletionScope Begin(IEnumerable<string> inventory)
        {
            if (current != null) throw new InvalidOperationException("A deletion permit is already active.");
            return current = new ApprovedDeletionScope(inventory);
        }

        internal static bool Contains(string path)
        {
            return current != null && current.paths.Contains(AssetPaths.Normalize(path));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(current, this)) current = null;
            paths.Clear();
        }
    }

    // This queue is a set of blocked paths, NOT an inferred native transaction or approval.
    // No AssetDatabase calls, window creation, or deletion occur in the delete callback.
    [InitializeOnLoad]
    internal static class NativeDeleteBridge
    {
        private static readonly HashSet<string> pending = new HashSet<string>(StringComparer.Ordinal);
        internal static int PendingCount { get { return pending.Count; } }

        static NativeDeleteBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelPending;
            EditorApplication.quitting += CancelPending;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static bool ShouldBlock(string path, bool interactive, bool enabled)
        {
            return interactive && enabled && AssetPaths.IsDeletablePath(path) && !ApprovedDeletionScope.Contains(path);
        }

        internal static AssetDeleteResult Intercept(string path)
        {
            if (!ShouldBlock(path, !Application.isBatchMode, NativeDeleteSettings.Enabled))
                return AssetDeleteResult.DidNotDelete;
            pending.Add(AssetPaths.Normalize(path));
            EditorApplication.delayCall -= OpenPending;
            EditorApplication.delayCall += OpenPending;
            return AssetDeleteResult.FailedDelete;
        }

        private static void OpenPending()
        {
            EditorApplication.delayCall -= OpenPending;
            if (!NativeDeleteSettings.Enabled || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CancelPending();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenPending;
                return;
            }
            var paths = AssetPaths.ReduceRoots(pending);
            pending.Clear(); // Transfer ownership before opening a window; never retain a replay request.
            if (paths.Length > 0) DeleteReviewWindow.Open(paths, true, true);
        }

        internal static void CancelPending()
        {
            EditorApplication.delayCall -= OpenPending;
            pending.Clear();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state) { CancelPending(); }
    }

    internal sealed class NativeDeleteProcessor : AssetModificationProcessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            return NativeDeleteBridge.Intercept(assetPath);
        }
    }
}
