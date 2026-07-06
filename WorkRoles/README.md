# WorkRoles

WorkRoles replaces [RimWorld](https://rimworldgame.com/)'s Work tab with role-based work management.

Instead of assigning priority numbers, you assign a set of ordered roles to colonists. A role is simply a named set of ordered jobs. Priorities are calculated and set automatically behind the scenes.

The system makes it easy to manage both small and large colonies. Roles make work assignments readable, reusable and quick to change.

## Key features

- **Roles, not numbers:** a role is an ordered list of jobs (whole work types like Cooking or single jobs like Smelting). A colonist's roles combine into one clear work order: earlier roles win, and within a role, earlier jobs win. If no role mentions a job, the colonist never does it.
- **Overlapping roles:** the same job can live in multiple roles, and a colonist can hold both. Assign "Emergency Harvester" to half the colony at high priority but disabled, and when blight or a cold snap hits, one click re-tasks everyone to the fields.
- **One-click situational control:** toggle any role globally (everyone holding it adapts instantly) or per colonist, without touching the underlying assignments.
- **Ready-made setup:** seeds pre-made roles (Doctor, Farmer, Grunt, …) covering every vanilla and DLC work type. Auto-assigns roles to match existing priorities.
- **Drag & drop everything:** assign or re-order colonist roles by dragging.
- **Smart recommendations:** a per-colonist panel suggests roles from burning passions, gene aptitudes, colony-best skills and training opportunities.
- **Deep job control:** the role editor provides a filterable job tree. Once added to a role, jobs can be freely reordered.
- **Copy & paste:** transfer complete role sets between colonists, disabled states included.
- **Modded jobs just work:** work types and jobs added by other mods appear automatically, no compatibility patches needed.
- **Multiplayer ready:** full RimWorld Multiplayer support.
- **Translation ready:** all text resources are read from resource files (get in touch if you want to help).

## Worth noting

- **Seeded roles:** an initial role set is seeded into the game, covering all detected work types and jobs. The Basics role conflates vanilla jobs that all colonists should have. Grunt combines hauling and cleaning. Farmer combines planting and harvesting.

## How it works

The engine compiles each pawn's ordered roles into one strict job order and feeds it to the game through the same lists vanilla's job selection already consumes (`Pawn_WorkSettings`), so pawn AI behavior needs no changes. Emergency-flagged jobs (firefighting, urgent tending) go to the game's emergency work pass when any assigned role covers them. Priorities read by other mods are answered coherently; priority *writes* are owned by WorkRoles for managed pawns. The mod continuously mirrors role priorities into vanilla's 0–4 priority map, so uninstalling hands back a sensibly populated vanilla Work tab.

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to existing saves (priorities convert to roles) and safe to remove (the vanilla Work tab comes back sensibly populated).
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

## License

See [LICENSE](LICENSE).
