using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    public partial class QualityJobsStore
    {
        // Cache contract — Owner: per-save store. Key: unique thing ID, with map
        // identity in each entry. Value: immutable quality/map snapshot for a
        // spawned player building with CompQuality and build blueprint/frame defs.
        // Dependencies: spawn/map, faction, quality and definition database.
        // Refresh: spawn/despawn, faction/quality events, load and definition reload.
        // Equality: equal quality/map facts preserve snapshot identity.
        // Teardown: despawn removes an entry, ReleaseMap removes its map's entries,
        // ReleasePresentation clears all. No periodic polling or retained Things.
        private readonly Dictionary<int, CompletedBuildingSnapshot> completedBuildings =
            new Dictionary<int, CompletedBuildingSnapshot>();

        internal bool TryGetCompletedBuilding(int id, out CompletedBuildingSnapshot? value)
            => completedBuildings.TryGetValue(id, out value);

        internal void NotifyCompletedBuildingChanged(Thing thing)
        {
            if (!(thing is Building) || thing is Frame) return;
            if (!CompletedBuilding.IsCandidate(thing))
            { completedBuildings.Remove(thing.thingIDNumber); return; }
            QualityCategory quality = thing.TryGetComp<CompQuality>()!.Quality;
            if (completedBuildings.TryGetValue(thing.thingIDNumber, out var old)
                && ReferenceEquals(old.Map, thing.Map) && old.Quality == quality) return;
            completedBuildings[thing.thingIDNumber] = new CompletedBuildingSnapshot(thing.Map, quality);
        }

        private void RebuildCompletedBuildings()
        {
            // Lifecycle/definition boundary only; maps and things are traversed
            // here, never from GetGizmos or a window draw pass.
            var seen = new HashSet<int>();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> things = maps[m].listerThings.AllThings;
                for (int i = 0; i < things.Count; i++)
                {
                    if (!(things[i] is Building) || things[i] is Frame) continue;
                    NotifyCompletedBuildingChanged(things[i]);
                    if (completedBuildings.ContainsKey(things[i].thingIDNumber))
                        seen.Add(things[i].thingIDNumber);
                }
            }
            var removed = new List<int>();
            foreach (var entry in completedBuildings)
                if (!seen.Contains(entry.Key)) removed.Add(entry.Key);
            for (int i = 0; i < removed.Count; i++) completedBuildings.Remove(removed[i]);
        }

        private void ReleaseCompletedBuildings(Map map)
        {
            var removed = new List<int>();
            foreach (var entry in completedBuildings)
                if (ReferenceEquals(entry.Value.Map, map)) removed.Add(entry.Key);
            for (int i = 0; i < removed.Count; i++) completedBuildings.Remove(removed[i]);
        }

        internal void StartRebuild(int thingId, int minSkill, bool inspired,
            bool specialist, int minQuality, bool autoBest)
        {
            minQuality = ConfigurationLimits.Quality(minQuality);
            if (FindPlanById(thingId) != null) return;
            if (!(FindSpawnedThing(thingId) is Building building)
                || !CompletedBuilding.CanStart(building)) return;
            if (!RetryDecision.ShouldRetry(true,
                (QualityLevel)building.TryGetComp<CompQuality>()!.Quality,
                (QualityLevel)minQuality)) return;
            var plan = new ConstructionPlan
            {
                target = building, state = ConstructionPlanState.AwaitingRebuild,
                minSkill = ConfigurationLimits.Skill(minSkill),
                requireInspired = inspired,
                requireSpecialist = specialist && ModsConfig.IdeologyActive,
                minQuality = minQuality, autoBest = autoBest
            };
            building.Map.designationManager.AddDesignation(new Designation(building, DesignationDefOf.Deconstruct));
            AddPlan(plan);
        }

        internal void ClearPlanSettings(ConstructionPlan plan)
        {
            Dispatcher.RemoveOurDeconstructDesignation(plan);
            if (plan.target is Building existing && !(existing is Frame))
                plan.rebuildSettings?.Apply(existing, accepted: true);
            if (plan.rebuildSettings == null || plan.target is Building && !(plan.target is Frame))
            { RemovePlan(plan); return; }
            if (plan.restoreOnly) return;
            // Cancellation after demolition must not lose the original settings.
            // This carrier gates no work and is excluded from UI/API/overlays.
            plan.restoreOnly = true;
            plan.minSkill = plan.minQuality = 0;
            plan.requireInspired = plan.requireSpecialist = plan.autoBest = false;
            plan.finisher = null;
            plan.state = ConstructionPlanState.Active;
            NotifyPlanStructureChanged();
        }

        internal void NotifyDeconstructionCancelled(Thing target)
        {
            // Destroy also removes designations, while the old target is already
            // despawned. Its transition is owned by the rebuild hook.
            if (!target.Spawned) return;
            ConstructionPlan? plan = FindPlan(target);
            if (plan?.state == ConstructionPlanState.AwaitingRebuild)
            {
                if (target is Building building) plan.rebuildSettings?.Apply(building, accepted: true);
                RemovePlan(plan);
            }
        }

        internal void PreserveRebuildSettings(ConstructionPlan plan, BuildingRebuildSettings settings)
        {
            if (plan.rebuildSettings == null)
                plan.rebuildSettings = settings;
        }

        internal void CompleteBuiltPlan(ConstructionPlan plan, Thing built)
        {
            CompQuality? quality = built.TryGetComp<CompQuality>();
            bool retry = !plan.restoreOnly && quality != null
                && RetryDecision.ShouldRetry(true, (QualityLevel)quality.Quality,
                    (QualityLevel)plan.minQuality);
            if (built is Building building)
                plan.rebuildSettings?.Apply(building, accepted: !retry);
            if (!retry) { RemovePlan(plan); return; }
            RetargetPlan(plan, built, ConstructionPlanState.AwaitingRebuild);
            built.Map.designationManager.AddDesignation(new Designation(built, DesignationDefOf.Deconstruct));
        }
    }
}
