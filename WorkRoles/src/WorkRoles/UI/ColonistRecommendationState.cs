using System;
using System.Collections.Generic;
using System.Linq;
using RimShared.Common;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Signals;

namespace WorkRoles.UI
{
    /// Owns recommendation-engine results and their preview projections. The
    /// view decides when to display or apply a plan; builders never issue a
    /// command or open a window, while published command payloads can be invoked
    /// only by the view's input path.
    internal sealed class ColonistRecommendationState
    {
        private readonly ColonistRecommendationProducer producer =
            new ColonistRecommendationProducer();

        // Owner: Colonists window/store. Key: shared computation identity and
        // assignment presentation revision; map cohort keyed by map identity,
        // roster revision and pawn-list revision. Value: owned fix targets and
        // preview explanations, immutable after publication. Dependencies: the
        // producer's plan plus current assignment state/pins, role identities.
        // Refresh: immediate on an explicit model/external change. Equality:
        // unchanged producer/assignment inputs reuse targets; the expensive
        // plan survives assignment enabled/disabled/force-on edits. Teardown:
        // Reset/ReleaseSnapshots drops producer, cohort, targets and previews.
        private List<PawnFixPlan>? plans;
        // The engine plan behind `plans` and its pawn order, kept for
        // PlacementIndex; both share the plan key and teardown.
        private RecommendationPlan? recommendations;
        private List<Pawn>? planPawns;
        private readonly Dictionary<Pawn, Dictionary<int, SignalBucket>> planSuitability =
            new Dictionary<Pawn, Dictionary<int, SignalBucket>>();
        private RoleStore? planOwner;
        private ScopeCacheStamp planStamp = ScopeCacheStamp.Invalid;
        private int planMapId = -1;
        private int planTuningRevision = -1;
        private int planDefinitionRevision = -1;
        private RecommendationSnapshot? planSnapshot;
        private int planAssignmentPresentation = -1;
        private int cohortRoster = -1;
        private int cohortListRevision = -1;
        private int cohortMapId = -1;
        private RoleStore? cohortOwner;
        // Same owner/cohort lifecycle as plans. Each target additionally keys
        // its pawn's presentation revision, so another pawn's toggle cannot
        // rebuild this pawn's command payload or explanation projection.
        private readonly RevisionedCache<Pawn, (RecommendationPlan, int), PawnFixPlan> pawnPlans =
            new RevisionedCache<Pawn, (RecommendationPlan, int), PawnFixPlan>();
        private static readonly Func<(ColonistRecommendationState owner, int index), PawnFixPlan> BuildPawnPlan =
            state => state.owner.BuildPawnFixPlan(state.index);

        // Owner: Colonists window. Key: RoleStore, selected Pawn and its fix-plan
        // identity, selected external-facts identity, presentation/activation/rule
        // revisions for referenced roles, map identity, recommendation tuning,
        // UI metrics/definitions/language, verdict preference, panel dimensions.
        // Value: immutable ColonistRecommendationRenderSnapshot with detached
        // chip facts, local geometry, tooltips, command ids/indices, and a
        // producer-owned target-assignment buffer. Dependencies: the keyed plan,
        // role/assignment presentation, external pawn snapshots, translated
        // labels/tooltips, verdicts, and dimensions. Refresh: immediate on the
        // next read after a key change; plan and external-fact access share their
        // existing producer gates. Equality: equal rebuilt
        // contents preserve the published snapshot identity. Teardown: Reset,
        // language invalidation, and ReleaseSnapshots drop every owned reference.
        private RoleStore? previewOwner;
        private PawnFixPlan? previewPlan;
        private PawnExternalSnapshot? previewFacts;
        private int previewRoleRevision = -1;
        private int previewMetricRevision = -1;
        private int previewDefinitionRevision = -1;
        private Pawn? previewPawn;
        private bool previewVerdictsShown;
        private int previewTuningRevision = -1;
        private int previewMapId = -1;
        private int previewLanguageRevision = -1;
        private float previewWidth = -1f;
        private float previewHeight = -1f;
        private ColonistRecommendationRenderSnapshot? preview;

