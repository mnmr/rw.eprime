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
- **Designed for Performance:** everything is pre-computed at assignment and nothing runs per-tick. If you use auto-roles, priorities do get invalidated and recomputed every game hour (shouldn't be measurable).

## Worth noting

- **Seeded Roles:** an initial role set is seeded into the game, covering all detected work types and jobs.
- **Combo Roles:** the Basics role covers patient, rescue, firefighting and bed rest. Haul urgently from AllowTool is also added to Basics (if installed). Grunt combines hauling and cleaning. Farmer combines planting and harvesting.
- **Extra Roles:** convenience roles that the recommendation engine uses (Fabricator, Medic, Butcher, Brewer).
- **Blocker Roles:** used to specify things that pawns will never do (for jobs in any subsequent role assignment), e.g. so you can put "No firefighting" before Basics (which has Firefighting).
- **Invisible Jobs:** some mods add hidden jobs with no work-type checkbox; WorkRoles adds these to the "Odd Jobs" role (enabled by default from seeding, but removable and reorderable).

## How it works

Each colonist's ordered roles compile into one strict job order: earlier roles win, and within a role, earlier jobs win; a job that appears in several of a colonist's roles keeps its earliest position. The compiled order is fed to the game through the same lists vanilla's job selection already consumes (`Pawn_WorkSettings`), so pawn AI behaves exactly as it would with a hand-tuned priority grid and needs no changes.

Everything is computed when assignments or roles change, then cached — no per-tick patches. Auto roles (time or location rules) additionally recompute once per in-game hour, which is when their rules can change state.

Emergency-flagged jobs (firefighting, urgent tending) go to the game's emergency work pass when any assigned role covers them.

Other mods that read priorities get the values WorkRoles computed; priority *writes* are owned by WorkRoles for managed pawns — which is why priority-setting mods don't mix. The mod continuously mirrors role priorities into vanilla's 0–4 priority map, so uninstalling hands the vanilla Work tab back with your roles converted to priorities.

In multiplayer, every change (role edits, assignments, toggles) is a synced command, so all players stay in agreement.

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to existing saves (priorities convert to roles) and safe to remove (the vanilla Work tab comes back sensibly populated).
- Integrates with Vanilla Skills Expanded and Alpha Skills when installed (not requirements).
- Tested alongside Better Workbench Management, AllowTool, PUAH+, Common Sense and many others.
- Multiplayer compatible.

## Known incompatible

- **Work Tab Mods:** not compatible with other mods that replace the Work tab (Fluffy's Work Tab forks, Better Work Tab, Enhanced Work Tab, among others).
- **Priority-Setting Mods:** anything that uses SetPriority to control job priorities will not work with this mod (e.g. Free Will).
- Colony Groups works, except for the group work-priority presets (which sets priorities).

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

This was created with the help of Claude Code. Since this is controversial (and more than I'd have thought), let me just say that I've worked as C# developer since it was released (yes, that old). 
I do backend/web development, and it was pretty invaluable to get help with RimWorld UI widgets and not having to hand-roll System.Drawing calls (if you don't know what that is, just think of something painful). 
The mod is free and was created in my spare time, and without AI it likely wouldn't exist.


## License

See [LICENSE](LICENSE).
