using System;
using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace Implanner
{
    internal static class CompatibilityWarnings
    {
        // Reuse the automation gate's established catalog. Detection,
        // acknowledgement and presentation belong to the shared welcome.
        // Build only at the welcome lifecycle boundary, never during drawing.
        internal static WelcomeConflictNotice Current()
        {
            var conflicts = new WelcomeModConflict[PlannerAutomation.LevelModCount];
            for (int i = 0; i < conflicts.Length; i++)
            {
                string packageId = PlannerAutomation.LevelModPackageIdAt(i);
                conflicts[i] = new WelcomeModConflict(packageId,
                    "IMP_CompatibilityLevelMod".Translate(ModName(packageId)));
            }
            return WelcomeDialog.FindConflicts(conflicts);
        }

        private static string ModName(string packageId)
        {
            var mods = LoadedModManager.RunningModsListForReading;
            for (int i = 0; i < mods.Count; i++)
                if (string.Equals(mods[i].PackageIdPlayerFacing, packageId,
                        StringComparison.OrdinalIgnoreCase))
                    return mods[i].Name;
            return packageId;
        }
    }
}