        internal void Reset()
        {
            producer.Reset();
            pawnPlans.Clear();
            plans = null;
            planSnapshot = null;
            recommendations = null;
            planPawns = null;
            planSuitability.Clear();
            planOwner = null;
            planStamp = ScopeCacheStamp.Invalid;
            planMapId = -1;
            planTuningRevision = -1;
            planDefinitionRevision = -1;
            planAssignmentPresentation = cohortRoster = cohortListRevision = cohortMapId = -1;
            cohortOwner = null;
            ClearPreview();
        }

        internal void InvalidatePlan()
        {
            planStamp = ScopeCacheStamp.Invalid;
        }

        internal void InvalidateExternalFacts()
        {
            producer.InvalidateExternalFacts();
            cohortRoster = -1;
            InvalidatePlan();
        }

        internal IReadOnlyList<Dictionary<int, SignalBucket>> SuitabilityFor(
            RoleStore store, IReadOnlyList<Pawn> pawns,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
            => producer.For(store, pawns, externalSnapshot).Suitability;

        internal void InvalidateLanguageCaches()
        {
            ClearPreview();
        }

        internal void ReleaseSnapshots() => Reset();

        private void ClearPreview()
        {
            previewOwner = null;
            previewPlan = null;
            previewFacts = null;
            previewRoleRevision = previewMetricRevision = previewDefinitionRevision = -1;
            previewPawn = null;
            previewVerdictsShown = false;
            previewTuningRevision = -1;
            previewMapId = -1;
            previewLanguageRevision = -1;
            previewWidth = -1f;
            previewHeight = -1f;
            preview = null;
        }

        internal IReadOnlyList<PawnFixPlan> Plans(Pawn? anchor, ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
            => Plans(RoleStore.Current, anchor, stamp, externalSnapshot);

        private IReadOnlyList<PawnFixPlan> Plans(RoleStore? store, Pawn? anchor,
            ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            Map? map = anchor?.MapHeld ?? Find.CurrentMap;
            int mapId = map?.uniqueID ?? -1;
            int tuningRevision = store?.RecommendationTuningRevision ?? -1;
            int revision = Math.Max(Math.Max(UiVersion.RoleWork, UiVersion.RoleRecommendation),
                Math.Max(Math.Max(UiVersion.RoleActivation, UiVersion.RoleRules),
                    Math.Max(Math.Max(UiVersion.AssignmentPresentation, UiVersion.RecommendationOrder), UiVersion.Roster)));
            var dependencyStamp = new ScopeCacheStamp(revision, stamp.PawnListRevision);
            if (plans == null
                || !ReferenceEquals(planOwner, store)
                || planStamp != dependencyStamp
                || planMapId != mapId
                || planDefinitionRevision != DefinitionReloadCoordinator.Revision
                || planTuningRevision != tuningRevision)
            {
                planOwner = store;
                planStamp = dependencyStamp;
                planDefinitionRevision = DefinitionReloadCoordinator.Revision;
                planMapId = mapId;
                planTuningRevision = tuningRevision;
                plans = BuildColonyFixPlan(store, map, stamp, externalSnapshot);
            }
            return plans;
        }

        /// Where the engine would let a hand-added role rise in the pawn's
        /// current order (see RecommendationPlan.PlacementIndex); -1 (append)
        /// when the pawn is not in the keyed plan.
        internal int PlacementIndex(Pawn pawn, IReadOnlyList<int> existing, int roleId,
            ScopeCacheStamp stamp, Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            Plans(pawn, stamp, externalSnapshot);
            if (recommendations == null || planPawns == null) return -1;
            int pawnIndex = planPawns.IndexOf(pawn);
            return pawnIndex < 0 ? -1
                : recommendations.PlacementIndex(pawnIndex, existing, roleId);
        }

        internal ColonistRecommendationRenderSnapshot RenderSnapshot(
            RoleStore store, Pawn pawn,
            ColonistsRosterCatalogSnapshot catalog, ScopeCacheStamp stamp,
            float width, float height,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            bool verdictsShown = VerdictsShown;
            int tuningRevision = store?.RecommendationTuningRevision ?? -1;
            int mapId = (pawn?.MapHeld ?? Find.CurrentMap)?.uniqueID ?? -1;
            int languageRevision = LanguageChangeCoordinator.Revision;
            IReadOnlyList<PawnFixPlan> source = Plans(store,
                pawn, stamp, externalSnapshot);
            PawnFixPlan? plan = null;
            for (int i = 0; i < source.Count; i++)
                if (ReferenceEquals(source[i].Pawn, pawn))
                {
                    plan = source[i];
                    break;
                }
            PawnExternalSnapshot facts = externalSnapshot(pawn!);
            int roleRevision = plan?.PresentationRevision() ?? 0;
            int metricRevision = UiRevision.Current;
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (preview != null && ReferenceEquals(previewOwner, store)
                && ReferenceEquals(previewPlan, plan) && ReferenceEquals(previewFacts, facts)
                && previewRoleRevision == roleRevision && ReferenceEquals(previewPawn, pawn)
                && previewMetricRevision == metricRevision && previewDefinitionRevision == definitionRevision
                && previewVerdictsShown == verdictsShown
                && previewTuningRevision == tuningRevision && previewMapId == mapId
                && previewLanguageRevision == languageRevision
                && previewWidth == width && previewHeight == height)
                return preview;
            ColonistRecommendationRenderSnapshot rebuilt =
                BuildRenderSnapshot(store!, pawn!, catalog, plan, width, height, // null store/pawn yield a null plan, so neither is dereferenced
                    externalSnapshot);
            bool ownerChanged = !ReferenceEquals(previewOwner, store);
            if (preview == null || ownerChanged
                || !ReferenceEquals(previewPawn, pawn)
                || !preview.ContentEquals(rebuilt))
                preview = rebuilt;

            previewOwner = store;
            previewPlan = plan;
            previewFacts = facts;
            previewRoleRevision = roleRevision;
            previewMetricRevision = metricRevision;
            previewDefinitionRevision = definitionRevision;
            previewPawn = pawn;
            previewVerdictsShown = verdictsShown;
            previewTuningRevision = tuningRevision;
            previewMapId = mapId;
            previewLanguageRevision = languageRevision;
            previewWidth = width;
            previewHeight = height;
            return preview;
        }

        private ColonistRecommendationRenderSnapshot BuildRenderSnapshot(
            RoleStore store, Pawn pawn,
            ColonistsRosterCatalogSnapshot catalog, PawnFixPlan? plan,
            float width, float height,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            string header = "WR_RecommendedRoles".Translate().ToString();
            string applyLabel = "WR_MakeItSo".Translate().ToString();
            var chips = new List<ColonistRecommendationRenderChip>();
            var target = new List<RoleAssignment>(plan?.Target.Count ?? 0);
            if (plan != null)
                for (int i = 0; i < plan.Target.Count; i++)
                {
                    RoleAssignment assignment = plan.Target[i];
                    target.Add(new RoleAssignment
                    {
                        roleId = assignment.roleId,
                        state = assignment.state,
                        pinned = assignment.pinned,
                    });
                }

            if (plan != null)
            {
                Dialog_ChangesPreview.Line line = BuildPreviewEntry(
                    store, plan, externalSnapshot).lines[0];
                store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
                List<RoleAssignment>? existing = set?.assignments;
                float chipBottom = height - 28f;
                float chipX = 0f;
                float chipY = 28f;
                GameFont previousFont = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    for (int i = 0; i < line.ChipCount; i++)
                    {
                        var source = line.ChipAt(i);
                        int roleId = source.RoleId;
                        if (!catalog.TryGetChip(roleId,
                                out RoleChipRenderData chip))
                            continue;
                        bool assigned = source.State
                            != Dialog_ChangesPreview.ChipState.Added;
                        RoleChipVerdict verdict = line.VerdictAt(i);
                        float chipWidth = RoleChipUI.WidthFor(chip,
                            showRemove: assigned,
                            verdictSlot: verdict.Shown);
                        if (chipX + chipWidth > width && chipX > 0f)
                        {
                            chipX = 0f;
                            chipY += RoleChipUI.Height + 4f;
                            if (chipY + RoleChipUI.Height > chipBottom) break;
                        }

                        AssignmentState assignmentState =
                            AssignmentState.Enabled;
                        if (assigned && existing != null)
                            for (int existingIndex = 0;
                                    existingIndex < existing.Count;
                                    existingIndex++)
                                if (existing[existingIndex].roleId == roleId)
                                {
                                    assignmentState =
                                        existing[existingIndex].state;
                                    break;
                                }
                        bool enabled = !assigned
                            || RoleActivation.IsActive(
                                catalog.IsRoleEnabled(roleId),
                                assignmentState);
                        StructuredTip? tooltip = line.StructuredTipAt(i);
                        string? fallback = source.Tip;
                        if (fallback == null && assigned)
                            fallback = (source.State
                                    == Dialog_ChangesPreview.ChipState.Removed
                                ? "WR_WillBeRemoved"
                                : "WR_AlreadyAssigned").Translate().ToString();
                        ChipStyle style = !assigned ? ChipStyle.Normal
                            : enabled ? ChipStyle.Subtle : ChipStyle.ConditionalOff;
                        chips.Add(new ColonistRecommendationRenderChip(
                            chip, assigned, style,
                            enabled && source.State
                                == Dialog_ChangesPreview.ChipState.Removed,
                            new Rect(chipX, chipY, chipWidth,
                                RoleChipUI.Height), verdict, tooltip,
                            fallback,
                            assigned ? -1 : RecommendedInsertIndex(
                                roleId, plan.Target, existing)));
                        chipX += chipWidth + 4f;
                    }
                }
                finally
                {
                    Text.Font = previousFont;
                }
            }

            return new ColonistRecommendationRenderSnapshot(pawn, chips,
                target, plan?.HasChanges == true, header, applyLabel,
                new Rect(0f, 0f, width, 28f),
                new Rect(width - 110f, height - 26f, 106f, 24f));
        }

        private static int RecommendedInsertIndex(int roleId,
            List<RoleAssignment> target, List<RoleAssignment>? existing)
        {
            if (existing == null) return -1;
            int clickedRank = RoleIndex(target, roleId);
            if (clickedRank < 0) return -1;
            for (int i = 0; i < existing.Count; i++)
            {
                int rank = RoleIndex(target, existing[i].roleId);
                if (rank >= 0 && rank > clickedRank) return i;
            }
            return -1;
        }

        private static int RoleIndex(List<RoleAssignment> assignments,
            int roleId)
        {
            for (int i = 0; i < assignments.Count; i++)
                if (assignments[i].roleId == roleId) return i;
            return -1;
        }

        internal List<Dialog_ChangesPreview.PawnPreview> FixEntries(RoleStore store,
            Pawn? only, Pawn? anchor, ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            var entries = new List<Dialog_ChangesPreview.PawnPreview>();
            foreach (PawnFixPlan plan in Plans(anchor, stamp, externalSnapshot))
            {
                if (only != null && plan.Pawn != only) continue;
                if (!plan.HasChanges) continue;
                entries.Add(BuildPreviewEntry(store, plan, externalSnapshot));
            }
            return entries;
        }

        /// Recommendation chips carry the verdict badge for the pawn they are
        /// recommended to; the setting is read here so toggling it rebuilds
        /// previews without touching the plan cache.
        private static bool VerdictsShown =>
            WorkRolesMod.Settings?.verdictsOnRecommendationChips ?? true;

        private RoleChipVerdict VerdictFor(Pawn pawn, int roleId) =>
            planSuitability.TryGetValue(
                pawn, out Dictionary<int, SignalBucket> buckets)
            && buckets.TryGetValue(roleId, out SignalBucket bucket)
                ? SkillSignalPresentation.VerdictBadge(bucket)
                : default;

        private Dialog_ChangesPreview.PawnPreview BuildPreviewEntry(
            RoleStore store, PawnFixPlan plan,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            store.pawnSets.TryGetValue(plan.Pawn, out PawnRoleSet set);
            List<RoleAssignment> existing = set?.assignments ?? new List<RoleAssignment>();
            var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
            var targetIds = new HashSet<int>(plan.Target.Select(a => a.roleId));
            SkillBucketSnapshot skillBuckets = externalSnapshot(plan.Pawn)
                .Signals.SkillBuckets;
            bool verdictsShown = VerdictsShown;

            var line = new Dialog_ChangesPreview.Line();
            foreach (RoleAssignment assignment in plan.Target)
            {
                Role? role = store.RoleById(assignment.roleId);
                if (role == null) continue;
                bool kept = existingIds.Contains(assignment.roleId);
                var state = kept
                    ? Dialog_ChangesPreview.ChipState.Kept
                    : Dialog_ChangesPreview.ChipState.Added;
                plan.Explanations.TryGetValue(role.id, out var explanation);
                line.AddChip(role, state, RecommendationPresentation.CreateTooltip(
                    store, plan.Pawn, role, state, explanation, skillBuckets),
                    verdictsShown && !role.blocker
                        ? VerdictFor(plan.Pawn, role.id) : default);
            }
            for (int i = 0; i < existing.Count; i++)
            {
                if (targetIds.Contains(existing[i].roleId)) continue;
                Role? role = store.RoleById(existing[i].roleId);
                if (role == null) continue;
                var state = Dialog_ChangesPreview.ChipState.Removed;
                plan.Explanations.TryGetValue(role.id, out var explanation);
                line.InsertChip(Math.Min(i, line.ChipCount), role, state,
                    RecommendationPresentation.CreateTooltip(
                        store, plan.Pawn, role, state, explanation, skillBuckets),
                    verdictsShown && !role.blocker
                        ? VerdictFor(plan.Pawn, role.id) : default);
            }

            var entry = new Dialog_ChangesPreview.PawnPreview { pawn = plan.Pawn };
            entry.lines.Add(line);
            return entry;
        }

        private List<PawnFixPlan> BuildColonyFixPlan(RoleStore? store, Map? map,
            ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            if (store == null)
            {
                Reset();
                return new List<PawnFixPlan>();
            }
            int mapId = map?.uniqueID ?? -1;
            if (planPawns == null || !ReferenceEquals(cohortOwner, store)
                || cohortRoster != UiVersion.Roster
                || cohortListRevision != stamp.PawnListRevision || cohortMapId != mapId)
            {
                List<Pawn> next = MapColonists(map);
                bool same = ReferenceEquals(cohortOwner, store) && planPawns != null
                    && planPawns.Count == next.Count;
                if (same)
                    for (int index = 0; index < next.Count; index++)
                        if (!ReferenceEquals(planPawns![index], next[index])) { same = false; break; }
                if (!same)
                {
                    pawnPlans.Clear();
                    planPawns = next;
                }
                cohortOwner = store;
                cohortRoster = UiVersion.Roster;
                cohortListRevision = stamp.PawnListRevision;
                cohortMapId = mapId;
            }
            List<Pawn> pawns = planPawns!;
            RecommendationSnapshot snapshot = producer.For(store, pawns, externalSnapshot);
            if (plans != null && ReferenceEquals(planSnapshot, snapshot)
                && planAssignmentPresentation == UiVersion.AssignmentPresentation)
                return plans;
            planSnapshot = snapshot;
            planAssignmentPresentation = UiVersion.AssignmentPresentation;
            RecommendationPlan recommendations = snapshot.Plan;
            this.recommendations = recommendations;
            var result = new List<PawnFixPlan>(pawns.Count);
            IReadOnlyList<Dictionary<int, SignalBucket>> suitability = snapshot.Suitability;
            planSuitability.Clear();
            for (int i = 0; i < pawns.Count; i++)
                planSuitability[pawns[i]] = suitability[i];

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                result.Add(pawnPlans.Get(pawn,
                    (recommendations, UiVersion.PresentationForPawn(pawn)), (this, i), BuildPawnPlan));
            }
            if (plans != null && plans.Count == result.Count)
            {
                bool equal = true;
                for (int index = 0; index < result.Count; index++)
                    if (!ReferenceEquals(plans[index], result[index])) { equal = false; break; }
                if (equal) return plans;
            }
            return result;
        }

