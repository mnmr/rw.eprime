using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// One pawn as the rules see it. Skill dictionaries key by skill defName;
    /// an absent skill is totally disabled. Signal buckets are precomputed
    /// from the pawn signal snapshot before the recommendation engine runs.
    public class PawnView
    {
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();
        public Dictionary<string, SignalBucket> SignalBuckets =
            new Dictionary<string, SignalBucket>();
        public HashSet<string> CapableWorkTypes = new HashSet<string>();
        public bool HasRangedWeapon;
        public int ShootingLevel;
        public bool FireFear;
        public List<AssignmentView> Existing = new List<AssignmentView>();
    }

    public struct AssignmentView
    {
        public int RoleId;
        public bool Enabled;
        public bool Pinned;
    }

    /// One skill's role-level importance, derived from the role's actual jobs.
    /// Every required skill participates in eligibility; Primary drives the
    /// signal bucket used for qualification and ranking.
    public sealed class RoleSkillView
    {
        public string SkillDefName;
        public bool Primary;
        public bool Required = true;
        public int Importance = 1;
        public int UsedJobs;
        public int TrainedJobs;
        public int RequiredContent;
    }

    /// One catalog role as the rules see it. Auto MinHolders arrives resolved
    /// through the RoleDef; Custom carries the stored inclusive range.
    public class RoleView
    {
        public int Id;
        public HashSet<string> Coverage = new HashSet<string>();
        /// Coverage in the role's own job order; null = no order data
        /// (redundancy folding stays permissive).
        public List<string> OrderedCoverage;
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool Hunting;
        public float NaturalPriority;
        public List<string> WorkTypes = new List<string>();
        public RoleHolderMode HolderMode;
        /// -2 never, -1 auto-coverage (one scaled unit), 0 interest-only,
        /// N needed slots.
        public int MinHolders;
        public int MaxHolders = RoleHolderRange.Uncapped;
        public int TrainingWaivers;
        public List<RoleSkillView> Skills = new List<RoleSkillView>();
        /// Measured skill for band gating (most XP-frequent across the role's
        /// jobs); null = unskilled entry, never gates.
        public string PrimarySkill;
        public bool Unskilled;
        public bool Available = true;
        public bool Enabled = true;
    }

    /// One training path: bands are [min, max) with 21 = open top; the anchor
    /// places the whole block into the recommendation order.
    public class PathView
    {
        public int Id;
        public List<int> RoleIds = new List<int>();
        public List<int> BandMins = new List<int>();
        public List<int> BandMaxes = new List<int>();
        public int AnchorRoleId = -1;
        public bool AnchorBefore = true;
    }

    public class ColonyView
    {
        public List<PawnView> Pawns = new List<PawnView>();
        public List<RoleView> Roles = new List<RoleView>();
        public List<PathView> Paths = new List<PathView>();
        /// Resolved recommendation-order template (role ids).
        public List<int> OrderTemplate = new List<int>();
        public IReadOnlyDictionary<string, IReadOnlyList<string>> WorkTypeSkills =
            new Dictionary<string, IReadOnlyList<string>>();
        public Dictionary<string, int> SkillMaxLevels = new Dictionary<string, int>();
        public int HunterRoleId = -1;
        public int FireBlockerRoleId = -1;
    }
}
