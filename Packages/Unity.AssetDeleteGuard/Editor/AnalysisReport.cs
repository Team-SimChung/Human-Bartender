using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssetDeleteGuard
{
    public sealed class AnalysisReport
    {
        public AnalysisState State { get; internal set; }
        public ReadOnlyCollection<string> Roots { get; internal set; }
        public ReadOnlyCollection<string> TargetPaths { get; internal set; }
        public ReadOnlyCollection<string> DiskPaths { get; internal set; }
        public ReadOnlyCollection<ReferenceEntry> References { get; internal set; }
        public ReadOnlyCollection<string> IndirectReferencers { get; internal set; }
        public ReadOnlyCollection<string> Warnings { get; internal set; }
        public ReadOnlyCollection<string> Errors { get; internal set; }
        public string UnityVersion { get; internal set; }
        public DateTime CompletedUtc { get; internal set; }
        public int ScannedAssetCount { get; internal set; }
        public bool CanDelete { get { return State == AnalysisState.Ready && Roots.Count > 0; } }
        internal string Signature;
        internal int Generation;
        internal bool Consumed;

        internal AnalysisReport()
        {
            Roots = new List<string>().AsReadOnly();
            TargetPaths = new List<string>().AsReadOnly();
            DiskPaths = new List<string>().AsReadOnly();
            References = new List<ReferenceEntry>().AsReadOnly();
            IndirectReferencers = new List<string>().AsReadOnly();
            Warnings = new List<string>().AsReadOnly();
            Errors = new List<string>().AsReadOnly();
        }
    }

    public sealed class DeletionItemResult
    {
        public string Path { get; internal set; }
        public string Status { get; internal set; }
        public string Detail { get; internal set; }
    }

    internal sealed class ExecutionResult
    {
        public readonly List<DeletionItemResult> Items = new List<DeletionItemResult>();
        public AnalysisReport UpdatedReport;
        public string Error;
    }
}
