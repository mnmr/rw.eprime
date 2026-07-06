# WorkRoles

WorkRoles replaces [RimWorld](https://rimworldgame.com/)'s Work tab with role-based work management. No more number grids: define roles as ordered lists of jobs, stack them on your colonists, and let ordering decide who does what first. Whether you run a three-pawn camp or a sprawling colony, roles make work assignments readable, reusable and quick to change.

## Key features

- **Roles, not numbers:** a role is an ordered list of jobs — whole work types like Cooking, or single jobs like "smelt". A colonist's roles combine into one clear work order: earlier roles win, and within a role, earlier jobs win. If no role mentions a job, the colonist never does it.
- **Overlapping roles:** the same job can live in multiple roles, and a colonist can hold both. Keep an "Emergency Harvest" role sharing Farmer's jobs assigned to half the colony at high priority but disabled — when blight or a cold snap hits, one click re-tasks everyone to the fields.
- **One-click situational control:** toggle any role globally (everyone holding it adapts instantly) or per colonist, without touching the underlying assignments.
- **Ready-made setup:** 17 color-coded roles (Doctor, Farmer, Grunt, …) covering every vanilla and DLC work type. Adding the mod to an existing save converts current priorities into matching role sets automatically.
- **Drag & drop everything:** drag roles onto colonists, reorder with a drop marker, move roles between pawns — per-pawn state travels along.
- **Smart recommendations:** a per-colonist panel suggests roles from burning passions, gene aptitudes, colony-best skills and training opportunities — click to assign, or "Make It So" for all of them. Skills, passions and aptitudes are shown alongside.
- **Deep job control:** the role editor's filterable job tree works like the storage filter — check a whole work type or individual jobs, then reorder them freely, including specific jobs above a catch-all.
- **Copy & paste:** transfer complete role sets between colonists, disabled states included.
- **Modded jobs just work:** work types and jobs added by other mods appear automatically — no compatibility patches needed.
- **Multiplayer ready:** full RimWorld Multiplayer support — every role edit and assignment is synced.

## How it works

The engine compiles each pawn's ordered roles into one strict job order and feeds it to the game through the same lists vanilla's job selection already consumes (`Pawn_WorkSettings`), so pawn AI behavior needs no changes. Emergency-flagged jobs (firefighting, urgent tending) go to the game's emergency work pass when any assigned role covers them. Priorities read by other mods are answered coherently; priority *writes* are owned by WorkRoles for managed pawns. The mod continuously mirrors role priorities into vanilla's 0–4 priority map, so uninstalling hands back a sensibly populated vanilla Work tab.

## Compatibility

- Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Safe to add to existing saves (priorities convert to roles) and safe to remove (see above).
- **Incompatible** with other mods that replace the Work tab or continuously write priorities: Work Tab (all forks), Better Work Tab, Enhanced Work Tab, Work Manager.

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
