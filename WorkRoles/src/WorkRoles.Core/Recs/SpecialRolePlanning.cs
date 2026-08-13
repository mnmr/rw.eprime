namespace WorkRoles.Core.Recs
{
    internal sealed partial class PawnDraft
    {
        private const byte SpecialSource = 4;

        private int hunterTier = -1;
        private int fireBlockerRoleId = -1;

        internal void AddSpecialRole(int roleId)
            => AddRole(roleId, SpecialSource);

        internal bool IsSpecialRole(int roleId)
        {
            int index = roleIds.IndexOf(roleId);
            return index >= 0 && (roleSources[index] & SpecialSource) != 0;
        }

        internal void AddHunter(int roleId, int tier)
        {
            AddSpecialRole(roleId);
            hunterTier = tier;
        }

        internal void PromoteHunterToFirstTier() => hunterTier = 1;

        internal void AddFireBlocker(int roleId)
        {
            AddSpecialRole(roleId);
            fireBlockerRoleId = roleId;
        }

        private void ApplySpecialKeys(EngineContext facts, long[] keys)
        {
            if (fireBlockerRoleId >= 0)
            {
                int fireAt = roleIds.IndexOf(fireBlockerRoleId);
                if (fireAt >= 0) keys[fireAt] = long.MinValue;
            }
        }

        private bool TierPlacesHunter(EngineContext facts, int pawnIndex)
        {
            int hunterRoleId = facts.Colony.HunterRoleId;
            return hunterTier >= 1
                && hunterRoleId >= 0
                && !facts.Colony.OrderTemplate.Contains(hunterRoleId)
                && !facts.HasProtectedDirectAssignment(pawnIndex, hunterRoleId);
        }

        /// A tier-placed Hunter is withheld from keyed ordering, protected
        /// anchoring, and score bubbling so it cannot shift other roles;
        /// PlaceHunter inserts it afterwards.
        private int[] WithoutPlacedHunter(
            EngineContext facts, int pawnIndex, int[] roles)
        {
            if (!TierPlacesHunter(facts, pawnIndex)) return roles;
            int at = System.Array.IndexOf(roles, facts.Colony.HunterRoleId);
            if (at < 0) return roles;
            var trimmed = new int[roles.Length - 1];
            for (int index = 0, target = 0; index < roles.Length; index++)
                if (index != at) trimmed[target++] = roles[index];
            return trimmed;
        }

        /// Places Hunter by shooting tier inside the pawn's published role
        /// order: tier 1 follows the leading block of non-normal roles, tier 2
        /// additionally the pawn's minimum and champion picks, tiers 3 and 4
        /// follow the first and third full-time normal role (capped at the
        /// first unskilled chore), and tier 5 goes last, ahead of trailing
        /// preserve-order roles. A Hunter pinned in the order template or
        /// pinned on the pawn keeps that slot instead.
        internal int[] PlaceHunter(
            EngineContext facts, int pawnIndex, int[] ordered)
        {
            int at = HunterInsertionIndex(facts, pawnIndex, ordered);
            var placed = new int[ordered.Length + 1];
            for (int index = 0; index < at; index++)
                placed[index] = ordered[index];
            placed[at] = facts.Colony.HunterRoleId;
            for (int index = at; index < ordered.Length; index++)
                placed[index + 1] = ordered[index];
            return placed;
        }

        private int HunterInsertionIndex(
            EngineContext facts,
            int pawnIndex,
            int[] roles)
        {
            int afterLeadingBlock = 0;
            while (afterLeadingBlock < roles.Length
                && !IsNormalRole(facts, pawnIndex, roles[afterLeadingBlock]))
                afterLeadingBlock++;
            if (hunterTier == 1) return afterLeadingBlock;
            if (hunterTier == 2)
            {
                // Past the pawn's minimum/champion picks and any interleaved
                // non-normal roles: the slot is before the first plain normal
                // role, and never past an unskilled chore.
                int at = afterLeadingBlock;
                while (at < roles.Length)
                {
                    RoleView role = facts.RoleOf(roles[at]);
                    if (role != null && role.Unskilled) break;
                    if (IsNormalRole(facts, pawnIndex, roles[at])
                        && !IsMinimumRole(roles[at])
                        && !IsChampionPick(roles[at]))
                        break;
                    at++;
                }
                return at;
            }

            // The last slot still precedes trailing preserve-order roles; the
            // full-time scan for tiers 3 and 4 falls back to it as well.
            int last = roles.Length;
            while (last > 0 && PreservesOrder(facts, roles[last - 1])) last--;
            if (hunterTier == 5) return last;

            int needed = hunterTier == 3 ? 1 : 3;
            int seen = 0;
            for (int index = afterLeadingBlock; index < last; index++)
            {
                RoleView role = facts.RoleOf(roles[index]);
                if (role == null) continue;
                if (role.Unskilled) return index;
                if (role.Category == RoleCategory.Normal
                    && role.Time == RoleTime.FullTime
                    && ++seen == needed)
                    return index + 1;
            }
            return last;
        }

        private static bool IsNormalRole(
            EngineContext facts, int pawnIndex, int roleId)
        {
            RoleView role = facts.RoleOf(roleId);
            return role != null
                && !role.AutoAssign
                && !role.HasRules
                && !role.Blocker
                && role.Category != RoleCategory.Important
                && !facts.HasProtectedDirectAssignment(pawnIndex, roleId);
        }

        private static bool PreservesOrder(EngineContext facts, int roleId)
        {
            RoleView role = facts.RoleOf(roleId);
            return role != null && role.PreserveRecommendationOrder;
        }
    }

    public sealed partial class RecommendationPlan
    {
        private static void AddSpecialRoles(
            EngineContext facts,
            PawnDraft[] drafts)
        {
            for (int roleIndex = 0;
                 roleIndex < facts.Colony.Roles.Count;
                 roleIndex++)
            {
                RoleView role = facts.Colony.Roles[roleIndex];
                if (!role.AutoAssign
                    || role.HasRules
                    || role.Blocker
                    || role.MemberRoleIds != null
                    || !role.Enabled
                    || !role.Available)
                    continue;
                for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
                    if (facts.Capable(pawnIndex, role))
                        drafts[pawnIndex].AddSpecialRole(role.Id);
            }

            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
            {
                PawnView pawn = facts.Colony.Pawns[pawnIndex];
                for (int assignmentIndex = 0;
                     assignmentIndex < pawn.Existing.Count;
                     assignmentIndex++)
                {
                    AssignmentView assignment = pawn.Existing[assignmentIndex];
                    RoleView role = facts.RoleOf(assignment.RoleId);
                    if (role == null || role.MemberRoleIds != null)
                        continue;
                    // Unskilled roles with demand are planned like any other and
                    // demand-less ones drop out; only an explicit pin, rule, or
                    // blocker carries an existing assignment over.
                    if (assignment.Pinned || role.HasRules || role.Blocker)
                        drafts[pawnIndex].AddSpecialRole(role.Id);
                }
            }

        }

        private static void AddLateSpecialRoles(
            EngineContext facts,
            PawnDraft[] drafts,
            RecommendationFormulaEngine formulas)
        {
            AddHunters(facts, drafts, formulas);
            if (facts.RoleOf(facts.Colony.FireBlockerRoleId) == null) return;
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
                if (facts.Colony.Pawns[pawnIndex].FireFear)
                    drafts[pawnIndex].AddFireBlocker(
                        facts.Colony.FireBlockerRoleId);
        }

        private static void AddHunters(
            EngineContext facts,
            PawnDraft[] drafts,
            RecommendationFormulaEngine formulas)
        {
            RoleView hunter = facts.RoleOf(facts.Colony.HunterRoleId);
            if (hunter == null
                || hunter.HasRules
                || hunter.Blocker
                || !hunter.Enabled
                || !hunter.Available)
                return;
            int firstTier = -1;
            int lowest = -1;
            int lowestShooting = int.MaxValue;
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
            {
                PawnView pawn = facts.Colony.Pawns[pawnIndex];
                if (!pawn.HasRangedWeapon || !facts.Capable(pawnIndex, hunter))
                    continue;
                if (!drafts[pawnIndex].ContainsRole(hunter.Id)
                    && PawnHasCoverer(
                        facts, drafts[pawnIndex], pawnIndex, hunter))
                    continue;
                int tier = formulas.HunterTier(pawn.ShootingLevel);
                drafts[pawnIndex].AddHunter(hunter.Id, tier);
                if (tier == 1) firstTier = pawnIndex;
                if (pawn.ShootingLevel < lowestShooting
                    || pawn.ShootingLevel == lowestShooting
                        && pawnIndex < lowest)
                {
                    lowest = pawnIndex;
                    lowestShooting = pawn.ShootingLevel;
                }
            }
            if (firstTier < 0 && lowest >= 0)
                drafts[lowest].PromoteHunterToFirstTier();
        }
    }
}
