using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
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
            RecommendationFormulaEngine formulas)
        {
            RoleId = roleId;
            DirectMinimum = directMinimum;
            this.candidates = candidates;
            this.selectionFlags = selectionFlags;
            this.minimumBonuses = minimumBonuses;
            this.minimumPickCount = minimumPickCount;
            this.formulas = formulas;
        }

        internal int RoleId { get; }
        internal int DirectMinimum { get; }
        internal int CandidateCount => candidates.Length;
        internal CandidateFact CandidateAt(int index) => candidates[index];
        internal bool IsSelected(int index) =>
            (selectionFlags[index] & Selected) != 0;
        internal bool IsTrainingWaiver(int index) =>
            (selectionFlags[index] & TrainingWaiver) != 0;
        internal bool IsSurplus(int index) =>
            (selectionFlags[index] & Surplus) != 0;
        internal byte MinimumBonusAt(int index) => minimumBonuses[index];
        internal byte SelectForCoverage(int index)
        {
            if (minimumBonuses[index] != 0)
                return minimumBonuses[index];
            selectionFlags[index] = Selected;
            byte bonus = MinimumBonus(minimumPickCount);
            minimumBonuses[index] = bonus;
            minimumPickCount++;
            return bonus;
        }
        internal static RolePlan Build(
            EngineContext facts,
            RoleView role,
            IScalingAlgorithm scaling,
            RecommendationFormulaEngine formulas)
        {
            int colonySize = facts.Colony.Pawns.Count;
            int maximum = role.MaxHoldersAt(colonySize);
            int capacity = System.Math.Max(0, colonySize);
            if (maximum < RoleHolderRange.Uncapped)
                capacity = System.Math.Min(capacity, System.Math.Max(0, maximum));
            HolderRequirement requirement = scaling.Requirement(role, colonySize);
            int requiredTotal = System.Math.Min(
                capacity, requirement.RequiredTotal);
            int trainingWaivers = System.Math.Min(
                requirement.TrainingWaivers, requiredTotal);
            int directMinimum = requiredTotal - trainingWaivers;
            PathView championPath = PathActivation.PreferredTargetPath(
                facts.Colony.Paths, role.Id);

            var eligible = new List<CandidateFact>(colonySize);
            for (int pawnIndex = 0; pawnIndex < colonySize; pawnIndex++)
            {
                if (!facts.FullyCapable(pawnIndex, role)) continue;
                SignalBucket verdict = facts.BestSignal(
                    pawnIndex, role, out string skillDefName, out _);
                if (verdict == SignalBucket.Awful) continue;
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
                eligible.Add(new CandidateFact(
                    pawnIndex,
                    verdict,
                    skillLevel,
                    championScore,
                    championSignalScore));
            }

            CandidateFact? champion = directMinimum > 0
                ? BestChampion(eligible)
                : (CandidateFact?)null;
            var ordered = new List<CandidateFact>(eligible.Count);
            if (champion.HasValue) ordered.Add(champion.Value);
            for (int index = 0; index < eligible.Count; index++)
                if (!champion.HasValue
                    || eligible[index].PawnIndex != champion.Value.PawnIndex)
                    ordered.Add(eligible[index]);
            int rankedStart = champion.HasValue ? 1 : 0;
            SortRanked(ordered, rankedStart);

            CandidateFact[] candidates = ordered.ToArray();
            var flags = new byte[candidates.Length];
            var bonuses = new byte[candidates.Length];
            int remainingDirect = directMinimum;
            int remainingTrainingWaivers = trainingWaivers;
            int minimumPickCount = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                CandidateFact candidate = candidates[index];
                byte classification;
                if (remainingDirect > 0)
                {
                    classification = 0;
                    bonuses[index] = MinimumBonus(minimumPickCount);
                    minimumPickCount++;
                    remainingDirect--;
                }
                else
                {
                    bool optionalAptitude =
                        PathActivation.QualifiesOptionalTarget(
                            facts,
                            candidate.PawnIndex,
                            role,
                            out bool aptitudeApplies);
                    if (!optionalAptitude) continue;
                    if (remainingTrainingWaivers > 0)
                    {
                        classification = TrainingWaiver;
                        remainingTrainingWaivers--;
                    }
                    else if (candidate.Verdict >= formulas.SurplusMinimumSignal
                        || aptitudeApplies)
                    {
                        classification = Surplus;
                    }
                    else
                    {
                        continue;
                    }
                }
                flags[index] = (byte)(Selected | classification);
            }
            return new RolePlan(
                role.Id,
                directMinimum,
                candidates,
                flags,
                bonuses,
                minimumPickCount,
                formulas);
        }

        private static byte MinimumBonus(int pickIndex)
        {
            switch (pickIndex)
            {
                case 0: return 10;
                case 1: return 5;
                case 2: return 2;
                default: return 1;
            }
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
            signalScore = SignalTieBreakScore(fallbackVerdict);
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
                if (signal == SignalBucket.Awful) return int.MinValue;
                score += formulas.ChampionSkillScore(level, signal);
                qualifyingSignalScore += SignalTieBreakScore(signal);
            }
            if (count < 2)
                return formulas.ChampionSkillScore(
                    fallbackLevel, fallbackVerdict);
            signalScore = qualifyingSignalScore;
            return score;
        }

        private static int SignalTieBreakScore(SignalBucket verdict)
        {
            switch (verdict)
            {
                case SignalBucket.Exceptional: return 5;
                case SignalBucket.Great: return 3;
                case SignalBucket.Strong: return 1;
                case SignalBucket.Poor: return -3;
                default: return 0;
            }
        }

        private static void SortRanked(
            List<CandidateFact> candidates,
            int start)
        {
            for (int index = start + 1; index < candidates.Count; index++)
            {
                CandidateFact candidate = candidates[index];
                int insertion = index;
                while (insertion > start
                    && CompareRanked(candidate, candidates[insertion - 1]) < 0)
                {
                    candidates[insertion] = candidates[insertion - 1];
                    insertion--;
                }
                candidates[insertion] = candidate;
            }
        }

        private static int CompareRanked(CandidateFact left, CandidateFact right)
        {
            bool leftStrong = left.Verdict >= SignalBucket.Strong;
            bool rightStrong = right.Verdict >= SignalBucket.Strong;
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
