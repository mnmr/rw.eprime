using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    public sealed class WelcomeModConflict
    {
        public string PackageId { get; }
        public string Description { get; }
        public WelcomeModConflict(string packageId, string description)
        { PackageId = packageId.ToLowerInvariant(); Description = description; }
    }

    /// Immutable, window-bound conflict selection. No live mod metadata escapes
    /// the builder. The caller supplies descriptions already translated.
    public sealed class WelcomeConflictNotice
    {
        private readonly WelcomeModConflict[] conflicts;
        public int Count => conflicts.Length;
        public WelcomeModConflict this[int index] => conflicts[index];
        private WelcomeConflictNotice(WelcomeModConflict[] conflicts) { this.conflicts = conflicts; }

        public static WelcomeConflictNotice ForActivePackages(
            IReadOnlyList<WelcomeModConflict> configured, IReadOnlyList<string> active)
        {
            var found = new List<WelcomeModConflict>();
            for (int i = 0; i < configured.Count; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < found.Count; j++)
                    if (found[j].PackageId == configured[i].PackageId) { duplicate = true; break; }
                if (duplicate) continue;
                for (int j = 0; j < active.Count; j++)
                    if (string.Equals(configured[i].PackageId, active[j], StringComparison.OrdinalIgnoreCase))
                    { found.Add(configured[i]); break; }
            }
            return new WelcomeConflictNotice(found.ToArray());
        }

        public string Format(string heading, string advice)
        {
            if (conflicts.Length == 0) return string.Empty;
            var text = new System.Text.StringBuilder(heading);
            for (int i = 0; i < conflicts.Length; i++)
            {
                if (text.Length > 0) text.Append("\n\n");
                text.Append(conflicts[i].Description);
            }
            if (advice.Length > 0) text.Append("\n\n").Append(advice);
            return text.ToString();
        }

        public bool HasUnseen(string saveId, IReadOnlyList<string> seen)
        {
            for (int i = 0; i < conflicts.Length; i++)
                if (!Contains(seen, saveId + ":" + conflicts[i].PackageId)) return true;
            return false;
        }

        public void MarkSeen(string saveId, List<string> seen)
        {
            for (int i = 0; i < conflicts.Length; i++)
            {
                string key = saveId + ":" + conflicts[i].PackageId;
                if (!seen.Contains(key)) seen.Add(key);
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (values[i] == value) return true;
            return false;
        }
    }
}
