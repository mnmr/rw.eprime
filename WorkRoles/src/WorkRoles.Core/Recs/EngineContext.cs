using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public enum SignalSource { None, Aggregated }

    /// Why a role is on a pawn's list. RuleId keys the UI's translated text;
    /// the payload fields fill its placeholders.
    public struct Reason
    {
        public string RuleId;
        public SignalBucket Bucket;
        public SignalSource Source;
        public string SkillDefName;   // null for content-keyed reasons
        /// Allowance substitutions: the needed role trained toward (-1 otherwise).
        public int TowardRoleId;
    }

    public sealed class Candidate
    {
        public int RoleId;
        public Reason Reason;
        public SignalBucket Strength = SignalBucket.Neutral;
    }

    /// The specific training path that produced a role assignment. This is
    /// separate from Candidate.Reason because a stronger existing reason may
    /// win while the selected path must still control placement.
    public struct TrainingPathPlacement
    {
        public int PathId;
        public int TargetRoleId;
    }

    /// This pawn's exact position in the need-driven candidate ordering for
    /// one role. OpenSlots is the shortage before the draft selected anyone.
    public struct DraftRanking
    {
        public int Rank;
        public int EligibleCount;
        public int OpenSlots;
        public string SkillDefName;
        public int SkillLevel;
    }

    /// Shared working state of one engine run. Rules read the colony views
    /// and contribute candidates / vetoes / results through this surface only.
    public class EngineContext
    {
        public readonly ColonyView Colony;
        public readonly Dictionary<int, RoleView> RolesById;
        /// Excluded roles: AddCandidate refuses them (force bypasses — fire safety).
        public readonly HashSet<int> Vetoed = new HashSet<int>();
        /// Per pawn, by role id; AddCandidate keeps the strongest entry.
        public readonly List<Dictionary<int, Candidate>> Candidates = new List<Dictionary<int, Candidate>>();
        /// Coverage want per role id (CoverageScalingRule fills it).
        public readonly Dictionary<int, int> Want = new Dictionary<int, int>();
        public readonly Dictionary<int, int> BaseWant = new Dictionary<int, int>();
        public readonly Dictionary<int, int> InboundTraining = new Dictionary<int, int>();
        /// Effective exact-role maximums computed by HolderLimitRule with its
        /// injected demand policy. Explanations consume these decision facts
        /// instead of independently re-running a policy.
        public readonly Dictionary<int, int> EffectiveMaxHolders = new Dictionary<int, int>();
        public readonly List<HashSet<int>> TrainingToward = new List<HashSet<int>>();
        public readonly List<Dictionary<int, TrainingPathPlacement>> TrainingPathPlacements =
            new List<Dictionary<int, TrainingPathPlacement>>();
        public readonly List<HashSet<int>> HolderLimitRejected = new List<HashSet<int>>();
        /// Per pawn, by role id; includes eligible candidates that were not
        /// selected and candidates considered after coverage was already full.
        public readonly List<Dictionary<int, DraftRanking>> DraftRankings =
            new List<Dictionary<int, DraftRanking>>();
        public readonly int[] HunterTiers;   // -1 = not hunting
        public readonly bool[] FireGranted;
        /// One per pawn; OrderingRule and ProtectedReentryRule fill them.
        public readonly List<PawnResult> Results = new List<PawnResult>();
        public readonly Dictionary<int, PathView> PathsById;

        // Run-invariant precomputes: roles/paths/coverage never change during a
        // run, so pairwise redundancy, ordering skeleton and per-role skill
        // lists are computed once instead of per pawn or per query.
        private readonly Dictionary<int, HashSet<int>> redundantBy;
        private readonly Dictionary<int, PathView> soloPathByRole;
        private readonly Dictionary<int, IReadOnlyList<RoleSkillView>> requiredSkillsByRole =
            new Dictionary<int, IReadOnlyList<RoleSkillView>>();
        private readonly Dictionary<int, List<RoleSkillView>> orderedSkillsByRole =
            new Dictionary<int, List<RoleSkillView>>();
        private Dictionary<int, long> basePositions;

        public EngineContext(ColonyView colony)
        {
            Colony = colony;
            RolesById = colony.Roles.ToDictionary(r => r.Id);
            PathsById = colony.Paths.ToDictionary(p => p.Id);
            HunterTiers = new int[colony.Pawns.Count];
            FireGranted = new bool[colony.Pawns.Count];
            for (int i = 0; i < colony.Pawns.Count; i++)
            {
                HunterTiers[i] = -1;
                Candidates.Add(new Dictionary<int, Candidate>());
                DraftRankings.Add(new Dictionary<int, DraftRanking>());
                TrainingToward.Add(new HashSet<int>());
                TrainingPathPlacements.Add(new Dictionary<int, TrainingPathPlacement>());
                HolderLimitRejected.Add(new HashSet<int>());
                Results.Add(new PawnResult());
            }

            redundantBy = new Dictionary<int, HashSet<int>>(colony.Roles.Count);
            foreach (var role in colony.Roles)
                redundantBy[role.Id] = new HashSet<int>();
            foreach (var covering in colony.Roles)
                foreach (var covered in colony.Roles)
                    if (covering.Id != covered.Id && CoverageMath.MakesRedundant(
                            covering.Coverage, covering.Id, covered.Coverage, covered.Id))
                        redundantBy[covered.Id].Add(covering.Id);

            soloPathByRole = new Dictionary<int, PathView>();
            var multiPathRoles = new HashSet<int>();
            foreach (var path in colony.Paths)
                foreach (int roleId in path.RoleIds)
                {
                    if (multiPathRoles.Contains(roleId)) continue;
                    if (soloPathByRole.ContainsKey(roleId))
                    {
                        soloPathByRole.Remove(roleId);
                        multiPathRoles.Add(roleId);
                    }
                    else soloPathByRole[roleId] = path;
                }
        }

        public RoleView RoleOf(int id) => RolesById.TryGetValue(id, out var role) ? role : null;

        /// Precomputed CoverageMath.MakesRedundant over the run's role catalog.
        public bool Redundant(int coveringRoleId, int coveredRoleId)
            => redundantBy.TryGetValue(coveredRoleId, out var covering)
               && covering.Contains(coveringRoleId);

        /// The unique path containing the role, or null (none, or ambiguous).
        public PathView SoloPathOf(int roleId)
            => soloPathByRole.TryGetValue(roleId, out var path) ? path : null;

        /// The pawn-independent ordering skeleton, computed once per run.
        /// Callers must not mutate the returned dictionary.
        public Dictionary<int, long> BasePositions()
        {
            if (basePositions == null)
                basePositions = Ordering.BasePositions(Colony.Roles, Colony.OrderTemplate);
            return basePositions;
        }

        /// A partial match is enough for eligibility: a pawn can still use a
        /// mixed role for the work types they can perform.
        public bool Capable(int pawnIndex, RoleView role)
        {
            var capable = Colony.Pawns[pawnIndex].CapableWorkTypes;
            foreach (var workType in role.WorkTypes)
                if (capable.Contains(workType)) return true;
            return false;
        }

        /// Coverage is stricter than eligibility: the pawn must be able to
        /// perform every work type supplied by the requested role.
        public bool FullyCapable(int pawnIndex, RoleView role)
        {
            var capable = Colony.Pawns[pawnIndex].CapableWorkTypes;
            foreach (var workType in role.WorkTypes)
                if (!capable.Contains(workType)) return false;
            return true;
        }

        public int SkillLevel(int pawnIndex, string skill)
            => skill != null && Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(skill, out int level)
                ? level : 0;

        public IReadOnlyList<RoleSkillView> RequiredSkills(RoleView role)
        {
            if (requiredSkillsByRole.TryGetValue(role.Id, out var cached)) return cached;
            var skills = role.Skills.Where(s => s.Required)
                .OrderByDescending(s => s.Primary)
                .ThenByDescending(s => s.Importance)
                .ThenBy(s => s.SkillDefName, System.StringComparer.Ordinal)
                .ToList();
            if (skills.Count == 0 && role.PrimarySkill != null)
                skills.Add(new RoleSkillView
                {
                    SkillDefName = role.PrimarySkill,
                    Primary = true,
                });
            requiredSkillsByRole[role.Id] = skills;
            return skills;
        }

        /// Skills by preference (primary, importance, name) — sorted once per
        /// role per run so BestSignal can forward-scan.
        private List<RoleSkillView> OrderedSkills(RoleView role)
        {
            if (orderedSkillsByRole.TryGetValue(role.Id, out var cached)) return cached;
            var ordered = role.Skills
                .OrderByDescending(s => s.Primary)
                .ThenByDescending(s => s.Importance)
                .ThenBy(s => s.SkillDefName, System.StringComparer.Ordinal)
                .ToList();
            orderedSkillsByRole[role.Id] = ordered;
            return ordered;
        }

        public bool InsideBand(int pawnIndex, RoleView role, PathView path, int entry)
        {
            var skills = RequiredSkills(role);
            if (skills.Count == 0) return true;
            foreach (var roleSkill in skills)
            {
                if (!Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(
                        roleSkill.SkillDefName, out int level)
                    || !PathMath.InsideBand(path, entry, level))
                    return false;
            }
            return true;
        }

        /// The pawn's strongest signal over the role's work-type skills.
        /// Roles with no mapped skills read Neutral (content roles).
        public SignalBucket BestSignal(int pawnIndex, RoleView role, out string skill, out SignalSource source)
        {
            var pawn = Colony.Pawns[pawnIndex];
            foreach (var required in RequiredSkills(role))
            {
                if (pawn.SkillLevels.TryGetValue(required.SkillDefName, out _)
                    && (!pawn.SignalBuckets.TryGetValue(required.SkillDefName, out var requiredBucket)
                        || requiredBucket != SignalBucket.Awful))
                    continue;
                skill = required.SkillDefName;
                source = SignalSource.Aggregated;
                return SignalBucket.Awful;
            }

            if (role.Skills.Count > 0)
            {
                var ordered = OrderedSkills(role);
                foreach (var primary in ordered)
                {
                    if (!pawn.SkillLevels.ContainsKey(primary.SkillDefName)) continue;
                    skill = primary.SkillDefName;
                    source = SignalSource.Aggregated;
                    return pawn.SignalBuckets.TryGetValue(skill, out var classified)
                        ? classified : SignalBucket.Neutral;
                }
            }
            skill = null;
            source = SignalSource.None;
            bool any = false;
            var best = SignalBucket.Awful;
            foreach (var workType in role.WorkTypes)
            {
                if (!Colony.WorkTypeSkills.TryGetValue(workType, out var skills)) continue;
                foreach (var s in skills)
                {
                    if (!pawn.SkillLevels.ContainsKey(s)) continue;
                    SignalBucket bucket = pawn.SignalBuckets.TryGetValue(s, out var classified)
                        ? classified
                        : SignalBucket.Neutral;
                    if (!any || bucket > best
                        || bucket == best
                        && System.StringComparer.Ordinal.Compare(s, skill) < 0)
                    {
                        best = bucket;
                        skill = s;
                        source = SignalSource.Aggregated;
                    }
                    any = true;
                }
            }
            return any ? best : SignalBucket.Neutral;
        }

        public void AddCandidate(int pawnIndex, int roleId, Reason reason, SignalBucket strength,
            bool force = false)
        {
            if (RoleOf(roleId)?.HolderMode == RoleHolderMode.Never) return;
            if (!force && Vetoed.Contains(roleId)) return;
            var byRole = Candidates[pawnIndex];
            if (!byRole.TryGetValue(roleId, out var existing) || strength > existing.Strength)
                byRole[roleId] = new Candidate { RoleId = roleId, Reason = reason, Strength = strength };
        }

        public void RemoveCandidate(int pawnIndex, int roleId) => Candidates[pawnIndex].Remove(roleId);

        /// The pawn's candidates contain the role or a non-blocker covering it.
        public bool CoversRole(int pawnIndex, RoleView role)
        {
            if (!FullyCapable(pawnIndex, role)) return false;
            var byRole = Candidates[pawnIndex];
            if (byRole.ContainsKey(role.Id)) return true;
            foreach (var id in byRole.Keys)
                if (RoleOf(id) is RoleView other && !other.Blocker && Redundant(other.Id, role.Id))
                    return true;
            foreach (var assignment in Colony.Pawns[pawnIndex].Existing)
            {
                var assignedRole = RoleOf(assignment.RoleId);
                if (assignedRole == null || assignedRole.HolderMode == RoleHolderMode.Never
                    || (!assignment.Pinned && !assignedRole.HasRules && !assignedRole.Blocker))
                    continue;
                if (assignedRole.Id == role.Id
                    || !assignedRole.Blocker && Redundant(assignedRole.Id, role.Id))
                    return true;
            }
            return false;
        }

        public int ProtectedDirectHoldersOf(int roleId)
        {
            int count = 0;
            for (int pawn = 0; pawn < Colony.Pawns.Count; pawn++)
                if (Colony.Pawns[pawn].Existing.Any(assignment =>
                    assignment.RoleId == roleId && assignment.Pinned))
                    count++;
            return count;
        }

        public bool HasProtectedDirectAssignment(int pawnIndex, int roleId)
            => Colony.Pawns[pawnIndex].Existing.Any(assignment =>
            {
                if (assignment.RoleId != roleId) return false;
                var role = RoleOf(roleId);
                return assignment.Pinned || role != null && (role.HasRules || role.Blocker);
            });

        /// Pawns whose candidates provide the role (directly or by coverage).
        public int HoldersOf(int roleId)
        {
            var role = RoleOf(roleId);
            if (role == null) return 0;
            int count = 0;
            for (int i = 0; i < Candidates.Count; i++)
                if (CoversRole(i, role)) count++;
            return count;
        }

        public int AllocatedHoldersOf(int roleId)
        {
            var role = RoleOf(roleId);
            if (role == null) return 0;
            int count = 0;
            for (int i = 0; i < Candidates.Count; i++)
                if (CoversRole(i, role) || TrainingToward[i].Contains(roleId))
                    count++;
            return count;
        }

        /// Band gating for INTEREST candidates: every required role skill must
        /// sit inside some containing path's strict band. Roles in no path and
        /// unskilled entries never gate.
        /// Overlap coexists, disjoint supersedes. The need-driven floor
        /// draft ignores this gate: minHolders is absolute.
        public bool PassesBands(int pawnIndex, RoleView role)
        {
            bool member = false;
            IReadOnlyList<RoleSkillView> skills = null;
            foreach (var path in Colony.Paths)
            {
                int entry = path.RoleIds.IndexOf(role.Id);
                if (entry < 0) continue;
                member = true;
                if (skills == null) skills = RequiredSkills(role);
                if (skills.Count == 0) return true;
                if (InsideBand(pawnIndex, role, path, entry)) return true;
            }
            return !member;
        }

    }
}
