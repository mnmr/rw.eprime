# Implanner

A RimWorld mod for planning, ranking, and automating colonist implants:
define implant plans, assign them to colonists, and let the mod reserve
implant items and schedule the operations.

## Repository layout

- `src/Implanner.Core` — deterministic, game-independent logic (netstandard2.0). Compiles the shared `RimShared.Common` sources from `..\Shared\Common`.
- `src/Implanner` — game integration: patches, persistence, rendering, UI (net472).
- `src/Implanner.Core.Tests` — behavioral and regression tests (net10.0, TUnit).
- `mod/` — the shipped mod folder (About, Languages, Textures; assemblies are build output).
- `scripts/` — build, test, deploy, and Workshop publish scripts.

## Building and testing

```powershell
dotnet build -c Release src/Implanner.slnx --no-restore
dotnet build -c Release src/Implanner/Implanner.csproj -p:RimWorldVersion=1.7
dotnet test src/Implanner.Core.Tests --no-restore
```

The default build produces the 1.6 assemblies. The second command produces the
net10.0 1.7 assemblies against the installed game and Harmony 3. Build both
versions before packaging. Override `InstalledGameRoot` and
`InstalledHarmonyPath` when their locations differ from the shared defaults.

Building never deploys. Automated verification uses independent managed runs
through `Shared/tools/automation`; `pwsh scripts/deploy.ps1` updates the player
installation and requires a game restart.

## Engineering rules

See [AGENTS.md](AGENTS.md) and the general contract in `..\AGENTS.md`.

## Check out my other mods

- [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092): a modern, compact resource readout with support for custom resource pools.
- [EPrime's Quality Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=3776722051): manage quality crafting and construction, so your items get the best possible quality.
- [WorkRoles](https://steamcommunity.com/sharedfiles/filedetails/?id=3760146134): easily and intuitively manage work priorities by assigning named roles to colonists.
