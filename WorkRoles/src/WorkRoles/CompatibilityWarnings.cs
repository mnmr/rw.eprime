using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace WorkRoles
{
    internal static class CompatibilityWarnings
    {
        // These are the established incompatibilities in README.md and
        // About/About.xml. Other work/skill integrations are intentionally
        // excluded. Build only at the welcome lifecycle boundary; detection,
        // acknowledgement and immutable presentation belong to Shared.
        internal static WelcomeConflictNotice Current() => WelcomeDialog.FindConflicts(new[]
        {
            new WelcomeModConflict("fluffy.worktab",
                "WR_CompatibilityWorkTab".Translate("Work Tab")),
            new WelcomeModConflict("arof.fluffy.worktab",
                "WR_CompatibilityWorkTab".Translate("Work Tab")),
            new WelcomeModConflict("arof.fluffy.worktab.continued",
                "WR_CompatibilityWorkTab".Translate("Work Tab")),
            new WelcomeModConflict("natee.enhancedworktab",
                "WR_CompatibilityWorkTab".Translate("Enhanced Work Tab")),
            new WelcomeModConflict("coolnether123.betterworktab",
                "WR_CompatibilityWorkTab".Translate("Better Work Tab")),
            new WelcomeModConflict("freemapa.freewill",
                "WR_CompatibilityFreeWill".Translate()),
            new WelcomeModConflict("lauriichan.prioritymaster",
                "WR_CompatibilityPriorityMaster".Translate())
        });
    }
}
