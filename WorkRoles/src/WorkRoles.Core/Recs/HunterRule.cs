using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 8, hardcoded for now: every capable gun carrier hunts. Shooting
    /// &lt;15 = tier 0 (food before skilled work), &lt;19 = tier 1 (template
    /// slot), else tier 2 (dead last); at least one tier-0 hunter whenever
    /// anyone hunts. OrderingRule consumes the tiers.
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
            var hunters = new List<(int pawn, int level, int passion)>();
            for (int i = 0; i < context.Colony.Pawns.Count; i++)
            {
                var pawn = context.Colony.Pawns[i];
                if (!pawn.HasRangedWeapon || !context.Capable(i, role)) continue;
                // A covering candidate already provides the hunting jobs.
                if (!context.Candidates[i].ContainsKey(role.Id) && context.CoversRole(i, role))
                    continue;
                context.AddCandidate(i, role.Id,
                    new Reason { RuleId = Id, TowardRoleId = -1 }, SignalBucket.Neutral);
                context.HunterTiers[i] = pawn.ShootingLevel < 15 ? 0
                    : pawn.ShootingLevel < 19 ? 1 : 2;
                pawn.PassionScores.TryGetValue("Shooting", out int passion);
                hunters.Add((i, pawn.ShootingLevel, passion));
            }
            if (hunters.Count > 0 && !context.HunterTiers.Any(t => t == 0))
            {
                var best = hunters
                    .OrderByDescending(h => h.level)
                    .ThenByDescending(h => h.passion)
                    .ThenBy(h => h.pawn)
                    .First();
                context.HunterTiers[best.pawn] = 0;
            }
        }
    }
}