        private PawnFixPlan BuildPawnFixPlan(int i)
        {
                RoleStore store = planOwner!;
                Pawn pawn = planPawns![i];
                RecommendationPlan recommendations = this.recommendations!;
                store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
                List<RoleAssignment> existing =
                    set?.assignments ?? new List<RoleAssignment>();
                PawnFixTarget fixTarget = planSnapshot!.FixTargets[i];
                var target = new List<RoleAssignment>(fixTarget.RoleIds.Count);
                for (int roleIndex = 0;
                     roleIndex < fixTarget.RoleIds.Count;
                     roleIndex++)
                {
                    int roleId = fixTarget.RoleIds[roleIndex];
                    RoleAssignment held = existing.FirstOrDefault(
                        assignment => assignment.roleId == roleId);
                    target.Add(new RoleAssignment
                    {
                        roleId = roleId,
                        state = held?.state ?? AssignmentState.Enabled,
                        pinned = held?.pinned ?? false,
                    });
                }

                var plan = new PawnFixPlan(pawn, target, fixTarget.Changed);
                for (int roleIndex = 0;
                     roleIndex < recommendations.RoleCountAt(i);
                     roleIndex++)
                {
                    int roleId = recommendations.RoleAt(i, roleIndex);
                    if (recommendations.TryGetExplanation(
                            i,
                            roleId,
                            out RoleRecommendationExplanation explanation))
                        plan.Explanations[roleId] = explanation;
                }
                for (int existingIndex = 0;
                     existingIndex < existing.Count;
                     existingIndex++)
                {
                    int roleId = existing[existingIndex].roleId;
                    if (!plan.Explanations.ContainsKey(roleId)
                        && recommendations.TryGetExplanation(
                            i,
                            roleId,
                            out RoleRecommendationExplanation explanation))
                        plan.Explanations[roleId] = explanation;
                }
                var targetIds = new HashSet<int>(target.Select(a => a.roleId));
                var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
                foreach (RoleAssignment assignment in target)
                {
                    if (existingIds.Contains(assignment.roleId)) continue;
                    Role? role = store.RoleById(assignment.roleId);
                    if (role != null) plan.Added.Add(role);
                }
                foreach (RoleAssignment assignment in existing)
                {
                    if (targetIds.Contains(assignment.roleId)) continue;
                    Role? role = store.RoleById(assignment.roleId);
                    if (role != null) plan.Removed.Add(role);
                }
                plan.SealPresentationDependencies();
                return plan;
        }

