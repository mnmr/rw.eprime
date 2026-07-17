using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Rule 4: path members must sit inside a containing band (measured on
    /// the role's primary skill; colony-best escape opens unreachable mins).
    /// The interval test lives in EngineContext.PassesBands so the colony
    /// rules (draft, allowance) apply the same gate.
    public sealed class BandGatingRule : RecRule
    {
        public override string Id => "bands";
        public override RuleKind Kind => RuleKind.PerPawn;
        public override bool Relevant(EngineContext context) => context.Colony.Paths.Count > 0;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var drop = new List<int>();
            foreach (var candidate in context.Candidates[pawnIndex].Values)
                if (context.RoleOf(candidate.RoleId) is RoleView role
                    && !context.PassesBands(pawnIndex, role))
                    drop.Add(role.Id);
            foreach (int id in drop)
                context.RemoveCandidate(pawnIndex, id);
        }
    }
}
