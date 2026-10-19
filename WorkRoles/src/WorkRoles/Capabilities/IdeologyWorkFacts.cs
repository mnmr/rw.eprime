using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core.Recs;

namespace WorkRoles.Capabilities
{
    internal static class IdeologyWorkFacts
    {
        internal static string Source(Def def) => def.modContentPack?.PackageIdPlayerFacing ?? "ludeon.rimworld.ideology";

        internal static PreceptDef? Collect(Pawn pawn, List<WorkAversionFact> aversions)
        {
            if (pawn.Ideo == null) return null;
            foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                if (precept.def.opposedWorkTypes != null)
                    foreach (WorkTypeDef workType in precept.def.opposedWorkTypes)
                        aversions.Add(new WorkAversionFact(workType.defName, precept.def.defName, Source(precept.def)));
            return MiningRefusal(pawn);
        }

        internal static PreceptDef? MiningRefusal(Pawn pawn)
        {
            if (pawn.Ideo == null || pawn.Faction == null) return null;
            // Do not use DoerWillingToDo: unknown subclasses include a random
            // willingness roll. This exact base implementation is deterministic
            // and preserves vanilla's faction/trait/hediff exceptions.
            foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                if (precept.def.comps != null)
                    foreach (PreceptComp comp in precept.def.comps)
                        if (comp.GetType() == typeof(PreceptComp_UnwillingToDo)
                            && comp is PreceptComp_UnwillingToDo unwilling
                            && (unwilling.eventDef == null || unwilling.eventDef == HistoryEventDefOf.Mined)
                            && !unwilling.MemberWillingToDo(new HistoryEvent(HistoryEventDefOf.Mined, pawn.Named(HistoryEventArgsNames.Doer))))
                            return precept.def;
            return null;
        }
    }
}
