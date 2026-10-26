using RimShared.Common;
using RimShared.UiLib;
using Verse;

namespace QualityJobs
{
    internal static class CompatibilityWarnings
    {
        // Quality Jobs owns only the evidence-backed catalog. Detection,
        // acknowledgement and presentation are shared with every welcome host.
        internal static WelcomeConflictNotice Current() => WelcomeDialog.FindConflicts(new[]
        {
            new WelcomeModConflict("hatti.qualitybuilder", "QJ_CompatibilityQualityBuilder".Translate()),
            new WelcomeModConflict("doug.nojobauthors", "QJ_CompatibilityNoJobAuthors".Translate()),
            new WelcomeModConflict("xandrmoro.rim.finishit", "QJ_CompatibilityFinishIt".Translate())
        });
    }
}
