# EPrimeReadouts Engineering Contract

The general contract in the repository root `AGENTS.md` (one level up) applies
in full. This file records only what is specific to EPrimeReadouts: project
names, canonical cache dependencies, approved refresh boundaries, and
verification commands. Where this file is silent, the root contract governs.

## Asset pipeline

- `mod/Textures/EPrimeReadouts/ModIcon.png`: 256x256 (in addition to the root-mandated About assets).
- Regenerate workshop assets only via `assets/workshop/export-assets.ps1`.

## Project boundaries

- `src/EPrimeReadouts.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/EPrimeReadouts` owns game integration, persistence, patches, rendering, and UI.
- `src/EPrimeReadouts.Core.Tests` owns executable behavioral and regression tests.

## Canonical refresh boundaries

- The canonical resource-count refresh interval is 204 game ticks.
- A new periodic game-data cache must use an explicitly named interval in the approved 200–500 game-tick range unless the owner approves another interval.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Per-map render data | Store/world identity and map identity |
| Pool structure snapshot | `PoolsVersion`, immediately |
| Resource-count snapshot | Canonical map identity (the MultiFloors ground map when the map belongs to a level stack), the map-set stamp while MultiFloors is active, `PlannedWorkOptions` immediately (including while paused), and 204 elapsed game ticks; replace only when contents differ |
| Main readout layout/draw model | Map, width, view state, `GroupsVersion`, `ThresholdsVersion`, pool snapshot identity, count snapshot identity |
| Editor bands | Selected group, width, `GroupsVersion`, `ThresholdsVersion`, pool snapshot identity, count snapshot identity |
| Pool list/editor rows | `PoolsVersion` and relevant selection or expansion state |
| Pool list desired height | `PoolsVersion`, row count, fold state, width, UI metric revision |
| Tooltip content | Token, render snapshot identity, `ThresholdsVersion`; capture when a display session begins and retain until it ends |
| Tooltip geometry | Tooltip model identity, maximum width, UI metric revision; capture when a display session begins and retain until it ends |
| Text width/height | Text, font, available width where applicable, UI metric revision |
| Export snapshot | `GroupsVersion` and `PoolsVersion`; threshold-only edits must not invalidate it |

Changes to these dependencies require updated behavioral tests in the same change.

## Authoritative state

- `ReadoutStore` is authoritative per-save state.
- Only `ReadoutCommands` and deterministic store lifecycle code may mutate the shared model.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src\EPrimeReadouts.slnx --no-restore
dotnet test src\EPrimeReadouts.slnx --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.
