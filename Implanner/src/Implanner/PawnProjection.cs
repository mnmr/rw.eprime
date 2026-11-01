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
            IReadOnlyList<ImplantGoal> goals, bool away, int minQuality, string planBody)
        {
            Project(pawn, goals, minQuality, planBody,
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
            int minQuality, string planBody)
        {
            Project(pawn, goals, minQuality, planBody,
                out List<InstalledImplant> installed,
                out ImplantContext[] implantContexts);
            return PlanEvaluator.MissingImplantSlotKeys(
                goals, installed, implantContexts,
                ImplantConflicts.SameSlotExclusive, model.KindsExclusiveDelegate);
        }

        /// The shared projection prologue: both evaluation entry points must
        /// feed PlanEvaluator identical inputs.
        private static void Project(Pawn pawn, IReadOnlyList<ImplantGoal> goals,
            int minQuality, string planBody,
            out List<InstalledImplant> installed, out ImplantContext[] contexts)
        {
            installed = BuildInstalledImplants(pawn);
            // A plan pinned to another body (or to one that is not loaded)
            // has no target on this pawn: every slot reads as missing
            // anatomy, so nothing is counted, reserved or scheduled.
            if (!PlanBodies.Accepts(planBody, pawn))
            {
                contexts = new ImplantContext[goals.Count];
                for (int i = 0; i < goals.Count; i++)
                    contexts[i] = new ImplantContext(new string?[goals[i].Slots.Count], 1f);
                return;
            }
            // Each slot judged on THIS pawn's body (SlotApplicability): a
            // slot under an installed artificial part, or under a part
            // another goal of the plan replaces on this body, is outside
            // the pawn's target. The facts are the conflict cache's
            // per-body records, so record indices agree with the slot keys.
            int body = Catalogs.PlannableBodies().IndexOf(pawn.RaceProps.body);
            SlotBlock[][]? blocks = null;
            string?[][]? blockers = null;
            if (body >= 0)
            {
                var facts = new PlannedSlotFacts?[goals.Count][];
                blocks = new SlotBlock[goals.Count][];
                blockers = new string?[goals.Count][];
                for (int i = 0; i < goals.Count; i++)
                {
                    ImplantGoal goal = goals[i];
                    facts[i] = new PlannedSlotFacts?[goal.Slots.Count];
                    blocks[i] = new SlotBlock[goal.Slots.Count];
                    blockers[i] = new string?[goal.Slots.Count];
                    for (int j = 0; j < goal.Slots.Count; j++)
                        facts[i][j] = ImplantConflicts.FactsOn(
                            goal.ImplantDefName, goal.Slots[j], body);
                }
                SlotApplicability.Resolve(facts, ArtificialParts(pawn), blocks, blockers);
            }
            contexts = new ImplantContext[goals.Count];
            for (int i = 0; i < goals.Count; i++)
                contexts[i] = BuildImplantContext(pawn, goals[i], minQuality,
                    blocks?[i], blockers?[i]);
        }

        /// The pawn's installed artificial parts (the game's own test,
        /// HediffSet.HasDirectlyAddedPartFor: a Hediff_AddedPart on the
        /// record), as anatomy record index plus implant kind.
        private static List<ArtificialPart> ArtificialParts(Pawn pawn)
        {
            var result = new List<ArtificialPart>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            BodyDef body = pawn.RaceProps.body;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.Part == null || !(hediff is Hediff_AddedPart)) continue;
                result.Add(new ArtificialPart(
                    body.GetIndexOfPart(hediff.Part), hediff.def.defName));
            }
            return result;
        }

        /// The lowest item quality this plan accepts for the entry: the
        /// plan's minimum for an item that carries a quality, any otherwise.
        internal static int PlanMinimumFor(ImplantCatalogEntry entry, int planMinQuality) =>
            ImplantQualities.HasQuality(entry.Def.spawnThingOnRemoved)
                ? planMinQuality
                : ImplantQuality.Lowest;

        /// The lowest item quality this slot takes (ImplantQuality
        /// .MinimumAcceptable): the plan's minimum, raised until the
        /// implant leaves the part no worse than it can recover to, so an implant
        /// worse than a healthy part never replaces it but may replace a
        /// missing or damaged one. ImplantQuality.None when no quality
        /// qualifies. Reconcile passes and snapshot builders only.
        internal static int MinimumAcceptableQuality(Pawn pawn,
            ImplantCatalogEntry entry, BodyPartRecord part, int planMinQuality)
        {
            return ImplantQuality.MinimumAcceptable(ImplantQualities.AfterInstall(entry),
                EfficiencyFloor(pawn, entry, part), PlanMinimumFor(entry, planMinQuality));
        }

        internal static float EfficiencyFloor(Pawn pawn, ImplantCatalogEntry entry, BodyPartRecord part) =>
            entry.IsReplacement || entry.WipesPart ? RecoverablePartEfficiency.Of(pawn, part) : 1f;

        internal static int AcceptableQualities(Pawn pawn, ImplantCatalogEntry entry,
            BodyPartRecord part, int planMinQuality, float retainedFloor = 0f) =>
            ImplantQuality.AcceptableQualities(ImplantQualities.AfterInstall(entry),
                Math.Max(retainedFloor, EfficiencyFloor(pawn, entry, part)),
                PlanMinimumFor(entry, planMinQuality));

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
            Pawn pawn, ImplantCatalogEntry entry, PartAddress slot)
        {
            ThingDef? item = entry.Def.spawnThingOnRemoved;
            if (item == null || entry.UpgradesFrom == null) return item;
            BodyPartRecord? part = ResolveSlotPart(pawn, entry, slot);
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

        /// The pawn body part a goal slot's address denotes: the address's
        /// occurrence among the pawn body's records of that part kind; null
        /// when the body lacks it (or the address is not one of the
        /// implant's slots). Allocation-free: tick path.
        internal static BodyPartRecord? ResolveSlotPart(
            Pawn pawn, ImplantCatalogEntry entry, PartAddress slot)
        {
            CatalogSlot? known = entry.SlotOf(slot);
            if (known == null) return null;
            List<BodyPartRecord> records = pawn.RaceProps.body.GetPartsWithDef(known.Part);
            return slot.Occurrence < records.Count ? records[slot.Occurrence] : null;
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

        /// The pawn's anatomy instance for each goal slot, parallel to the
        /// goal's slots: the record its part address denotes on this body
        /// (ResolveSlotPart), keyed like InstalledImplant.SlotKey; null
        /// where the body lacks it. blocks/blockers carry the per-pawn
        /// applicability verdicts (null = anatomy only).
        private static ImplantContext BuildImplantContext(Pawn pawn, ImplantGoal goal,
            int minQuality, SlotBlock[]? blocks, string?[]? blockers)
        {
            var slots = new string?[goal.Slots.Count];
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
            if (entry == null)
            {
                // Temporarily missing mod content: no applicable anatomy, so
                // the whole request surfaces as blocked.
                return new ImplantContext(slots, 1f);
            }
            BodyDef body = pawn.RaceProps.body;
            for (int j = 0; j < slots.Length; j++)
            {
                BodyPartRecord? part = ResolveSlotPart(pawn, entry, goal.Slots[j]);
                if (part != null) slots[j] = body.GetIndexOfPart(part).ToStringCached();
            }
            // The substitution floor is the goal's implant as automation
            // would install it on a healthy part: the plan's minimum
            // quality, raised until it is no worse than natural (an Awful
            // bionic arm is never the bar a prosthetic has to clear).
            float[] byQuality = ImplantQualities.AfterInstall(entry);
            int floorMinimum = PlanMinimumFor(entry, minQuality);
            int floorQuality = ImplantQuality.MinimumAcceptable(byQuality, 1f, floorMinimum);
            return new ImplantContext(slots, byQuality[
                floorQuality != ImplantQuality.None ? floorQuality : floorMinimum],
                blocks, blockers);
        }
    }
}
