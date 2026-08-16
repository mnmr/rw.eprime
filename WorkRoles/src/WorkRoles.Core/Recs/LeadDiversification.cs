using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal sealed partial class PawnDraft
    {
        private readonly List<int> leadRoleIds = new List<int>();

        internal void MarkLead(int roleId)
        {
            if (!leadRoleIds.Contains(roleId)) leadRoleIds.Add(roleId);
        }

        private void AddLeadEdges(
            IReadOnlyList<PathView> paths,
            bool[,] edges,
            int[] incoming)
        {
            for (int leadIndex = 0; leadIndex < leadRoleIds.Count; leadIndex++)
            {
                int leadAt = roleIds.IndexOf(leadRoleIds[leadIndex]);
                if (leadAt < 0) continue;
                for (int roleAt = 0; roleAt < roleIds.Count; roleAt++)
                {
                    if (roleAt == leadAt
                        || edges[leadAt, roleAt]
                        || !PathActivation.Connected(
                            paths, roleIds[leadAt], roleIds[roleAt])
                        || ReversesPathTarget(
                            paths, roleIds[leadAt], roleIds[roleAt]))
                        continue;
                    edges[leadAt, roleAt] = true;
                    incoming[roleAt]++;
                }
            }
        }

        private static bool ReversesPathTarget(
            IReadOnlyList<PathView> paths,
            int leadRoleId,
            int otherRoleId)
        {
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                PathView path = paths[pathIndex];
                if (PathActivation.UniqueTargetRoleId(path) != otherRoleId)
                    continue;
                int leadAt = path.RoleIds.IndexOf(leadRoleId);
                int targetAt = path.RoleIds.IndexOf(otherRoleId);
                if (leadAt >= 0
                    && path.BandMins[leadAt] < path.BandMins[targetAt])
                    return true;
            }
            return false;
        }
    }

    public sealed partial class RecommendationPlan
    {
        private static void DiversifyQualifiedLeads(
            EngineContext facts,
            PawnDraft[] drafts,
            IReadOnlyDictionary<int, long> positions,
            RecommendationFormulaEngine formulas)
        {
            var targets = new List<int>();
            for (int pathIndex = 0;
                 pathIndex < facts.Colony.Paths.Count;
                 pathIndex++)
            {
                int targetRoleId = PathActivation.UniqueTargetRoleId(
                    facts.Colony.Paths[pathIndex]);
                if (targetRoleId >= 0 && !targets.Contains(targetRoleId))
                    targets.Add(targetRoleId);
            }

            while (targets.Count > 0)
            {
                int first = targets[0];
                var component = new List<int>();
                for (int index = targets.Count - 1; index >= 0; index--)
                {
                    int roleId = targets[index];
                    if (!PathActivation.Connected(
                            facts.Colony.Paths, first, roleId))
                        continue;
                    component.Add(roleId);
                    targets.RemoveAt(index);
                }
                if (component.Count < formulas.LeadMinimumConnectedTargets)
                    continue;
                SortLeadRoles(component, facts.Colony.Paths, positions);

                var chosenPawns = new int[component.Count];
                var usedPawns = new bool[drafts.Length];
                if (!TryAssignQualifiedLeads(
                        facts,
                        drafts,
                        component,
                        roleIndex: 0,
                        usedPawns,
                        chosenPawns,
                        formulas))
                    continue;
                for (int roleIndex = 0; roleIndex < component.Count; roleIndex++)
                    drafts[chosenPawns[roleIndex]].MarkLead(component[roleIndex]);
            }
        }

        private static bool TryAssignQualifiedLeads(
            EngineContext facts,
            PawnDraft[] drafts,
            List<int> roleIds,
            int roleIndex,
            bool[] usedPawns,
            int[] chosenPawns,
            RecommendationFormulaEngine formulas)
        {
            if (roleIndex == roleIds.Count) return true;
            int roleId = roleIds[roleIndex];
            RoleView role = facts.RoleOf(roleId);
            int minimum = PathActivation.TargetMinimum(
                facts.Colony.Paths, roleId);
            var candidates = new List<int>();
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
            {
                if (usedPawns[pawnIndex] || !drafts[pawnIndex].ContainsRole(roleId))
                    continue;
                if (QualifiedLead(
                        facts,
                        pawnIndex,
                        role,
                        minimum,
                        formulas,
                        out _,
                        out _))
                    candidates.Add(pawnIndex);
            }
            candidates.Sort((left, right) => CompareQualifiedPawns(
                left, right, facts, role));
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                int pawnIndex = candidates[candidateIndex];
                usedPawns[pawnIndex] = true;
                chosenPawns[roleIndex] = pawnIndex;
                if (TryAssignQualifiedLeads(
                        facts,
                        drafts,
                        roleIds,
                        roleIndex + 1,
                        usedPawns,
                        chosenPawns,
                        formulas))
                {
                    return true;
                }
                usedPawns[pawnIndex] = false;
            }
            return false;
        }

        private static int CompareQualifiedPawns(
            int left,
            int right,
            EngineContext facts,
            RoleView role)
        {
            SignalBucket leftVerdict = facts.BestSignal(
                left, role, out string leftSkill, out _);
            SignalBucket rightVerdict = facts.BestSignal(
                right, role, out string rightSkill, out _);
            int leftLevel = facts.SkillLevel(left, leftSkill);
            int rightLevel = facts.SkillLevel(right, rightSkill);
            int level = rightLevel.CompareTo(leftLevel);
            if (level != 0) return level;
            int verdict = rightVerdict.CompareTo(leftVerdict);
            return verdict != 0 ? verdict : left.CompareTo(right);
        }

        private static bool QualifiedLead(
            EngineContext facts,
            int pawnIndex,
            RoleView role,
            int minimum,
            RecommendationFormulaEngine formulas,
            out SignalBucket verdict,
            out int level)
        {
            verdict = facts.BestSignal(
                pawnIndex, role, out string skillDefName, out _);
            level = facts.SkillLevel(pawnIndex, skillDefName);
            return verdict >= formulas.LeadMinimumSignal
                && MeetsMinimum(facts, pawnIndex, role, minimum);
        }

        private static bool MeetsMinimum(
            EngineContext facts,
            int pawnIndex,
            RoleView role,
            int minimum)
        {
            IReadOnlyList<RoleSkillFact> skills = facts.RequiredSkills(role);
            if (skills.Count == 0) return false;
            for (int index = 0; index < skills.Count; index++)
                if (facts.SkillLevel(pawnIndex, skills[index].SkillDefName) < minimum)
                    return false;
            return true;
        }

        private static void SortLeadRoles(
            List<int> roleIds,
            IReadOnlyList<PathView> paths,
            IReadOnlyDictionary<int, long> positions)
        {
            for (int index = 1; index < roleIds.Count; index++)
            {
                int roleId = roleIds[index];
                int insertion = index;
                while (insertion > 0
                    && CompareLeadRoles(
                        roleId, roleIds[insertion - 1], paths, positions) < 0)
                {
                    roleIds[insertion] = roleIds[insertion - 1];
                    insertion--;
                }
                roleIds[insertion] = roleId;
            }
        }

        private static int CompareLeadRoles(
            int left,
            int right,
            IReadOnlyList<PathView> paths,
            IReadOnlyDictionary<int, long> positions)
        {
            int minimum = PathActivation.TargetMinimum(paths, right)
                .CompareTo(PathActivation.TargetMinimum(paths, left));
            if (minimum != 0) return minimum;
            int position = positions[left].CompareTo(positions[right]);
            return position != 0 ? position : left.CompareTo(right);
        }
    }
}
