using System;
using System.Collections.Generic;
using Implanner.Core;
using Verse;

namespace Implanner
{
    /// Projects live pawn state into Core evaluation inputs. Callers supply
    /// the plan's effective goal list (PlannerModel.EffectiveImplants) so
    /// inherited base-plan goals evaluate identically to own goals. Builder
    /// path only: called from snapshot builders behind their invalidation
    /// gates, never during rendering.
    internal static class PawnProjection
    {
        internal static PlanEvaluation Evaluate(Pawn pawn,
            IReadOnlyList<ImplantGoal> goals, bool away)
        {
            Project(pawn, goals,
                out List<InstalledImplant> installed,
                out ImplantContext[] implantContexts);
            return PlanEvaluator.Evaluate(
                goals, installed, implantContexts, away,
                ImplantConflicts.SameSlotExclusive);
        }

        /// The implant slots surgery automation still has to deliver for this
        /// pawn (unblocked, not satisfied by the evaluator's one-to-one
        /// matching).
        internal static List<string> MissingImplantSlotKeys(
            Pawn pawn, IReadOnlyList<ImplantGoal> goals)
        {
            Project(pawn, goals,
                out List<InstalledImplant> installed,
                out ImplantContext[] implantContexts);
            return PlanEvaluator.MissingImplantSlotKeys(
                goals, installed, implantContexts,
                ImplantConflicts.SameSlotExclusive);
        }

        /// The shared projection prologue: both evaluation entry points must
        /// feed PlanEvaluator identical inputs.
        private static void Project(Pawn pawn, IReadOnlyList<ImplantGoal> goals,
            out List<InstalledImplant> installed, out ImplantContext[] contexts)
        {
            installed = BuildInstalledImplants(pawn);
            contexts = new ImplantContext[goals.Count];
            for (int i = 0; i < goals.Count; i++)
                contexts[i] = BuildImplantContext(pawn, goals[i]);
        }

        /// The pawn body part a goal slot ordinal denotes, following the same
        /// canonical enumeration as BuildImplantContext; null when the pawn's
        /// body lacks that slot.
        internal static BodyPartRecord? ResolveSlotPart(
            Pawn pawn, ImplantCatalogEntry entry, int ordinal)
        {
            BodyDef body = pawn.RaceProps.body;
            int index = 0;
            for (int p = 0; p < entry.FixedParts.Count; p++)
            {
                List<BodyPartRecord> records = body.GetPartsWithDef(entry.FixedParts[p]);
                if (records == null) continue;
                for (int r = 0; r < records.Count; r++)
                {
                    if (index == ordinal) return records[r];
                    index++;
                }
            }
            return null;
        }

        private static List<InstalledImplant> BuildInstalledImplants(Pawn pawn)
        {
            var result = new List<InstalledImplant>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            BodyDef body = pawn.RaceProps.body;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part == null || !hediff.def.countsAsAddedPartOrImplant)
                    continue;
                result.Add(new InstalledImplant(
                    hediff.def.defName,
                    body.GetIndexOfPart(hediff.Part).ToStringCached(),
                    hediff.def.addedPartProps?.partEfficiency ?? 1f));
            }
            return result;
        }

        /// The canonical slot enumeration: FixedParts order, then body record
        /// order. Goal slot ordinals index this list; it must stay in
        /// lockstep with Catalogs.BuildSlotLabels, which enumerates the same
        /// way on the reference body for the editor.
        private static ImplantContext BuildImplantContext(Pawn pawn, ImplantGoal goal)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
            if (entry == null)
            {
                // Temporarily missing mod content: no applicable anatomy, so
                // the whole request surfaces as blocked.
                return new ImplantContext(Array.Empty<string>(), 1f);
            }
            var slots = new List<string>();
            BodyDef body = pawn.RaceProps.body;
            for (int p = 0; p < entry.FixedParts.Count; p++)
            {
                List<BodyPartRecord> records = body.GetPartsWithDef(entry.FixedParts[p]);
                if (records == null) continue;
                for (int r = 0; r < records.Count; r++)
                    slots.Add(body.GetIndexOfPart(records[r]).ToStringCached());
            }
            return new ImplantContext(slots, entry.Efficiency);
        }
    }
}