        private static List<Pawn> MapColonists(Map? map)
        {
            return ColonyScope.PawnsOnMap(map!) // PawnsOnMap handles a null map
                .Where(pawn => !pawn.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }
    }

    internal sealed class PawnFixPlan
    {
        private int[] presentationRoleIds = Array.Empty<int>();
        internal PawnFixPlan(Pawn pawn, List<RoleAssignment> target, bool orderChanged)
        {
            Pawn = pawn;
            Target = target;
            OrderChanged = orderChanged;
        }

        internal Pawn Pawn { get; }
        internal List<RoleAssignment> Target { get; }
        internal List<Role> Added { get; } = new List<Role>();
        internal List<Role> Removed { get; } = new List<Role>();
        internal bool OrderChanged { get; }
        internal bool HasChanges => Added.Count > 0 || Removed.Count > 0 || OrderChanged;
        internal Dictionary<int, RoleRecommendationExplanation> Explanations { get; } =
            new Dictionary<int, RoleRecommendationExplanation>();

        internal void SealPresentationDependencies()
        {
            var roles = new HashSet<int>();
            for (int index = 0; index < Target.Count; index++) roles.Add(Target[index].roleId);
            for (int index = 0; index < Removed.Count; index++) roles.Add(Removed[index].id);
            foreach (var pair in Explanations) AddExplanationRoles(pair.Value, roles);
            presentationRoleIds = new int[roles.Count];
            roles.CopyTo(presentationRoleIds);
        }

