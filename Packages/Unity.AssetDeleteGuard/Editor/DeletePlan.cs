using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AssetDeleteGuard
{
    internal sealed class DeletePlan
    {
        internal readonly List<string> Roots = new List<string>();
        internal readonly Dictionary<string, string> Targets = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly List<string> DiskPaths = new List<string>();
        internal readonly List<string> Fingerprint = new List<string>();
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();

        internal static DeletePlan Create(IEnumerable<string> selected, string[] catalog)
        {
            var plan = new DeletePlan();
            try
            {
                plan.Roots.AddRange(AssetPaths.ReduceRoots(selected));
                if (plan.Roots.Count == 0) plan.Errors.Add("No assets selected.");
                var known = new HashSet<string>(catalog, StringComparer.Ordinal);
                foreach (var root in plan.Roots)
                {
                    var absolute = ProjectFiles.Absolute(root);
                    if (!ProjectFiles.Exists(root)) throw new IOException("Asset no longer exists: " + root);
                    ProjectFiles.RejectLinks(absolute);
                    if (!known.Contains(root)) throw new IOException("Asset is not imported: " + root);
                    plan.AddDisk(root, absolute, known);
                    if (File.Exists(absolute + ".meta")) plan.AddFile(root + ".meta", absolute + ".meta");
                    else plan.Errors.Add("Missing metadata: " + root + ".meta");
                }
                foreach (var path in catalog.Where(p => plan.Contains(p)))
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) plan.Errors.Add("Asset identity unavailable: " + path);
                    else plan.Targets[path] = guid;
                    if (!File.Exists(ProjectFiles.Absolute(path) + ".meta"))
                        plan.Errors.Add("Missing metadata: " + path + ".meta");
                    var extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".cs" || extension == ".dll" || extension == ".asmdef" || extension == ".asmref" ||
                        extension == ".shader" || extension == ".hlsl" || extension == ".cginc")
                        plan.Warnings.Add("Code/include dependencies are not fully analysed: " + path);
                    if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        AssetPaths.IsSameOrChild(path, "Assets/StreamingAssets"))
                        plan.Warnings.Add("Runtime loading location: " + path);
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                        plan.Warnings.Add("AssetBundle '" + importer.assetBundleName + "': " + path);
                }
                foreach (var duplicate in plan.Targets.GroupBy(p => p.Value).Where(g => g.Count() > 1))
                    plan.Errors.Add("Duplicate target GUID: " + string.Join(", ", duplicate.Select(p => p.Key).ToArray()));
            }
            catch (Exception ex) { plan.Errors.Add(ex.Message); }
            plan.DiskPaths.Sort(StringComparer.Ordinal);
            plan.Fingerprint.Sort(StringComparer.Ordinal);
            return plan;
        }

        internal bool Contains(string path) { return Roots.Any(root => AssetPaths.IsSameOrChild(path, root)); }

        private void AddDisk(string path, string absolute, HashSet<string> known)
        {
            ProjectFiles.RejectLinks(absolute);
            if (Directory.Exists(absolute))
            {
                DiskPaths.Add(path);
                Fingerprint.Add("directory|" + path);
                foreach (var item in Directory.GetFileSystemEntries(absolute).OrderBy(p => p, StringComparer.Ordinal))
                    AddDisk(path + "/" + Path.GetFileName(item), item, known);
            }
            else
            {
                AddFile(path, absolute);
                if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !known.Contains(path))
                    Warnings.Add("Not indexed by Unity; references not inspected: " + path);
            }
        }

        private void AddFile(string path, string absolute)
        {
            ProjectFiles.RejectLinks(absolute);
            DiskPaths.Add(path);
            Fingerprint.Add(path + "|" + ProjectFiles.ContentHash(absolute) + "|" + ProjectFiles.FileStamp(absolute));
            if ((File.GetAttributes(absolute) & FileAttributes.ReadOnly) != 0)
                Errors.Add("Read-only file; resolve permissions or VCS state first: " + path);
        }
    }
}
