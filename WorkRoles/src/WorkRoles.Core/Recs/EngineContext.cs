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
                Results.Add(new PawnResult());
            }
        }

        public RoleView RoleOf(int id) => RolesById.TryGetValue(id, out var role) ? role : null;

        public bool Capable(int pawnIndex, RoleView role)
            => role.WorkTypes.Any(Colony.Pawns[pawnIndex].CapableWorkTypes.Contains);

        public int SkillLevel(int pawnIndex, string skill)
            => skill != null && Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(skill, out int level)
                ? level : 0;

        /// The pawn's strongest signal over the role's work-type skills.
        /// Roles with no mapped skills read Neutral (content roles).
        public SignalBucket BestSignal(int pawnIndex, RoleView role, out string skill, out SignalSource source)
        {
            var pawn = Colony.Pawns[pawnIndex];
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
            return false;
        }

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

        /// Band gating for INTEREST candidates: inside some containing path's
        /// band, or the colony-best escape (below a min nobody better can
        /// meet). Roles in no path — and unskilled entries — never gate.
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
                if (role.PrimarySkill == null) return true;
                int level = SkillLevel(pawnIndex, role.PrimarySkill);
                if (PathMath.InsideBand(path, entry, level)) return true;
                if (level > 0 && level < path.BandMins[entry]
                    && Colony.SkillMaxLevels.TryGetValue(role.PrimarySkill, out int max)
                    && level >= max)
                    return true;
            }
            return !member;
        }

    }
}
