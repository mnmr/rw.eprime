namespace WorkRoles.Core
{
    /// How important holding a role is when planning a colony. None means the
    /// role carries no classification and rules must not assume one.
    public enum RoleCategory
    {
        None = 0,
        Important = 1,
        Normal = 2,
        Optional = 3,
    }

    /// How time-consuming a role's work is. None means unclassified.
    public enum RoleTime
    {
        None = 0,
        PartTime = 1,
        FullTime = 2,
        Opportunistic = 3,
    }
}
