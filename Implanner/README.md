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
dotnet test src/Implanner.Core.Tests --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.

## Engineering rules

See [AGENTS.md](AGENTS.md) and the general contract in `..\AGENTS.md`.
