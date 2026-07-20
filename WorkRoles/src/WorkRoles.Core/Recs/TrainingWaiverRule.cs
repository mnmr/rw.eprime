using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Allocates the minimum cohort of a path target. Strictly in-band pawns
    /// receive the target role. The most-ready remaining pawns are promoted
    /// until the non-waived floor is met; signals and biological age break
    /// skill ties. The rest may consume training waivers and receive every
    /// matching lower-band role.
    public sealed class TrainingWaiverRule : RecRule
    {
        private readonly ITrainingDemandPolicy demandPolicy;

        public TrainingWaiverRule(ITrainingDemandPolicy demandPolicy)
        {
            this.demandPolicy = demandPolicy;
        }

        public override string Id => "training";
        public override RuleKind Kind => RuleKind.Colony;
        public override bool Relevant(EngineContext context)
            => context.Colony.Paths.Count > 0
            && context.Colony.Roles.Any(r => r.TrainingWaivers > 0);

        public override void Apply(EngineContext context)
        {
            var positions = context.BasePositions();
            foreach (var target in context.Colony.Roles
                         .Where(r => r.TrainingWaivers > 0
                             && context.Want.TryGetValue(r.Id, out int want) && want > 0)
                         .OrderBy(r => positions[r.Id]).ThenBy(r => r.Id))
                AllocateTarget(context, target);
        }

        private void AllocateTarget(EngineContext context, RoleView target)
        {
            int want = context.Want[target.Id];
            int open = System.Math.Max(0, want - context.AllocatedHoldersOf(target.Id));
            if (open == 0) return;

            var matches = new List<PathMatch>();
            for (int pawn = 0; pawn < context.Colony.Pawns.Count; pawn++)
            {
                if (context.CoversRole(pawn, target)
                    || context.TrainingToward[pawn].Count > 0
                    || !context.FullyCapable(pawn, target)
                    || target.Hunting && !context.Colony.Pawns[pawn].HasRangedWeapon)
                    continue;
                SignalBucket bucket = context.BestSignal(pawn, target,
                    out string signalSkill, out _);
                if (bucket == SignalBucket.Awful) continue;
                PathMatch match = BestPath(context, pawn, target);
                if (match == null) continue;
                match.Bucket = bucket;
                match.SignalSkill = signalSkill;
                matches.Add(match);
            }

            var selected = matches
                .OrderByDescending(m => m.InTargetBand)
                .ThenByDescending(m => m.Readiness)
                .ThenByDescending(m => m.WeightedLevel)
                .ThenByDescending(m => m.Bucket)
                .ThenBy(m => context.Colony.Pawns[m.Pawn].BiologicalAgeTicks)
                .ThenBy(m => context.Candidates[m.Pawn].Count)
                .ThenBy(m => m.Pawn)
                .Take(open)
                .ToList();

            int directFloor = System.Math.Max(0, want - target.TrainingWaivers);
            int direct = context.HoldersOf(target.Id)
                + selected.Count(m => m.InTargetBand);
            int promotions = System.Math.Max(0, directFloor - direct);

            foreach (var match in selected.Where(m => m.InTargetBand))
                AddTarget(context, target, match);

            // selected is already ordered by these keys after InTargetBand.
            // Filtering preserves that order, so a second comparer can only
            // drift from the cohort selection contract.
            var below = selected.Where(m => !m.InTargetBand).ToList();
            foreach (var match in below.Take(promotions))
                AddTarget(context, target, match);

            int waiversLeft = System.Math.Max(0, target.TrainingWaivers);
            foreach (var match in below.Skip(promotions))
            {
                if (waiversLeft > 0 && match.TrainingRoles.Count > 0)
                {
                    AddWaiver(context, target, match);
                    waiversLeft--;
                }
                else
                    AddTarget(context, target, match);
            }
        }

        private static PathMatch BestPath(EngineContext context, int pawn, RoleView target)
        {
            PathMatch best = null;
            foreach (var path in context.Colony.Paths)
            {
                int targetEntry = path.RoleIds.IndexOf(target.Id);
                if (targetEntry < 0 || !PathMath.IsTarget(path, targetEntry)) continue;
                bool inTarget = context.InsideBand(pawn, target, path, targetEntry);
                var trainingRoles = inTarget
                    ? new List<(RoleView role, int entry)>()
                    : MatchingTrainingRoles(context, pawn, target, path, targetEntry);
                int readiness = context.RequiredSkills(target)
                    .Select(s => context.SkillLevel(pawn, s.SkillDefName))
                    .DefaultIfEmpty(0).Min();
                int weighted = context.RequiredSkills(target)
                    .Sum(s => context.SkillLevel(pawn, s.SkillDefName)
                        * System.Math.Max(1, s.Importance));
                var match = new PathMatch
                {
                    Pawn = pawn,
                    Path = path,
                    TargetEntry = targetEntry,
                    InTargetBand = inTarget,
                    TrainingRoles = trainingRoles,
                    Readiness = readiness,
                    WeightedLevel = weighted,
                };
                if (best == null || BetterPath(match, best))
                    best = match;
            }
            return best;
        }

        private static bool BetterPath(PathMatch candidate, PathMatch current)
        {
            if (candidate.InTargetBand != current.InTargetBand)
                return candidate.InTargetBand;
            if (!candidate.InTargetBand)
            {
                bool candidateComplete = candidate.TrainingRoles.Count > 0;
                bool currentComplete = current.TrainingRoles.Count > 0;
                if (candidateComplete != currentComplete) return candidateComplete;
            }
            return candidate.Readiness > current.Readiness
                || candidate.Readiness == current.Readiness
                    && candidate.WeightedLevel > current.WeightedLevel;
        }

        private static List<(RoleView role, int entry)> MatchingTrainingRoles(
            EngineContext context, int pawn, RoleView target, PathView path, int targetEntry)
        {
            var roles = new List<(RoleView role, int entry)>();
            foreach (int entry in PathMath.LowerBandEntries(path, targetEntry))
            {
                var role = context.RoleOf(path.RoleIds[entry]);
                if (role == null || role.HolderMode == RoleHolderMode.Never
                    || !context.Capable(pawn, role)
                    || context.HasProtectedDirectAssignment(pawn, role.Id)
                    || context.BestSignal(pawn, role, out _, out _) == SignalBucket.Awful
                    || !context.InsideBand(pawn, role, path, entry))
                    continue;
                roles.Add((role, entry));
            }

            var coveredSkills = new HashSet<string>(roles
                .SelectMany(pair => context.RequiredSkills(pair.role))
                .Select(skill => skill.SkillDefName));
            return context.RequiredSkills(target)
                .All(skill => coveredSkills.Contains(skill.SkillDefName))
                ? roles : new List<(RoleView role, int entry)>();
        }

        private static void AddTarget(EngineContext context, RoleView target, PathMatch match)
        {
            RecordPlacement(context, match, target.Id, target.Id);
            context.AddCandidate(match.Pawn, target.Id, new Reason
            {
                RuleId = "draft",
                Bucket = match.Bucket,
                SkillDefName = match.SignalSkill,
                TowardRoleId = -1,
            }, match.Bucket);
        }

        private void AddWaiver(EngineContext context, RoleView target, PathMatch match)
        {
            context.TrainingToward[match.Pawn].Add(target.Id);
            foreach (var pair in match.TrainingRoles)
            {
                RecordPlacement(context, match, pair.role.Id, target.Id);
                context.AddCandidate(match.Pawn, pair.role.Id, new Reason
                {
                    RuleId = Id,
                    Bucket = match.Bucket,
                    SkillDefName = match.SignalSkill,
                    TowardRoleId = target.Id,
                }, match.Bucket);
                int inbound = context.InboundTraining.TryGetValue(pair.role.Id, out int count)
                    ? count + 1 : 1;
                context.InboundTraining[pair.role.Id] = inbound;
                int baseWant = context.BaseWant.TryGetValue(pair.role.Id, out int value)
                    ? value : 0;
                context.Want[pair.role.Id] = demandPolicy.Minimum(baseWant, inbound);
            }
        }

        private static void RecordPlacement(EngineContext context, PathMatch match,
            int roleId, int targetRoleId)
        {
            context.TrainingPathPlacements[match.Pawn][roleId] =
                new TrainingPathPlacement
                {
                    PathId = match.Path.Id,
                    TargetRoleId = targetRoleId,
                };
        }

        private sealed class PathMatch
        {
            public int Pawn;
            public PathView Path;
            public int TargetEntry;
            public bool InTargetBand;
            public List<(RoleView role, int entry)> TrainingRoles;
            public SignalBucket Bucket;
            public string SignalSkill;
            public int Readiness;
            public int WeightedLevel;
        }
    }
}
