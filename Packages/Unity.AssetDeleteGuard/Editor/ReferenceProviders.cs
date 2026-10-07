using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetDeleteGuard
{
    public static class ReferenceProviders
    {
        private static readonly Dictionary<string, IReferenceProvider> providers =
            new Dictionary<string, IReferenceProvider>(StringComparer.Ordinal);

        public static void Register(IReferenceProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.Id) || string.IsNullOrEmpty(provider.Version))
                throw new ArgumentException("A provider needs a stable ID and version.");
            if (providers.ContainsKey(provider.Id)) throw new InvalidOperationException("Duplicate provider ID: " + provider.Id);
            providers.Add(provider.Id, provider);
            ProjectChanges.Invalidate();
        }

        public static void Unregister(string id)
        {
            if (providers.Remove(id)) ProjectChanges.Invalidate();
        }

        internal static IReferenceProvider[] Snapshot()
        {
            return providers.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
