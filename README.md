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
<Mod>/src/<Mod>/              game integration: net472 for 1.6, net10.0 for 1.7
<Mod>/src/<Mod>.Core.Tests/   behavioral tests (TUnit, .NET 10)
<Mod>/mod/                    the shippable mod (About, Textures, Languages, Help, versioned Assemblies)
<Mod>/scripts/deploy.ps1      build output → RimWorld Mods folder
```

## Shared

Code shared across the mods lives in [Shared](Shared/) and is compiled directly into each mod (source inclusion via `$(RimSharedRoot)`, no shared assembly):

- **[Shared/Common](Shared/Common/)** (`RimShared.Common`) — deterministic, game-independent building blocks: caching and snapshot publication, revision/invalidation gates, layout and viewport math, text/count formatting, tooltip policies, lifecycle helpers. No RimWorld, Unity or Harmony references, so it is fully unit-testable.
- **[Shared/UiLib](Shared/UiLib/)** (`RimShared.UiLib`) — game-side UI helpers (tiny-text rendering, segmented controls, pixel-exact boxes). May reference Verse/Unity; compiled only into the game assemblies, never into `*.Core`.
- **[Shared/GameLib](Shared/GameLib/)** (`RimShared.GameLib`) — game-side mechanics shared by some mods, opted into one subfolder at a time because a subfolder may carry Harmony patches. [Locations](Shared/GameLib/Locations/) (floor-map canonicalization, map classification, the player-faction binding and the location-transition patches) is compiled into Implanner and WorkRoles only; each mod supplies its side of the partial hooks.
- **[Shared/Tests](Shared/Tests/)** — behavioral tests for `RimShared.Common` (`RimShared.Common.Tests`), test-only helpers in `Shared/Tests/Support`, and isolated in-game regression probes. These are separate from the shipped mods.
- **[Shared/tools/automation](Shared/tools/automation/)** — shared commands for automated in-game verification (run creation, launch, input, capture and cleanup). Each run owns an independent disposable profile and copied mod assemblies. The curated profile baseline is committed under [AutomationProfiles/Shared/Config](AutomationProfiles/Shared/Config/).
- **[Shared/tools/localization](Shared/tools/localization/)** — translation checks and incremental updates through Codex CLI, with English-source hashes retained in [Shared/Translations/Manifests](Shared/Translations/Manifests/).

## Localization and welcome dialogs

All four mods follow RimWorld's selected language. Bundled translations cover
Danish, German, French, Spanish, Italian, Brazilian Portuguese, Dutch, Swedish,
Finnish, Norwegian Bokmål, European Portuguese, Latin American Spanish,
Simplified Chinese and Traditional Chinese, including interface text,
definition labels and help pages.

Installed community translations take precedence regardless of mod load order.
Partial packs retain bundled translations for missing content, with English as
the final fallback. Player-authored names stay verbatim; names recorded as
unchanged seeded defaults can follow the display language without changing
their saved text. Legacy group and pool names without seed provenance remain
as saved.

Welcome dialogs can be dragged and are automatically tiled without overlap.
When the available screen cannot fit every dialog, the remaining dialogs wait
until space is available.

Before publishing, run the localization helpers (Python 3.10+ on `PATH`):

```powershell
scripts\check-localization.cmd
scripts\update-localization.cmd --dry-run
scripts\update-localization.cmd
scripts\check-localization.cmd
```

The checker runs offline and returns a failing exit code for missing, broken or
stale translations. The updater requires a signed-in Codex CLI, translates only
affected entries, removes retired content, and advances source hashes after all
selected locales validate. Review its Git diff before publishing. Add repeatable
`--locale Danish` / `--locale German` arguments to limit either helper to those
languages; `--dry-run` previews updates without using Codex.

Verification on 2026-10-25 covered all fourteen locales in RimWorld 1.7,
community overrides in both load orders, custom-name save/reload across a
language change, and welcome layout, overflow and lifecycle transitions.
Both 1.6 and 1.7 builds and the shared/Core regression suites passed. Runtime
coverage used 1.7; rendering checks do not constitute a comprehensive human
linguistic review. See the [translation maintenance guide](Shared/tools/localization/README.md)
and [in-game verification notes](Shared/Tests/InGame/Localization/README.md)
for commands, evidence locations and known fixture limitations.

## Engineering contract

[AGENTS.md](AGENTS.md) is the repository-wide engineering contract (cached render paths, snapshot/invalidation rules, multiplayer determinism, testing requirements). Each mod adds its own `AGENTS.md` with mod-specific cache dependencies and verification commands.

## Building

The default build targets RimWorld 1.6. Per mod:

```powershell
dotnet build -c Release <Mod>/src/<Solution>.slnx
dotnet test <Mod>/src/<Mod>.Core.Tests
```

For RimWorld 1.7, build each game project with `RimWorldVersion=1.7`, for example:

```powershell
dotnet build WorkRoles/src/WorkRoles/WorkRoles.csproj -c Release -p:RimWorldVersion=1.7
```

This uses the installed Waylith game and Harmony 3 through
[Shared/Build/RimWorld17.props](Shared/Build/RimWorld17.props). Override
`InstalledGameRoot` and `InstalledHarmonyPath` when necessary. Build both
versions before packaging; their outputs are kept in separate version folders.

Everything at once (default 1.6 build + deploy, stops on first failure):

```powershell
scripts\build-deploy-all.cmd
```

Building never deploys — installing a mod into the game requires its `scripts/deploy.ps1` and a game restart.

## Workshop publishing

Each mod's `scripts/publish.ps1` uploads content and Workshop category/version tags
together through the Steam desktop API. Keep the Steam desktop client online and
logged in as the item owner. Publishing uses the installed RimWorld 1.7 Steamworks
libraries and PowerShell 7.6 or newer; SteamCMD is no longer required. Legacy
`-SteamCmd` and `-Username` arguments are accepted with a warning and ignored.

SteamCMD can disconnect the desktop client with "Session Replaced". A local API
connection still initializes in that state, but Workshop requests can fail with
`k_EResultFileNotFound`. If you used SteamCMD recently, reconnect or restart Steam
before publishing. The scripts now check the server connection before submitting.

The publisher retains change-note confirmation, preserves existing titles and
visibility, and sends the preview only when it changed. New item IDs are saved
before content submission; if that step fails or needs Workshop agreement
acceptance, retrying completes the same item. Keep `temp/workshop-created-item.txt`
until the first publication succeeds.

To repair tags without uploading mod files or changing the description:

```powershell
pwsh -NoProfile -Command "& ./Shared/tools/deploy/sync-workshop-tags.ps1 -ModPath Readouts/mod,QualityJobs/mod,WorkRoles/mod,Implanner/mod"
```

Add `-WhatIf` to preview the item IDs and tags without contacting Steam.

## History

The repo was consolidated from the mods' individual repositories with full history preserved; historical commits are prefixed with their origin (`[ER]` Readouts, `[QJ]` Quality Jobs, `[WR]` WorkRoles).

## License

See [LICENSE](LICENSE).
