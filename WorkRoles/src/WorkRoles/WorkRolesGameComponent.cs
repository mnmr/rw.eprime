using RimWorld;
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
            var generated = Seeding.EnsureWorkTypeCoverage();
            if (generated.Count > 0)
                Messages.Message("WR_NewWorkDetected".Translate(generated.ToCommaList()),
                    MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
