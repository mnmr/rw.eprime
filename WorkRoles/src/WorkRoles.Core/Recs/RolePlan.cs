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
        private const byte SignalQualifiedSurplus = 64;

        private readonly CandidateFact[] candidates;
        private readonly byte[] selectionFlags;
        private readonly byte[] minimumBonuses;
        private readonly RecommendationSelectionStage[] selectionStages;
        private readonly int[] stageRanks;
        private readonly int[] selectionSlots;
        private readonly SignalBucket[] selectionSignals;
        // The training-path activation each downgraded pick publishes (null for
        // direct picks and for picks with no path activation). Computed once
        // here so publish does not re-run path resolution.
        private readonly PathActivation[] resolvedActivations;
        private readonly RecommendationFormulaEngine formulas;
        private int minimumPickCount;
        private int coverageRepairCount;

        private RolePlan(
            int roleId,
            int directMinimum,
            CandidateFact[] candidates,
            byte[] selectionFlags,
            byte[] minimumBonuses,
            RecommendationSelectionStage[] selectionStages,
            int[] stageRanks,
            int[] selectionSlots,
            SignalBucket[] selectionSignals,
            PathActivation[] resolvedActivations,
            int selectionSlotCount,
            int minimumPickCount,
            RecommendationFormulaEngine formulas,
            int championPawnIndex)
        {
            RoleId = roleId;
            DirectMinimum = directMinimum;
            this.candidates = candidates;
            this.selectionFlags = selectionFlags;
            this.minimumBonuses = minimumBonuses;
            this.selectionStages = selectionStages;
            this.stageRanks = stageRanks;
            this.selectionSlots = selectionSlots;
            this.selectionSignals = selectionSignals;
            this.resolvedActivations = resolvedActivations;
            this.minimumPickCount = minimumPickCount;
            this.formulas = formulas;
            SelectionSlotCount = selectionSlotCount;
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
        internal bool IsSignalQualifiedSurplus(int index) =>
            (selectionFlags[index] & SignalQualifiedSurplus) != 0;
        internal bool IsMinimumPick(int index) =>
            (selectionFlags[index] & CoverageMinimum) != 0;
        internal RecommendationSelectionStage SelectionStageAt(int index) =>
            selectionStages[index];
        internal int StageRankAt(int index) => stageRanks[index];
        internal int SelectionSlotAt(int index) => selectionSlots[index];
        internal int SelectionSlotCount { get; }
        internal SignalBucket SelectionSignalAt(int index) =>
            selectionSignals[index];
        internal PathActivation ResolvedActivationAt(int index) =>
            resolvedActivations[index];
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
        internal byte SelectForCoverage(int index, int selectionSlot)
        {
            if (AssignmentKindAt(index)
                == RecommendationTargetAssignmentKind.Direct)
                return minimumBonuses[index];
            selectionFlags[index] = (byte)(
                (selectionFlags[index]
                    & ~(TrainingWaiver
                        | Surplus
                        | RequiredWaiver
                        | SignalQualifiedSurplus))
                | Selected
                | CoverageMinimum
                | DirectAssignment);
            selectionStages[index] =
                RecommendationSelectionStage.CoverageRepair;
            stageRanks[index] = ++coverageRepairCount;
            selectionSlots[index] = selectionSlot;
            byte bonus = formulas.MinimumBonus(minimumPickCount);
            minimumBonuses[index] = bonus;
            minimumPickCount++;
            return bonus;
        }
        internal static RolePlan Build(
            EngineContext facts,
            RoleView role,
            RecommendationFormulaEngine formulas,
            PawnDraft[] drafts = null,
            List<int>[] priorChampionsByPawn = null)
        {
            int colonySize = facts.Colony.Pawns.Count;
            // Unskilled means "everyone capable": Max never caps it, so an
            // all-zero (NoMin) scale still fills the whole colony.
            bool unskilled = role.Mode == ScaleMode.Unskilled;
            int maximum = unskilled
                ? RoleHolderRange.Uncapped
                : role.MaxHoldersAt(colonySize);
            int capacity = System.Math.Max(0, colonySize);
            if (maximum < RoleHolderRange.Uncapped)
                capacity = System.Math.Min(capacity, System.Math.Max(0, maximum));
            int protectedDirectHolders = 0;
            for (int pawnIndex = 0; pawnIndex < colonySize; pawnIndex++)
                if (facts.HasProtectedDirectAssignment(pawnIndex, role.Id))
                    protectedDirectHolders++;
            int selectionCapacity = System.Math.Max(
                0, capacity - protectedDirectHolders);
            HolderRequirement requirement = unskilled
                ? new HolderRequirement(
                    role.RequiredTotalAt(colonySize),
                    role.TrainingWaiversAt(colonySize))
                : role.RequirementAt(colonySize);
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
                // The path keeps roles at the colonist's skill level: a pawn in
                // a higher band does not also hold this lower trainee role.
                if (PathActivation.BelongsToHigherBand(facts, pawnIndex, role))
                    continue;
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
            var stages = new RecommendationSelectionStage[candidates.Length];
            var stageRanks = new int[candidates.Length];
            var selectionSlots = new int[candidates.Length];
            var selectionSignals = new SignalBucket[candidates.Length];
            int minimumPickCount = 0;
            int requiredStageCount = 0;
            int trainingWaiverStageCount = 0;
            int surplusStageCount = 0;
            int selectedCount = 0;
            // Unskilled fills every remaining capable pawn (Awful was already
            // dropped by the candidate floor); Skilled surplus stops once three
            // ranked candidates in a row fail the signal criteria.
            bool unskilledFill = role.Mode == ScaleMode.Unskilled;
            // A pick folds under a broader covering role assigned by an
            // earlier-processed role, except a minimum pick of an Unskilled or
            // training-path role, whose own holders survive coverage.
            bool exemptMinimum = unskilledFill || championPath != null;
            const int SurplusMissLimit = 3;
            int consecutiveSurplusMisses = 0;
            // Single pass over the best-first candidate list: the first
            // directPicks take the target directly; the next up to requiredTotal
            // are training picks (downgraded to the band role at publish, or the
            // target if already at its band); the rest are signal-gated surplus.
            // A covered pick folds under a broader role assigned earlier; a
            // covered required slot is satisfied by that coverer and not refilled.
            int directFilled = 0;
            int requiredFilled = 0;
            for (int index = 0;
                 index < candidates.Length && selectedCount < selectionCapacity;
                 index++)
            {
                CandidateFact candidate = candidates[index];
                bool inDirectPhase = directFilled < directPicks;
                bool inRequiredPhase = requiredFilled < requiredTotal;
                bool covered = drafts != null
                    && RecommendationPlan.PawnHasCoverer(
                        facts, drafts[candidate.PawnIndex],
                        candidate.PawnIndex, role);
                if (covered && !(inRequiredPhase && exemptMinimum))
                {
                    if (inRequiredPhase)
                    {
                        requiredFilled++;
                        if (inDirectPhase) directFilled++;
                    }
                    continue;
                }
                if (inDirectPhase)
                {
                    flags[index] = Selected | CoverageMinimum | DirectAssignment;
                    stages[index] = RecommendationSelectionStage.Required;
                    stageRanks[index] = ++requiredStageCount;
                    bonuses[index] = formulas.MinimumBonus(minimumPickCount);
                    minimumPickCount++;
                    selectionSlots[index] =
                        protectedDirectHolders + selectedCount + 1;
                    selectedCount++;
                    directFilled++;
                    requiredFilled++;
                }
                else if (inRequiredPhase)
                {
                    flags[index] = Selected
                        | TrainingWaiver
                        | CoverageMinimum
                        | RequiredWaiver;
                    stages[index] = RecommendationSelectionStage.TrainingWaiver;
                    stageRanks[index] = ++trainingWaiverStageCount;
                    selectionSlots[index] =
                        protectedDirectHolders + selectedCount + 1;
                    selectedCount++;
                    requiredFilled++;
                }
                else
                {
                    bool qualifiedByMultiSkillAptitude = false;
                    bool qualifiesForTarget = unskilledFill
                        || PathActivation.QualifiesOptionalTarget(
                            facts,
                            candidate.PawnIndex,
                            role,
                            formulas,
                            out qualifiedByMultiSkillAptitude);
                    SignalBucket surplusSignal = SurplusSignal(
                        facts,
                        candidate.PawnIndex,
                        role,
                        candidate.Verdict,
                        candidate.SkillLevel,
                        formulas);
                    if (qualifiesForTarget
                        && (unskilledFill
                            || surplusSignal >= formulas.SurplusMinimumSignal
                            || qualifiedByMultiSkillAptitude))
                    {
                        flags[index] = (byte)(Selected
                            | Surplus
                            | (surplusSignal >= formulas.SurplusMinimumSignal
                                ? SignalQualifiedSurplus
                                : 0));
                        stages[index] = RecommendationSelectionStage.Surplus;
                        stageRanks[index] = ++surplusStageCount;
                        selectionSignals[index] = surplusSignal;
                        selectedCount++;
                        consecutiveSurplusMisses = 0;
                    }
                    else if (!unskilledFill
                        && ++consecutiveSurplusMisses >= SurplusMissLimit)
                    {
                        break;
                    }
                }
            }
            // Resolve the training-path activation each downgraded pick will
            // publish, once, so publish never re-runs path resolution. Direct
            // picks always take the target and carry no activation.
            var resolvedActivations = new PathActivation[candidates.Length];
            for (int index = 0; index < candidates.Length; index++)
            {
                if ((flags[index] & Selected) == 0
                    || (flags[index] & DirectAssignment) != 0)
                    continue;
                resolvedActivations[index] = PathActivation.Find(
                    facts, candidates[index].PawnIndex, role, formulas);
            }
            return new RolePlan(
                role.Id,
                directMinimum,
                candidates,
                flags,
                bonuses,
                stages,
                stageRanks,
                selectionSlots,
                selectionSignals,
                resolvedActivations,
                configuredRequiredTotal,
                minimumPickCount,
                formulas,
                champion.HasValue ? champion.Value.PawnIndex : -1);
        }

        internal static SignalBucket SurplusSignal(
            EngineContext facts,
            int pawnIndex,
            RoleView role,
            SignalBucket signal,
            int skillLevel,
            RecommendationFormulaEngine formulas)
        {
            if (facts.RequiredSkills(role).Count != 1)
                return signal;
            // Skill level promotes the signal regardless of band: a high-skill
            // pawn is a surplus for a path target and is downgraded to the
            // band-appropriate trainee at publish time.
            return formulas.PromoteSkillSignal(skillLevel, signal);
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
