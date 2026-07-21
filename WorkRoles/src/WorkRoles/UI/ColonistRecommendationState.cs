using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;

namespace WorkRoles.UI
{
    /// Owns recommendation-engine results and their preview projections. The
    /// view decides when to display or apply a plan; this state never issues a
    /// command or opens a window.
    internal sealed class ColonistRecommendationState
    {
        private static readonly IReadOnlyList<
            (Role role, Dialog_ChangesPreview.ChipState state, string tip)> NoChips =
                Array.Empty<(Role, Dialog_ChangesPreview.ChipState, string)>();

        private List<PawnFixPlan> plans;
        private ScopeCacheStamp planStamp = ScopeCacheStamp.Invalid;
        private int planMapId = -1;

        private ScopeCacheStamp previewStamp = ScopeCacheStamp.Invalid;
        private Pawn previewPawn;
        private IReadOnlyList<PawnFixPlan> previewSource;
        private ColonistRecommendationPreview preview;

        internal void Reset()
        {
            plans = null;
            planStamp = ScopeCacheStamp.Invalid;
            planMapId = -1;
            ClearPreview();
        }

        internal void InvalidatePlan()
        {
            plans = null;
            ClearPreview();
        }

        internal void InvalidateLanguageCaches()
        {
            ClearPreview();
        }

        internal void ReleaseSnapshots() => Reset();

        private void ClearPreview()
        {
            previewStamp = ScopeCacheStamp.Invalid;
            previewPawn = null;
            previewSource = null;
            preview = null;
        }

        internal IReadOnlyList<PawnFixPlan> Plans(Pawn anchor, ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            Map map = anchor?.MapHeld ?? Find.CurrentMap;
            int mapId = map?.uniqueID ?? -1;
            if (plans == null || planStamp != stamp || planMapId != mapId)
            {
                planStamp = stamp;
                planMapId = mapId;
                plans = BuildColonyFixPlan(map, externalSnapshot);
            }
            return plans;
        }

