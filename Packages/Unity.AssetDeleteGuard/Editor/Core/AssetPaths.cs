using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetDeleteGuard
{
    public static class AssetPaths
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/').TrimEnd('/');
        }

        public static bool IsSameOrChild(string path, string root)
        {
            return string.Equals(path, root, StringComparison.Ordinal) ||
                   path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        public static bool IsDeletablePath(string path)
        {
            path = Normalize(path);
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                   !path.Split('/').Any(p => p.Length == 0 || p == "." || p == ".." || p.Contains(":"));
        }

        public static string[] ReduceRoots(IEnumerable<string> paths)
        {
            var result = new List<string>();
            foreach (var path in paths.Select(Normalize).Distinct(StringComparer.Ordinal)
                         .OrderBy(p => p.Length).ThenBy(p => p, StringComparer.Ordinal))
            {
                if (!IsDeletablePath(path))
                    throw new ArgumentException("Only project assets below Assets/ can be deleted: " + path);
                if (!result.Any(root => IsSameOrChild(path, root))) result.Add(path);
            }
            return result.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
    }
}
