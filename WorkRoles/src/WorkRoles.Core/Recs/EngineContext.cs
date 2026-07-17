using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public enum SignalSource { None, Expertise, MajorPassion, MinorPassion, Aptitude }

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
                    if (!pawn.SkillLevels.TryGetValue(s, out int level)) continue;
                    pawn.PassionScores.TryGetValue(s, out int passion);
                    pawn.Aptitudes.TryGetValue(s, out int aptitude);
                    var bucket = SignalBuckets.Classify(level, passion, aptitude,
                        pawn.ExpertiseSkills.Contains(s));
                    if (!any || bucket > best)
                    {
                        best = bucket;
                        skill = s;
                        source = aptitude < 0 ? SignalSource.None
                            : pawn.ExpertiseSkills.Contains(s) ? SignalSource.Expertise
                            : passion >= 2 ? SignalSource.MajorPassion
                            : passion == 1 ? SignalSource.MinorPassion
                            : aptitude > 0 ? SignalSource.Aptitude
                            : SignalSource.None;
                    }
                    any = true;
                }
            }
            return any ? best : SignalBucket.Neutral;
        }

        public void AddCandidate(int pawnIndex, int roleId, Reason reason, SignalBucket strength,
            bool force = false)
        {
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
        /// (draft/allowance) ignores this gate: minHolders is absolute.
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

        /// The pawn holds a lower-band partner of the role (a trainee toward it).
        public bool HoldsPartner(int pawnIndex, RoleView role)
        {
            var byRole = Candidates[pawnIndex];
            foreach (var path in Colony.Paths)
            {
                int entry = path.RoleIds.IndexOf(role.Id);
                if (entry < 0) continue;
                foreach (int i in PathMath.LowerBandEntries(path, entry))
                    if (byRole.ContainsKey(path.RoleIds[i])) return true;
            }
            return false;
        }

        /// Floor slots that trainees satisfy: partner-holders capped by the
        /// role's allowance (draft credits these before dealing directly).
        public int TraineeCredit(RoleView role)
        {
            if (role.InTrainingAllowance <= 0) return 0;
            int count = 0;
            for (int i = 0; i < Colony.Pawns.Count; i++)
                if (HoldsPartner(i, role)) count++;
            return System.Math.Min(role.InTrainingAllowance, count);
        }
    }
}
