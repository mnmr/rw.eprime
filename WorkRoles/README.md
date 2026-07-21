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
- **Bill Support:** configure work bills to be restricted to a particular role. Useful when a bill requires multiple skills, e.g. Leaf Roller (crafting + cooking) vs Drug Maker (crafting + intellectual).
- **Combo Roles:** these combine jobs that are often paired at equal priority (e.g. Farmer for Growing and Plant Cutting). Combo roles show as parent of the roles they cover in the role list.
- **Deep Job Control:** the role editor provides a filterable job tree. Jobs within a role can be freely reordered. All of the seeded roles can be edited or removed.
- **Role Groups:** organize the role list into named, collapsible groups (drag to move or reorder). Roles with rules gather under Auto-Roles automatically.
- **Smart Recommendations:** a per-colonist panel suggests roles from burning passions, gene aptitudes, colony-best skills, training opportunities and colony needs. See below for details.
- **Powerful UI:** grouping, sorting, filtering, drag & drop, key bindings, tooltips, customizable role chip display — whatever you need and it should be there.
- **Import/Export:** easily share or backup your role setup. Import allows overwrite or merge.
- **Multiplayer Support:** fully RimWorld Multiplayer compatible.
- **Translation Ready:** all text resources are read from resource files.
- **Rules Engine:** configure roles to only be enabled at certain times of day or at specific locations (settlements, the gravship, caravans).

## Performance

- **Designed for Performance:** everything is pre-computed at assignment.
- **Cache Invalidation:** if you use auto-roles, priorities get invalidated and recomputed every game hour (every 2500 ticks; not measurable).

## Worth noting

- **Seeded Roles:** an initial role set is seeded into the game, covering all detected work types and jobs.
- **Combo Roles:** various roles (Basics, Farmer, Grunt) that combine multiple work types to make work assignment easier.
- **Blocker Roles:** used to specify things that pawns will never do (for jobs in any subsequent role assignment), e.g. so you can put Pyrophobe before Core (which has Firefighting).
- **Extra Roles:** training roles that enable role progression and convenience roles to make prioritization easier.
- **Modded Jobs:** jobs added by mods are unassigned by default. The role editor shows a warning when it detects a job that isn't in any role. AllowTool jobs are automatically assigned (Haul urgently to Basics, Finish off to Herder/Hunter).

## How it works

Each colonist's ordered roles compile into one strict job order: earlier (enabled) roles win, and within a role, earlier jobs win; a job that appears in several of a colonist's roles keeps its earliest position. The compiled order is fed to the game through the same lists vanilla's job selection already consumes (`Pawn_WorkSettings`), so pawn AI behaves exactly as it would with a hand-tuned priority grid and needs no changes.

Everything is computed when assignments or roles change, then cached — no per-tick patches. Auto roles (time or location rules) additionally recompute exactly at hour boundaries and whenever a colonist changes location.

Emergency-flagged jobs (firefighting, urgent tending) go to the game's emergency work pass when any assigned role covers them.

Other mods that read priorities get the values WorkRoles computed; priority *writes* are owned by WorkRoles for managed pawns — which is why priority-setting mods don't mix. The mod continuously mirrors role priorities into vanilla's 0–4 priority map, so uninstalling hands the vanilla Work tab back with your roles converted to priorities.

In multiplayer, every change (role edits, assignments, toggles) is a synced command, so all players stay in agreement.

## Recommendation logic

- **Colony Needs:** ensures that every essential role is assigned to at least one pawn. Roles are configured to be essential by specifying a min assignment requirement.
- **Intelligent Defaults:** WorkRoles seeds roles with reasonable defaults (but you can override these to set your own requirements).
- **Training Paths:** design your own role progressions or use the default set to flexibly configure how pawns should progress as their skill improves.
- **Shiny Happy People:** training paths are suggested based on colonist genes, traits, passions and expertise, to make sure pawns primarily do what they like or are good at.
- **Hunting:** recommended for everyone with a gun as a way to improve shooting skill, but at different priority based on current skill level.
- **Apply Per Colonist:** click single roles in the "Recommended Roles" panel to cherry-pick or "Make It So" to apply the displayed role set to the selected colonist.
- **Fix My Colony:** apply recommendations across all colonists, with a preview to see what would change.
- **Role Pinning:** pin role assignments that you don't want the recommendation engine to touch. Right-click on assigned roles; pinned roles show a pin icon.
- **Untouched:** auto (rule-carrying) roles, pinned and blockers are never suggested, moved or removed.
- **Genetics:** pawns terrified of fire automatically get Pyrophobe (blocker role) placed before Core (which has Firefighting).

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to existing saves (priorities convert to roles) and safe to remove (the vanilla Work tab comes back sensibly populated).
- Integrates with Vanilla Skills Expanded and Alpha Skills when installed: expertise and passions are recommendation signals.
- Integrates with Colony Groups when installed: pawn groups appear as a grouping option for the colonist table.
- Tested alongside Better Workbench Management, AllowTool, PUAH+, Common Sense and many, many others.
- Multiplayer compatible.

## Known incompatible

- **Work Tab Mods:** not compatible with other mods that replace the Work tab (Fluffy's Work Tab forks, Better Work Tab, Enhanced Work Tab, among others).
- **Priority-Setting Mods:** anything that uses SetPriority to control job priorities will not work with this mod (e.g. Free Will, PriorityMaster).
- If you've tested and found WorkRoles to be incompatible with another mod, let me know so I can add it here.

## Specific mods

These are mods I've checked for compatibility:

- **Colony Groups:** works, and its groups show up in the colonist table's Group dropdown — but avoid its group work-priority presets (they set priorities).
- **Complex Jobs:** moves jobs to new work types — should work fine.
- **Keep Building:** auto-creates work bills — should work fine (no public source, so cannot verify that it doesn't also try to set priorities).

## Building from source

```
dotnet build WorkRoles.slnx
```

Three projects: `WorkRoles.Core` (pure logic, netstandard2.0, unit-tested), `WorkRoles` (net472 game assembly; game refs via the `Krafs.Rimworld.Ref` NuGet package — no game files needed), and `WorkRoles.Core.Tests` (.NET 10, TUnit).

Building never deploys the mod. After building, mirror the current `mod/` folder to your local RimWorld installation with:

```
pwsh scripts/deploy.ps1
```

Override the Mods directory with `pwsh scripts/deploy.ps1 -RimWorldMods <path>`. The script removes stale deployed files and PDBs while preserving Steam's `PublishedFileId.txt`.

Run the tests with:

```
dotnet test --project tests/WorkRoles.Core.Tests/WorkRoles.Core.Tests.csproj
```

## Disclaimer

This was created with the help of Claude Code. Since this is controversial (and more than I'd have thought), let me just say that I've worked as a C# developer since it was released (yes, that old). 
I do backend/web development, and it was pretty invaluable to get help with RimWorld UI widgets and not having to hand-roll System.Drawing calls (if you don't know what that is, just think of something painful). 
The mod is free and was created in my spare time, and without AI it likely wouldn't exist.


## License

See [LICENSE](LICENSE).
