using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core
{
    /// Skills each vanilla/DLC non-bill giver actually uses for work
    /// performance. This is an audited code-defined-job baseline, separate
    /// from XP because use and training are different facts. Every XP-baseline
    /// giver has an entry here, including exact empty entries; consumers must
    /// not treat an empty list as permission to infer parent-work-type skills.
    public static class VanillaUsedSkillBaseline
    {
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>>
            UsedByGiver = Build();

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> Build()
        {
            // Decompiled-job audit: used == trained for every covered direct
            // giver except these explicit performance-stat dependencies. Copy
            // every array so the two public baselines do not share mutable
            // values.
            var result = new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, string[]> pair in VanillaXpBaseline.XpByGiver)
                result.Add(pair.Key, Array.AsReadOnly((string[])pair.Value.Clone()));

            result["HunterHunt"] = Array.AsReadOnly(
                new[] { "Shooting", "Animals" });
            result["ActivitySuppression"] = Array.AsReadOnly(
                new[] { "Intellectual", "Social" });
            return new ReadOnlyDictionary<string, IReadOnlyList<string>>(result);
        }
    }
}
