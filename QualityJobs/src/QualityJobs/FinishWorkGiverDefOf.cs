using RimWorld;
using Verse;

namespace QualityJobs
{
    [DefOf]
    internal static class FinishWorkGiverDefOf
    {
        public static WorkGiverDef ConstructFinishFrames = null!;

        static FinishWorkGiverDefOf()
            => DefOfHelper.EnsureInitializedInCtor(typeof(FinishWorkGiverDefOf));
    }
}
