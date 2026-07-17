using Verse;

namespace WorkRoles
{
    /// A player-defined organizational group in the role list. Purely
    /// presentational: never consulted by recommendations, plans or compiled
    /// orders. Id 0 is reserved for the auto-created "Default" group (fixed
    /// label, recreated on demand); like any user group it is swept when empty.
    /// The Auto-Roles section is NOT a group — its membership is derived
    /// (HasRules) and a member's stored group is remembered.
    public class RoleGroup : IExposable
    {
        public const int DefaultId = 0;

        public int id;
        public string label;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
        }
    }
}
