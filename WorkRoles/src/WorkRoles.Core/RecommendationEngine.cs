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
        public Dictionary<string, int> Aptitudes = new Dictionary<string, int>();     // positive only
        public HashSet<string> ExpertiseSkills = new HashSet<string>();
        public HashSet<string> CapableWorkTypes = new HashSet<string>();
        public bool HasRangedWeapon;
        public int ShootingLevel;
    }

    /// One catalog role as recommendation scoring sees it.
    public class RecRole
    {
        public int Id;
        public IReadOnlyList<JobEntry> Entries = new List<JobEntry>();
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool Unskilled;
        public bool Hunting;                 // touches the Hunting work type
        public float NaturalPriority;
        // Colony-plan facts (ignored by per-pawn scoring):
        public bool Enabled = true;
        public bool Managed;
        public bool Gated;                   // hunting or def-gated (training paths)
        public bool SkipCoverage;            // Artist: no colony minimum
        public int WantOverride;             // >0 replaces the default holder count (Researcher)
        public List<string> WorkTypes = new List<string>(); // member work type defNames
        // Template gates (resolved from the role's def game-side; null = ungated).
        public string GateSkill;
        public int GateMinLevel;
        public int GateMaxLevel;
        public bool GateNeedsPassion;
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

    /// Per-pawn recommendation scoring: content-keyed groups (everyone / duty /
    /// hunter / grunt) plus skill signals (expertise > burning passion > passion >
    /// colony-best > aptitude), ordered by group then the pawn's ability at the
    /// matched skill; a role covered by another recommended role is dropped (the
    /// combo wins). Auto (rule-carrying) roles and blockers are never recommended.
    public static class RecommendationEngine
    {
        public static List<Recommendation> Compute(
            IReadOnlyList<RecRole> catalog,
            RecPawn pawn,
            IReadOnlyDictionary<string, int> skillMaxLevels,
            IReadOnlyDictionary<string, IReadOnlyList<string>> workTypeSkills)
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

                        if (pawn.ExpertiseSkills.Contains(skill)) Candidate(GroupExpertise, level, skill);
                        int passionScore = pawn.PassionScores.TryGetValue(skill, out var p) ? p : 0;
                        if (passionScore == 2) Candidate(GroupMajorPassion, level, skill);
                        else if (passionScore == 1) Candidate(GroupMinorPassion, level, skill);
                        if (skillMaxLevels.TryGetValue(skill, out int maxLevel)
                            && level >= maxLevel && level > 0)
                            Candidate(GroupBestInColony, level, skill);
                        if (pawn.Aptitudes.TryGetValue(skill, out int aptitude) && aptitude > 0)
                            Candidate(GroupAptitude, aptitude * 1000f + level, skill);
                    }
                }

                if (group != int.MaxValue
                    && role.WorkTypes.Any(wt => wt == "Warden" || wt == "Childcare"))
                    group = GroupWardenCarer;

                if (group != int.MaxValue)
                    scored.Add((role, group, sortKey, matchedSkill));
            }

            var ordered = scored
                .OrderBy(t => t.group)
                .ThenByDescending(t => t.sortKey)
                .ToList();

            // A combo beats its parts: never recommend a role another recommended
            // role covers (no Grower next to Farmer, no Firefighter next to Basics).
            var result = new List<Recommendation>();
            foreach (var (role, group, _, skill) in ordered)
            {
                if (ordered.Any(other => EntryMath.Covers(other.role.Entries, role.Entries))) continue;
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
        /// carrier hunts (placement is skill-tiered in the colony plan). Every
        /// other gate comes from the role's template def: a minimum gate passes at
        /// the level or when best in colony; a maximum gate passes below the level,
        /// with a passion when the def demands one.
        public static bool PassesGates(RecRole role, RecPawn pawn,
            IReadOnlyDictionary<string, int> skillMaxLevels)
        {
            if (role.Hunting)
                return pawn.HasRangedWeapon;
            if (role.GateSkill == null) return true;

            int level = pawn.SkillLevels.TryGetValue(role.GateSkill, out var l) ? l : 0;
            if (role.GateMinLevel > 0)
            {
                bool best = level > 0 && skillMaxLevels.TryGetValue(role.GateSkill, out int max) && level >= max;
                if (level < role.GateMinLevel && !best) return false;
            }
            if (role.GateMaxLevel > 0 && level >= role.GateMaxLevel) return false;
            int passion = pawn.PassionScores.TryGetValue(role.GateSkill, out var p) ? p : 0;
            if (role.GateNeedsPassion && passion == 0) return false;
            return true;
        }
    }

    /// Entry-list relations shared by the planners.
    public static class EntryMath
    {
        /// True when a's entries strictly include every entry of b.
        public static bool Covers(IReadOnlyList<JobEntry> a, IReadOnlyList<JobEntry> b)
        {
            if (ReferenceEquals(a, b)) return false;
            if (b.Count == 0 || b.Count >= a.Count) return false;
            foreach (var entry in b)
                if (!a.Contains(entry)) return false;
            return true;
        }
    }
}
