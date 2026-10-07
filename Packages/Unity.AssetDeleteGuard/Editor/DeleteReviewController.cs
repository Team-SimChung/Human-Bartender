using System;
using System.Linq;
using UnityEditor;

namespace AssetDeleteGuard
{
    // The window owns this lifetime. No static event subscriptions or serialized approvals.
    internal sealed class DeleteReviewController : IDisposable
    {
        private readonly string[] paths;
        private readonly bool allowDelete;
        private AnalysisSession session;
        private bool disposed;
        internal AnalysisReport Report { get; private set; }
        internal ExecutionResult Execution { get; private set; }
        internal bool Acknowledged { get; private set; }
        internal bool IsScanning { get { return session != null; } }
        internal string Progress { get { return session == null ? "" : session.Progress; } }
        internal bool CanExecute
        {
            get { return !disposed && allowDelete && !IsScanning && Report != null && Report.CanDelete && !Report.Consumed && Acknowledged; }
        }

        internal DeleteReviewController(string[] requested, bool delete)
        {
            paths = (string[])requested.Clone();
            allowDelete = delete;
        }

        internal void Scan()
        {
            if (disposed) return;
            Reset();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            session = new AnalysisSession(paths);
        }

        internal bool Advance()
        {
            if (session == null || disposed) return false;
            var deadline = EditorApplication.timeSinceStartup + .005;
            while (!session.IsDone && EditorApplication.timeSinceStartup < deadline) session.Step();
            if (session.IsDone) CompleteScan();
            return true;
        }

        internal void CancelScan()
        {
            if (session == null) return;
            session.Dispose();
            CompleteScan();
        }

        private void CompleteScan()
        {
            Report = session.Report;
            session.Dispose();
            session = null;
            Acknowledged = false;
        }

        internal void Acknowledge(bool value)
        {
            Acknowledged = !disposed && allowDelete && !IsScanning && Report != null && Report.CanDelete && !Report.Consumed && value;
        }

        internal void Execute(ITrashBackend backend)
        {
            if (!CanExecute) return;
            Acknowledged = false; // Confirmation is consumed even when revalidation rejects the request.
            Execution = DeleteExecutor.Execute(Report, backend);
            if (Execution.UpdatedReport != null) Report = Execution.UpdatedReport;
        }

        internal void Reset()
        {
            if (session != null) session.Dispose();
            session = null;
            Report = null;
            Execution = null;
            Acknowledged = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            Reset();
            disposed = true;
        }
    }
}
