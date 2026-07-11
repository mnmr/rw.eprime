using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorkRoles
{
    public class WorkRolesGameComponent : GameComponent
    {
        // Last observed local hour per map (map.uniqueID) and per player caravan
        // (WorldObject.ID). Non-scribed: after load the compiled cache is fully
        // invalidated anyway, so first observation just records without invalidating.
        private readonly Dictionary<int, int> lastMapHour = new Dictionary<int, int>();
        private readonly Dictionary<int, int> lastCaravanHour = new Dictionary<int, int>();

        public WorkRolesGameComponent(Game game) { }

        // Runs after both "new game started" and "save loaded".
        public override void FinalizeInit()
        {
            Seeding.SeedIfNeeded();
            Seeding.RefreshWorkTypeSnapshots();
            var generated = Seeding.EnsureWorkTypeCoverage();
            if (generated.Count > 0)
                Messages.Message("WR_NewWorkDetected".Translate(generated.ToCommaList()),
                    MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            var store = RoleStore.Current;
            if (store == null) return;

            // Cheap linear scan: only bother tracking hours when a time rule exists.
            bool anyTimeRule = false;
            foreach (var role in store.roles)
                if (role.activeHours != Role.AllHours) { anyTimeRule = true; break; }
            if (!anyTimeRule) return;

            bool hourChanged = false;
            foreach (var map in Find.Maps)
            {
                int hour = GenLocalDate.HourInteger(map);
                if (lastMapHour.TryGetValue(map.uniqueID, out var prev) && prev != hour)
                    hourChanged = true;
                lastMapHour[map.uniqueID] = hour;
            }
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (!caravan.IsPlayerControlled) continue;
                int hour = GenLocalDate.HourInteger(caravan.Tile);
                if (lastCaravanHour.TryGetValue(caravan.ID, out var prev) && prev != hour)
                    hourChanged = true;
                lastCaravanHour[caravan.ID] = hour;
            }

            if (hourChanged)
                CompiledJobOrders.InvalidateAllTimeRuled();
        }
    }
}