        private static void AddExplanationRoles(RoleRecommendationExplanation explanation, HashSet<int> roles)
        {
            roles.Add(explanation.RoleId);
            if (explanation.RelatedRoleId >= 0) roles.Add(explanation.RelatedRoleId);
            for (int index = 0; index < explanation.BundledMembers.Count; index++)
                AddExplanationRoles(explanation.BundledMembers[index], roles);
        }

        internal int PresentationRevision()
        {
            int revision = 0;
            for (int index = 0; index < presentationRoleIds.Length; index++)
            {
                int id = presentationRoleIds[index];
                revision = Math.Max(revision, UiVersion.PresentationForRole(id));
                revision = Math.Max(revision, UiVersion.ActivationForRole(id));
                revision = Math.Max(revision, UiVersion.RulesForRole(id));
            }
            return revision;
        }
    }

    internal readonly struct ColonistRecommendationRenderChip
    {
        internal ColonistRecommendationRenderChip(RoleChipRenderData chip,
            bool assigned, ChipStyle style, bool removedOutline, Rect rect,
            RoleChipVerdict verdict, StructuredTip? tooltip, string? fallbackTip,
            int insertIndex)
        {
            Chip = chip;
            Assigned = assigned;
            Style = style;
            RemovedOutline = removedOutline;
            Rect = rect;
            Verdict = verdict;
            Tooltip = tooltip;
            FallbackTip = fallbackTip;
            InsertIndex = insertIndex;
        }

        internal RoleChipRenderData Chip { get; }
        internal bool Assigned { get; }
        internal ChipStyle Style { get; }
        internal bool RemovedOutline { get; }
        internal Rect Rect { get; }
        internal RoleChipVerdict Verdict { get; }
        internal StructuredTip? Tooltip { get; }
        internal string? FallbackTip { get; }
        internal int InsertIndex { get; }

        internal bool ContentEquals(ColonistRecommendationRenderChip other)
        {
            if (!Chip.ContentEquals(other.Chip)
                || Assigned != other.Assigned || Style != other.Style
                || RemovedOutline != other.RemovedOutline
                || InsertIndex != other.InsertIndex
                || Rect.x != other.Rect.x || Rect.y != other.Rect.y
                || Rect.width != other.Rect.width
                || Rect.height != other.Rect.height
                || !VerdictEquals(Verdict, other.Verdict)
                || !string.Equals(FallbackTip, other.FallbackTip,
                    StringComparison.Ordinal))
                return false;
            if (ReferenceEquals(Tooltip, other.Tooltip)) return true;
            return Tooltip != null && other.Tooltip != null
                && string.Equals(Tooltip.StableKey, other.Tooltip.StableKey,
                    StringComparison.Ordinal)
                && RecommendationTipEquals(Tooltip.Model,
                    other.Tooltip.Model);
        }

        private static bool RecommendationTipEquals(TipModel left,
            TipModel right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null
                || !string.Equals(left.Title, right.Title,
                    StringComparison.Ordinal)
                || !string.Equals(left.Badge, right.Badge,
                    StringComparison.Ordinal)
                || !ColorEquals(left.BadgeColor, right.BadgeColor)
                || left.Padding != right.Padding
                || left.Sections.Count != right.Sections.Count)
                return false;
            for (int sectionIndex = 0;
                    sectionIndex < left.Sections.Count; sectionIndex++)
            {
                TipSection leftSection = left.Sections[sectionIndex];
                TipSection rightSection = right.Sections[sectionIndex];
                if (!string.Equals(leftSection.Header, rightSection.Header,
                        StringComparison.Ordinal)
                    || leftSection.Rows.Count != rightSection.Rows.Count)
                    return false;
                for (int rowIndex = 0;
                        rowIndex < leftSection.Rows.Count; rowIndex++)
                {
                    TipFactRow? leftFact =
                        leftSection.Rows[rowIndex] as TipFactRow;
                    TipFactRow? rightFact =
                        rightSection.Rows[rowIndex] as TipFactRow;
                    if (leftFact == null || rightFact == null
                        || !string.Equals(leftFact.Label, rightFact.Label,
                            StringComparison.Ordinal)
                        || !string.Equals(leftFact.Value, rightFact.Value,
                            StringComparison.Ordinal)
                        || !Nullable.Equals(leftFact.ValueColor,
                            rightFact.ValueColor)
                        || !Nullable.Equals(leftFact.LabelColor,
                            rightFact.LabelColor))
                        return false;
                }
            }
            return true;
        }

