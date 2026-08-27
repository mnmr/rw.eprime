using System.Collections.Generic;
using Multiplayer.API;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Hourly automatic Fix My Colony. Runs from each map's 2500-tick hour
    /// boundary (WorkRolesMapComponent) as deterministic simulation code:
    /// every multiplayer client computes the same plan from synced state and
    /// applies it inline — an owner-approved exception to the sync-method
    /// rule, valid because the shared simulation clock is the synchronizer
    /// and RoleCommands sync interception is inert during ticking. Nothing
    /// client-local (current map, view faction, open windows) may influence
    /// the multiplayer outcome.
    internal static class AutoOptimizer
    {
        internal static void RunForMap(Map map)
        {
            RoleStore? store = RoleStore.Current;
            if (store == null || !store.autoOptimize) return;
            // Only player colony locations (settlements and gravships) are
            // planned; encounter and other transient maps are not.
            if (!ColonyScope.IsColonyLocationForSimulation(map)) return;
            // While a Fix My Colony preview is under review, its dialog stays
            // the only writer. Single-player only: window state is
            // client-local, so multiplayer relies on the preview's own
            // stale-plan handling instead.
            if (!MP.IsInMultiplayer && Find.WindowStack
                    ?.WindowOfType<UI.Dialog_ChangesPreview>() != null)
                return;

            List<Pawn> pawns = SimulationColonists(map);
            if (pawns.Count == 0) return;
            ColonyView colony = RecsAdapter.BuildColonyView(store, pawns);
            IReadOnlyList<PawnFixTarget> targets = ColonyFixPlanner.Build(
                colony,
                store.recommendationTuning
                    ?? RecommendationsTuningOptions.Default);
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                PawnFixTarget target = targets[pawnIndex];
                if (!target.Changed) continue;
                RoleCommands.PasteRoleSet(pawns[pawnIndex],
                    MaterializeAssignments(store, pawns[pawnIndex], target));
            }
        }

        /// The map's colonists in spawn-list order (synced sim state), the
        /// same cohort Fix My Colony plans: faction colonists and slaves, no
        /// babies. ColonyScope.PawnsOnMap is deliberately not reused — its
        /// view faction is client-local under multifaction multiplayer.
        private static List<Pawn> SimulationColonists(Map map)
        {
            var result = new List<Pawn>();
            RimWorld.Faction faction = RimWorld.Faction.OfPlayer;
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < spawned.Count; index++)
            {
                Pawn pawn = spawned[index];
                if (pawn?.Faction == faction
                    && (pawn.IsFreeColonist || pawn.IsSlaveOfColony)
                    && !pawn.DevelopmentalStage.Baby())
                    result.Add(pawn);
            }
            return result;
        }

        /// The target in store terms: recommended order wins, kept roles
        /// preserve their stored state and pin, added roles arrive enabled
        /// and unpinned — the same materialization the Fix My Colony preview
        /// applies.
        private static List<RoleAssignment> MaterializeAssignments(
            RoleStore store, Pawn pawn, PawnFixTarget target)
        {
            store.pawnSets.TryGetValue(pawn, out PawnRoleSet set);
            List<RoleAssignment>? existing = set?.assignments;
            var result = new List<RoleAssignment>(target.RoleIds.Count);
            for (int index = 0; index < target.RoleIds.Count; index++)
            {
                int roleId = target.RoleIds[index];
                RoleAssignment? held = null;
                if (existing != null)
                    for (int existingIndex = 0;
                         existingIndex < existing.Count;
                         existingIndex++)
                        if (existing[existingIndex].roleId == roleId)
                        {
                            held = existing[existingIndex];
                            break;
                        }
                result.Add(new RoleAssignment
                {
                    roleId = roleId,
                    state = held?.state ?? AssignmentState.Enabled,
                    pinned = held?.pinned ?? false,
                });
            }
            return result;
        }
    }
}
