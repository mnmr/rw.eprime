using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Champion spreading: every championship a pawn already holds subtracts
    /// a colony-scaled penalty from that pawn's later champion scores, so
    /// championships distribute unless the repeat pick is clearly better.
    internal static class RepeatChampionPenalties
    {
        internal static int PenaltyFor(
            EngineContext facts,
            RoleView role,
            List<int> priorChampionRoleIds,
            RecommendationFormulaEngine formulas)
        {
            if (priorChampionRoleIds == null) return 0;
            int colonistCount = facts.Colony.Pawns.Count;
            int total = 0;
            for (int index = 0; index < priorChampionRoleIds.Count; index++)
            {
                RoleView prior = facts.RoleOf(priorChampionRoleIds[index]);
                if (prior == null) continue;
                total += formulas.RepeatChampionPenalty(
                    !prior.ChampionPenalty,
                    SharesRequiredSkill(facts, role, prior),
                    colonistCount);
            }
            return total;
        }

        internal static bool SharesRequiredSkill(
            EngineContext facts,
            RoleView left,
            RoleView right)
        {
            IReadOnlyList<RoleSkillView> leftSkills = facts.RequiredSkills(left);
            IReadOnlyList<RoleSkillView> rightSkills =
                facts.RequiredSkills(right);
            for (int leftAt = 0; leftAt < leftSkills.Count; leftAt++)
                for (int rightAt = 0; rightAt < rightSkills.Count; rightAt++)
                    if (leftSkills[leftAt].SkillDefName
                        == rightSkills[rightAt].SkillDefName)
                        return true;
            return false;
        }
    }
}
