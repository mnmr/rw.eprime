using Verse;

namespace Implanner
{
    /// Whether Implanner's assistant half may run at all.
    ///
    /// Level mods spread one colony across several maps (Strata, MultiFloors,
    /// As above So below) or across bands of a single map (As above So below
    /// II). Implanner automation cannot deliver across a level boundary and
    /// no level mod closes the gap for it: a medical bill's ingredient search
    /// never leaves the patient's map, and none of the three covers
    /// `Bill_Medical` — Strata's shortfall hauling only walks colonist
    /// buildings that are `IBillGiver`, MultiFloors additionally demands
    /// `Bill_Production` (a sibling of `Bill_Medical`, not a base), and As
    /// above So below II's only `WorkGiver_DoBill` patch just records timings.
    ///
    /// Scheduling operations that can silently never complete is worse than a
    /// clear boundary, so with any of them active Implanner runs as a
    /// planning tool only: plans, assignments, priorities, rankings, progress
    /// and blockers all keep working, while item reservations, operation
    /// scheduling and the automatic doctor floor stand down.
    ///
    /// Cache contract:
    ///   Owner: process.
    ///   Key: none — a single value.
    ///   Value: immutable availability flag plus the name of the mod that
    ///     disabled it.
    ///   Dependencies: the active mod list, which cannot change without
    ///     restarting the game.
    ///   Refresh policy: resolved once, on first read.
    ///   Equality policy: constant for the session.
    ///   Teardown: none — no world, map or game state is retained.
    /// Every multiplayer client resolves the identical value from its own
    /// identical mod list, so the gate cannot desynchronize a tick.
    internal static class PlannerAutomation
    {
        /// As above So below (the first one) is discontinued but shares the
        /// separate-map design, so automation breaks there identically.
        /// Standing down is not the same as supporting it.
        private static readonly string[] LevelModPackageIds =
        {
            "AzraelGodKing.Strata",
            "telardo.MultiFloors",
            "astryl.AsAboveSoBelow",
            "astryl.AsAboveSoBelow2",
        };

        private static bool resolved;
        private static bool available = true;
        private static string blockedBy = "";

        internal static bool Available
        {
            get
            {
                Resolve();
                return available;
            }
        }

        /// The name of the level mod that stood automation down; empty while
        /// automation is available.
        internal static string BlockedBy
        {
            get
            {
                Resolve();
                return blockedBy;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            for (int i = 0; i < LevelModPackageIds.Length; i++)
            {
                // ignorePostfix so a locally copied or Steam-suffixed install
                // ("...\_copy", "..._steam") still matches.
                var mod = ModLister.GetActiveModWithIdentifier(
                    LevelModPackageIds[i], ignorePostfix: true);
                if (mod == null) continue;
                available = false;
                blockedBy = mod.Name;
                Log.Message("[Implanner] " + mod.Name + " is active; Implanner "
                    + "automation stands down and the mod runs as a planning "
                    + "tool only.");
                return;
            }
        }
    }
}
