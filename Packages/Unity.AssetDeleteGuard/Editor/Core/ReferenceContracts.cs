using System;
using System.Collections.Generic;

namespace AssetDeleteGuard
{
    public enum AnalysisState { Building, Ready, Partial, Failed, Cancelled }
    public enum ReferenceKind { AssetDependency, OpenObject, ProjectSetting, Extension }

    [Serializable]
    public sealed class ReferenceEntry
    {
        public string SourceId;
        public string SourcePath;
        public string SourceLabel;
        public string TargetPath;
        public string PropertyPath;
        public string ProviderId;
        public ReferenceKind Kind;

        // Provider-local source IDs share this collision-free identity everywhere in the UI.
        public string SourceKey
        {
            get { var provider = ProviderId ?? ""; return provider.Length + ":" + provider + SourceId; }
        }

        public string Key
        {
            get
            {
                return ProviderId + "\n" + SourceId + "\n" + TargetPath + "\n" +
                       PropertyPath + "\n" + (int)Kind;
            }
        }
    }

    public sealed class ReferenceQuery
    {
        public readonly string[] TargetPaths;
        public readonly string[] TargetGuids;
        public ReferenceQuery(string[] paths, string[] guids)
        {
            TargetPaths = (string[])paths.Clone();
            TargetGuids = (string[])guids.Clone();
        }
    }

    public sealed class ProviderResult
    {
        public readonly List<ReferenceEntry> References = new List<ReferenceEntry>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public bool Applicable = true;
    }

    /// <summary>
    /// Called on the editor main thread, including immediately before deletion.
    /// Implementations must be synchronous, read-only and deterministic; never modify or save assets.
    /// A provider must return an error instead of returning incomplete results as complete.
    /// </summary>
    public interface IReferenceProvider
    {
        string Id { get; }
        string Version { get; }
        ProviderResult Collect(ReferenceQuery query);
    }
}
