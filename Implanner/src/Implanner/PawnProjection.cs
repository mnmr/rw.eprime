using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
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
        /// model supplies the option-driven kind exclusivity (mod
        /// compatibility) as its once-allocated delegate, beside the
        /// definition-derived same-slot gate. minQuality is the assigned
        /// plan's minimum item quality: a different installed kind stands in
        /// for a goal only when it is at least as good as the goal's implant
        /// at that quality.
        internal static PlanEvaluation Evaluate(PlannerModel model, Pawn pawn,
            IReadOnlyList<ImplantGoal> goals, bool away, int minQuality)
        {
            Project(pawn, goals, minQuality,
                out List<InstalledImplant> installed,
                out ImplantContext[] implantContexts);
            return PlanEvaluator.Evaluate(
                goals, installed, implantContexts, away,
                ImplantConflicts.SameSlotExclusive, model.KindsExclusiveDelegate);
        }

        /// The implant slots surgery automation still has to deliver for this
        /// pawn (unblocked, not satisfied by the evaluator's one-to-one
        /// matching).
        internal static List<string> MissingImplantSlotKeys(
            PlannerModel model, Pawn pawn, IReadOnlyList<ImplantGoal> goals,
            int minQuality)
        {
            Project(pawn, goals, minQuality,
                out List<InstalledImplant> installed,
                out ImplantContext[] implantContexts);
            return PlanEvaluator.MissingImplantSlotKeys(
                goals, installed, implantContexts,
                ImplantConflicts.SameSlotExclusive, model.KindsExclusiveDelegate);
        }

        /// The shared projection prologue: both evaluation entry points must
        /// feed PlanEvaluator identical inputs.
        private static void Project(Pawn pawn, IReadOnlyList<ImplantGoal> goals,
            int minQuality,
            out List<InstalledImplant> installed, out ImplantContext[] contexts)
        {
            installed = BuildInstalledImplants(pawn);
            contexts = new ImplantContext[goals.Count];
            for (int i = 0; i < goals.Count; i++)
                contexts[i] = BuildImplantContext(pawn, goals[i], minQuality);
        }

        /// The lowest item quality this plan accepts for the entry: the
        /// plan's minimum for an item that carries a quality, any otherwise.
        internal static int PlanMinimumFor(ImplantCatalogEntry entry, int planMinQuality) =>
            ImplantQualities.HasQuality(entry.Def.spawnThingOnRemoved)
                ? planMinQuality
                : ImplantQuality.Lowest;

        /// The lowest item quality this slot takes (ImplantQuality
        /// .MinimumAcceptable): the plan's minimum, raised until the
        /// implant leaves the part no worse than it is now, so an implant
        /// worse than a healthy part never replaces it but may replace a
        /// missing or damaged one. ImplantQuality.None when no quality
        /// qualifies. Reconcile passes and snapshot builders only.
        internal static int MinimumAcceptableQuality(Pawn pawn,
            ImplantCatalogEntry entry, BodyPartRecord part, int planMinQuality)
        {
            float current = entry.IsReplacement
                ? PawnCapacityUtility.CalculatePartEfficiency(pawn.health.hediffSet, part)
                : 1f;
            return ImplantQuality.MinimumAcceptable(ImplantQualities.AfterInstall(entry),
                current, PlanMinimumFor(entry, planMinQuality));
        }

        /// The pawn facts the ASAP candidate ranking reads: move speed,
        /// whether the equipped weapon is a melee weapon, and Intellectual
        /// plus Crafting. Sampled once per pawn per reconcile pass or
        /// snapshot build, never per work item.
        internal static SurgeryCandidate CandidateOf(Pawn pawn)
        {
            SkillRecord? intellectual = pawn.skills?.GetSkill(SkillDefOf.Intellectual);
            SkillRecord? crafting = pawn.skills?.GetSkill(SkillDefOf.Crafting);
            return new SurgeryCandidate(
                pawn.GetStatValue(StatDefOf.MoveSpeed),
                pawn.equipment?.Primary?.def.IsMeleeWeapon ?? false,
                (intellectual?.Level ?? 0) + (crafting?.Level ?? 0));
        }

        /// The implant item a goal slot still needs delivered, or null when
        /// none: an in-place upgrade whose base (or any kind below it in
        /// the chain) already sits on the slot part needs only the upgrade
        /// surgery's ordinary ingredients. Shared by reservation, surgery
        /// release, production demand, and the strip so they never
        /// disagree. Allocation-free: reached from the reconcile tick path.
        internal static ThingDef? RequiredItem(
            Pawn pawn, ImplantCatalogEntry entry, int ordinal)
        {
            ThingDef? item = entry.Def.spawnThingOnRemoved;
            if (item == null || entry.UpgradesFrom == null) return item;
            BodyPartRecord? part = ResolveSlotPart(pawn, entry, ordinal);
            if (part == null) return item;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part != part || hediff.def == entry.Def) continue;
                if (Catalogs.UpgradeChainContains(entry, hediff.def.defName))
                    return null;
            }
            return item;
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

        /// Whether an installed hediff kind is an implant Implanner tracks:
        /// the game's own implant flag, or any catalog kind regardless of
        /// the flag (Bionic modularity's modules set
        /// countsAsAddedPartOrImplant false to avoid doubled mood effects,
        /// yet they are surgeries the plan installs and must count as
        /// delivered). The same filter gates the facts revision
        /// (Patch_PawnFacts), so evaluation and invalidation agree.
        /// Dictionary lookup only: reached from hediff add/remove patches.
        internal static bool IsTrackedImplant(HediffDef def) =>
            def.countsAsAddedPartOrImplant
            || Catalogs.ImplantByDefName(def.defName) != null;

        private static List<InstalledImplant> BuildInstalledImplants(Pawn pawn)
        {
            var result = new List<InstalledImplant>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            BodyDef body = pawn.RaceProps.body;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part == null || !IsTrackedImplant(hediff.def))
                    continue;
                result.Add(new InstalledImplant(
                    hediff.def.defName,
                    body.GetIndexOfPart(hediff.Part).ToStringCached(),
                    ImplantQualities.InstalledEfficiency(hediff)));
            }
            return result;
        }

        /// The canonical slot enumeration: FixedParts order, then body record
        /// order. Goal slot ordinals index this list; it must stay in
        /// lockstep with Catalogs.BuildSlotLabels, which enumerates the same
        /// way on the reference body for the editor.
        private static ImplantContext BuildImplantContext(Pawn pawn, ImplantGoal goal,
            int minQuality)
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
            // The substitution floor is the goal's implant as automation
            // would install it on a healthy part: the plan's minimum
            // quality, raised until it is no worse than natural (an Awful
            // bionic arm is never the bar a prosthetic has to clear).
            float[] byQuality = ImplantQualities.AfterInstall(entry);
            int floorMinimum = PlanMinimumFor(entry, minQuality);
            int floorQuality = ImplantQuality.MinimumAcceptable(byQuality, 1f, floorMinimum);
            return new ImplantContext(slots, byQuality[
                floorQuality != ImplantQuality.None ? floorQuality : floorMinimum]);
        }
    }
}