        private static bool VerdictEquals(RoleChipVerdict left,
            RoleChipVerdict right) => left.Shown == right.Shown
            && ColorEquals(left.Bottom, right.Bottom)
            && ColorEquals(left.Top, right.Top);

        private static bool ColorEquals(Color left, Color right) =>
            left.r == right.r && left.g == right.g && left.b == right.b
            && left.a == right.a;
    }

    internal sealed class ColonistRecommendationRenderSnapshot
    {
        private readonly List<ColonistRecommendationRenderChip> chips;
        private readonly List<RoleAssignment> target;

        internal ColonistRecommendationRenderSnapshot(Pawn pawn,
            List<ColonistRecommendationRenderChip> chips,
            List<RoleAssignment> target, bool hasChanges, string headerLabel,
            string applyLabel, Rect headerRect, Rect applyRect)
        {
            Pawn = pawn;
            this.chips = chips;
            this.target = target;
            HasChanges = hasChanges;
            HeaderLabel = headerLabel;
            ApplyLabel = applyLabel;
            HeaderRect = headerRect;
            ApplyRect = applyRect;
        }

        internal Pawn Pawn { get; }
        internal bool HasChanges { get; }
        internal string HeaderLabel { get; }
        internal string ApplyLabel { get; }
        internal Rect HeaderRect { get; }
        internal Rect ApplyRect { get; }
        internal int ChipCount => chips.Count;
        internal ColonistRecommendationRenderChip ChipAt(int index) =>
            chips[index];

