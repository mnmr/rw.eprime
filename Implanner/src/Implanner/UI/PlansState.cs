using System;
using System.Collections.Generic;
using Implanner.Core;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    internal sealed class PlanListRow
    {
        internal int PlanId;
        internal string Name = "";

        /// "N implants · M colonists" plus the base-plan link when present.
        internal string CountsText = "";

        /// Aggregate delivery progress of the enlisted colonists (satisfied
        /// over total units, 0..1; 0 while nobody is enlisted).
        internal float Progress;

        /// Whole-percent progress text; empty while there is no enlisted
        /// work to measure.
        internal string PercentText = "";
    }

    /// One row of the plan editor's selection tree: an expandable node
    /// (body-part group) or a selectable implant slot. Rendered in the
    /// vanilla storage-filter tree style.
    internal sealed class PickerRow
    {
        internal int Depth;
        internal bool Node;
        internal string SectionKey = ""; // nodes: fold-state identity
        internal string DefName = "";
        internal string Label = "";
        internal ThingDef? IconDef;      // leaves: observed def icon
        internal bool Selected;

        /// The slot is covered by a base plan but not selected here;
        /// checking it re-includes the slot as this plan's own goal.
        internal bool Inherited;

        /// Selecting this slot can never coexist with the named planned goal
        /// (one part per slot, replacement subtrees, incompatible glands):
        /// checking it deselects an own blocker or suppresses an inherited
        /// one — the click IS the choice.
        internal string OverridesLabel = "";
        internal int Ordinal;            // leaves: slot ordinal
    }

    /// One of the selected plan's own implants inside a star tier of the
    /// editor's ranking panel, resolved for drawing.
    internal sealed class RankedRow
    {
        internal int GoalId;
        internal string DefName = "";
        internal string Label = "";
        internal ThingDef? IconDef;

        /// How many anatomy slots of this implant the plan selects.
        internal string CountText = "";
    }

    internal sealed class PlansSnapshot
    {
        internal List<PlanListRow> Plans = new List<PlanListRow>();
        internal int SelectedPlanId;
        internal string SelectedPlanName = "";

        internal List<PickerRow> Tree = new List<PickerRow>();

        /// One combined ranking list over the plan's own implants (inherited
        /// goals are not listed unless explicitly re-included). Index 0 =
        /// five stars … index 4 = one star.
        internal List<RankedRow>[] RankTiers = null!;
    }

    /// Plans tab presentation state. Owned by the dialog; dies with it.
    internal sealed class PlansState
    {
        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: UiVersion.Current, store identity, PlansVersion,
        //   RankingsVersion, AssignmentsVersion, ExternalPawnFacts.Revision,
        //   the selected plan id, and the region filter segment.
        // Value: an immutable plans snapshot (list cards with enlisted
        //   counts and aggregate delivery progress, the filtered implant
        //   selection tree resolved against the selected plan, its base
        //   chain and the conflict facts, the ranking tiers over the plan's
        //   own implants).
        // Dependencies: plan structure, content and base links
        //   (PlansVersion), the implant star rankings (RankingsVersion),
        //   pawn-to-plan assignments (AssignmentsVersion), installed
        //   implants and roster membership for the progress aggregate
        //   (ExternalPawnFacts.Revision), the implant catalog and conflict
        //   facts (language revision folded into UiVersion.Current; conflict
        //   facts are def-derived and static per session), the selection,
        //   and the filter segments.
        // Refresh policy: immediate on the next Repaint read after a key
        //   component moves; command bumps make structural edits visible
        //   while paused.
        // Equality policy: rebuilds replace the snapshot.
        // Teardown: Release() drops the snapshot.
        private PlansSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int plansStamp = -1;
        private int rankingsStamp = -1;
        private int assignmentsStamp = -1;
        private int factsStamp = -1;
        private int selectionStamp = -1;
        private int regionStamp = -1;

        internal int SelectedPlanId;

        /// Region filter segment, in ImplantRegion order (Limbs, Torso,
        /// Head). Regions cluster mutually blocking slots so override
        /// captions point at neighbours in the same view.
        internal int Region;

        /// Session view state: COLLAPSED tree node keys — every group starts
        /// expanded (the trees mostly fit on screen). Folding only skips
        /// drawing already-built rows; it is not a snapshot dependency.
        internal readonly HashSet<string> CollapsedSections =
            new HashSet<string>(StringComparer.Ordinal);

        internal void Release()
        {
            snapshot = null;
            owner = null;
            uiStamp = -1;
            plansStamp = -1;
            rankingsStamp = -1;
            assignmentsStamp = -1;
            factsStamp = -1;
            selectionStamp = -1;
            regionStamp = -1;
        }

        /// Called on the Repaint pass only.
        internal PlansSnapshot Current(ImplannerStore store)
        {
            if (snapshot == null
                || uiStamp != UiVersion.Current
                || !ReferenceEquals(owner, store)
                || plansStamp != store.PlansVersion
                || rankingsStamp != store.RankingsVersion
                || assignmentsStamp != store.AssignmentsVersion
                || factsStamp != ExternalPawnFacts.Revision
                || selectionStamp != SelectedPlanId
                || regionStamp != Region)
            {
                snapshot = Build(store);
                uiStamp = UiVersion.Current;
                owner = store;
                plansStamp = store.PlansVersion;
                rankingsStamp = store.RankingsVersion;
                assignmentsStamp = store.AssignmentsVersion;
                factsStamp = ExternalPawnFacts.Revision;
                selectionStamp = SelectedPlanId;
                regionStamp = Region;
            }
            return snapshot;
        }

        private PlansSnapshot Build(ImplannerStore store)
        {
            var result = new PlansSnapshot();
            PlannerModel model = store.Model;
            IReadOnlyList<Plan> plans = model.Plans;

            Plan? selected = model.PlanById(SelectedPlanId);
            if (selected == null && plans.Count > 0) selected = plans[0];
            SelectedPlanId = selected?.Id ?? 0;
            result.SelectedPlanId = SelectedPlanId;
            result.SelectedPlanName = selected?.Name ?? "";

            // Enlisted colonists per plan: the same pawn set and evaluation
            // the overview uses, so the card counts and bars never disagree
            // with the table.
            var enlisted = new Dictionary<int, List<Pawn>>();
            List<Pawn> colonists = ColonyScope.AllPlanableColonists();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (!model.Assignments.TryGetValue(
                        pawn.thingIDNumber, out int planId))
                    continue;
                if (!enlisted.TryGetValue(planId, out List<Pawn> pawns))
                {
                    pawns = new List<Pawn>();
                    enlisted.Add(planId, pawns);
                }
                pawns.Add(pawn);
            }

            for (int i = 0; i < plans.Count; i++)
            {
                Plan plan = plans[i];
                // The counter is total selected slots (two bionic legs are
                // two implants), not distinct implant kinds.
                List<ImplantGoal> planGoals = model.EffectiveImplants(plan);
                int slotCount = 0;
                for (int g = 0; g < planGoals.Count; g++)
                    slotCount += planGoals[g].Count;

                enlisted.TryGetValue(plan.Id, out List<Pawn>? planPawns);
                int satisfied = 0, total = 0;
                for (int p = 0; planPawns != null && p < planPawns.Count; p++)
                {
                    PlanEvaluation evaluation = PawnProjection.Evaluate(
                        planPawns[p], planGoals, away: false,
                        model.LatchesFor(planPawns[p].thingIDNumber));
                    satisfied += evaluation.SatisfiedUnits;
                    total += evaluation.TotalUnits;
                }

                string counts = "IMP_PlanCounts".Translate(slotCount)
                    + " · " + "IMP_PlanColonists".Translate(planPawns?.Count ?? 0);
                Plan? basePlan = plan.BasePlanId != 0
                    ? model.PlanById(plan.BasePlanId)
                    : null;
                if (basePlan != null)
                    counts += " · " + "IMP_PlanExtends".Translate(basePlan.Name);
                result.Plans.Add(new PlanListRow
                {
                    PlanId = plan.Id,
                    Name = plan.Name,
                    CountsText = counts,
                    Progress = total == 0 ? 0f : (float)satisfied / total,
                    PercentText = total == 0 ? "" : (satisfied * 100 / total) + "%",
                });
            }

            if (selected == null)
            {
                result.RankTiers = EmptyTiers();
                return result;
            }

            BuildTree(model, selected, result);
            BuildRankTiers(model, selected, result);
            return result;
        }

        private bool PassesFilters(ImplantCatalogEntry entry) =>
            entry.Region == (ImplantRegion)Region;

        /// Builds the filtered selection tree: one node per body-part group
        /// (the catalog's GroupLabel), then the group's implant slots as
        /// leaves. Base-plan coverage shows as an inherited marker, and
        /// slots that can never coexist with an own goal are blocked with
        /// the blocker's name.
        private void BuildTree(PlannerModel model, Plan selected, PlansSnapshot result)
        {
            // The effective goal set (post override/conflict suppression):
            // inherited slots get their marker, and any effective slot a
            // candidate conflicts with names the choice the click replaces.
            List<ImplantGoal> effective = model.EffectiveImplants(selected);
            var inherited = new HashSet<(string, int)>();
            var plannedSlots = new List<(string Def, int Ordinal)>();
            for (int i = 0; i < effective.Count; i++)
            {
                ImplantGoal goal = effective[i];
                bool own = OwnGoal(selected, goal.Id) != null;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    var slot = (goal.ImplantDefName, goal.SlotOrdinals[j]);
                    plannedSlots.Add(slot);
                    if (!own) inherited.Add(slot);
                }
            }

            IReadOnlyList<ImplantCatalogEntry> implants = Catalogs.Implants();
            List<PickerRow> tree = result.Tree;
            string? lastGroup = null;
            for (int i = 0; i < implants.Count; i++)
            {
                ImplantCatalogEntry entry = implants[i];
                if (!PassesFilters(entry)) continue;
                if (!string.Equals(lastGroup, entry.GroupLabel, StringComparison.Ordinal))
                {
                    lastGroup = entry.GroupLabel;
                    tree.Add(new PickerRow
                    {
                        Depth = 0,
                        Node = true,
                        SectionKey = "t|" + entry.GroupLabel,
                        Label = entry.GroupLabel,
                    });
                }
                ImplantGoal? goal = FindGoal(selected, entry.Def.defName);
                for (int ordinal = 0; ordinal < entry.SlotLabels.Count; ordinal++)
                {
                    string slotLabel = entry.SlotLabels[ordinal];
                    bool own = goal != null && HasOrdinal(goal, ordinal);
                    var row = new PickerRow
                    {
                        Depth = 1,
                        DefName = entry.Def.defName,
                        Ordinal = ordinal,
                        IconDef = entry.Def.spawnThingOnRemoved,
                        Label = entry.SlotLabels.Count > 1 && slotLabel.Length > 0
                            ? entry.Label + " (" + slotLabel + ")"
                            : entry.Label,
                        Selected = own,
                        Inherited = !own
                            && inherited.Contains((entry.Def.defName, ordinal)),
                    };
                    if (!own)
                    {
                        string overridden = FirstConflictLabel(
                            plannedSlots, entry.Def.defName, ordinal);
                        if (overridden.Length > 0)
                            row.OverridesLabel =
                                "IMP_Overrides".Translate(overridden);
                    }
                    tree.Add(row);
                }
            }
        }

        /// The label of the first planned slot the candidate can never
        /// coexist with, or empty. Same-kind slots never conflict (they are
        /// the same goal's other slots or a re-include of an inherited one).
        private static string FirstConflictLabel(
            List<(string Def, int Ordinal)> slots, string defName, int ordinal)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                (string otherDef, int otherOrdinal) = slots[i];
                if (string.Equals(otherDef, defName, StringComparison.Ordinal))
                    continue;
                if (!ImplantConflicts.Conflicts(otherDef, otherOrdinal, defName, ordinal))
                    continue;
                ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(otherDef);
                return entry?.Label ?? otherDef;
            }
            return "";
        }

        /// One combined ranking list over the plan's OWN implants: a newly
        /// added implant lands in the three-star tier (the unranked default)
        /// and moves only when the player drags it.
        private static void BuildRankTiers(
            PlannerModel model, Plan selected, PlansSnapshot result)
        {
            List<RankedRow>[] tiers = EmptyTiers();
            for (int i = 0; i < selected.Implants.Count; i++)
            {
                ImplantGoal goal = selected.Implants[i];
                ImplantCatalogEntry? entry =
                    Catalogs.ImplantByDefName(goal.ImplantDefName);
                var row = new RankedRow
                {
                    GoalId = goal.Id,
                    DefName = goal.ImplantDefName,
                    Label = entry?.Label ?? goal.ImplantDefName,
                    IconDef = entry?.Def.spawnThingOnRemoved,
                    CountText = goal.Count.ToStringCached(),
                };
                int tier = StarRanking.TierOf(
                    model.ImplantStarsOf(goal.ImplantDefName));
                tiers[tier].Add(row);
            }
            // Player-arranged order first (MoveImplantRank), then defName
            // for kinds never explicitly positioned.
            for (int t = 0; t < tiers.Length; t++)
                tiers[t].Sort((a, b) =>
                {
                    int order = model.ImplantOrderOf(a.DefName)
                        .CompareTo(model.ImplantOrderOf(b.DefName));
                    return order != 0
                        ? order
                        : string.CompareOrdinal(a.DefName, b.DefName);
                });
            result.RankTiers = tiers;
        }

        private static List<RankedRow>[] EmptyTiers()
        {
            var tiers = new List<RankedRow>[StarRanking.Max];
            for (int i = 0; i < tiers.Length; i++)
                tiers[i] = new List<RankedRow>();
            return tiers;
        }

        private static ImplantGoal? OwnGoal(Plan plan, int goalId)
        {
            for (int i = 0; i < plan.Implants.Count; i++)
                if (plan.Implants[i].Id == goalId)
                    return plan.Implants[i];
            return null;
        }

        private static ImplantGoal? FindGoal(Plan plan, string defName)
        {
            for (int i = 0; i < plan.Implants.Count; i++)
                if (string.Equals(plan.Implants[i].ImplantDefName, defName, StringComparison.Ordinal))
                    return plan.Implants[i];
            return null;
        }

        private static bool HasOrdinal(ImplantGoal goal, int ordinal)
        {
            for (int i = 0; i < goal.SlotOrdinals.Count; i++)
                if (goal.SlotOrdinals[i] == ordinal)
                    return true;
            return false;
        }
    }
}
