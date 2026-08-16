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
        /// False when the game reports that biological age does not disable
        /// work for this pawn. Defaults to the ordinary age-gated behavior.
        public bool AgeLimitsApply = true;
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

    /// One catalog role as the rules see it. Work and skill facts live on the
    /// role's WorkSpec; the members here are policy, demand, and identity.
    /// Ordinary recommendation roles carry colonyMin/coverage demand; an
    /// unskilled role without demand is never assigned.
    public class RoleView
    {
        public int Id;
        /// Template def the role was seeded from; null for player-made roles.
        public string TemplateDefName;
        /// Live non-composite members of a composite role; null for ordinary
        /// roles. Band gates read these: a bundle cannot downgrade to a lower
        /// path rung the way a path target can.
        public List<int> MemberRoleIds;
        public HashSet<string> Coverage = new HashSet<string>();
        /// Coverage in the role's own job order; null = no order data
        /// (redundancy folding stays permissive).
        public List<string> OrderedCoverage;
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
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
        /// Maximum biological age (years, inclusive) for holding the role;
        /// 0 = no gate. The exclusive tick bound is one year past the cap.
        public int MaxAge;
        public long MaxAgeTicks => (MaxAge + 1L) * BiologicalAge.TicksPerYear;
        /// Authored demand: minimum assignment count and ideal colonist
        /// percentage. EngineContext precomputes the resulting requirement
        /// per plan build (RoleDemand.RequirementFor).
        public int ColonyMin;
        public int CoveragePercent;
        public bool Available = true;
        public bool Enabled = true;

        /// The complete immutable work-facts projection; every work, skill,
        /// and gate read below derives from it.
        public RoleWorkSpec WorkSpec = RoleWorkSpec.Empty;

        public IReadOnlyList<string> WorkTypes => WorkSpec.CapabilityWorkTypes;
        public bool Hunting => WorkSpec.HasHuntingCapability;
        public float NaturalPriority => WorkSpec.MaxNaturalPriority;
        /// User-authored hard skill gates. A pawn must have every listed skill
        /// enabled to be eligible; an empty list adds no gate.
        public IReadOnlyList<string> DeclaredRequiredSkills =>
            WorkSpec.AssignmentSkillGates;
        public IReadOnlyList<RoleSkillFact> Skills => WorkSpec.Skills;
        /// True when the derived facts say at least one skill is used by the
        /// role's jobs. Skilled roles require full work-type capability;
        /// unskilled roles require partial capability.
        public bool UsesSkills => WorkSpec.IsSkilled;
        /// Decisive skill for band gating and signals; null = unskilled entry,
        /// never gates.
        public string PrimarySkill => WorkSpec.PrimarySkillDefName;
        /// The work fact alone: no used-skill evidence. Channel-independent;
        /// an automatic or rule-carrying chore is still unskilled.
        public bool Unskilled => !WorkSpec.IsSkilled;
        /// Demand-planning rules for plain unskilled chores. Automatic and
        /// rule-carrying roles are granted through their own channels, so the
        /// never/fill placement rules do not apply to them.
        public bool UseUnskilledPlacementRules =>
            Unskilled && !HasRules && !AutoAssign;

        public bool HasDemand => ColonyMin > 0 || CoveragePercent > 0;
        /// Unskilled role the player opted into demand: assigns every capable pawn.
        public bool UnskilledFill => UseUnskilledPlacementRules && HasDemand;
        /// Unskilled role without demand: the planner never assigns it.
        public bool IsNever => UseUnskilledPlacementRules && !HasDemand;
        public bool PlannedByDemand => !AutoAssign && !HasRules && !Blocker
            && !Hunting;
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
        public Dictionary<string, int> SkillMaxLevels = new Dictionary<string, int>();
        public int HunterRoleId = -1;
        public int FireBlockerRoleId = -1;
    }
}
