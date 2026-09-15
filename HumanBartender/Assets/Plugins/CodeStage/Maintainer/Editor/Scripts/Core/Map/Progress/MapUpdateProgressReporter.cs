#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Progress
{
	using UnityEditor;

    internal class MapUpdateProgressReporter : IProgressReporter
    {
        public bool ShowProgress(int phase, string currentOperation, int currentItem, int totalItems)
        {
            return EditorUtility.DisplayCancelableProgressBar($"Updating Assets Map, phase {phase} of 3",
                string.Format(currentOperation, currentItem, totalItems),
                (float)currentItem / totalItems);
        }

        public void ClearProgress()
        {
            EditorUtility.ClearProgressBar();
        }
    }
} 