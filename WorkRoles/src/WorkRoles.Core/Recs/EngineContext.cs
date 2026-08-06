using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public enum SignalSource { None, Aggregated }

    /// Immutable-run facts shared by the production recommendation planner.
    public sealed class EngineContext
    {
        public readonly ColonyView Colony;
        public readonly Dictionary<int, RoleView> RolesById;
        public readonly Dictionary<int, PathView> PathsById;

        private readonly Dictionary<int, HashSet<int>> redundantBy;
        private readonly Dictionary<int, IReadOnlyList<RoleSkillView>>
            requiredSkillsByRole =
                new Dictionary<int, IReadOnlyList<RoleSkillView>>();
        private readonly Dictionary<int, List<RoleSkillView>>
            orderedSkillsByRole =
                new Dictionary<int, List<RoleSkillView>>();
        private Dictionary<int, long> basePositions;
        private IReadOnlyDictionary<int, long> basePositionsView;

        public EngineContext(ColonyView colony)
        {
            Colony = colony;
            RolesById = colony.Roles.ToDictionary(role => role.Id);
            PathsById = colony.Paths.ToDictionary(path => path.Id);
            redundantBy = new Dictionary<int, HashSet<int>>(
                colony.Roles.Count);
            foreach (RoleView role in colony.Roles)
                redundantBy[role.Id] = new HashSet<int>();
            foreach (RoleView covering in colony.Roles)
                foreach (RoleView covered in colony.Roles)
                    if (covering.Id != covered.Id
                        && CoverageMath.MakesRedundant(
                            covering.Coverage,
                            covering.Id,
                            covered.Coverage,
                            covered.Id))
                        redundantBy[covered.Id].Add(covering.Id);
        }

        public RoleView RoleOf(int id) =>
            RolesById.TryGetValue(id, out RoleView role) ? role : null;

        public bool Redundant(int coveringRoleId, int coveredRoleId) =>
            redundantBy.TryGetValue(
                coveredRoleId, out HashSet<int> covering)
            && covering.Contains(coveringRoleId);

        public IReadOnlyDictionary<int, long> BasePositions()
        {
            if (basePositions != null) return basePositionsView;
            basePositions = Ordering.BasePositions(
                Colony.Roles, Colony.OrderTemplate);
            basePositionsView = new ReadOnlyDictionary<int, long>(basePositions);
            return basePositionsView;
        }

        public bool Capable(int pawnIndex, RoleView role)
        {
            HashSet<string> capable = Colony.Pawns[pawnIndex].CapableWorkTypes;
            foreach (string workType in role.WorkTypes)
                if (capable.Contains(workType)) return true;
            return false;
        }

        public bool FullyCapable(int pawnIndex, RoleView role)
        {
            HashSet<string> capable = Colony.Pawns[pawnIndex].CapableWorkTypes;
            foreach (string workType in role.WorkTypes)
                if (!capable.Contains(workType)) return false;
            return true;
        }

        public int SkillLevel(int pawnIndex, string skill) =>
            skill != null
            && Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(
                skill, out int level)
                ? level
                : 0;

        public IReadOnlyList<RoleSkillView> RequiredSkills(RoleView role)
        {
            if (requiredSkillsByRole.TryGetValue(
                    role.Id, out IReadOnlyList<RoleSkillView> cached))
                return cached;
            var skills = role.Skills.Where(skill => skill.Required)
                .OrderByDescending(skill => skill.Primary)
                .ThenByDescending(skill => skill.Importance)
                .ThenBy(
                    skill => skill.SkillDefName,
                    System.StringComparer.Ordinal)
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

        private List<RoleSkillView> OrderedSkills(RoleView role)
        {
            if (orderedSkillsByRole.TryGetValue(
                    role.Id, out List<RoleSkillView> cached))
                return cached;
            List<RoleSkillView> ordered = role.Skills
                .OrderByDescending(skill => skill.Primary)
                .ThenByDescending(skill => skill.Importance)
                .ThenBy(
                    skill => skill.SkillDefName,
                    System.StringComparer.Ordinal)
                .ToList();
            orderedSkillsByRole[role.Id] = ordered;
            return ordered;
        }

        public bool InsideBand(
            int pawnIndex,
            RoleView role,
            PathView path,
            int entry)
        {
            IReadOnlyList<RoleSkillView> skills = RequiredSkills(role);
            if (skills.Count == 0) return true;
            bool targetRole = PathActivation.UniqueTargetRoleId(path)
                == role.Id;
            foreach (RoleSkillView roleSkill in skills)
            {
                if (targetRole
                    && !PathActivation.IsQualifyingTargetSkill(
                        this, role, path, roleSkill))
                    continue;
                if (!Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(
                        roleSkill.SkillDefName, out int level)
                    || !PathMath.InsideBand(path, entry, level))
                    return false;
            }
            return true;
        }

        public SignalBucket BestSignal(
            int pawnIndex,
            RoleView role,
            out string skill,
            out SignalSource source)
        {
            PawnView pawn = Colony.Pawns[pawnIndex];
            if (pawn.WorkTypeSignalBuckets != null)
                foreach (string workType in role.WorkTypes)
                    if (pawn.WorkTypeSignalBuckets.TryGetValue(
                            workType, out SignalBucket workTypeBucket)
                        && workTypeBucket == SignalBucket.Awful)
                    {
                        skill = null;
                        source = SignalSource.Aggregated;
                        return SignalBucket.Awful;
                    }

            IReadOnlyList<RoleSkillView> required = RequiredSkills(role);
            if (required.Count > 0)
            {
                RoleSkillView primaryRequired = required[0];
                if (!pawn.SkillLevels.ContainsKey(
                        primaryRequired.SkillDefName)
                    || pawn.SignalBuckets.TryGetValue(
                        primaryRequired.SkillDefName,
                        out SignalBucket primaryBucket)
                    && primaryBucket == SignalBucket.Awful)
                {
                    skill = primaryRequired.SkillDefName;
                    source = SignalSource.Aggregated;
                    return SignalBucket.Awful;
                }
            }

            if (role.Skills.Count > 0)
                foreach (RoleSkillView primary in OrderedSkills(role))
                {
                    if (!pawn.SkillLevels.ContainsKey(primary.SkillDefName))
                        continue;
                    skill = primary.SkillDefName;
                    source = SignalSource.Aggregated;
                    SignalBucket bucket = pawn.SignalBuckets.TryGetValue(
                        skill, out SignalBucket classified)
                        ? classified
                        : SignalBucket.Neutral;
                    return Dampen(pawn, required, skill, bucket);
                }

            skill = null;
            source = SignalSource.None;
            bool any = false;
            SignalBucket best = SignalBucket.Awful;
            foreach (string workType in role.WorkTypes)
            {
                if (!Colony.WorkTypeSkills.TryGetValue(
                        workType, out IReadOnlyList<string> skills))
                    continue;
                foreach (string candidateSkill in skills)
                {
                    if (!pawn.SkillLevels.ContainsKey(candidateSkill)) continue;
                    SignalBucket bucket = pawn.SignalBuckets.TryGetValue(
                        candidateSkill, out SignalBucket classified)
                        ? classified
                        : SignalBucket.Neutral;
                    if (!any
                        || bucket > best
                        || bucket == best
                        && System.StringComparer.Ordinal.Compare(
                            candidateSkill, skill) < 0)
                    {
                        best = bucket;
                        skill = candidateSkill;
                        source = SignalSource.Aggregated;
                    }
                    any = true;
                }
            }
            if (any && best == SignalBucket.Awful)
                best = SignalBucket.Poor;
            return any ? best : SignalBucket.Neutral;
        }

        private static SignalBucket Dampen(
            PawnView pawn,
            IReadOnlyList<RoleSkillView> required,
            string primarySkill,
            SignalBucket bucket)
        {
            if (bucket <= SignalBucket.Poor) return bucket;
            foreach (RoleSkillView roleSkill in required)
            {
                if (roleSkill.SkillDefName == primarySkill) continue;
                if (pawn.SkillLevels.ContainsKey(roleSkill.SkillDefName)
                    && pawn.SignalBuckets.TryGetValue(
                        roleSkill.SkillDefName, out SignalBucket secondary)
                    && secondary == SignalBucket.Awful)
                    return bucket - 1;
            }
            return bucket;
        }

        public bool HasProtectedDirectAssignment(int pawnIndex, int roleId)
        {
            IReadOnlyList<AssignmentView> existing =
                Colony.Pawns[pawnIndex].Existing;
            RoleView role = RoleOf(roleId);
            for (int index = 0; index < existing.Count; index++)
                if (existing[index].RoleId == roleId
                    && (existing[index].Pinned
                        || role != null && (role.HasRules || role.Blocker)))
                    return true;
            return false;
        }
    }
}
