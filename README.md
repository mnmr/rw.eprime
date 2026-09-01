# rw.eprime

Monorepo for my [RimWorld](https://rimworldgame.com/) mods and the shared library they build on.

## Mods

| Mod | What it does | Docs |
|---|---|---|
| [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092) | A modern, compact resource readout with display groups, tiers and custom resource pools. | [README](Readouts/README.md) |
| [EPrime's Quality Jobs](https://steamcommunity.com/sharedfiles/filedetails/?id=3776722051) | Manage quality crafting and construction, so your items get the best possible quality. | [README](QualityJobs/README.md) |
| [WorkRoles](https://steamcommunity.com/sharedfiles/filedetails/?id=3760146134) | Replaces the Work tab with role-based work management. | [README](WorkRoles/README.md) |
| [EPrime's Implanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3793988932) | Plan, track and optionally automate the rollouts of bionic implants. | [README](Implanner/README.md) |

Each mod folder is self-contained and follows the same layout:

```
<Mod>/src/<Mod>.Core/         deterministic logic — no game references (netstandard2.0)
<Mod>/src/<Mod>/              game integration: store, patches, commands, UI (net472)
<Mod>/src/<Mod>.Core.Tests/   behavioral tests (TUnit, .NET 10)
<Mod>/mod/                    the shippable mod (About, Textures, Languages, Assemblies)
<Mod>/scripts/deploy.ps1      build output → RimWorld Mods folder
```

## Shared

Code shared across the mods lives in [Shared](Shared/) and is compiled directly into each mod (source inclusion via `$(RimSharedRoot)`, no shared assembly):

- **[Shared/Common](Shared/Common/)** (`RimShared.Common`) — deterministic, game-independent building blocks: caching and snapshot publication, revision/invalidation gates, layout and viewport math, text/count formatting, tooltip policies, lifecycle helpers. No RimWorld, Unity or Harmony references, so it is fully unit-testable.
- **[Shared/UiLib](Shared/UiLib/)** (`RimShared.UiLib`) — game-side UI helpers (tiny-text rendering, segmented controls, pixel-exact boxes). May reference Verse/Unity; compiled only into the game assemblies, never into `*.Core`.
- **[Shared/Tests](Shared/Tests/)** — behavioral tests for `RimShared.Common`.
- **[Shared/tools/automation](Shared/tools/automation/)** — shared commands for automated in-game verification (launch, input, capture, profile refresh) run against a disposable profile. The curated profile baseline is committed under [AutomationProfiles/Shared/Config](AutomationProfiles/Shared/Config/).

## Engineering contract

[AGENTS.md](AGENTS.md) is the repository-wide engineering contract (cached render paths, snapshot/invalidation rules, multiplayer determinism, testing requirements). Each mod adds its own `AGENTS.md` with mod-specific cache dependencies and verification commands.

## Building

Per mod:

```powershell
dotnet build -c Release <Mod>/src/<Solution>.slnx
dotnet test <Mod>/src/<Mod>.Core.Tests
```

Everything at once (build + deploy, stops on first failure):

```powershell
scripts\build-deploy-all.cmd
```

Building never deploys — installing a mod into the game requires its `scripts/deploy.ps1` and a game restart.

## History

The repo was consolidated from the mods' individual repositories with full history preserved; historical commits are prefixed with their origin (`[ER]` Readouts, `[QJ]` Quality Jobs, `[WR]` WorkRoles).

## License

See [LICENSE](LICENSE).
