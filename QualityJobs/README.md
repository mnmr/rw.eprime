# EPrime's Quality Jobs

A RimWorld 1.6 and 1.7 mod that gets you high-quality gear and furniture reliably: work
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
  "Finish …" bill; the bill cleans itself up. If the
  working crafter already qualifies, they finish directly with no interruption.
  By default the finisher takes the finish job ahead of all other work except
  firefighting, medical care, rescue and bed rest; a global option can limit
  it to first place within its own work type instead.
- High-priority finishing ignores the category holding the finisher task;
  the pawn must have the finished item's work category enabled.
  With high priority disabled, finishers retain their original work scope
  until moved; a moved finisher can complete any eligible assigned item or frame.
- Shared unfinished work: idle unfinished items no longer lock their bill to
  the original crafter — anyone can continue them (also rescues items whose
  creator died, a long-standing vanilla annoyance).
- Stock caps per product: once enough unfinished items are waiting, bills stop
  starting new ones (resume paths are never blocked, so in-flight work drains
  cleanly).

**Construction**

- Per-building opt-in via the "Quality Job" button on quality blueprints and
  frames, or globally for all quality buildings from mod settings. Enablement
  is implicit: set any option and the build is managed; the
  Clear button resets it. Managed builds are marked with a sparkle overlay.
- Per-build finisher conditions — minimum skill, require Inspired Creativity,
  require production specialist — with a live quality-odds preview for your
  configuration and current best builder.
- Managed frames pause at 100% work until a builder meets your conditions,
  then the chosen finisher completes them.
- Target quality with retries: builds that roll below your chosen quality are
  automatically marked for deconstruction and rebuilt until you get the
  desired quality (or you clear the build from being a Quality Job).
- Upgrade completed buildings later: select one or more buildings, open
  **Quality Job**, choose a target and builder requirements, then click
  **Start rebuilding**. Configuration alone leaves the buildings untouched.
  Buildings already meeting the target are kept. Others enter the normal
  deconstruct-and-rebuild loop, consuming materials and becoming unavailable
  during construction.
- Replacements keep their material, orientation, style and paint, plus bed
  medical/prisoner settings. Original bed owners return when the loop ends if
  they are still eligible and have not taken another bed. Clear or cancel the
  deconstruction order to stop; after demolition, Clear lets the replacement
  finish normally with its saved settings.

**General**

- Built-in Help: the toolbar window has **Options** and **Help** tabs, with
  illustrated guides to sharing, crafting, construction, defaults and troubleshooting.
  Help follows the game's language and remembers your reading progress.
- Vanilla Skills Expanded support: construction, crafting, and art quality
  bonuses count when choosing the best finisher and predicting their quality
  odds. Speed expertises do not receive a quality preference. The integration
  activates automatically when the corresponding stats are present.
- Expertise changes use the existing 250-game-tick refresh fallback. An edit
  made while paused becomes visible after play resumes unless another relevant
  change already triggers a refresh; no extra frame or tick polling is added.
- Not a cheat: quality is still rolled by the pawn who finishes the work,
  exactly as vanilla rules dictate — the mod just helps ensure that pawn is
  someone with a high success rate.
- Inspired Creativity is consumed on the finishing touch, exactly as vanilla
  intends.
- Paused items show "Author: reserved (Quality Jobs)" and cannot be resumed
  until a qualifying pawn exists.
- RimWorld Multiplayer compatible: all mutations are synced commands or
  deterministic simulation; UI scans are read-only.
- Safe to add mid-game. Clean uninstall: disable in mod settings — items are
  restored to vanilla ownership and subsequent saves carry zero trace of the
  mod. Removing without disabling first leaves unfinished items without an
  owner (they must be cancelled). Finish replacement buildings before disabling
  or removing the mod to retain their saved bed settings and paint. Other mods'
  custom building settings follow their normal deconstruct-and-rebuild behavior.
- No defs are patched and unmanaged bills/builds behave exactly as vanilla;
  integration is a handful of targeted Harmony patches at job boundaries.
- Designed for performance: cached rendering, no per-frame work, event- and
  interval-driven logic.
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
dotnet build -c Release src/QualityJobs.slnx --no-restore     # zero warnings required
dotnet build -c Release src/QualityJobs/QualityJobs.csproj -p:RimWorldVersion=1.7
dotnet test src/QualityJobs.Core.Tests --no-restore
pwsh scripts/deploy.ps1                  # deploy to the game's Mods folder
```

Building never deploys. Automated verification uses independent managed runs
through `Shared/tools/automation`; the deploy script updates the player installation
and requires a game restart. The game assembly targets net472 against Krafs.Rimworld.Ref;
Core is netstandard2.0 with no game references so its tests run anywhere.

The default build produces the 1.6 assemblies. The second command produces the
net10.0 1.7 assemblies against the installed game and Harmony 3. Build both
versions before packaging. Override `InstalledGameRoot` and
`InstalledHarmonyPath` when their locations differ from the shared defaults.

The completed-building batch is in `tests/InGame/CompletedBuildings`. Build its
`Probe.csproj` after the mod, then run `run.ps1 -Version 1.6 -Renderer Software` and
`run.ps1 -Version 1.7` in parallel. The 1.6 batch uses the repository's independent
Linux game through WSL; software rendering avoids native crashes observed with
the NVIDIA backend during verification. The 1.7 batch uses the installed Windows game. Each run
preserves logs and captures under `temp/completed-buildings`, then stops and
removes its private game profile. `-Clean` verifies loading without the probe.

## Check out my other mods

- [EPrime's Readouts](https://steamcommunity.com/sharedfiles/filedetails/?id=3769342092): a modern, compact resource readout with support for custom resource pools.
- [WorkRoles](https://steamcommunity.com/sharedfiles/filedetails/?id=3760146134): easily and intuitively manage work priorities by assigning named roles to colonists.
- [EPrime's Implanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3793988932): plan, track and optionally automate the rollouts of bionic implants.

## Disclaimer

Created with the help of Claude Code.

## License

See [LICENSE](../LICENSE).