        internal PawnFixPlan PlanFor(Pawn pawn, Pawn anchor, ScopeCacheStamp stamp,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
            => Plans(anchor, stamp, externalSnapshot)
                .FirstOrDefault(plan => plan.Pawn == pawn);

        internal ColonistRecommendationPreview Preview(RoleStore store, Pawn pawn,
            ScopeCacheStamp stamp, Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            externalSnapshot(pawn);
            IReadOnlyList<PawnFixPlan> source = Plans(
                pawn, stamp, externalSnapshot);
            if (preview != null && previewStamp == stamp
                && previewPawn == pawn && previewSource == source)
                return preview;

            previewStamp = stamp;
            previewPawn = pawn;
            previewSource = source;
            PawnFixPlan plan = source.FirstOrDefault(candidate => candidate.Pawn == pawn);
            if (plan == null)
                preview = new ColonistRecommendationPreview(null, NoChips, null);
            else
            {
                Dialog_ChangesPreview.Line line = BuildPreviewEntry(
                    store, plan, externalSnapshot).lines[0];
                preview = new ColonistRecommendationPreview(plan, line.chips, line);
            }
            return preview;
        }

        internal List<Dialog_ChangesPreview.PawnPreview> FixEntries(RoleStore store,
            Pawn only, Pawn anchor, ScopeCacheStamp stamp,
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

        internal List<Role> RecommendedRoles(RoleStore store, Pawn pawn, Pawn anchor,
            ScopeCacheStamp stamp, Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            PawnFixPlan plan = PlanFor(pawn, anchor, stamp, externalSnapshot);
            if (plan == null) return new List<Role>();
            var result = new List<Role>();
            foreach (RoleAssignment assignment in plan.Target)
            {
                Role role = store.RoleById(assignment.roleId);
                if (role != null) result.Add(role);
            }
            return result;
        }

        private static Dialog_ChangesPreview.PawnPreview BuildPreviewEntry(
            RoleStore store, PawnFixPlan plan,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            store.pawnSets.TryGetValue(plan.Pawn, out PawnRoleSet set);
            List<RoleAssignment> existing = set?.assignments ?? new List<RoleAssignment>();
            var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
            var targetIds = new HashSet<int>(plan.Target.Select(a => a.roleId));
            SkillBucketSnapshot skillBuckets = externalSnapshot(plan.Pawn)
                .Signals.SkillBuckets;

            var line = new Dialog_ChangesPreview.Line();
            foreach (RoleAssignment assignment in plan.Target)
            {
                Role role = store.RoleById(assignment.roleId);
                if (role == null) continue;
                bool kept = existingIds.Contains(assignment.roleId);
                var state = kept
                    ? Dialog_ChangesPreview.ChipState.Kept
                    : Dialog_ChangesPreview.ChipState.Added;
                plan.Explanations.TryGetValue(role.id, out var explanation);
                line.AddChip(role, state, RecommendationPresentation.CreateTooltip(
                    store, plan.Pawn, role, state, explanation, skillBuckets));
            }
            for (int i = 0; i < existing.Count; i++)
            {
                if (targetIds.Contains(existing[i].roleId)) continue;
                Role role = store.RoleById(existing[i].roleId);
                if (role == null) continue;
                var state = Dialog_ChangesPreview.ChipState.Removed;
                plan.Explanations.TryGetValue(role.id, out var explanation);
                line.InsertChip(Math.Min(i, line.chips.Count), role, state,
                    RecommendationPresentation.CreateTooltip(
                        store, plan.Pawn, role, state, explanation, skillBuckets));
            }

            var entry = new Dialog_ChangesPreview.PawnPreview { pawn = plan.Pawn };
            entry.lines.Add(line);
            return entry;
        }

        private static List<PawnFixPlan> BuildColonyFixPlan(Map map,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            var result = new List<PawnFixPlan>();
            RoleStore store = RoleStore.Current;
            if (store == null) return result;
            List<Pawn> pawns = MapColonists(map);
            var recommendations = RecsEngine.Run(
                RecsAdapter.BuildColonyView(store, pawns, externalSnapshot));

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
                List<RoleAssignment> existing =
                    set?.assignments ?? new List<RoleAssignment>();
                List<RoleAssignment> target = recommendations[i].Assignments
                    .Select(a => new RoleAssignment
                    {
                        roleId = a.RoleId,
                        enabled = a.Enabled,
                        pinned = a.Pinned,
                    })
                    .ToList();

                var plan = new PawnFixPlan(
                    pawn,
                    target,
                    !existing.Select(a => a.roleId)
                        .SequenceEqual(target.Select(a => a.roleId)));
                foreach (var pair in recommendations[i].Explanations)
                    plan.Explanations[pair.Key] = pair.Value;

                var targetIds = new HashSet<int>(target.Select(a => a.roleId));
                var existingIds = new HashSet<int>(existing.Select(a => a.roleId));
                foreach (RoleAssignment assignment in target)
                {
                    if (existingIds.Contains(assignment.roleId)) continue;
                    Role role = store.RoleById(assignment.roleId);
                    if (role != null) plan.Added.Add(role);
                }
                foreach (RoleAssignment assignment in existing)
                {
                    if (targetIds.Contains(assignment.roleId)) continue;
                    Role role = store.RoleById(assignment.roleId);
                    if (role != null) plan.Removed.Add(role);
                }
                result.Add(plan);
            }
            return result;
        }

        private static List<Pawn> MapColonists(Map map)
        {
            if (map == null) return new List<Pawn>();
            return map.mapPawns.FreeColonistsSpawned
                .Concat(map.mapPawns.SlavesOfColonySpawned)
                .Where(pawn => !pawn.DevelopmentalStage.Baby())
                .Distinct()
                .ToList();
        }
    }

    internal sealed class PawnFixPlan
    {
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
    }

    internal sealed class ColonistRecommendationPreview
    {
        internal ColonistRecommendationPreview(PawnFixPlan plan,
            IReadOnlyList<(Role role, Dialog_ChangesPreview.ChipState state, string tip)> chips,
            Dialog_ChangesPreview.Line line)
        {
            Plan = plan;
            Chips = chips;
            Line = line;
        }

        internal PawnFixPlan Plan { get; }
        internal IReadOnlyList<
            (Role role, Dialog_ChangesPreview.ChipState state, string tip)> Chips { get; }
        internal Dialog_ChangesPreview.Line Line { get; }
    }
}
