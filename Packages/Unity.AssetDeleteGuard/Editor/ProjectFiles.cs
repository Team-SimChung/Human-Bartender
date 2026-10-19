using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    internal static class ProjectFiles
    {
        internal static string Root { get { return Path.GetDirectoryName(Application.dataPath); } }

        internal static string Absolute(string assetPath)
        {
            var full = Path.GetFullPath(Path.Combine(Root, assetPath));
            var assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(assets, comparison))
                throw new IOException("Path is outside Assets: " + assetPath);
            return full;
        }

        internal static bool Exists(string assetPath)
        {
            var absolute = Absolute(assetPath);
            return File.Exists(absolute) || Directory.Exists(absolute);
        }

        internal static void RejectLinks(string absolute)
        {
            var current = absolute;
            var stop = Path.GetDirectoryName(Application.dataPath);
            while (!string.IsNullOrEmpty(current) && current != stop)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked paths are not supported for deletion: " + absolute);
                current = Path.GetDirectoryName(current);
            }
        }

        internal static string Hash(IEnumerable<string> values)
        {
            using (var hash = SHA256.Create())
            {
                foreach (var value in values)
                {
                    var bytes = Encoding.UTF8.GetBytes(value.Length + ":" + value);
                    hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return Convert.ToBase64String(hash.Hash);
            }
        }

        internal static string ContentHash(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream));
        }

        internal static string FileStamp(string absolute)
        {
            if (!File.Exists(absolute)) return "missing";
            var file = new FileInfo(absolute);
            return file.Length + ":" + file.LastWriteTimeUtc.Ticks + ":" + (int)file.Attributes;
        }

        // Package assets have virtual paths. Resolve them through the public package API.
        internal static string ResolveReadPath(string path)
        {
            if (path.StartsWith("Assets/", StringComparison.Ordinal)) return Absolute(path);
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (info == null || string.IsNullOrEmpty(info.resolvedPath)) return string.Empty;
                var prefix = "Packages/" + info.name;
                if (!AssetPaths.IsSameOrChild(path, prefix)) return string.Empty;
                return Path.Combine(info.resolvedPath, path.Substring(prefix.Length).TrimStart('/'));
            }
            return string.Empty;
        }

        internal static string[] AssetCatalog()
        {
            return AssetDatabase.GetAllAssetPaths().Where(p =>
                p.StartsWith("Assets/", StringComparison.Ordinal) ||
                p.StartsWith("Packages/", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        internal static string EnvironmentStamp(string[] paths)
        {
            var values = new List<string> { Application.unityVersion, EditorUserBuildSettings.activeBuildTarget.ToString() };
            foreach (var path in paths)
            {
                var absolute = ResolveReadPath(path);
                values.Add(path + "|" + AssetDatabase.AssetPathToGUID(path) + "|" +
                           (string.IsNullOrEmpty(absolute) ? "virtual" : FileStamp(absolute) + "|" + FileStamp(absolute + ".meta")));
            }
            var settings = Path.Combine(Root, "ProjectSettings");
            if (Directory.Exists(settings))
                foreach (var file in Directory.GetFiles(settings).OrderBy(p => p, StringComparer.Ordinal))
                    values.Add(Path.GetFileName(file) + "|" + ContentHash(file));
            foreach (var name in new[] { "manifest.json", "packages-lock.json" })
            {
                var file = Path.Combine(Root, "Packages", name);
                if (File.Exists(file)) values.Add(name + "|" + ContentHash(file));
            }
            return Hash(values);
        }
    }
}
