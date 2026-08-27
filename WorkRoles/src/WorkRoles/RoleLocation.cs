namespace WorkRoles
{
    /// Legacy save format only: pre-1.1 roles scribed a single Home/Away value,
    /// migrated to LocationRules tokens on load (Role.ExposeData).
    public enum RoleLocation
    {
        Any,
        HomeOnly,
        AwayOnly
    }
}
