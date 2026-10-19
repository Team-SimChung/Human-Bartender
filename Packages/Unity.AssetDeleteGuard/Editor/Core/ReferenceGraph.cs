using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetDeleteGuard
{
    /// <summary>Direct asset dependencies. Replacing a source also removes its old reverse edges.</summary>
    public sealed class ReferenceGraph
    {
        private readonly Dictionary<string, HashSet<string>> dependencies =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> referencers =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public void Replace(string source, IEnumerable<string> targets)
        {
            Remove(source);
            var next = new HashSet<string>(targets.Where(t => !string.IsNullOrEmpty(t) && t != source),
                StringComparer.Ordinal);
            dependencies[source] = next;
            foreach (var target in next)
            {
                HashSet<string> incoming;
                if (!referencers.TryGetValue(target, out incoming))
                    referencers[target] = incoming = new HashSet<string>(StringComparer.Ordinal);
                incoming.Add(source);
            }
        }

        public void Remove(string source)
        {
            HashSet<string> previous;
            if (!dependencies.TryGetValue(source, out previous)) return;
            foreach (var target in previous)
            {
                var incoming = referencers[target];
                incoming.Remove(source);
                if (incoming.Count == 0) referencers.Remove(target);
            }
            dependencies.Remove(source);
        }

        public string[] GetReferencers(string target)
        {
            HashSet<string> incoming;
            return referencers.TryGetValue(target, out incoming)
                ? incoming.OrderBy(p => p, StringComparer.Ordinal).ToArray() : new string[0];
        }

        public string[] GetIndirectReferencers(IEnumerable<string> targets)
        {
            var selected = new HashSet<string>(targets, StringComparer.Ordinal);
            var direct = new HashSet<string>(selected.SelectMany(GetReferencers), StringComparer.Ordinal);
            var visited = new HashSet<string>(selected, StringComparer.Ordinal);
            var pending = new Queue<string>(selected);
            while (pending.Count > 0)
            {
                foreach (var source in GetReferencers(pending.Dequeue()))
                    if (visited.Add(source)) pending.Enqueue(source);
            }
            visited.ExceptWith(selected);
            visited.ExceptWith(direct);
            return visited.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
    }
}
