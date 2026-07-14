using Verse;

namespace WorkRoles
{
    /// A seeded role-list group: seeding creates the store's groups from these
    /// (lowest order first) before any role lands, so the group display order
    /// is authored rather than first-mention. Roles join via RoleDef.group
    /// matching the label.
    public class RoleGroupDef : Def
    {
        public int order;
    }
}
