using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// One pawn as the rules see it. Skill dictionaries key by skill defName;
    /// an absent skill is totally disabled. Aptitudes carry sign only
    /// (negative = apathy).
    public class PawnView
    {
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();
        public Dictionary<string, int> PassionScores = new Dictionary<string, int>(); // 0/1/2
        public Dictionary<string, int> Aptitudes = new Dictionary<string, int>();
        public HashSet<string> ExpertiseSkills = new HashSet<string>();
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

    /// One catalog role as the rules see it. MinHolders arrives RESOLVED
    /// (never Auto-with-a-def-default): the builder already mapped Auto to
    /// the RoleDef default (player roles 0). Essential iff MinHolders >= 1.
    public class RoleView
    {
        /// The role is never recommended and never drafted.
        public const int NeverHolders = -2;

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
        /// -2 never, -1 auto-coverage (one scaled unit), 0 interest-only,
        /// N needed slots.
        public int MinHolders;
        /// Of MinHolders, how many slots a lower-band path partner may fill.
        public int InTrainingAllowance;
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
