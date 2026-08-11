using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public static class BiologicalAge
    {
        public const long TicksPerYear = 3_600_000L;
    }

    /// One pawn as the rules see it. Skill dictionaries key by skill defName;
    /// an absent skill is totally disabled. Signal buckets are precomputed
    /// from the pawn signal snapshot before the recommendation engine runs.
    public class PawnView
    {
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();
        public Dictionary<string, SignalBucket> SignalBuckets =
            new Dictionary<string, SignalBucket>();
        public Dictionary<string, SignalBucket> WorkTypeSignalBuckets;
        public HashSet<string> CapableWorkTypes = new HashSet<string>();
        public long BiologicalAgeTicks = long.MaxValue;
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

    /// One catalog role as the rules see it. Ordinary recommendation roles use
    /// a holder scale; a missing or Never scale excludes the role.
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
        /// Ignores training-path placement and remains at its recommendation
        /// order slot. When unlisted, Ordering provides a conservative tail
        /// fallback ahead of trailing unskilled work.
        public bool PreserveRecommendationOrder;
        /// False routes repeat championships after this role through the
        /// occasional-work penalty instead of the full overlap/distinct tiers.
        public bool ChampionPenalty = true;
        /// How important holding the role is for a colony; None = unclassified.
        public RoleCategory Category;
        /// How time-consuming the role's work is; None = unclassified.
        public RoleTime Time;
        /// Minimum biological age (years) for holding the role; 0 = no gate.
        public int MinAge;
        public long MinAgeTicks => MinAge * BiologicalAge.TicksPerYear;
        /// Authored skill classification (def tuning or per-save role data).
        /// Carried for future gating and validation; rules still consume the
        /// derived Skills profile today. Null = no authored data.
        public List<string> DeclaredRequiredSkills;
        public List<string> DeclaredOptionalSkills;
        public float NaturalPriority;
        public List<string> WorkTypes = new List<string>();
        /// Banded holder demand; null for Never.
        public HolderScale Scale;
        /// Fill mode from the role's assignment strategy. Unskilled assigns
        /// every capable pawn; Never assigns none.
        public ScaleMode Mode = ScaleMode.Skilled;

        public bool IsNever => Mode == ScaleMode.Never || Scale == null;
        public bool UsesHolderScale => !AutoAssign && !HasRules && !Blocker
            && !Hunting;

        public int RequiredTotalAt(int colonySize) =>
            Scale?.RequiredTotalAt(colonySize) ?? 0;

        public int MaxHoldersAt(int colonySize) =>
            Scale?.MaxAt(colonySize) ?? 0;

        public int TrainingWaiversAt(int colonySize) =>
            Scale?.TrainingWaiversAt(colonySize) ?? 0;
        public HolderRequirement RequirementAt(int colonySize)
        {
            int capacity = System.Math.Max(0, colonySize);
            int maximum = MaxHoldersAt(colonySize);
            if (maximum < RoleHolderRange.Uncapped)
                capacity = System.Math.Min(capacity, System.Math.Max(0, maximum));
            return new HolderRequirement(
                System.Math.Min(capacity, RequiredTotalAt(colonySize)),
                TrainingWaiversAt(colonySize));
        }
        public List<RoleSkillView> Skills = new List<RoleSkillView>();
        /// Measured skill for band gating (most XP-frequent across the role's
        /// jobs); null = unskilled entry, never gates.
        public string PrimarySkill;
        public bool Unskilled;
        public bool Available = true;
        public bool Enabled = true;
    }

    /// One training path: bands are [min, max) with 21 = open top. Paths are
    /// role-owned (Id = the owning target role); the recommendation order
    /// places the block.
    public class PathView
    {
        public int Id;
        public List<int> RoleIds = new List<int>();
        public List<int> BandMins = new List<int>();
        public List<int> BandMaxes = new List<int>();
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
