using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// <summary>Optional VSE stats, read through the game's stat pipeline so
    /// expertise levels, settings and other stat modifiers are included.</summary>
    [StaticConstructorOnStartup]
    internal static class QualityBonusStats
    {
        // Cache contract — Owner: process. Key: the three VSE stat defNames.
        // Value: borrowed StatDef references (null when absent), stable per def epoch.
        // Dependencies: definition database only. Refresh: startup and explicit
        // ManagedRecipes.Invalidate after definition reload. Equality: unchanged
        // defs retain their references. Teardown: Refresh releases obsolete refs;
        // no pawns, worlds or maps are retained and no game assets are owned.
        private static StatDef? construction;
        private static StatDef? crafting;
        private static StatDef? artistic;

        static QualityBonusStats() => Refresh();

        internal static void Refresh()
        {
            construction = DefDatabase<StatDef>.GetNamedSilentFail("VSE_ConstructQuality");
            crafting = DefDatabase<StatDef>.GetNamedSilentFail("VSE_CraftingQuality");
            artistic = DefDatabase<StatDef>.GetNamedSilentFail("VSE_ArtQuality");
        }

        // Called only while building candidate facts (dispatch/completion or a
        // revision-gated presentation builder). There is no per-pawn cache and
        // no new polling schedule. Default GetStatValue bypasses the temporary
        // stat cache, matching VSE's actual quality roll at completion.
        internal static int For(Pawn pawn, SkillDef? skill)
        {
            StatDef? stat = skill == SkillDefOf.Construction ? construction
                : skill == SkillDefOf.Crafting ? crafting
                : skill == SkillDefOf.Artistic ? artistic : null;
            return stat == null || stat.Worker.IsDisabledFor(pawn)
                ? 0 : QualityBonus.FromStat(pawn.GetStatValue(stat));
        }
    }
}
