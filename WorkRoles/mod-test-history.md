# Mod test history

Verification runs of WorkRoles against third-party mods, newest first, at most
one row per mod per day (see the root `AGENTS.md`, "Mod test history").
`Version` is the mod's `modVersion`, or its Workshop id and newest file date
when About.xml has none. `Tested` points at the evidence under `temp/`.

| Date | Mod | Version | WorkRoles | Tested | Outcome |
|---|---|---|---|---|---|
| 2026-11-02 | Milira Race | Workshop 3256974620, files 2026-11-02 | 1.8.0 | Milian Pawn I mechanoid as a mechanical worker: roster, native givers, recommendations, chips, Milian work tab writes, both Milian tabs, a consumed Cleaner role (`tests/InGame/MilianMechs`, `temp/milian-mechs`) | Passed: Milians are plain Biotech mechs to WorkRoles; the Ancot work tab behaves like Mech Work Tab (write lands with support off, blocked with the notice for a managed Milian); the Milian cleaned filth through its role. |
| 2026-11-02 | MiscRobots WorkTab support | Workshop 2271338001, files 2026-11-02 | 1.7.9 | Misc. Robots++ Crafter/Omni research and every model's firefighting as WorkRoles capability, recommendation, role chip and consumed job (`tests/InGame/MiscRobotsWorkTab`, `temp/misc-robots-worktab`) | Before the fix: Researcher/Firefighter chips Critical and roles never executed. After: no chip warning, Researcher and Firefighter recommended, bots researched and extinguished fires through their roles. |
| 2026-11-02 | Misc. Robots++ | Workshop 3253308913, files 2026-10-28 | 1.7.9 | Same run as MiscRobots WorkTab support (Crafter III, Omni III, Builder I) | Passed with the fix; without WorkTab support the native rule (`robotWorkTypes` priority above 0) is unchanged. |
| 2026-11-02 | Misc. Robots | 1.7.0 | 1.7.9 | Same run as MiscRobots WorkTab support (Hauler II, Cleaner) | Passed with the fix: both fight fires through the Firefighter role under WorkTab support. |
| 2026-11-02 | WVC - Work Modes | 1.1.8 | 1.7.9 | Mech work-mode mix with Mech Work Tab and Smart Work Mode (`tests/InGame/MechModMix`, `temp/mech-mod-mix`) | Passed: WVC work/recharge/escort-work modes count as working, combat/escort/recharge modes do not; its priority writes land while mech support is off and are blocked with the one-time notice for managed mechs. |
| 2026-11-02 | Mech Work Tab | Workshop 3268299107, files 2026-11-01 | 1.7.9 | Mech work-mode mix (`tests/InGame/MechModMix`, `temp/mech-mod-mix`) | Passed: its mechs work tab opens beside WorkRoles; priority writes land while mech support is off and are blocked with the one-time notice for managed mechs. |
| 2026-11-02 | Smart Work Mode | 1.0.4 | 1.7.9 | SmartWork mode as a working mode for WorkRoles and Quality Jobs (`tests/InGame/SmartWorkMode`, `temp/smart-work-mode`) | Passed: SmartWork mechs count as workers and available finishers; Self-shutdown drops them. |
| 2026-11-02 | EndlessGrowth | Workshop 2894401734, files 2026-11-02 | 1.7.9 | Skills past 20 in facts, plans, roster and selected panel (`tests/InGame/EndlessGrowth`, `temp/endless-growth`) | Passed: Crafting 60 and Plants 100 flow through facts, plans and the Colonists tab; the open top band covers them. |
