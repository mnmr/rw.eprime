# EPrime's Quality Jobs

A RimWorld 1.6 mod that gets you high-quality gear and furniture reliably: work
on quality items and buildings is set aside at the last moment and finished by
your best (or inspired) pawn, capturing their quality roll.

Quality in RimWorld is rolled by the pawn who *finishes* the work. Players
exploit this manually by parking nearly-done items and letting an inspired
master crafter do the final touch — this mod automates the entire loop.

## Features

**Crafting (workbench bills)**

- Quality bill work pauses the instant before completion; the unfinished item
  is set aside (haulable, shelvable, safe) until a qualifying colonist exists.
- Per-bill finisher conditions — minimum skill, require Inspired Creativity,
  require production specialist (Ideology) — with a live quality-odds table
  comparing your configuration against your current best crafter.
- Automatic dispatch: the best qualifying pawn gets a one-shot pawn-restricted
  "Finish …" bill and a notification letter; the bill cleans itself up. If the
  working crafter already qualifies, they finish directly with no interruption.
- Shared unfinished work: idle unfinished items no longer lock their bill to
  the original crafter — anyone can continue them (also rescues items whose
  creator died, a long-standing vanilla annoyance).
- Stock caps per product: once enough unfinished items are waiting, bills stop
  starting new ones (resume paths are never blocked, so in-flight work drains
  cleanly).

**Construction**

- Per-building opt-in via the "Quality job" gizmo on quality blueprints and
  frames. Enablement is implicit: set any option and the build is managed; the
  Clear button resets it. Managed builds are marked with a sparkle overlay.
- Managed frames pause at 100% work until a builder meets your conditions,
  then the chosen finisher completes them.
- Target quality with retries: builds that roll below your chosen quality are
  deconstructed and rebuilt automatically until the target is met (each cycle
  costs part of the materials; cancelling the deconstruct designation opts
  out).

**General**

- RimWorld Multiplayer compatible: all mutations are synced commands or
  deterministic simulation; UI scans are read-only.
- Safe to add mid-game. Clean uninstall: disable in mod settings — items are
  restored to vanilla ownership and subsequent saves carry zero trace of the
  mod.
- Requires Harmony. No DLC required (Ideology enriches, never gates).

## Repository layout

```
src/QualityJobs.Core/        deterministic decision logic — no game references
src/QualityJobs/             game integration: store, patches, commands, UI
src/QualityJobs.Core.Tests/  behavioral tests (TUnit)
mod/                         the shippable mod (About, Textures, Languages, Assemblies)
images/                      authoritative art sources (synced into mod/Textures by deploy)
docs/superpowers/            design spec and implementation plans
scripts/deploy.ps1           build output → RimWorld Mods folder
```

The engineering contract lives in [AGENTS.md](AGENTS.md) (deterministic core,
cached render paths, MP-safe commands, tests-first). The design spec is at
[docs/superpowers/specs/2026-08-01-quality-crafting-design.md](docs/superpowers/specs/2026-08-01-quality-crafting-design.md).

## Building

```powershell
dotnet build -c Release --no-restore     # zero warnings required
dotnet test src/QualityJobs.Core.Tests --no-restore
pwsh scripts/deploy.ps1                  # deploy to the game's Mods folder
```

Building never deploys; in-game verification requires the deploy script and a
game restart. The game assembly targets net472 against Krafs.Rimworld.Ref;
Core is netstandard2.0 with no game references so its tests run anywhere.

## Check out my other mods

- [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092): a modern, compact resource readout with support for custom resource pools.
- [WorkRoles](https://steamcommunity.com/sharedfiles/filedetails/?id=3760146134): easily and intuitively manage work priorities by assigning named roles to colonists.

## License

See [LICENSE](LICENSE).
