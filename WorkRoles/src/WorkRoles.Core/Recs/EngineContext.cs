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
        /// Per-role holder requirement, precomputed once for this colony size.
        private readonly Dictionary<int, HolderRequirement> requirementByRoleId;
        private readonly Dictionary<int, IReadOnlyList<RoleSkillFact>>
            participatingUsedByRole =
                new Dictionary<int, IReadOnlyList<RoleSkillFact>>();
        private readonly Dictionary<int, List<RoleSkillFact>>
            orderedSkillsByRole =
                new Dictionary<int, List<RoleSkillFact>>();
        private readonly Dictionary<int, string[]> neededByRole =
            new Dictionary<int, string[]>();
        private readonly Dictionary<int, HashSet<string>> trainedByRole =
            new Dictionary<int, HashSet<string>>();
        private readonly Dictionary<int, PathSkillModel> pathSkillsByPathId =
            new Dictionary<int, PathSkillModel>();
        private readonly Dictionary<int, RoleWorkContentSpec[]> gatedContentsByRole =
            new Dictionary<int, RoleWorkContentSpec[]>();
        private Dictionary<int, long> basePositions;
        private IReadOnlyDictionary<int, long> basePositionsView;

        public EngineContext(ColonyView colony)
        {
            Colony = colony;
            RolesById = colony.Roles.ToDictionary(role => role.Id);
            PathsById = colony.Paths.ToDictionary(path => path.Id);
            requirementByRoleId = new Dictionary<int, HolderRequirement>(
                colony.Roles.Count);
            foreach (RoleView role in colony.Roles)
                requirementByRoleId[role.Id] = RoleDemand.RequirementFor(
                    role.ColonyMin, role.CoveragePercent, colony.Pawns.Count);
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

        public HolderRequirement RequirementOf(int roleId) =>
            requirementByRoleId.TryGetValue(
                roleId, out HolderRequirement requirement)
                ? requirement
                : default;

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
            PawnView pawn = Colony.Pawns[pawnIndex];
            if (!WithinAgeLimits(pawn, role)
                || !MeetsExplicitRequiredSkills(pawnIndex, role)) return false;
            IReadOnlyList<string> workTypes = role.WorkTypes;
            for (int index = 0; index < workTypes.Count; index++)
                if (pawn.CapableWorkTypes.Contains(workTypes[index]))
                    return true;
            return false;
        }

        public bool FullyCapable(int pawnIndex, RoleView role)
        {
            PawnView pawn = Colony.Pawns[pawnIndex];
            if (!WithinAgeLimits(pawn, role)
                || !MeetsExplicitRequiredSkills(pawnIndex, role)) return false;
            IReadOnlyList<string> workTypes = role.WorkTypes;
            for (int index = 0; index < workTypes.Count; index++)
                if (!pawn.CapableWorkTypes.Contains(workTypes[index]))
                    return false;
            return true;
        }

        /// The any/all capability policy is derived once by the spec builder;
        /// eligibility stays level-free (content gates are readiness facts).
        public bool MeetsCapabilityRequirement(int pawnIndex, RoleView role) =>
            role.WorkSpec.CapabilityRequirement
                == RoleWorkCapabilityRequirement.Any
                ? Capable(pawnIndex, role)
                : FullyCapable(pawnIndex, role);

        public bool MeetsExplicitRequiredSkills(int pawnIndex, RoleView role)
        {
            IReadOnlyList<string> required = role.DeclaredRequiredSkills;
            if (required == null || required.Count == 0) return true;
            PawnView pawn = Colony.Pawns[pawnIndex];
            for (int index = 0; index < required.Count; index++)
                if (!string.IsNullOrEmpty(required[index])
                    && !pawn.SkillLevels.ContainsKey(required[index]))
                    return false;
            return true;
        }

        private static bool WithinAgeLimits(PawnView pawn, RoleView role) =>
            !pawn.AgeLimitsApply
            || (pawn.BiologicalAgeTicks >= role.MinAgeTicks
                && (role.MaxAge <= 0
                    || pawn.BiologicalAgeTicks < role.MaxAgeTicks));

        public int SkillLevel(int pawnIndex, string skill) =>
            skill != null
            && Colony.Pawns[pawnIndex].SkillLevels.TryGetValue(
                skill, out int level)
                ? level
                : 0;

        /// The primary and gate-bearing skill facts, primary first: the
        /// decisive set for signals, dampening, surplus promotion, champion
        /// overlap, and lead qualification, unchanged from the pre-spec
        /// engine so published rankings do not move.
        public IReadOnlyList<RoleSkillFact> RequiredSkills(RoleView role)
        {
            if (participatingUsedByRole.TryGetValue(
                    role.Id, out IReadOnlyList<RoleSkillFact> cached))
                return cached;
            var skills = role.Skills
                .Where(skill => skill.Primary || skill.GatedContents > 0)
                .OrderByDescending(skill => skill.Primary)
                .ThenByDescending(skill => skill.Importance)
                .ThenBy(
                    skill => skill.SkillDefName,
                    System.StringComparer.Ordinal)
                .ToList();
            participatingUsedByRole[role.Id] = skills;
            return skills;
        }

        /// Skills a training path can address for this target: the decisive
        /// primary and gate-bearing skills. A secondary used skill never
        /// gates a target on its own; it joins per path only when a path
        /// role trains it as that role's primary (PathSkills).
        public string[] NeededSkills(RoleView role)
        {
            if (neededByRole.TryGetValue(role.Id, out string[] cached))
                return cached;
            var names = role.Skills
                .Where(skill => skill.Primary || skill.GatedContents > 0)
                .Select(skill => skill.SkillDefName)
                .ToArray();
            neededByRole[role.Id] = names;
            return names;
        }

        /// Participating trained skills: what the role actually contributes
        /// to a training path.
        public HashSet<string> TrainedSkillSet(RoleView role)
        {
            if (trainedByRole.TryGetValue(role.Id, out HashSet<string> cached))
                return cached;
            var names = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (RoleSkillFact skill in role.Skills)
                if (skill.Participates && skill.TrainedGivers > 0)
                    names.Add(skill.SkillDefName);
            trainedByRole[role.Id] = names;
            return names;
        }

        /// Gate-bearing contents the pawn currently meets. Plan-build ranking
        /// tie-break only; eligibility and cached verdicts never read this.
        public int GateReadinessCount(int pawnIndex, RoleView role)
        {
            RoleWorkContentSpec[] gated = GatedContents(role);
            if (gated.Length == 0) return 0;
            PawnView pawn = Colony.Pawns[pawnIndex];
            int met = 0;
            for (int index = 0; index < gated.Length; index++)
            {
                IReadOnlyList<RoleContentGate> gates = gated[index].Gates;
                bool meetsAll = true;
                for (int gateAt = 0; gateAt < gates.Count && meetsAll; gateAt++)
                    if (!pawn.SkillLevels.TryGetValue(
                            gates[gateAt].SkillDefName, out int level)
                        || level < gates[gateAt].MinimumLevel)
                        meetsAll = false;
                if (meetsAll) met++;
            }
            return met;
        }

        private RoleWorkContentSpec[] GatedContents(RoleView role)
        {
            if (gatedContentsByRole.TryGetValue(
                    role.Id, out RoleWorkContentSpec[] cached))
                return cached;
            var gated = new List<RoleWorkContentSpec>();
            foreach (RoleWorkCapabilitySpec capability
                in role.WorkSpec.Capabilities)
                foreach (RoleWorkGiverSpec giver in capability.Givers)
                    foreach (RoleWorkContentSpec content in giver.Contents)
                        if (content.Gates.Count > 0) gated.Add(content);
            RoleWorkContentSpec[] result = gated.ToArray();
            gatedContentsByRole[role.Id] = result;
            return result;
        }

        /// Per-path skill model. Qualifying = the target's primary and gated
        /// skills plus skills a path role carries as its primary: the set
        /// that gates the target's band, champion scoring, and aptitude,
        /// exactly as the pre-spec engine derived it. Covered = qualifying
        /// intersect what the path's roles actually train: the subset that
        /// substitution can address; trainee entries activate only on their
        /// own contribution.
        internal sealed class PathSkillModel
        {
            internal int TargetRoleId;
            internal string[] Qualifying = System.Array.Empty<string>();
            internal string[] Covered = System.Array.Empty<string>();
            internal string[][] Contributions = System.Array.Empty<string[]>();
        }

        internal PathSkillModel PathSkills(PathView path)
        {
            if (pathSkillsByPathId.TryGetValue(
                    path.Id, out PathSkillModel cached))
                return cached;
            var model = new PathSkillModel
            {
                TargetRoleId = PathActivation.UniqueTargetRoleId(path),
                Contributions = new string[path.RoleIds.Count][],
            };
            RoleView target = RoleOf(model.TargetRoleId);
            var qualifying = new List<string>();
            if (target != null)
            {
                qualifying.AddRange(NeededSkills(target));
                for (int entry = 0; entry < path.RoleIds.Count; entry++)
                {
                    RoleView trainer = RoleOf(path.RoleIds[entry]);
                    if (trainer == null || trainer.Id == model.TargetRoleId)
                        continue;
                    string primary = trainer.PrimarySkill;
                    if (primary == null || qualifying.Contains(primary))
                        continue;
                    foreach (RoleSkillFact fact in target.Skills)
                        if (fact.SkillDefName == primary)
                        {
                            qualifying.Add(primary);
                            break;
                        }
                }
            }
            var covered = new List<string>();
            for (int entry = 0; entry < path.RoleIds.Count; entry++)
            {
                RoleView role = RoleOf(path.RoleIds[entry]);
                if (role == null || role.Id == model.TargetRoleId
                    || target == null)
                {
                    model.Contributions[entry] = System.Array.Empty<string>();
                    continue;
                }
                HashSet<string> trained = TrainedSkillSet(role);
                var contribution = new List<string>();
                foreach (string skill in qualifying)
                    if (trained.Contains(skill))
                    {
                        contribution.Add(skill);
                        if (!covered.Contains(skill)) covered.Add(skill);
                    }
                model.Contributions[entry] = contribution.ToArray();
            }
            model.Qualifying = qualifying.ToArray();
            model.Covered = covered.ToArray();
            pathSkillsByPathId[path.Id] = model;
            return model;
        }

        /// Every skill fact in ranking order: the signal fallback reads the
        /// first skill the pawn has, exactly as the pre-spec engine did.
        private List<RoleSkillFact> OrderedSkills(RoleView role)
        {
            if (orderedSkillsByRole.TryGetValue(
                    role.Id, out List<RoleSkillFact> cached))
                return cached;
            List<RoleSkillFact> ordered = role.Skills
                .OrderByDescending(skill => skill.Primary)
                .ThenByDescending(skill => skill.Importance)
                .ThenBy(
                    skill => skill.SkillDefName,
                    System.StringComparer.Ordinal)
                .ToList();
            orderedSkillsByRole[role.Id] = ordered;
            return ordered;
        }

        /// Band evaluation. The target entry gates on the path's qualifying
        /// skills (primary, gated, trainer-primary), unchanged from the
        /// pre-spec engine. A non-target entry is inside when at least one
        /// skill it contributes lies inside its band; an empty contribution
        /// never qualifies it as a trainee.
        public bool InsideBand(
            int pawnIndex,
            RoleView role,
            PathView path,
            int entry)
        {
            PathSkillModel model = PathSkills(path);
            PawnView pawn = Colony.Pawns[pawnIndex];
            if (role.Id == model.TargetRoleId)
            {
                string[] gating = model.Qualifying;
                for (int index = 0; index < gating.Length; index++)
                    if (!pawn.SkillLevels.TryGetValue(
                            gating[index], out int level)
                        || !PathMath.InsideBand(path, entry, level))
                        return false;
                return true;
            }
            string[] contribution = entry >= 0
                && entry < model.Contributions.Length
                    ? model.Contributions[entry]
                    : System.Array.Empty<string>();
            for (int index = 0; index < contribution.Length; index++)
                if (pawn.SkillLevels.TryGetValue(
                        contribution[index], out int level)
                    && PathMath.InsideBand(path, entry, level))
                    return true;
            return false;
        }

        public SignalBucket BestSignal(
            int pawnIndex,
            RoleView role,
            out string skill,
            out SignalSource source)
        {
            PawnView pawn = Colony.Pawns[pawnIndex];
            if (!MeetsExplicitRequiredSkills(pawnIndex, role))
            {
                skill = FirstMissingExplicitRequiredSkill(pawn, role);
                source = SignalSource.Aggregated;
                return SignalBucket.Awful;
            }
            if (pawn.WorkTypeSignalBuckets != null)
            {
                IReadOnlyList<string> workTypes = role.WorkTypes;
                for (int index = 0; index < workTypes.Count; index++)
                    if (pawn.WorkTypeSignalBuckets.TryGetValue(
                            workTypes[index], out SignalBucket workTypeBucket)
                        && workTypeBucket == SignalBucket.Awful)
                    {
                        skill = null;
                        source = SignalSource.Aggregated;
                        return SignalBucket.Awful;
                    }
            }

            IReadOnlyList<RoleSkillFact> required = RequiredSkills(role);
            if (required.Count > 0)
            {
                RoleSkillFact primaryRequired = required[0];
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

            foreach (RoleSkillFact primary in OrderedSkills(role))
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
            return SignalBucket.Neutral;
        }

        private static string FirstMissingExplicitRequiredSkill(
            PawnView pawn, RoleView role)
        {
            IReadOnlyList<string> required = role.DeclaredRequiredSkills;
            if (required == null) return null;
            for (int index = 0; index < required.Count; index++)
                if (!string.IsNullOrEmpty(required[index])
                    && !pawn.SkillLevels.ContainsKey(required[index]))
                    return required[index];
            return null;
        }

        private static SignalBucket Dampen(
            PawnView pawn,
            IReadOnlyList<RoleSkillFact> required,
            string primarySkill,
            SignalBucket bucket)
        {
            if (bucket <= SignalBucket.Poor) return bucket;
            foreach (RoleSkillFact roleSkill in required)
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
