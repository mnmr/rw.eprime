# WorkRoles

WorkRoles replaces [RimWorld](https://rimworldgame.com/)'s Work tab with role-based work management. No setup required, safe to remove if you don't like it.

Instead of assigning priority numbers, you assign a set of ordered roles to colonists. A role is simply a named set of ordered jobs. Priorities are calculated and set automatically behind the scenes.

Set up a role once, hand it to any number of colonists, and adjust everyone by editing one list.

## Key features

- **No Priorities:** a role is an ordered list of jobs (whole work types like Cooking or single jobs like Smelting). A colonist's roles combine into one clear work order: earlier roles win, and within a role, earlier jobs win. If no role mentions a job, the colonist never does it.
- **No Setup:** seeds pre-made roles (Doctor, Smith, Hauler, …) covering every vanilla and official DLC work type. Auto-assigns roles to match existing priorities.
- **Overlapping Roles:** the same job can live in multiple roles, and a colonist can hold both. Assign "Emergency Harvester" to half the colony at high priority but disabled, and when blight or a cold snap hits, one click re-tasks everyone to the fields.
- **Easy Switching:** toggle any role globally or per colonist, without touching the underlying assignments. No more remembering what priorities colonists used to have.
- **Modded Jobs:** work types and jobs added by other mods appear automatically, no compatibility patches needed.
- **Rules Engine:** configure roles to only be enabled at certain times of day, or only when home or away. Allows colonists to assume extra responsibilities while away on missions.
- **Bill Support:** configure work bills to be restricted to a particular role. Useful when a bill requires multiple skills, e.g. Leaf Roller (crafting + cooking) vs Drug Maker (crafting + intellectual).
- **Combo Roles:** these combine jobs that are often paired at equal priority (e.g. Farmer for Growing and Plant Cutting). Combo roles show as parent of the roles they cover in the role editor.
- **Deep Job Control:** the role editor provides a filterable job tree. Jobs within a role can be freely reordered. All of the seeded roles can be edited or removed.
- **Smart Recommendations:** a per-colonist panel suggests roles from burning passions, gene aptitudes, colony-best skills, training opportunities and colony needs.
- **Fix My Colony:** apply recommendations across all colonists, with a preview to see what would change.
- **Role Pinning:** pin role assignments that you don't want the recommendation engine to touch.
- **Copy & Paste:** transfer complete role sets between colonists.
- **Drag & Drop:** assign or re-order colonist roles by dragging.
- **Multiplayer Support:** fully RimWorld Multiplayer compatible.
- **Translation Ready:** all text resources are read from resource files (get in touch if you want to help translate).
- **Designed for Performance:** everything is pre-computed at assignment and nothing runs per-tick.

## Worth noting

- **Seeded Roles:** an initial role set is seeded into the game, covering all detected work types and jobs.
- **Combo Roles:** the Basics role covers patient, rescue, firefighting and bed rest. Haul urgently from AllowTool is also added to Basics (if installed). Grunt combines hauling and cleaning. Farmer combines planting and harvesting.
- **Extra Roles:** convenience roles that the recommendation engine uses (Fabricator, Medic, Butcher, Brewer).
- **Invisible Jobs:** some mods add hidden jobs with no work-type checkbox; WorkRoles quietly enables these for everyone, as vanilla would.

## How it works

The engine compiles each pawn's ordered roles into one strict job order and feeds it to the game through the same lists vanilla's job selection already consumes (`Pawn_WorkSettings`), so pawn AI behavior needs no changes. Emergency-flagged jobs (firefighting, urgent tending) go to the game's emergency work pass when any assigned role covers them. Other mods that read priorities get the values WorkRoles computed; priority *writes* are owned by WorkRoles for managed pawns. The mod continuously mirrors role priorities into vanilla's 0–4 priority map, so uninstalling hands the vanilla Work tab back with your roles converted to priorities.

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to existing saves (priorities convert to roles) and safe to remove (the vanilla Work tab comes back sensibly populated).
- Tested alongside Better Workbench Management, AllowTool, PUAH+ and many others.
- **Incompatible** with other mods that replace the Work tab (Fluffy's Work Tab forks, Better Work Tab, Enhanced Work Tab, among others).

## Building from source

```
dotnet build WorkRoles.slnx
```

Three projects: `WorkRoles.Core` (pure logic, netstandard2.0, unit-tested), `WorkRoles` (net472 game assembly; game refs via the `Krafs.Rimworld.Ref` NuGet package — no game files needed), and `WorkRoles.Core.Tests` (.NET 10, TUnit). The build deploys the `mod/` folder to your RimWorld `Mods` directory automatically; override the location with `-p:RimWorldMods=<path>`.

Run the tests with:

```
dotnet test --project tests/WorkRoles.Core.Tests/WorkRoles.Core.Tests.csproj
```

## Disclaimer

This was created with the help of Claude Code. I'm a developer, but would likely not have ventured into modding without the assistance.

## License

See [LICENSE](LICENSE).
