using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorkRoles.UI
{
    /// Shared translated labels for work jobs. Both the role editor and the
    /// colonist presentation use this cache, so it belongs outside either view.
    internal static class WorkJobLabels
    {
        private static Dictionary<WorkGiverDef, string> giverDisplayCache;

        internal static string GiverDisplayName(WorkGiverDef giver)
        {
            if (giverDisplayCache == null)
                giverDisplayCache = BuildGiverDisplayCache();
            return giverDisplayCache.TryGetValue(giver, out string name)
                ? name
                : (giver.label ?? giver.defName).CapitalizeFirst();
        }

        internal static void InvalidateLanguageCaches()
            => giverDisplayCache = null;

        private static Dictionary<WorkGiverDef, string> BuildGiverDisplayCache()
        {
            var result = new Dictionary<WorkGiverDef, string>();
            foreach (WorkTypeDef type in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var byLabel = new Dictionary<string, List<WorkGiverDef>>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (WorkGiverDef giver in type.workGiversByPriority)
                {
                    string baseName = BaseName(giver);
                    if (!byLabel.TryGetValue(baseName, out List<WorkGiverDef> siblings))
                        byLabel[baseName] = siblings = new List<WorkGiverDef>();
                    siblings.Add(giver);
                }

                foreach (KeyValuePair<string, List<WorkGiverDef>> group in byLabel)
                {
                    int emergencyCount = 0;
                    foreach (WorkGiverDef giver in group.Value)
                        if (giver.emergency) emergencyCount++;

                    bool distinguishEmergency = group.Value.Count > 1
                        && emergencyCount == 1;
                    foreach (WorkGiverDef giver in group.Value)
                        result[giver] = distinguishEmergency && giver.emergency
                            ? group.Key + "WR_EmergencySuffix".Translate()
                            : group.Key;
                }
            }
            return result;
        }

        private static string BaseName(WorkGiverDef giver)
            => (giver.label ?? giver.defName).CapitalizeFirst();
    }
}
