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

        internal void PromoteHunterToTierZero() => hunterTier = 0;

        internal void AddFireBlocker(int roleId)
        {
            AddSpecialRole(roleId);
            fireBlockerRoleId = roleId;
        }

        private void ApplySpecialKeys(EngineContext facts, long[] keys)
        {
            if (hunterTier >= 0
                && facts.Colony.HunterRoleId >= 0
                && !facts.Colony.OrderTemplate.Contains(
                    facts.Colony.HunterRoleId))
            {
                int hunterAt = roleIds.IndexOf(facts.Colony.HunterRoleId);
                if (hunterAt >= 0)
                    keys[hunterAt] = Ordering.HunterPosition(
                        facts.Colony, facts.RolesById, hunterTier);
            }
            if (fireBlockerRoleId >= 0)
            {
                int fireAt = roleIds.IndexOf(fireBlockerRoleId);
                if (fireAt >= 0) keys[fireAt] = long.MinValue;
            }
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
                    || !role.Enabled
                    || !role.Available
                    || role.HolderMode == RoleHolderMode.Never)
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
                    if (role == null || role.HolderMode == RoleHolderMode.Never)
                        continue;
                    bool protectedRole = assignment.Pinned
                        || role.HasRules
                        || role.Blocker;
                    bool retainedChore = role.Unskilled
                        && !role.AutoAssign
                        && role.Enabled
                        && role.Available;
                    if (protectedRole || retainedChore)
                        drafts[pawnIndex].AddSpecialRole(role.Id);
                }
            }

        }

        private static void AddLateSpecialRoles(
            EngineContext facts,
            PawnDraft[] drafts)
        {
            AddHunters(facts, drafts);
            if (facts.RoleOf(facts.Colony.FireBlockerRoleId) == null) return;
            for (int pawnIndex = 0; pawnIndex < drafts.Length; pawnIndex++)
                if (facts.Colony.Pawns[pawnIndex].FireFear)
                    drafts[pawnIndex].AddFireBlocker(
                        facts.Colony.FireBlockerRoleId);
        }

        private static void AddHunters(
            EngineContext facts,
            PawnDraft[] drafts)
        {
            RoleView hunter = facts.RoleOf(facts.Colony.HunterRoleId);
            if (hunter == null
                || hunter.HasRules
                || hunter.Blocker
                || !hunter.Enabled
                || !hunter.Available
                || hunter.HolderMode == RoleHolderMode.Never)
                return;
            int tierZero = -1;
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
                int tier = pawn.ShootingLevel <= 10 ? 0
                    : pawn.ShootingLevel <= 15 ? 1
                    : pawn.ShootingLevel <= 18 ? 2 : 3;
                drafts[pawnIndex].AddHunter(hunter.Id, tier);
                if (tier == 0) tierZero = pawnIndex;
                if (pawn.ShootingLevel < lowestShooting
                    || pawn.ShootingLevel == lowestShooting
                        && pawnIndex < lowest)
                {
                    lowest = pawnIndex;
                    lowestShooting = pawn.ShootingLevel;
                }
            }
            if (tierZero < 0 && lowest >= 0)
                drafts[lowest].PromoteHunterToTierZero();
        }
    }
}
