using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    internal sealed class PlanListRow
    {
        internal int PlanId;
        internal string Name = "";

        /// "N implants · M colonists".
        internal string CountsText = "";

        /// "Extends {base}" when the plan has a base, drawn right-aligned
        /// on the card's name row (the caption row has no room beside the
        /// percent); empty otherwise.
        internal string ExtendsText = "";
        /// Fit width of ExtendsText in the effective Tiny font, measured at
        /// build; 0 while there is no base plan.
        internal float ExtendsWidth;

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

        /// Small-font fit width of Label, measured at build: the caption
        /// slot takes whatever room the label leaves on the row.
        internal float LabelWidth;

        /// For a slot the game only offers on an artificial part
        /// (ImplantCatalogEntry.RequiresArtificialPart) that the plan does
        /// not yet cover: the replacements that would host it, one of which
        /// the requirement dialog adds alongside the pick. Null when no
        /// requirement is missing or none is expressible.
        internal List<RequirementCandidate>? Requirement;

        /// The slot's anatomy label for the requirement dialog.
        internal string RequirementSlot = "";
    }

    /// A replacement that would satisfy a slot's artificial-part
    /// requirement; the dialog offers the candidates as a pick-one list.
    internal sealed class RequirementCandidate
    {
        internal string DefName = "";
        internal int Ordinal;
        internal string Label = "";
    }

    /// A leaf row's right-aligned caption ("overrides X" or "inherited")
    /// fitted to the row width the picker draws at: the display text (the
    /// tail behind an ellipsis when the slot is too narrow), its width, and
    /// the full text for the tooltip a truncated caption shows.
    internal readonly struct PickerCaption
    {
        internal PickerCaption(string text, float width, string full)
        {
            Text = text;
            Width = width;
            Full = full;
        }

        /// Null for nodes and plain slots (no caption).
        internal readonly string? Text;
        internal readonly float Width;
        internal readonly string Full;
        internal bool Truncated => !ReferenceEquals(Text, Full);
    }

    /// The selection tree's row geometry, shared by the fitting stage and
    /// the renderer so both derive the caption slot from the same numbers.
    /// Vanilla storage-filter style: 22px lines, 11px indent per level,
    /// 18px fold arrows.
    internal static class PickerGeometry
    {
        internal const float Line = 22f;
        internal const float Indent = 11f;
        internal const float Arrow = 18f;

        /// Row-right inset the checkbox column occupies (20px box, 6px
        /// margin, 6px gap to the caption).
        internal const float CheckboxZone = 32f;

        /// Breathing room between the label and its caption.
        internal const float LabelCaptionGap = 8f;

        internal static float LabelX(int depth) => depth * Indent + Arrow + 2f;

        /// The widest caption a row allows: exactly the room the label
        /// leaves. The caption reserves nothing; the label always shows in
        /// full and the caption expands into whatever remains.
        internal static float CaptionMax(float rowWidth, int depth, float labelWidth)
        {
            float available = rowWidth - LabelX(depth) - CheckboxZone;
            return Mathf.Max(0f, available - labelWidth - LabelCaptionGap);
        }
    }

    /// One of the selected plan's own implants inside a star tier of the
    /// editor's ranking panel, resolved for drawing.
    internal sealed class RankedRow
    {
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
        /// Medium-font fit width of SelectedPlanName, measured at build.
        internal float SelectedPlanNameWidth;

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
        //   RankingsVersion, AssignmentsVersion, OptionsVersion (the mod
        //   compatibility options feed the override captions and the
        //   catalog option filters purchase-only rows),
        //   ExternalPawnFacts.Revision, the selected plan id, and the
        //   region filter segment.
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
        //   and the filter segments. The plan-name and "extends" widths are
        //   measured here (the language and metric revisions are inside
        //   UiVersion.Current).
        // Refresh policy: immediate on the next Current read (from the
        //   dialog's WindowUpdate) after a key component moves; command
        //   bumps make structural edits visible while paused.
        // Equality policy: rebuilds replace the snapshot.
        // Teardown: Release() drops the snapshot.
        private PlansSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int plansStamp = -1;
        private int rankingsStamp = -1;
        private int assignmentsStamp = -1;
        private int optionsStamp = -1;
        private int factsStamp = -1;
        private int selectionStamp = -1;
        private int regionStamp = -1;

        // Cache contract (folded-node flags):
        // Owner: the Implanner dialog window.
        // Key: the plans snapshot identity plus the fold revision.
        // Value: one bool per tree row, true for a collapsed node
        //   (immutable once built).
        // Dependencies: the snapshot's tree and the collapsed-section set.
        // Refresh policy: immediate on the next read after either moves.
        // Equality policy: an unchanged key reuses the array.
        // Teardown: Release() drops it.
        private bool[]? foldedFlags;
        private PlansSnapshot? foldedFlagsSnapshot;
        private int foldedFlagsRevision = -1;
        private int foldRevision;

        // Cache contract (fitted captions):
        // Owner: the Implanner dialog window.
        // Key: the plans snapshot identity plus the tree row width.
        // Value: one caption per tree row, parallel to the snapshot's tree
        //   (immutable once built): display text, width, and the full text.
        // Dependencies: the snapshot's rows (override/inherited captions
        //   and the label widths measured at build; the UI metric and
        //   language revisions sit inside the snapshot key) and the row
        //   width the picker draws at (window size, scrollbar gutter).
        // Refresh policy: immediate on the next read after either moves.
        // Equality policy: an unchanged key reuses the array.
        // Teardown: Release() drops it.
        private PickerCaption[]? captions;
        private PlansSnapshot? captionsSnapshot;
        private float captionsWidth = -1f;

        internal int SelectedPlanId;

        /// Region filter segment, in ImplantRegion order (Limbs, Torso,
        /// Head). Regions cluster mutually blocking slots so override
        /// captions point at neighbours in the same view.
        internal int Region;

        /// Session view state: COLLAPSED tree node keys — every group starts
        /// expanded (the trees mostly fit on screen). Folding only skips
        /// drawing already-built rows; it is not a snapshot dependency.
        private readonly HashSet<string> collapsedSections =
            new HashSet<string>(StringComparer.Ordinal);

        internal void Release()
        {
            snapshot = null;
            owner = null;
            foldedFlags = null;
            foldedFlagsSnapshot = null;
            foldedFlagsRevision = -1;
            captions = null;
            captionsSnapshot = null;
            captionsWidth = -1f;
            uiStamp = -1;
            plansStamp = -1;
            rankingsStamp = -1;
            assignmentsStamp = -1;
            optionsStamp = -1;
            factsStamp = -1;
            selectionStamp = -1;
            regionStamp = -1;
        }

        /// Node click: folds or unfolds the group.
        internal void ToggleSection(string sectionKey)
        {
            if (!collapsedSections.Remove(sectionKey))
                collapsedSections.Add(sectionKey);
            foldRevision++;
        }

        /// One flag per tree row: a collapsed node. Rebuilt only when the
        /// snapshot or the fold set changed; the draw pass indexes it.
        internal bool[] FoldedFlags(PlansSnapshot current)
        {
            if (foldedFlags == null
                || !ReferenceEquals(foldedFlagsSnapshot, current)
                || foldedFlagsRevision != foldRevision)
            {
                List<PickerRow> tree = current.Tree;
                var flags = new bool[tree.Count];
                for (int i = 0; i < tree.Count; i++)
                    flags[i] = tree[i].Node
                        && collapsedSections.Contains(tree[i].SectionKey);
                foldedFlags = flags;
                foldedFlagsSnapshot = current;
                foldedFlagsRevision = foldRevision;
            }
            return foldedFlags;
        }

        /// One caption per tree row, fitted to the row width the picker
        /// draws at. Rebuilt only when the snapshot or the width changed
        /// (a window resize, or the scroll gutter appearing); the draw
        /// pass indexes it. Measures Tiny text inside this gate only.
        internal PickerCaption[] Captions(PlansSnapshot current, float rowWidth)
        {
            if (captions == null
                || !ReferenceEquals(captionsSnapshot, current)
                || captionsWidth != rowWidth)
            {
                captions = FitCaptions(current.Tree, rowWidth);
                captionsSnapshot = current;
                captionsWidth = rowWidth;
            }
            return captions;
        }

        /// Static delegate for the truncation search: the candidates are
        /// transient strings, so they bypass the (font, text) memo.
        private static readonly Func<string, float> MeasureFit =
            WrText.MeasureFitWidth;

        private static PickerCaption[] FitCaptions(List<PickerRow> tree, float rowWidth)
        {
            var result = new PickerCaption[tree.Count];
            using (TinyText.UseFont())
            {
                for (int i = 0; i < tree.Count; i++)
                {
                    PickerRow row = tree[i];
                    if (row.Node) continue;
                    string full = row.OverridesLabel.Length > 0 ? row.OverridesLabel
                        : row.Inherited ? PlannerLabels.Inherited
                        : "";
                    if (full.Length == 0) continue;
                    float max = PickerGeometry.CaptionMax(rowWidth, row.Depth, row.LabelWidth);
                    // Full captions repeat across rows ("inherited", the
                    // same blocker), so the memo carries them.
                    float width = WrText.FitWidth(full);
                    string text = full;
                    if (width > max)
                    {
                        text = TailTruncation.Fit(full, max, MeasureFit, out width);
                        // Not even the ellipsis and one character fit: the
                        // row has no room, so the caption is omitted.
                        if (width > max) continue;
                    }
                    result[i] = new PickerCaption(text, width, full);
                }
            }
            return result;
        }

        /// Called from the dialog's WindowUpdate (never inside a render
        /// pass) so every pass of a frame draws one snapshot.
        internal PlansSnapshot Current(ImplannerStore store)
        {
            if (snapshot == null
                || uiStamp != UiVersion.Current
                || !ReferenceEquals(owner, store)
                || plansStamp != store.PlansVersion
                || rankingsStamp != store.RankingsVersion
                || assignmentsStamp != store.AssignmentsVersion
                || optionsStamp != store.OptionsVersion
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
                optionsStamp = store.OptionsVersion;
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
            // Measured inside the UiVersion-gated build (Text.Font is
            // established first) so the draw pass reads stored widths.
            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            try
            {
                result.SelectedPlanNameWidth = result.SelectedPlanName.Length > 0
                    ? WrText.MeasureFitWidth(result.SelectedPlanName)
                    : 0f;
            }
            finally
            {
                Text.Font = font;
            }

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
                IReadOnlyList<ImplantGoal> planGoals = model.EffectiveImplants(plan);
                int slotCount = 0;
                for (int g = 0; g < planGoals.Count; g++)
                    slotCount += planGoals[g].Count;

                enlisted.TryGetValue(plan.Id, out List<Pawn>? planPawns);
                int satisfied = 0, total = 0;
                for (int p = 0; planPawns != null && p < planPawns.Count; p++)
                {
                    PlanEvaluation evaluation = PawnProjection.Evaluate(
                        model, planPawns[p], planGoals, away: false);
                    satisfied += evaluation.SatisfiedUnits;
                    total += evaluation.TotalUnits;
                }

                string counts = "IMP_PlanCounts".Translate(slotCount)
                    + " · " + "IMP_PlanColonists".Translate(planPawns?.Count ?? 0);
                Plan? basePlan = plan.BasePlanId != 0
                    ? model.PlanById(plan.BasePlanId)
                    : null;
                var row = new PlanListRow
                {
                    PlanId = plan.Id,
                    Name = plan.Name,
                    CountsText = counts,
                    ExtendsText = basePlan != null
                        ? "IMP_PlanExtends".Translate(basePlan.Name).ToString()
                        : "",
                    Progress = total == 0 ? 0f : (float)satisfied / total,
                    PercentText = total == 0 ? "" : (satisfied * 100 / total) + "%",
                };
                if (row.ExtendsText.Length > 0)
                {
                    using (RimShared.UiLib.TinyText.UseFont())
                        row.ExtendsWidth = WrText.MeasureFitWidth(row.ExtendsText);
                }
                result.Plans.Add(row);
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

        /// The region segment, and purchase-only kinds only while the
        /// catalog option shows them.
        private bool PassesFilters(ImplantCatalogEntry entry, bool showPurchaseOnly) =>
            entry.Region == (ImplantRegion)Region
            && (showPurchaseOnly || !entry.PurchaseOnly);

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
            IReadOnlyList<ImplantGoal> effective = model.EffectiveImplants(selected);
            var inherited = new HashSet<(string, int)>();
            var planned = new HashSet<(string, int)>();
            var plannedSlots = new List<(string Def, int Ordinal)>();
            for (int i = 0; i < effective.Count; i++)
            {
                ImplantGoal goal = effective[i];
                bool own = goal.PlanId == selected.Id;
                for (int j = 0; j < goal.SlotOrdinals.Count; j++)
                {
                    var slot = (goal.ImplantDefName, goal.SlotOrdinals[j]);
                    plannedSlots.Add(slot);
                    planned.Add(slot);
                    if (!own) inherited.Add(slot);
                }
            }

            IReadOnlyList<ImplantCatalogEntry> implants = Catalogs.Implants();
            List<PickerRow> tree = result.Tree;
            // Host replacements per anatomy instance, resolved once per
            // build for the module rows that need them.
            var hosts = new Dictionary<BodyPartRecord, List<RequirementCandidate>>();
            string? lastGroup = null;
            bool showPurchaseOnly = model.ShowPurchaseOnly;
            // Leaf labels are measured here (Small font established first)
            // so the caption fitting stage never measures them per width.
            GameFont font = Text.Font;
            Text.Font = GameFont.Small;
            try
            {
                for (int i = 0; i < implants.Count; i++)
                {
                    ImplantCatalogEntry entry = implants[i];
                    if (!PassesFilters(entry, showPurchaseOnly)) continue;
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
                        row.LabelWidth = WrText.FitWidth(row.Label);
                        if (!own)
                        {
                            string overridden = FirstConflictLabel(
                                model, plannedSlots, entry.Def.defName, ordinal);
                            if (overridden.Length > 0)
                                row.OverridesLabel =
                                    "IMP_Overrides".Translate(overridden);
                            if (entry.RequiresArtificialPart)
                            {
                                BodyPartRecord? record = entry.SlotRecords[ordinal];
                                List<RequirementCandidate>? candidates = record != null
                                    ? HostsOf(implants, hosts, record, showPurchaseOnly)
                                    : null;
                                if (candidates != null && candidates.Count > 0
                                    && !AnyPlanned(planned, candidates))
                                {
                                    row.Requirement = candidates;
                                    row.RequirementSlot = slotLabel;
                                }
                            }
                        }
                        tree.Add(row);
                    }
                }
            }
            finally
            {
                Text.Font = font;
            }
        }

        /// The replacements able to host a module on the given anatomy
        /// instance: catalog replacements targeting that record which carry
        /// the modular mark (ModCompatibility.IsModularReplacement), under
        /// the same purchase-only filter as the picker rows, cheapest
        /// efficiency first so the dialog's default is the plain bionic.
        /// Shared per record within one build; immutable once built.
        private static List<RequirementCandidate> HostsOf(
            IReadOnlyList<ImplantCatalogEntry> implants,
            Dictionary<BodyPartRecord, List<RequirementCandidate>> hosts,
            BodyPartRecord record, bool showPurchaseOnly)
        {
            if (hosts.TryGetValue(record, out List<RequirementCandidate> known))
                return known;
            var candidates = new List<RequirementCandidate>();
            var efficiencies = new List<float>();
            for (int i = 0; i < implants.Count; i++)
            {
                ImplantCatalogEntry entry = implants[i];
                if (!entry.IsReplacement || !ModCompatibility.IsModularReplacement(entry.Def))
                    continue;
                if (entry.PurchaseOnly && !showPurchaseOnly) continue;
                for (int ordinal = 0; ordinal < entry.SlotRecords.Count; ordinal++)
                {
                    if (entry.SlotRecords[ordinal] != record) continue;
                    // Insertion keeps efficiency ascending, then label order
                    // (the catalog list is already label-sorted).
                    int at = candidates.Count;
                    while (at > 0 && efficiencies[at - 1] > entry.Efficiency) at--;
                    candidates.Insert(at, new RequirementCandidate
                    {
                        DefName = entry.Def.defName,
                        Ordinal = ordinal,
                        Label = entry.Label,
                    });
                    efficiencies.Insert(at, entry.Efficiency);
                }
            }
            hosts.Add(record, candidates);
            return candidates;
        }

        private static bool AnyPlanned(
            HashSet<(string, int)> planned, List<RequirementCandidate> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
                if (planned.Contains((candidates[i].DefName, candidates[i].Ordinal)))
                    return true;
            return false;
        }

        /// The label of the first planned slot the candidate can never
        /// coexist with, or empty. Same-kind slots never conflict (they are
        /// the same goal's other slots or a re-include of an inherited one).
        /// Definition-derived conflicts plus the model's option-driven kind
        /// exclusivity: the same rule the synced command applies.
        private static string FirstConflictLabel(PlannerModel model,
            List<(string Def, int Ordinal)> slots, string defName, int ordinal)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                (string otherDef, int otherOrdinal) = slots[i];
                if (string.Equals(otherDef, defName, StringComparison.Ordinal))
                    continue;
                if (!ImplantConflicts.Conflicts(otherDef, otherOrdinal, defName, ordinal)
                    && !model.KindsExclusive(otherDef, defName))
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
