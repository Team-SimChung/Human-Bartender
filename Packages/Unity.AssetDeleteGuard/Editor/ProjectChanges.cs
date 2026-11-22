using UnityEditor;

namespace AssetDeleteGuard
{
    [InitializeOnLoad]
    internal static class ProjectChanges
    {
        internal static int Generation { get; private set; }
        static ProjectChanges()
        {
            EditorApplication.projectChanged += Invalidate;
            EditorApplication.hierarchyChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            Undo.postprocessModifications += OnUndo;
            EditorApplication.playModeStateChanged += state => Invalidate();
        }

        internal static void Invalidate() { unchecked { Generation++; } }

        private static UndoPropertyModification[] OnUndo(UndoPropertyModification[] changes)
        {
            Invalidate();
            return changes;
        }
    }

    internal sealed class ReferenceImportObserver : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            ProjectChanges.Invalidate();
        }
    }
}
