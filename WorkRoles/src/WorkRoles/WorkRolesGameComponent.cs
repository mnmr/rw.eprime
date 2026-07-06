using Verse;

namespace WorkRoles
{
    public class WorkRolesGameComponent : GameComponent
    {
        public WorkRolesGameComponent(Game game) { }

        // Runs after both "new game started" and "save loaded".
        public override void FinalizeInit()
        {
            Seeding.SeedIfNeeded();
        }
    }
}
