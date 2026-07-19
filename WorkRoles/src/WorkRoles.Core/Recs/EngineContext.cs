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

        public EngineContext(ColonyView colony)
        {
            Colony = colony;
            RolesById = colony.Roles.ToDictionary(r => r.Id);
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
        }

        public RoleView RoleOf(int id) => RolesById.TryGetValue(id, out var role) ? role : null;

        public bool Capable(int pawnIndex, RoleView role)
            => role.WorkTypes.Any(Colony.Pawns[pawnIndex].CapableWorkTypes.Contains);

        public int SkillLevel(int pawnIndex, string skill)
            => skill != null && Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(skill, out int level)
                ? level : 0;

        public IReadOnlyList<RoleSkillView> RequiredSkills(RoleView role)
        {
            var skills = role.Skills.Where(s => s.Required).ToList();
            if (skills.Count == 0 && role.PrimarySkill != null)
                skills.Add(new RoleSkillView
                {
                    SkillDefName = role.PrimarySkill,
                    Primary = true,
                });
            return skills;
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
            if (role.Skills.Count > 0)
            {
                foreach (var required in role.Skills
                             .Where(s => s.Required)
                             .OrderByDescending(s => s.Primary)
                             .ThenByDescending(s => s.Importance)
                             .ThenBy(s => s.SkillDefName))
                {
                    if (!pawn.SkillLevels.ContainsKey(required.SkillDefName)) continue;
                    SignalBucket requiredBucket = pawn.SignalBuckets.TryGetValue(
                        required.SkillDefName, out var classified)
                        ? classified : SignalBucket.Neutral;
                    if (requiredBucket != SignalBucket.Awful) continue;
                    skill = required.SkillDefName;
                    source = SignalSource.Aggregated;
                    return SignalBucket.Awful;
                }

                var primary = role.Skills
                    .Where(s => pawn.SkillLevels.ContainsKey(s.SkillDefName))
                    .OrderByDescending(s => s.Primary)
                    .ThenByDescending(s => s.Importance)
                    .ThenBy(s => s.SkillDefName)
                    .FirstOrDefault();
                if (primary != null)
                {
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
                    if (!any || bucket > best)
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
            var byRole = Candidates[pawnIndex];
            if (byRole.ContainsKey(role.Id)) return true;
            foreach (var id in byRole.Keys)
                if (RoleOf(id) is RoleView other && !other.Blocker
                    && CoverageMath.MakesRedundant(other.Coverage, other.Id, role.Coverage, role.Id))
                    return true;
            foreach (var assignment in Colony.Pawns[pawnIndex].Existing)
            {
                var assignedRole = RoleOf(assignment.RoleId);
                if (assignedRole == null || assignedRole.HolderMode == RoleHolderMode.Never
                    || (!assignment.Pinned && !assignedRole.HasRules && !assignedRole.Blocker))
                    continue;
                if (assignedRole.Id == role.Id
                    || !assignedRole.Blocker && CoverageMath.MakesRedundant(
                        assignedRole.Coverage, assignedRole.Id, role.Coverage, role.Id))
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
            foreach (var path in Colony.Paths)
            {
                int entry = path.RoleIds.IndexOf(role.Id);
                if (entry < 0) continue;
                member = true;
                if (RequiredSkills(role).Count == 0) return true;
                if (InsideBand(pawnIndex, role, path, entry)) return true;
            }
            return !member;
        }

    }
}
