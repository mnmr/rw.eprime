namespace WorkRoles.Core.Recs
{
    /// Rule 1: rule-carrying, blocker, Never, research-locked and disabled
    /// roles are vetoed — no later rule may recommend them.
    public sealed class ExclusionsRule : RecRule
    {
        public override string Id => "exclusions";
        public override RuleKind Kind => RuleKind.Colony;

        public override void Apply(EngineContext context)
        {
            foreach (var role in context.Colony.Roles)
                if (role.HasRules || role.Blocker || !role.Enabled || !role.Available
                    || role.MinHolders == RoleView.NeverHolders)
                    context.Vetoed.Add(role.Id);
        }
    }
}
