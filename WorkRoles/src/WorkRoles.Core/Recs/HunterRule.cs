using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Rule 8, intentionally hardcoded: every capable gun carrier hunts.
    /// Shooting 0-10 = tier 0, 11-15 = tier 1, 16-18 = tier 2, and 19+
    /// = tier 3. When nobody naturally lands in tier 0, the lowest-skilled
    /// hunter is promoted there. OrderingRule consumes the tiers unless the
    /// Hunter role has an explicit recommendation-order position.
    public sealed class HunterRule : RecRule
    {
        public override string Id => "hunter";
        public override RuleKind Kind => RuleKind.Colony;

        public override bool Relevant(EngineContext context)
            => context.Colony.HunterRoleId != -1
            && context.RoleOf(context.Colony.HunterRoleId) != null
            && !context.Vetoed.Contains(context.Colony.HunterRoleId);

        public override void Apply(EngineContext context)
        {
            var role = context.RoleOf(context.Colony.HunterRoleId);
            var hunters = new List<int>();
            for (int i = 0; i < context.Colony.Pawns.Count; i++)
            {
                var pawn = context.Colony.Pawns[i];
                if (!pawn.HasRangedWeapon || !context.Capable(i, role)) continue;
                // A covering candidate already provides the hunting jobs.
                if (!context.Candidates[i].ContainsKey(role.Id) && context.CoversRole(i, role))
                    continue;
                context.AddCandidate(i, role.Id,
                    new Reason { RuleId = Id, TowardRoleId = -1 }, SignalBucket.Neutral);
                context.HunterTiers[i] = pawn.ShootingLevel <= 10 ? 0
                    : pawn.ShootingLevel <= 15 ? 1
                    : pawn.ShootingLevel <= 18 ? 2 : 3;
                hunters.Add(i);
            }
            HunterTiering.EnsureTierZero(context, hunters);
        }
    }
}
