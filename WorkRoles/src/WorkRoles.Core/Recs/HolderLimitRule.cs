using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Enforces exact-role maximums after need, training, hunting and retention
    /// have all contributed candidates. Covering roles do not consume another
    /// role's maximum. Pinned assignments consume the cap but are never removed.
    public sealed class HolderLimitRule : RecRule
    {
        private readonly ITrainingDemandPolicy demandPolicy;

        public HolderLimitRule(ITrainingDemandPolicy demandPolicy)
        {
            this.demandPolicy = demandPolicy;
        }

        public override string Id => "holder-limit";
        public override RuleKind Kind => RuleKind.Colony;

        public override void Apply(EngineContext context)
        {
            foreach (var role in context.Colony.Roles)
            {
                if (role.AutoAssign || role.HasRules || role.Blocker) continue;
                int inbound = context.InboundTraining.TryGetValue(role.Id, out int count)
                    ? count : 0;
                int maximum = demandPolicy.Maximum(role.MaxHolders, inbound);
                context.EffectiveMaxHolders[role.Id] = maximum;
                if (maximum >= RoleHolderRange.Uncapped) continue;

                int available = System.Math.Max(0,
                    maximum - context.ProtectedDirectHoldersOf(role.Id));
                var direct = Enumerable.Range(0, context.Colony.Pawns.Count)
                    .Where(pawn => context.Candidates[pawn].ContainsKey(role.Id)
                        && !context.Colony.Pawns[pawn].Existing.Any(a =>
                            a.RoleId == role.Id && a.Pinned))
                    .Select(pawn => new
                    {
                        Pawn = pawn,
                        Candidate = context.Candidates[pawn][role.Id],
                        Readiness = context.RequiredSkills(role)
                            .Select(skill => context.SkillLevel(pawn, skill.SkillDefName))
                            .DefaultIfEmpty(0).Min(),
                        Existing = context.Colony.Pawns[pawn].Existing
                            .Any(a => a.RoleId == role.Id),
                    })
                    .OrderByDescending(item => item.Candidate.Strength)
                    .ThenByDescending(item => item.Readiness)
                    .ThenByDescending(item => item.Existing)
                    .ThenBy(item => item.Pawn)
                    .ToList();

                foreach (var item in direct.Skip(available))
                {
                    context.RemoveCandidate(item.Pawn, role.Id);
                    context.HolderLimitRejected[item.Pawn].Add(role.Id);
                    if (role.Hunting) context.HunterTiers[item.Pawn] = -1;
                }
                if (role.Hunting)
                    HunterTiering.EnsureTierZero(context,
                        Enumerable.Range(0, context.Colony.Pawns.Count)
                            .Where(pawn => context.Candidates[pawn].ContainsKey(role.Id)));
            }
        }
    }
}
