using Verse;

namespace Implanner
{
    /// Which level mod, if any, is active, so Implanner can warn the player.
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
    /// Automation stays available anyway (owner, 2026-11-02): players who
    /// keep implants and patients on one level get full automation, and the
    /// Automation tab and welcome notice warn the others. Before this the
    /// gate stood automation down entirely.
    ///
    /// Cache contract:
    ///   Owner: process.
    ///   Key: none — a single value.
    ///   Value: immutable name of the active level mod, empty when none.
    ///   Dependencies: the active mod list, which cannot change without
    ///     restarting the game.
    ///   Refresh policy: resolved once, on first read.
    ///   Equality policy: constant for the session.
    ///   Teardown: none — no world, map or game state is retained.
    internal static class PlannerAutomation
    {
        /// As above So below (the first one) is discontinued but shares the
        /// separate-map design, so automation is limited there identically.
        private static readonly string[] LevelModPackageIds =
        {
            "AzraelGodKing.Strata",
            "telardo.MultiFloors",
            "astryl.AsAboveSoBelow",
            "astryl.AsAboveSoBelow2",
        };

        // The welcome notice reads this same catalog without exposing its
        // backing array.
        internal static int LevelModCount => LevelModPackageIds.Length;
        internal static string LevelModPackageIdAt(int index) => LevelModPackageIds[index];

        private static bool resolved;
        private static string levelMod = "";

        /// The name of the active level mod; empty while none is active.
        internal static string LevelMod
        {
            get
            {
                Resolve();
                return levelMod;
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
                levelMod = mod.Name;
                Log.Message("[Implanner] " + mod.Name + " is active; Implanner "
                    + "automation cannot carry implants or operations across "
                    + "level boundaries.");
                return;
            }
        }
    }
}
