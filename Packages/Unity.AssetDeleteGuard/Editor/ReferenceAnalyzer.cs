using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    /// <summary>Main-thread, read-only analysis. Call after pending asset imports have finished.</summary>
    public static class ReferenceAnalyzer
    {
        public static AnalysisReport Analyze(IEnumerable<string> paths)
        {
            var session = new AnalysisSession(paths.ToArray());
            while (session.Step()) { }
            return session.Report;
        }
    }

    internal sealed class AnalysisSession : IDisposable
    {
        private readonly string[] requested;
        private readonly List<ReferenceEntry> entries = new List<ReferenceEntry>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();
        private readonly List<string> signatureParts = new List<string>();
        private readonly ReferenceGraph graph = new ReferenceGraph();
        private IEnumerator<int> work;
        private DeletePlan plan;
        private int initialGeneration;
        private int count;
        internal AnalysisReport Report { get; private set; }
        internal string Progress { get; private set; }
        internal bool IsDone { get { return Report != null; } }

        internal AnalysisSession(string[] paths)
        {
            requested = (string[])paths.Clone();
            work = Run().GetEnumerator();
            Progress = "Preparing analysis";
        }

        internal bool Step()
        {
            if (IsDone) return false;
            try
            {
                if (work.MoveNext()) return true;
                Finish(false);
            }
            catch (Exception ex)
            {
                errors.Add("Analysis failed: " + ex.Message);
                Finish(false);
            }
            return false;
        }

        private IEnumerable<int> Run()
        {
            initialGeneration = ProjectChanges.Generation;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Wait for imports/compilation to finish and leave Play Mode.");
            var paths = ProjectFiles.AssetCatalog();
            var environment = ProjectFiles.EnvironmentStamp(paths);
            plan = DeletePlan.Create(requested, paths);
            errors.AddRange(plan.Errors);
            warnings.AddRange(plan.Warnings);
            signatureParts.AddRange(plan.Fingerprint);
            foreach (var target in plan.Targets.OrderBy(p => p.Key, StringComparer.Ordinal))
                signatureParts.Add(target.Key + "|" + target.Value);
            if (plan.Errors.Count != 0) yield break;

            var targets = new HashSet<string>(plan.Targets.Values, StringComparer.Ordinal);
            var guidPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) guidPaths[guid] = path;
            }
            foreach (var path in paths)
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                Progress = (++count) + " / " + paths.Length + "  " + path;
                try
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid))
                    {
                        errors.Add("Source identity unavailable: " + path);
                        continue;
                    }
                    var dependencies = AssetDatabase.GetDependencies(path, false);
                    graph.Replace(guid, dependencies.Select(AssetDatabase.AssetPathToGUID));
                    signatureParts.Add(path + "|" + AssetDatabase.GetAssetDependencyHash(path));
                    if (!plan.Contains(path))
                    {
                        foreach (var dependency in dependencies.Distinct(StringComparer.Ordinal))
                        {
                            if (!plan.Targets.ContainsKey(dependency) || dependency == path) continue;
                            entries.Add(new ReferenceEntry
                            {
                                SourceId = guid, SourcePath = path, SourceLabel = path, TargetPath = dependency,
                                PropertyPath = "", ProviderId = "unity-dependencies", Kind = ReferenceKind.AssetDependency
                            });
                        }
                    }
                }
                catch (Exception ex) { errors.Add("Could not inspect " + path + ": " + ex.Message); }
                yield return 0;
            }
            Progress = "Inspecting open scenes and modified objects";
            foreach (var item in OpenObjectReferences.Collect(plan, entries, errors)) yield return item;
            ProjectUsageReferences.Collect(plan, entries, errors);
            foreach (var provider in ReferenceProviders.Snapshot())
            {
                Progress = "Provider: " + provider.Id;
                signatureParts.Add("provider|" + provider.Id + "|" + provider.Version);
                try
                {
                    var result = provider.Collect(new ReferenceQuery(plan.Targets.Keys.ToArray(), plan.Targets.Values.ToArray()));
                    if (result == null) throw new InvalidOperationException("Provider returned no result.");
                    signatureParts.Add(provider.Id + "|applicable:" + result.Applicable);
                    warnings.AddRange(result.Warnings.Select(w => provider.Id + ": " + w));
                    errors.AddRange(result.Errors.Select(e => provider.Id + ": " + e));
                    foreach (var entry in result.References)
                    {
                        if (entry == null || !plan.Targets.ContainsKey(entry.TargetPath) || string.IsNullOrEmpty(entry.SourceId))
                            throw new InvalidOperationException("Invalid reference from provider.");
                        if (!string.IsNullOrEmpty(entry.SourcePath) && plan.Contains(entry.SourcePath)) continue;
                        entries.Add(new ReferenceEntry
                        {
                            SourceId = entry.SourceId, SourcePath = entry.SourcePath ?? "", SourceLabel = entry.SourceLabel ?? entry.SourceId,
                            TargetPath = entry.TargetPath, PropertyPath = entry.PropertyPath ?? "",
                            ProviderId = provider.Id, Kind = ReferenceKind.Extension
                        });
                    }
                }
                catch (Exception ex) { errors.Add(provider.Id + ": " + ex.Message); }
                yield return 0;
            }
            warnings.Add("Dynamic loading, code/type use and arbitrary path/GUID strings are not fully inspected.");
            warnings.Add("Project settings coverage: build scenes/profiles, registered build configuration objects and preloaded assets. Other settings are not inspected.");
            if (paths.Any(p => p.StartsWith("Packages/com.unity.addressables/", StringComparison.Ordinal)) &&
                !ReferenceProviders.Snapshot().Any(p => p.Id == "addressables"))
                warnings.Add("Addressables is installed: registration and AssetReference strings are NOT inspected without an extension.");
