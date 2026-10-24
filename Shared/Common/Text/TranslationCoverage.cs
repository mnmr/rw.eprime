using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>Coverage of translated def paths during one language load.</summary>
    public sealed class TranslationCoverage
    {
        // Temporary builder state, discarded after one language package merge;
        // never published or retained by rendering or a world/store cache.
        private readonly Dictionary<string, bool> paths =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public void Add(string path, bool fullList)
        {
            paths[NormalizeFields(path)] = fullList;
        }

        public bool Covers(string path, bool fullList)
        {
            path = NormalizeFields(path);
            if (paths.ContainsKey(path)) return true;
            int separator = path.LastIndexOf('.');
            while (separator >= 0)
            {
                string parent = path.Substring(0, separator);
                if (paths.TryGetValue(parent, out bool parentIsList) && parentIsList) return true;
                separator = separator == 0 ? -1 : path.LastIndexOf('.', separator - 1);
            }
            if (fullList)
            {
                string prefix = path + ".";
                foreach (KeyValuePair<string, bool> existing in paths)
                    if (existing.Key.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string NormalizeFields(string path)
        {
            // Def names are case-sensitive; Verse field lookup is not.
            int separator = path.IndexOf('.');
            return separator < 0 ? path : path.Substring(0, separator)
                + path.Substring(separator).ToLowerInvariant();
        }
    }
}