        internal void Apply() => RoleCommands.PasteRoleSet(Pawn, target);

        internal bool ContentEquals(ColonistRecommendationRenderSnapshot other)
        {
            if (other == null || !ReferenceEquals(Pawn, other.Pawn)
                || HasChanges != other.HasChanges
                || !string.Equals(HeaderLabel, other.HeaderLabel,
                    StringComparison.Ordinal)
                || !string.Equals(ApplyLabel, other.ApplyLabel,
                    StringComparison.Ordinal)
                || !RectEquals(HeaderRect, other.HeaderRect)
                || !RectEquals(ApplyRect, other.ApplyRect)
                || chips.Count != other.chips.Count
                || target.Count != other.target.Count)
                return false;
            for (int i = 0; i < chips.Count; i++)
                if (!chips[i].ContentEquals(other.chips[i])) return false;
            for (int i = 0; i < target.Count; i++)
            {
                RoleAssignment left = target[i];
                RoleAssignment right = other.target[i];
                if (left.roleId != right.roleId || left.state != right.state
                    || left.pinned != right.pinned)
                    return false;
            }
            return true;
        }

        private static bool RectEquals(Rect left, Rect right) =>
            left.x == right.x && left.y == right.y
            && left.width == right.width && left.height == right.height;
    }
}