#if UNITY_2022_1_OR_NEWER
            foreach (var entry in entries.Where(e => e.Kind == ReferenceKind.AssetDependency && e.TargetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
            {
                var child = AssetDatabase.LoadAssetAtPath<Material>(entry.SourcePath);
                var parent = AssetDatabase.LoadAssetAtPath<Material>(entry.TargetPath);
                if (child != null && parent != null && child.parent == parent)
                    errors.Add("Material Variant parent deletion requires Unity's re-parenting workflow: " + entry.TargetPath);
            }
#endif
            // A scan publishes only if its catalog and on-disk environment remained stable.
            var finalPaths = ProjectFiles.AssetCatalog();
            var finalEnvironment = ProjectFiles.EnvironmentStamp(finalPaths);
            if (environment != finalEnvironment || initialGeneration != ProjectChanges.Generation)
                errors.Add("Project changed during analysis. Scan again after editing/importing has stopped.");
            signatureParts.Add(finalEnvironment);
            signatureParts.AddRange(entries.Select(e => e.Key).OrderBy(p => p, StringComparer.Ordinal));
            signatureParts.AddRange(warnings.Distinct().OrderBy(p => p, StringComparer.Ordinal));
            var indirect = graph.GetIndirectReferencers(targets).Where(guidPaths.ContainsKey)
                .Select(g => guidPaths[g]).Where(p => !plan.Contains(p)).ToList();
            Finish(false, indirect);
        }

        private void Finish(bool cancelled, List<string> indirect = null)
        {
            if (Report != null) return;
            Report = new AnalysisReport
            {
                State = cancelled ? AnalysisState.Cancelled : errors.Count == 0 ? AnalysisState.Ready : AnalysisState.Partial,
                Roots = (plan == null ? requested.ToList() : plan.Roots).AsReadOnly(),
                TargetPaths = (plan == null ? new List<string>() : plan.Targets.Keys.OrderBy(p => p, StringComparer.Ordinal).ToList()).AsReadOnly(),
                DiskPaths = (plan == null ? new List<string>() : plan.DiskPaths).AsReadOnly(),
                References = entries.GroupBy(e => e.Key).Select(g => g.First()).OrderBy(e => e.SourceLabel, StringComparer.Ordinal)
                    .ThenBy(e => e.TargetPath, StringComparer.Ordinal).ToList().AsReadOnly(),
                IndirectReferencers = (indirect ?? new List<string>()).AsReadOnly(),
                Warnings = warnings.Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList().AsReadOnly(),
                Errors = errors.Distinct().ToList().AsReadOnly(),
                Signature = ProjectFiles.Hash(signatureParts), Generation = ProjectChanges.Generation,
                CompletedUtc = DateTime.UtcNow, UnityVersion = Application.unityVersion, ScannedAssetCount = count
            };
        }

        public void Dispose()
        {
            if (work != null) work.Dispose();
            if (!IsDone) Finish(true);
        }
    }
}
