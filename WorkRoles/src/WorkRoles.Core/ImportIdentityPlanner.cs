using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    public readonly struct ImportIdentitySource
    {
        public ImportIdentitySource(string label, string preferredIdentity)
        {
            Label = label;
            PreferredIdentity = preferredIdentity;
        }

        public string Label { get; }
        public string PreferredIdentity { get; }
    }

    public readonly struct ImportIdentityExisting
    {
        public ImportIdentityExisting(string label, string preferredIdentity)
        {
            Label = label;
            PreferredIdentity = preferredIdentity;
        }

        public string Label { get; }
        public string PreferredIdentity { get; }
    }

    public readonly struct ImportIdentityDecision
    {
        public ImportIdentityDecision(int existingIndex, string displayLabel)
        {
            ExistingIndex = existingIndex;
            DisplayLabel = displayLabel;
        }

        public int ExistingIndex { get; }
        public string DisplayLabel { get; }
    }

    /// Plans document rows without depending on game objects. Preferred
    /// identities (role template defs) match first; legacy labels then consume
    /// the next unclaimed runtime object. New labels and matched renames are
    /// uniquified against both the runtime catalog and earlier planned rows.
    public static class ImportIdentityPlanner
    {
        public static IReadOnlyList<ImportIdentityDecision> Plan(
            IReadOnlyList<ImportIdentitySource> imports,
            IReadOnlyList<ImportIdentityExisting> existing,
            bool discardUnmatchedExistingLabels = false)
        {
            int importCount = imports?.Count ?? 0;
            int existingCount = existing?.Count ?? 0;
            var claimed = new bool[existingCount];
            var matches = new int[importCount];
            for (int importIndex = 0; importIndex < importCount; importIndex++)
            {
                ImportIdentitySource source = imports[importIndex];
                int match = FindPreferred(source, existing, claimed);
                if (match < 0) match = FindLabel(source, existing, claimed);
                matches[importIndex] = match;
                if (match >= 0) claimed[match] = true;
            }

            var reservedLabels = new List<string>(existingCount + importCount);
            if (!discardUnmatchedExistingLabels)
            {
                for (int i = 0; i < existingCount; i++)
                    Reserve(existing[i].Label, reservedLabels);
            }
            else
            {
                // Overwrite deletes every unmatched object before applying rows.
                // Reserve only matched labels that deliberately remain unchanged;
                // renamed targets are staged out of the namespace by the adapter.
                for (int importIndex = 0; importIndex < importCount; importIndex++)
                {
                    int match = matches[importIndex];
                    if (match >= 0 && SameLabel(
                            imports[importIndex].Label, existing[match].Label))
                        Reserve(existing[match].Label, reservedLabels);
                }
            }

            var result = new ImportIdentityDecision[importCount];
            for (int importIndex = 0; importIndex < importCount; importIndex++)
            {
                ImportIdentitySource source = imports[importIndex];
                int match = matches[importIndex];
                if (match >= 0)
                {
                    string displayLabel = source.Label?.Trim();
                    bool unchangedLegacyDuplicate = SameLabel(
                        displayLabel, existing[match].Label);
                    if (!unchangedLegacyDuplicate)
                    {
                        displayLabel = CatalogNameRules.Unique(
                            displayLabel, reservedLabels, label => label);
                        if (displayLabel != null) reservedLabels.Add(displayLabel);
                    }
                    result[importIndex] = new ImportIdentityDecision(
                        match, displayLabel);
                    continue;
                }

                string unique = CatalogNameRules.Unique(
                    source.Label, reservedLabels, label => label);
                if (unique != null) reservedLabels.Add(unique);
                result[importIndex] = new ImportIdentityDecision(-1, unique);
            }
            return result;
        }

        private static bool SameLabel(string first, string second) =>
            string.Equals(first?.Trim(), second?.Trim(),
                StringComparison.OrdinalIgnoreCase);

        private static void Reserve(string label, List<string> reservedLabels)
        {
            if (!string.IsNullOrWhiteSpace(label)) reservedLabels.Add(label.Trim());
        }

        private static int FindPreferred(ImportIdentitySource source,
            IReadOnlyList<ImportIdentityExisting> existing, bool[] claimed)
        {
            if (string.IsNullOrEmpty(source.PreferredIdentity) || existing == null)
                return -1;
            for (int i = 0; i < existing.Count; i++)
                if (!claimed[i] && string.Equals(
                        existing[i].PreferredIdentity, source.PreferredIdentity,
                        StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static int FindLabel(ImportIdentitySource source,
            IReadOnlyList<ImportIdentityExisting> existing, bool[] claimed)
        {
            if (string.IsNullOrWhiteSpace(source.Label) || existing == null)
                return -1;
            string label = source.Label.Trim();
            for (int i = 0; i < existing.Count; i++)
                if (!claimed[i] && string.Equals(
                        existing[i].Label?.Trim(), label,
                        StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }
    }
}
