using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One catalog role as target planning sees it (game-independent projection).
    public class TargetRole
    {
        public int Id;
        /// Expanded job coverage (CoverageMath.CoverageOf) — the nesting/redundancy identity.
        public HashSet<string> Coverage = new HashSet<string>();
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool Unskilled;          // no relevant skills
        public bool Doctoring;          // touches the Doctor work type
        public float NaturalPriority;   // max member work-type priority (auto block order)
        /// Roles this one trains toward (resolved ids).
        public List<int> TrainTargets = new List<int>();
    }

    public struct PlannedAssignment
    {
        public int RoleId;
        public bool Enabled;
        public bool Pinned;
    }

    /// Assembles a pawn's replacement role list. MEMBERSHIP comes from the
    /// pawn's existing roles, its recommendations and the colony plan's grants
    /// (covered unskilled singles never ride alongside their coverer). ORDER
    /// is the recommendation-order template: every role — autos included —
    /// sorts by its template position, the same ordering every other surface
    /// uses. Hunter tiers override: tier 0 hunts before all skilled work,
    /// tier 2 dead last, and hunting never outranks doctoring. Protected
    /// assignments (rule-carrying, blockers, pinned) skip
    /// normal placement and re-enter at their original position. Retained
    /// roles keep their per-pawn toggle.
    public static class TargetPlanner
    {
        public static List<PlannedAssignment> Build(
            IReadOnlyList<PlannedAssignment> existing,
            IReadOnlyList<TargetRole> catalog,
            IReadOnlyList<int> recommendations,
            IReadOnlyList<int> extraIds,
            IReadOnlyList<int> promoted,
            IReadOnlyDictionary<int, long> positions,
            int hunterTier, int hunterRoleId)
        {
            var byId = catalog.ToDictionary(r => r.Id);
            TargetRole RoleOf(int id) => byId.TryGetValue(id, out var role) ? role : null;
            bool hunterKnown = RoleOf(hunterRoleId) != null;

            var protectedIds = new HashSet<int>();
            foreach (var a in existing)
            {
                var role = RoleOf(a.RoleId);
                if (a.Pinned || role?.HasRules == true || role?.Blocker == true)
                    protectedIds.Add(a.RoleId);
            }

            var target = new List<PlannedAssignment>();
            void Add(int roleId)
            {
                if (protectedIds.Contains(roleId)) return;
                if (target.Any(a => a.RoleId == roleId)) return;
                bool enabled = true, pinned = false;
                foreach (var a in existing)
                    if (a.RoleId == roleId) { enabled = a.Enabled; pinned = a.Pinned; break; }
                target.Add(new PlannedAssignment { RoleId = roleId, Enabled = enabled, Pinned = pinned });
            }
            bool IsTieredHunter(int roleId)
                => hunterTier == 2 && hunterKnown && roleId == hunterRoleId;

            bool Covers(TargetRole a, TargetRole b)
            {
                if (a == null || b == null || ReferenceEquals(a, b)) return false;
                return CoverageMath.MakesRedundant(a.Coverage, a.Id, b.Coverage, b.Id);
            }

            bool CoveredByPlan(int roleId)
            {
                var role = RoleOf(roleId);
                if (role == null) return false;
                foreach (var a in target)
                    if (Covers(RoleOf(a.RoleId), role)) return true;
                foreach (var recId in recommendations)
                    if (Covers(RoleOf(recId), role)) return true;
                return false;
            }

            // Auto-assign roles lead, ordered by their work-type priority (the flag
            // grants membership in the block; content decides the order within it).
            foreach (var role in catalog.Where(r => r.AutoAssign).OrderByDescending(r => r.NaturalPriority))
                Add(role.Id);

            if (hunterTier == 0 && hunterKnown) Add(hunterRoleId);
            if (promoted != null)
                foreach (var id in promoted) Add(id);
            if (hunterTier == 1 && hunterKnown) Add(hunterRoleId);

            int firstSkilled = -1;
            for (int i = 0; i < existing.Count; i++)
            {
                var r = RoleOf(existing[i].RoleId);
                if (r != null && !r.AutoAssign && !r.HasRules && !r.Unskilled) { firstSkilled = i; break; }
            }
            var trailingUnskilled = new List<int>();
            for (int i = 0; i < existing.Count; i++)
            {
                var r = RoleOf(existing[i].RoleId);
                if (r == null || !r.Unskilled || CoveredByPlan(r.Id)) continue;
                if (firstSkilled < 0 || i < firstSkilled) Add(r.Id);
                else trailingUnskilled.Add(r.Id);
            }

            var recUnskilled = new List<int>();
            foreach (var recId in recommendations)
            {
                var role = RoleOf(recId);
                if (role == null) continue;
                if (role.AutoAssign || IsTieredHunter(recId)) continue;
                if (role.Unskilled) { recUnskilled.Add(recId); continue; }
                Add(recId);
            }

            if (extraIds != null)
                foreach (var id in extraIds)
                {
                    var r = RoleOf(id);
                    if (r == null || IsTieredHunter(id)) continue;
                    if (!r.HasRules && !r.AutoAssign && !r.Unskilled) Add(id);
                }

            foreach (var id in trailingUnskilled)
                if (!CoveredByPlan(id)) Add(id);
            foreach (var id in recUnskilled)
                if (!CoveredByPlan(id)) Add(id);
            if (extraIds != null)
                foreach (var id in extraIds)
                {
                    var r = RoleOf(id);
                    if (r != null && !r.HasRules && r.Unskilled && !CoveredByPlan(id)) Add(id);
                }
            if (hunterTier == 2 && hunterKnown) Add(hunterRoleId);

            // Template-driven ordering (membership was settled above): every
            // role — autos included — sorts by its template position. Hunter
            // tiers override: tier 0 sits between the above-template autos
            // (-100) and the first template slot (0), so food comes before all
            // skilled work but never before emergencies; tier 2 goes dead
            // last. OrderBy is stable, so ties keep assembly order.
            long PositionKey(int roleId)
            {
                if (hunterKnown && roleId == hunterRoleId)
                {
                    if (hunterTier == 0) return -50;
                    if (hunterTier == 2) return long.MaxValue;
                }
                return positions != null && positions.TryGetValue(roleId, out long p) ? p : 0L;
            }
            var ordered = target.OrderBy(a => PositionKey(a.RoleId)).ToList();
            target.Clear();
            target.AddRange(ordered);

            // Hunting never outranks doctoring: when the target holds both, the
            // hunter role demotes to just after the last doctoring role above it.
            if (hunterKnown)
            {
                int hunterIdx = target.FindIndex(a => a.RoleId == hunterRoleId);
                if (hunterIdx >= 0)
                {
                    int lastDoctoring = -1;
                    for (int i = 0; i < target.Count; i++)
                    {
                        var r = RoleOf(target[i].RoleId);
                        if (r != null && r.Id != hunterRoleId && r.Doctoring)
                            lastDoctoring = i;
                    }
                    if (lastDoctoring > hunterIdx)
                    {
                        var hunter = target[hunterIdx];
                        target.RemoveAt(hunterIdx);
                        target.Insert(lastDoctoring, hunter);
                    }
                }
            }

            // Training relations shape the final order: a planned train target
            // that COVERS its training role replaces it outright; one that
            // doesn't moves directly above it, supplementing the trainer's jobs
            // (Fabricator slots above Smith).
            for (int i = 0; i < target.Count; i++)
            {
                var trainer = RoleOf(target[i].RoleId);
                if (trainer == null || trainer.TrainTargets.Count == 0) continue;
                bool replaced = false;
                foreach (int trainedId in trainer.TrainTargets)
                {
                    var trained = RoleOf(trainedId);
                    if (trained == null || !target.Any(a => a.RoleId == trainedId)) continue;
                    if (CoverageMath.CoversOrMatches(trained.Coverage, trainer.Coverage))
                    {
                        target.RemoveAt(i);
                        i--;
                        replaced = true;
                        break;
                    }
                }
                if (replaced) continue;
                foreach (int trainedId in trainer.TrainTargets)
                {
                    int at = target.FindIndex(a => a.RoleId == trainedId);
                    int trainerAt = target.FindIndex(a => a.RoleId == trainer.Id);
                    if (trainerAt >= 0 && at > trainerAt)
                    {
                        var entry = target[at];
                        target.RemoveAt(at);
                        target.Insert(trainerAt, entry);
                    }
                }
            }

            // Protected assignments re-enter at min(original index, target count),
            // keeping their per-pawn toggle.
            for (int i = 0; i < existing.Count; i++)
            {
                var role = RoleOf(existing[i].RoleId);
                if (role == null) continue;
                if (!role.HasRules && !role.Blocker && !existing[i].Pinned) continue;
                if (target.Any(a => a.RoleId == existing[i].RoleId)) continue;
                int at = System.Math.Min(i, target.Count);
                target.Insert(at, existing[i]);
            }
            return target;
        }
    }
}
