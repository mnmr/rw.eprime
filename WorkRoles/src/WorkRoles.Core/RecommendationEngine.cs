using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One pawn as recommendation scoring sees it (game-independent projection).
    /// Skill dictionaries are keyed by skill defName; absent = totally disabled.
    public class RecPawn
    {
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();
        public Dictionary<string, int> PassionScores = new Dictionary<string, int>(); // 0/1/2
        public Dictionary<string, int> Aptitudes = new Dictionary<string, int>();     // negative = apathy (mutes signals)
        public HashSet<string> ExpertiseSkills = new HashSet<string>();
        public HashSet<string> CapableWorkTypes = new HashSet<string>();
        public bool HasRangedWeapon;
        public int ShootingLevel;
    }

    /// One catalog role as recommendation scoring sees it.
    public class RecRole
    {
        public int Id;
        /// Expanded job coverage (CoverageMath.CoverageOf) — the nesting/redundancy identity.
        public HashSet<string> Coverage = new HashSet<string>();
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool Unskilled;
        public bool Hunting;                 // touches the Hunting work type
        public float NaturalPriority;
        // Colony-plan facts (ignored by per-pawn scoring):
        public bool Enabled = true;
        public bool Managed;
        public bool Gated;                   // hunting or band-gated (training paths)
        public List<string> WorkTypes = new List<string>(); // member work type defNames
        // Training band: a pawn inside [TrainMin, TrainMax) fits the role; at
        // TrainMax it has outgrown it and its targets apply instead.
        public string TrainSkill;            // null = no band
        public int TrainMin;
        public int TrainMax;                 // 0 = open-ended
        /// Roles this one trains toward (resolved ids).
        public List<int> TrainTargets = new List<int>();
        /// Colonist count: -1 = auto (dealt at colony scale, no draft), 0 =
        /// never dealt by the planner (passion/trait recommendations still
        /// apply), N = N per colony-scale unit AND the "needed work" marker
        /// (best-in-colony only fires when > 0).
        public int MinHolders = -1;
        /// False while none of the role's bench work can exist yet (nothing
        /// built, nothing researched): not recommendable.
        public bool Available = true;
    }

    public enum RecReason
    {
        Everyone, Duty, Hunter, Unskilled, Expertise,
        MajorPassion, MinorPassion, Best, Aptitude,
    }

    public struct Recommendation
    {
        public int RoleId;
        public RecReason Reason;
        public string SkillDefName; // matched skill; null for content-keyed reasons
    }

    /// Per-pawn recommendations: SELECTION is signal-driven (auto/grunt/hunter
    /// membership, or a skill signal — expertise, passion, colony-best on
    /// needed work, aptitude — for skilled roles, all gated by the training
    /// band); ORDER is the recommendation template (vanilla-grid-derived,
    /// user-configurable), with every role — autos included — slotting in via
    /// RecommendationOrder.PositionOf. A role covered by
    /// another recommended role is dropped (the combo wins). Rule-carrying
    /// roles and blockers are never recommended.
    public static class RecommendationEngine
    {
        public static List<Recommendation> Compute(
            IReadOnlyList<RecRole> catalog,
            RecPawn pawn,
            IReadOnlyDictionary<string, int> skillMaxLevels,
            IReadOnlyDictionary<string, IReadOnlyList<string>> workTypeSkills,
            IReadOnlyList<int> orderTemplate)
        {
            const int GroupBasics = 0;
            const int GroupWardenCarer = 1; // duty roles sit above the vocations, as in vanilla
            const int GroupHunter = 2;      // training activity: must outrank the skilled work
            const int GroupExpertise = 3;   // VSE skill specialization: rarer and stronger than passion
            const int GroupMajorPassion = 4;
            const int GroupMinorPassion = 5;
            const int GroupBestInColony = 6;
            const int GroupAptitude = 7;
            const int GroupGrunt = 8;

            var scored = new List<(RecRole role, int group, float sortKey, string skill)>();

            foreach (var role in catalog)
            {
                if (role.HasRules || role.Blocker) continue;
                if (!role.WorkTypes.Any(pawn.CapableWorkTypes.Contains)) continue;
                if (!PassesGates(role, pawn, skillMaxLevels)) continue;

                int group = int.MaxValue;
                float sortKey = 0f;
                string matchedSkill = null;

                void Candidate(int g, float key, string skill = null)
                {
                    if (g < group || (g == group && key > sortKey))
                    {
                        group = g;
                        sortKey = key;
                        matchedSkill = skill;
                    }
                }

                if (role.AutoAssign)
                    Candidate(GroupBasics, role.NaturalPriority);
                else if (role.Unskilled)
                    Candidate(GroupGrunt, 0f);
                else if (role.Hunting)
                    Candidate(GroupHunter, pawn.ShootingLevel);

                foreach (var workType in role.WorkTypes)
                {
                    if (!workTypeSkills.TryGetValue(workType, out var skills)) continue;
                    foreach (var skill in skills)
                    {
                        if (!pawn.SkillLevels.TryGetValue(skill, out int level)) continue;
                        // Apathy (negative aptitude) mutes every signal for the skill.
                        pawn.Aptitudes.TryGetValue(skill, out int aptitude);
                        if (aptitude < 0) continue;

                        if (pawn.ExpertiseSkills.Contains(skill)) Candidate(GroupExpertise, level, skill);
                        int passionScore = pawn.PassionScores.TryGetValue(skill, out var p) ? p : 0;
                        if (passionScore == 2) Candidate(GroupMajorPassion, level, skill);
                        else if (passionScore == 1) Candidate(GroupMinorPassion, level, skill);
                        // Best-in-colony only drafts pawns into NEEDED work
                        // (MinHolders > 0); optional roles ride on interest alone.
                        if (role.MinHolders > 0
                            && skillMaxLevels.TryGetValue(skill, out int maxLevel)
                            && level >= maxLevel && level > 0)
                            Candidate(GroupBestInColony, level, skill);
                        if (aptitude > 0)
                            Candidate(GroupAptitude, aptitude * 1000f + level, skill);
                    }
                }

                if (group != int.MaxValue
                    && role.WorkTypes.Any(wt => wt == "Warden" || wt == "Childcare"))
                    group = GroupWardenCarer;

                if (group != int.MaxValue)
                    scored.Add((role, group, sortKey, matchedSkill));
            }

            // Order: every role — autos included — takes its template position;
            // autos interleave by their work-type priority (Core above Doctor,
            // Basics below). Work priority breaks position ties.
            var templateIndex = new Dictionary<int, int>();
            for (int i = 0; i < orderTemplate.Count; i++)
                templateIndex[orderTemplate[i]] = i;
            var byId = catalog.ToDictionary(r => r.Id);
            var ordered = scored
                .OrderBy(t => RecommendationOrder.PositionOf(t.role, templateIndex, byId))
                .ThenByDescending(t => t.role.NaturalPriority)
                .ToList();

            // A combo beats its parts: never recommend a role another recommended
            // role covers (no Grower next to Farmer, no Firefighter next to Basics)
            // — unless the covered role is a train TARGET of the coverer
            // (Fabricator under Smith): the subset specialization survives so the
            // plan can slot it above its trainer.
            var result = new List<Recommendation>();
            foreach (var (role, group, _, skill) in ordered)
            {
                if (ordered.Any(other =>
                        CoverageMath.MakesRedundant(other.role.Coverage, other.role.Id, role.Coverage, role.Id)
                        && !other.role.TrainTargets.Contains(role.Id)))
                    continue;
                result.Add(new Recommendation
                {
                    RoleId = role.Id,
                    Reason = group == 0 ? RecReason.Everyone
                        : group == 1 ? RecReason.Duty
                        : group == 2 ? RecReason.Hunter
                        : group == 3 ? RecReason.Expertise
                        : group == 4 ? RecReason.MajorPassion
                        : group == 5 ? RecReason.MinorPassion
                        : group == 6 ? RecReason.Best
                        : group == 7 ? RecReason.Aptitude
                        : RecReason.Unskilled,
                    SkillDefName = skill,
                });
            }
            return result;
        }

        /// Hard recommendation gates. Hunter's gate is a ranged weapon — every gun
        /// carrier hunts (placement is skill-tiered in the colony plan). Skilled
        /// roles gate on their training band: below TrainMin fails (unless best in
        /// colony), at or past TrainMax the pawn has outgrown the role — its train
        /// targets qualify through their own bands instead.
        public static bool PassesGates(RecRole role, RecPawn pawn,
            IReadOnlyDictionary<string, int> skillMaxLevels)
        {
            if (!role.Available) return false;
            if (role.Hunting)
                return pawn.HasRangedWeapon;
            if (role.TrainSkill == null) return true;

            int level = pawn.SkillLevels.TryGetValue(role.TrainSkill, out var l) ? l : 0;
            if (role.TrainMin > 0)
            {
                bool best = level > 0 && skillMaxLevels.TryGetValue(role.TrainSkill, out int max) && level >= max;
                if (level < role.TrainMin && !best) return false;
            }
            if (role.TrainMax > 0 && level >= role.TrainMax) return false;
            return true;
        }
    }

}
