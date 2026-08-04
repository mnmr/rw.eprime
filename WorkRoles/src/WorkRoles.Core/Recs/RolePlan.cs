using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal readonly struct CandidateFact
    {
        internal CandidateFact(
            int pawnIndex,
            SignalBucket verdict,
            int skillLevel,
            int championScore)
        {
            PawnIndex = pawnIndex;
            Verdict = verdict;
            SkillLevel = skillLevel;
            ChampionScore = championScore;
        }

        internal int PawnIndex { get; }
        internal SignalBucket Verdict { get; }
        internal int SkillLevel { get; }
        internal int ChampionScore { get; }
    }

    internal sealed class RolePlan
    {
        private const byte Selected = 1;
        private const byte Waiver = 2;
        private const byte Surplus = 4;

        private readonly CandidateFact[] candidates;
        private readonly byte[] selectionFlags;

        private RolePlan(
            int roleId,
            int directWant,
            CandidateFact[] candidates,
            byte[] selectionFlags)
        {
            RoleId = roleId;
            DirectWant = directWant;
            this.candidates = candidates;
            this.selectionFlags = selectionFlags;
        }

        internal int RoleId { get; }
        internal int DirectWant { get; }
        internal int CandidateCount => candidates.Length;
        internal CandidateFact CandidateAt(int index) => candidates[index];
        internal bool IsSelected(int index) =>
            (selectionFlags[index] & Selected) != 0;
        internal bool IsWaiver(int index) =>
            (selectionFlags[index] & Waiver) != 0;
        internal bool IsSurplus(int index) =>
            (selectionFlags[index] & Surplus) != 0;
        internal void SelectForCoverage(int index)
        {
            if ((selectionFlags[index] & Selected) == 0)
                selectionFlags[index] = Selected;
        }
        internal static RolePlan Build(
            EngineContext facts,
            RoleView role,
            IScalingAlgorithm scaling)
        {
            int colonySize = facts.Colony.Pawns.Count;
            int maximum = role.MaxHoldersAt(colonySize);
            int capacity = System.Math.Max(0, colonySize);
            if (maximum < RoleHolderRange.Uncapped)
                capacity = System.Math.Min(capacity, System.Math.Max(0, maximum));
            int directWant = System.Math.Min(
                capacity,
                System.Math.Max(0, scaling.Want(role, colonySize)));
            int trainingWant = System.Math.Min(
                System.Math.Max(0, role.TrainingWaiversAt(colonySize)),
                capacity - directWant);
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
                    verdict);
                if (championScore == int.MinValue) continue;
                eligible.Add(new CandidateFact(
                    pawnIndex,
                    verdict,
                    skillLevel,
                    championScore));
            }

            CandidateFact? champion = directWant > 0
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
            int openDirect = directWant;
            int openTraining = trainingWant;
            for (int index = 0; index < candidates.Length; index++)
            {
                CandidateFact candidate = candidates[index];
                byte classification;
                if (openDirect > 0)
                {
                    classification = 0;
                    openDirect--;
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
                    if (openTraining > 0)
                    {
                        classification = Waiver;
                        openTraining--;
                    }
                    else if (candidate.Verdict >= SignalBucket.Strong
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
                role.Id, directWant, candidates, flags);
        }

        private static CandidateFact? BestChampion(
            List<CandidateFact> candidates)
        {
            if (candidates.Count == 0) return null;
            CandidateFact best = candidates[0];
            int bestVirtualSkill = VirtualSkill(best);
            for (int index = 1; index < candidates.Count; index++)
            {
                CandidateFact candidate = candidates[index];
                int virtualSkill = VirtualSkill(candidate);
                if (virtualSkill > bestVirtualSkill
                    || virtualSkill == bestVirtualSkill
                    && candidate.PawnIndex < best.PawnIndex)
                {
                    best = candidate;
                    bestVirtualSkill = virtualSkill;
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
            SignalBucket fallbackVerdict)
        {
            if (path == null)
                return fallbackLevel + SignalAdjustment(fallbackVerdict);

            IReadOnlyList<RoleSkillView> targetSkills =
                facts.RequiredSkills(role);
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            int count = 0;
            int score = 0;
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
                score += level + SignalAdjustment(signal);
            }
            return count >= 2
                ? score
                : fallbackLevel + SignalAdjustment(fallbackVerdict);
        }

        private static int SignalAdjustment(SignalBucket verdict)
        {
            switch (verdict)
            {
                case SignalBucket.Poor: return -3;
                case SignalBucket.Strong: return 1;
                case SignalBucket.Great: return 3;
                case SignalBucket.Exceptional: return 5;
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
