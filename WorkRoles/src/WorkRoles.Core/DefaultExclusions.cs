using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Shipped recommendation-exclusion defaults are versioned so saves and
    /// exported setups adopt a retired default exactly once.
    ///
    /// Version 1 (1.7.0) excluded Rescuer, Firefighter, Patient, Bedrest and
    /// Laborer. Version 2 (1.7.5) retires all of them: each repeats work Core
    /// or Basics already give every colonist, so the planner never recommends
    /// them anyway, while the exclusion kept robots from Laborer and left a
    /// seeded flag in every save and new-game default file that nobody chose.
    public static class DefaultExclusions
    {
        public const int CurrentVersion = 2;

        /// Template defNames whose seeded exclusion version 2 clears. A role
        /// the player excluded after adopting version 2 is never touched.
        public static readonly IReadOnlyList<string> RetiredTemplates = new[]
        {
            "WS_Rescuer", "WS_Firefighter", "WS_Patient", "WS_Bedrest", "WS_Laborer",
        };

        public static bool IsRetired(string? templateDefName)
        {
            if (string.IsNullOrEmpty(templateDefName)) return false;
            for (int i = 0; i < RetiredTemplates.Count; i++)
                if (string.Equals(RetiredTemplates[i], templateDefName, StringComparison.Ordinal)) return true;
            return false;
        }

        /// Brings a parsed setup up to the current version: a document written
        /// before version 2 loses the retired exclusions; a current document is
        /// returned untouched so later player choices survive. Returns whether
        /// any role changed.
        public static bool ClearRetired(RoleFileDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.exclusionDefaultsVersion >= CurrentVersion) return false;
            bool changed = false;
            foreach (FileRole role in document.roles)
                if (role != null && role.excludedFromRecommendations && IsRetired(role.templateDef))
                {
                    role.excludedFromRecommendations = false;
                    changed = true;
                }
            document.exclusionDefaultsVersion = CurrentVersion;
            return changed;
        }
    }
}
