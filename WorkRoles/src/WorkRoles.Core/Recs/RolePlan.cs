using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal enum RecommendationTargetAssignmentKind : byte
    {
        None,
        Direct,
        TrainingWaiver,
        Surplus,
    }

    internal readonly struct CandidateFact
    {
        internal CandidateFact(
            int pawnIndex,
            SignalBucket verdict,
            int skillLevel,
            int championScore,
            int championSignalScore)
        {
            PawnIndex = pawnIndex;
            Verdict = verdict;
            SkillLevel = skillLevel;
            ChampionScore = championScore;
            ChampionSignalScore = championSignalScore;
        }

        internal int PawnIndex { get; }
        internal SignalBucket Verdict { get; }
        internal int SkillLevel { get; }
        internal int ChampionScore { get; }
        internal int ChampionSignalScore { get; }
    }

    internal sealed class RolePlan
    {
        private const byte Selected = 1;
        private const byte TrainingWaiver = 2;
        private const byte Surplus = 4;
        private const byte CoverageMinimum = 8;
        private const byte DirectAssignment = 16;
        private const byte RequiredWaiver = 32;

        private readonly CandidateFact[] candidates;
        private readonly byte[] selectionFlags;
        private readonly byte[] minimumBonuses;
        private readonly RecommendationFormulaEngine formulas;
        private int minimumPickCount;

        private RolePlan(
            int roleId,
            int directMinimum,
            CandidateFact[] candidates,
            byte[] selectionFlags,
            byte[] minimumBonuses,
            int minimumPickCount,
            RecommendationFormulaEngine formulas,
            int championPawnIndex)
        {
            RoleId = roleId;
            DirectMinimum = directMinimum;
            this.candidates = candidates;
            this.selectionFlags = selectionFlags;
            this.minimumBonuses = minimumBonuses;
            this.minimumPickCount = minimumPickCount;
            this.formulas = formulas;
            ChampionPawnIndex = championPawnIndex;
        }

        internal int RoleId { get; }
        internal int DirectMinimum { get; }
        /// The pawn selected as this role's champion; -1 when the role picked
        /// none. Later roles subtract repeat-champion penalties for it.
        internal int ChampionPawnIndex { get; }
        internal int CandidateCount => candidates.Length;
        internal CandidateFact CandidateAt(int index) => candidates[index];
        internal bool IsSelected(int index) =>
            (selectionFlags[index] & Selected) != 0;
        internal bool IsTrainingWaiver(int index) =>
            (selectionFlags[index] & TrainingWaiver) != 0;
        internal bool IsSurplus(int index) =>
            (selectionFlags[index] & Surplus) != 0;
        internal bool IsMinimumPick(int index) =>
            (selectionFlags[index] & CoverageMinimum) != 0;
        internal RecommendationTargetAssignmentKind AssignmentKindAt(
            int index)
        {
            byte flags = selectionFlags[index];
            if ((flags & Selected) == 0)
                return RecommendationTargetAssignmentKind.None;
            if ((flags & DirectAssignment) != 0)
                return RecommendationTargetAssignmentKind.Direct;
            if ((flags & RequiredWaiver) != 0)
                return RecommendationTargetAssignmentKind.TrainingWaiver;
            return (flags & Surplus) != 0
                ? RecommendationTargetAssignmentKind.Surplus
                : RecommendationTargetAssignmentKind.None;
        }
        internal byte MinimumBonusAt(int index) => minimumBonuses[index];
        internal byte SelectForCoverage(int index)
        {
            if (AssignmentKindAt(index)
                == RecommendationTargetAssignmentKind.Direct)
                return minimumBonuses[index];
            selectionFlags[index] = (byte)(
                (selectionFlags[index]
                    & ~(TrainingWaiver | Surplus | RequiredWaiver))
                | Selected
                | CoverageMinimum
                | DirectAssignment);
            byte bonus = formulas.MinimumBonus(minimumPickCount);
            minimumBonuses[index] = bonus;
            minimumPickCount++;
            return bonus;
        }
        internal static RolePlan Build(
            EngineContext facts,
            RoleView role,
            IScalingAlgorithm scaling,
            RecommendationFormulaEngine formulas,
            List<int>[] priorChampionsByPawn = null)
        {
            int colonySize = facts.Colony.Pawns.Count;
            int maximum = role.MaxHoldersAt(colonySize);
            int capacity = System.Math.Max(0, colonySize);
            if (maximum < RoleHolderRange.Uncapped)
                capacity = System.Math.Min(capacity, System.Math.Max(0, maximum));
            int protectedDirectHolders = 0;
            for (int pawnIndex = 0; pawnIndex < colonySize; pawnIndex++)
                if (facts.HasProtectedDirectAssignment(pawnIndex, role.Id))
                    protectedDirectHolders++;
            int selectionCapacity = System.Math.Max(
                0, capacity - protectedDirectHolders);
            HolderRequirement requirement = scaling.Requirement(role, colonySize);
            int configuredRequiredTotal = System.Math.Min(
                capacity, requirement.RequiredTotal);
            int directMinimum = System.Math.Min(
                configuredRequiredTotal, requirement.DirectMinimum);
            int requiredTotal = System.Math.Max(
                0, configuredRequiredTotal - protectedDirectHolders);
            int directPicks = System.Math.Min(
                requiredTotal,
                System.Math.Max(0, directMinimum - protectedDirectHolders));
            int trainingWaivers = requiredTotal - directPicks;
            PathView championPath = PathActivation.PreferredTargetPath(
                facts.Colony.Paths, role.Id);

            var eligible = new List<CandidateFact>(colonySize);
            for (int pawnIndex = 0; pawnIndex < colonySize; pawnIndex++)
            {
                if (facts.HasProtectedDirectAssignment(pawnIndex, role.Id))
                    continue;
                if (!facts.FullyCapable(pawnIndex, role)) continue;
                SignalBucket verdict = facts.BestSignal(
                    pawnIndex, role, out string skillDefName, out _);
                if (verdict < formulas.CandidateMinimumSignal) continue;
                int skillLevel = facts.SkillLevel(pawnIndex, skillDefName);
                int championScore = ChampionScore(
                    facts,
                    pawnIndex,
                    role,
                    championPath,
                    skillLevel,
                    verdict,
                    formulas,
                    out int championSignalScore);
                if (championScore == int.MinValue) continue;
                if (priorChampionsByPawn != null)
                    championScore -= RepeatChampionPenalties.PenaltyFor(
                        facts,
                        role,
                        priorChampionsByPawn[pawnIndex],
                        formulas);
                eligible.Add(new CandidateFact(
                    pawnIndex,
                    verdict,
                    skillLevel,
                    championScore,
                    championSignalScore));
            }

            CandidateFact? champion = directPicks > 0
                ? BestChampion(eligible)
                : (CandidateFact?)null;
            var ordered = new List<CandidateFact>(eligible.Count);
            if (champion.HasValue) ordered.Add(champion.Value);
            for (int index = 0; index < eligible.Count; index++)
                if (!champion.HasValue
                    || eligible[index].PawnIndex != champion.Value.PawnIndex)
                    ordered.Add(eligible[index]);
            int rankedStart = champion.HasValue ? 1 : 0;
            SortRanked(ordered, rankedStart, formulas);

            CandidateFact[] candidates = ordered.ToArray();
            var flags = new byte[candidates.Length];
            var bonuses = new byte[candidates.Length];
            int minimumPickCount = 0;
            int requiredPickCount = 0;

            for (int index = 0;
                 index < candidates.Length
                 && requiredPickCount < directPicks;
                 index++)
            {
                flags[index] = Selected
                    | CoverageMinimum
                    | DirectAssignment;
                bonuses[index] = formulas.MinimumBonus(minimumPickCount);
                minimumPickCount++;
                requiredPickCount++;
            }

            int remainingTrainingWaivers = trainingWaivers;
            for (int index = 0;
                 index < candidates.Length
                 && requiredPickCount < requiredTotal
                 && remainingTrainingWaivers > 0;
                 index++)
            {
                if ((flags[index] & Selected) != 0) continue;
                CandidateFact candidate = candidates[index];
                if (!PathActivation.QualifiesOptionalTarget(
                        facts,
                        candidate.PawnIndex,
                        role,
                        formulas,
                        out _))
                    continue;
                flags[index] = Selected
                    | TrainingWaiver
                    | CoverageMinimum
                    | RequiredWaiver;
                requiredPickCount++;
                remainingTrainingWaivers--;
            }

            for (int index = 0;
                 index < candidates.Length
                 && requiredPickCount < requiredTotal;
                 index++)
            {
                if ((flags[index] & Selected) != 0) continue;
                flags[index] = Selected
                    | CoverageMinimum
                    | RequiredWaiver;
                requiredPickCount++;
            }

            int selectedCount = requiredPickCount;
            for (int index = 0;
                 index < candidates.Length
                 && selectedCount < selectionCapacity;
                 index++)
            {
                if ((flags[index] & Selected) != 0) continue;
                CandidateFact candidate = candidates[index];
                bool qualifiesForTarget =
                    PathActivation.QualifiesOptionalTarget(
                        facts,
                        candidate.PawnIndex,
                        role,
                        formulas,
                        out bool qualifiedByMultiSkillAptitude);
                if (qualifiesForTarget
                    && (candidate.Verdict >= formulas.SurplusMinimumSignal
                        || qualifiedByMultiSkillAptitude))
                {
                    flags[index] = Selected | Surplus;
                    selectedCount++;
                }
            }
            return new RolePlan(
                role.Id,
                directMinimum,
                candidates,
                flags,
                bonuses,
                minimumPickCount,
                formulas,
                champion.HasValue ? champion.Value.PawnIndex : -1);
        }

        private static CandidateFact? BestChampion(
            List<CandidateFact> candidates)
        {
            if (candidates.Count == 0) return null;
            CandidateFact best = candidates[0];
            int bestVirtualSkill = VirtualSkill(best);
            int bestSignalScore = best.ChampionSignalScore;
            for (int index = 1; index < candidates.Count; index++)
            {
                CandidateFact candidate = candidates[index];
                int virtualSkill = VirtualSkill(candidate);
                if (virtualSkill > bestVirtualSkill
                    || virtualSkill == bestVirtualSkill
                    && (candidate.ChampionSignalScore > bestSignalScore
                        || candidate.ChampionSignalScore == bestSignalScore
                        && candidate.PawnIndex < best.PawnIndex))
                {
                    best = candidate;
                    bestVirtualSkill = virtualSkill;
                    bestSignalScore = candidate.ChampionSignalScore;
                }
            }
            return best;
        }

        private static int VirtualSkill(CandidateFact candidate) =>
            candidate.ChampionScore;

        private static int ChampionScore(
            EngineContext facts,
            int pawnIndex,
            RoleView role,
            PathView path,
            int fallbackLevel,
            SignalBucket fallbackVerdict,
            RecommendationFormulaEngine formulas,
            out int signalScore)
        {
            signalScore = formulas.ChampionSignalTieBreak(fallbackVerdict);
            if (path == null)
                return formulas.ChampionSkillScore(
                    fallbackLevel, fallbackVerdict);

            IReadOnlyList<RoleSkillView> targetSkills =
                facts.RequiredSkills(role);
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            int count = 0;
            int score = 0;
            int qualifyingSignalScore = 0;
            for (int index = 0; index < targetSkills.Count; index++)
            {
                RoleSkillView skill = targetSkills[index];
                if (!PathActivation.IsQualifyingTargetSkill(
                        facts, role, path, skill))
                    continue;
                count++;
                if (!pawn.SkillLevels.TryGetValue(
                        skill.SkillDefName, out int level))
                    return int.MinValue;
                SignalBucket signal = pawn.SignalBuckets.TryGetValue(
                    skill.SkillDefName, out SignalBucket classified)
                    ? classified
                    : SignalBucket.Neutral;
                if (signal < formulas.CandidateMinimumSignal)
                    return int.MinValue;
                score += formulas.ChampionSkillScore(level, signal);
                qualifyingSignalScore += formulas.ChampionSignalTieBreak(signal);
            }
            if (count < formulas.ChampionMultiSkillMinimumCount)
                return formulas.ChampionSkillScore(
                    fallbackLevel, fallbackVerdict);
            signalScore = qualifyingSignalScore;
            return score;
        }

        private static void SortRanked(
            List<CandidateFact> candidates,
            int start,
            RecommendationFormulaEngine formulas)
        {
            for (int index = start + 1; index < candidates.Count; index++)
            {
                CandidateFact candidate = candidates[index];
                int insertion = index;
                while (insertion > start
                    && CompareRanked(
                        candidate, candidates[insertion - 1], formulas) < 0)
                {
                    candidates[insertion] = candidates[insertion - 1];
                    insertion--;
                }
                candidates[insertion] = candidate;
            }
        }

        private static int CompareRanked(
            CandidateFact left,
            CandidateFact right,
            RecommendationFormulaEngine formulas)
        {
            bool leftStrong = left.Verdict >=
                formulas.RankedCandidatePrioritySignal;
            bool rightStrong = right.Verdict >=
                formulas.RankedCandidatePrioritySignal;
            if (leftStrong != rightStrong) return leftStrong ? -1 : 1;
            if (leftStrong)
            {
                int verdict = right.Verdict.CompareTo(left.Verdict);
                if (verdict != 0) return verdict;
            }
            int skill = right.SkillLevel.CompareTo(left.SkillLevel);
            return skill != 0
                ? skill
                : left.PawnIndex.CompareTo(right.PawnIndex);
        }
    }
}
