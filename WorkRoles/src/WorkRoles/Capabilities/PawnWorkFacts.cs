using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core.Recs;

namespace WorkRoles.Capabilities
{
    /// Collects explicit contracts only. Job search methods are deliberately
    /// absent: they can mutate jobs, reservations, bills and random state.
    internal static class PawnWorkFacts
    {
        private sealed class Entry
        {
            internal PawnWorkEvidence Evidence = PawnWorkEvidence.Empty;
            internal bool[] Capacities = Array.Empty<bool>();
            internal HediffDef[] Nullifiers = Array.Empty<HediffDef>();
            internal bool[] HasNullifiers = Array.Empty<bool>();
            internal int ViviAvailability;
        }

        // Owner: world. Key: pawn identity. Value: detached immutable evidence
        // plus its private capacity observations. Dependencies: pawn race, disabled work,
        // required-capacity booleans, current precepts, faction, nullifying
        // traits/hediffs, audited Vivi gathering-stat positivity (including
        // equipped straw/brush offsets), and definitions. Refresh: capture and matching events,
        // never periodic. Equal evidence keeps identity and publishes no stamp.
        // Teardown: ExternalPawnFacts.Release/InvalidateAll; no map references.
        private static readonly Dictionary<Pawn, Entry> entries = new Dictionary<Pawn, Entry>();

        // Owner: definition session. Key: definition revision via explicit reset.
        // Value: immutable array of capacities actually required by installed
        // givers. Dependencies: WorkGiverDef.requiredCapacities. Refresh: first
        // capture after reset; equality: retained until reset. Teardown: Clear.
        private static PawnCapacityDef[]? requiredCapacities;

        internal static PawnWorkEvidence Capture(Pawn pawn)
        {
            // Explicit captures (including Auto-optimize) read current facts.
            // Event observations only avoid rebuilding unchanged UI inputs.
            EnsureCapacities();
            var blocks = new List<WorkCapabilityFact>();
            var aversions = new List<WorkAversionFact>();
            PreceptDef? refusal = IdeologyWorkFacts.Collect(pawn, aversions);
            WorkTags disabled = pawn.CombinedDisabledWorkTags;
            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if ((giver.workTags & disabled) != WorkTags.None)
                    blocks.Add(new WorkCapabilityFact(giver.defName, null, "ludeon.rimworld", "WR_WorkBlockedTags"));
                if (giver.requiredCapacities != null)
                    foreach (PawnCapacityDef capacity in giver.requiredCapacities)
                        if (!pawn.health.capacities.CapableOf(capacity))
                        {
                            blocks.Add(new WorkCapabilityFact(giver.defName, null, "ludeon.rimworld", "WR_WorkBlockedCapacity"));
                            break;
                        }
                // Only the exact audited worker delegates to MineAIUtility.
                if (refusal != null && giver.giverClass == typeof(WorkGiver_Miner))
                    blocks.Add(new WorkCapabilityFact(giver.defName, null, IdeologyWorkFacts.Source(refusal), refusal.defName));
            }
            HumanoidAlienWorkFacts.Collect(pawn, blocks);
            int viviAvailability = ViviWorkFacts.Collect(pawn, blocks);
            PawnWorkEvidence evidence = PawnWorkEvidence.Create(blocks, aversions);
            if (entries.TryGetValue(pawn, out Entry old) && old.Evidence.ContentEquals(evidence))
                evidence = old.Evidence;
            var capacities = new bool[requiredCapacities!.Length];
            for (int i = 0; i < capacities.Length; i++) capacities[i] = pawn.health.capacities.CapableOf(requiredCapacities[i]);
            var nullifiers = new List<HediffDef>();
            if (pawn.Ideo != null)
                foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                    if (precept.def.comps != null)
                        foreach (PreceptComp comp in precept.def.comps)
                            if (comp.GetType() == typeof(PreceptComp_UnwillingToDo)
                                && comp is PreceptComp_UnwillingToDo unwilling
                                && (unwilling.eventDef == null || unwilling.eventDef == HistoryEventDefOf.Mined)
                                && unwilling.nullifyingHediffs != null)
                                foreach (HediffDef def in unwilling.nullifyingHediffs)
                                    if (!nullifiers.Contains(def)) nullifiers.Add(def);
            var present = new bool[nullifiers.Count];
            for (int i = 0; i < present.Length; i++) present[i] = pawn.health.hediffSet.HasHediff(nullifiers[i]);
            entries[pawn] = new Entry { Evidence = evidence, Capacities = capacities,
                Nullifiers = nullifiers.ToArray(), HasNullifiers = present, ViviAvailability = viviAvailability };
            return evidence;
        }

        internal static void Refresh(Pawn pawn)
        {
            if (!entries.TryGetValue(pawn, out Entry previous)) return;
            if (!ReferenceEquals(previous.Evidence, Capture(pawn))) ExternalPawnFacts.InvalidateWorkEvidence(pawn);
        }

        internal static void HealthChanged(Pawn pawn)
        {
            if (requiredCapacities == null || !entries.TryGetValue(pawn, out Entry previous)) return;
            bool changed = false;
            for (int i = 0; i < previous.Nullifiers.Length && !changed; i++)
                changed = previous.HasNullifiers[i] != pawn.health.hediffSet.HasHediff(previous.Nullifiers[i]);
            for (int i = 0; i < requiredCapacities.Length && !changed; i++)
                changed = previous.Capacities[i] != pawn.health.capacities.CapableOf(requiredCapacities[i]);
            // Vanilla apparel add/remove dirties capacities for stat-offset
            // gear, including both Vivi tools. Compare only recipe availability,
            // so health/speed changes that stay positive publish no revision.
            if (!changed) changed = previous.ViviAvailability != ViviWorkFacts.Observe(pawn);
            if (changed) Refresh(pawn);
        }

        internal static void IdeologyChanged(Ideo ideo)
        {
            // Recache is a structural edit; only captured believers need a UI
            // publication. Capture replaces values, so iterate a stable key list.
            var pawns = new List<Pawn>(entries.Keys);
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].Ideo == ideo) Refresh(pawns[i]);
        }

        private static void EnsureCapacities()
        {
            if (requiredCapacities != null) return;
            var capacities = new List<PawnCapacityDef>();
            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                if (giver.requiredCapacities != null)
                    foreach (PawnCapacityDef capacity in giver.requiredCapacities)
                        if (!capacities.Contains(capacity)) capacities.Add(capacity);
            requiredCapacities = capacities.ToArray();
        }

        internal static void Release(Pawn pawn) => entries.Remove(pawn);
        internal static void Clear()
        {
            entries.Clear();
            requiredCapacities = null;
            HumanoidAlienWorkFacts.Reset();
            ViviWorkFacts.Reset();
        }
    }
}
