#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Progress
{
    internal interface IProgressReporter
    {
        bool ShowProgress(int phase, string currentOperation, int currentItem, int totalItems);
        void ClearProgress();
    }
} 