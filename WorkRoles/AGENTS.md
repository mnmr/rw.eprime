# WorkRoles Engineering Contract

The general contract in the repository root `AGENTS.md` (one level up) applies
in full. This file records only what is specific to WorkRoles: project names,
canonical cache dependencies, approved refresh boundaries, and verification
commands. Where this file is silent, the root contract governs.

## Project boundaries

- `src/WorkRoles.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/WorkRoles` owns game integration, persistence, patches, rendering, and UI.
- `src/WorkRoles.Core.Tests` owns executable behavioral and regression tests.

## Canonical refresh boundaries

- Time-driven invalidation must fire on computed game-tick boundaries, never on per-frame or per-tick polling.
- The canonical boundary is the 2500-tick hour flip via `FixedTickBoundaryGate`.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Compiled job orders per pawn (`CompiledJobOrders`) | `UiVersion.Current`; role, pawn-lifecycle, and location-rule invalidations; a member-role edit also invalidates every composite bundling it and that composite's holders (depth-1 reverse scan in `InvalidateRole`); mid-operation evictions defer reconciles to the next game-component tick |
| Pawn signal snapshot (`PawnSignalSnapshotCache`) | Explicit invalidation via `ExternalPawnFacts`; generation cleared on window open and release; live skill XP intentionally not a dependency |
| External pawn facts (`ExternalPawnFacts.Revisions`) | Per-pawn revision on location/lifecycle change; `InvalidateAll` on language or definition reload; role and assignment mutations deliberately excluded |
| Colonist stats snapshots (`ColonistStatsState`) | `ExternalPawnFacts.Revisions` (`Current`, `FullGeneration`, per-pawn), refreshed at the window's Repaint boundary; presentations stamped by `UiVersion.Current`, RoleStore identity, and `RecommendationTuningRevision` |
| Roles list display (`RolesListState`) | `UiVersion.Current`, `ColonyScope.LocationRevision`, collapse revision, nested/search/job-filter state, language change |
| Priority grid column cache (`Dialog_PriorityGrid`) | `LanguageChangeCoordinator.Revision` + `DefinitionReloadCoordinator.Revision` via `RevisionPairGate`; sort state discarded on rebuild; pawn rows fixed at dialog construction |
| Text fit widths (`WrText.FitWidth`) | `(font, text)` key; cleared when `UiVersion.Current` moves or on language change |
| Map classification and locations (`ColonyScope`) | Classification invalidation per map, map-set changes, and the singular landed/traveling Gravship engine identity/state; publishes `LocationRevision` |
| Window scope stamps (roster/recommendation/editor states) | `ScopeCacheStamp` of `UiVersion.Current` and `PawnListRevisionTracker.Revision` (advances on observed-map change or explicit invalidation) |
| Time-rule boundaries | `FixedTickBoundaryGate(2500)` hour boundary, game ticks only; mid-hour timezone crossings (caravan or live-map tile change) are event-patched via `WorldObject.Tile` and dispatched by `TimezoneCrossingPolicy`. The same per-map boundary observation drives `AutoOptimizer` (no additional gate, no per-tick polling) |

Changes to these dependencies require updated behavioral tests in the same change.

## Text and layout measurement

- The shared measurement cache is `WrText.FitWidth`, keyed by `(font, text)` and cleared when `UiVersion.Current` moves or the language changes.
- Fractional UI-scale glyph drift is absorbed by `FitWidth` padding, not by re-measuring per frame.

## Authoritative state

- `RoleStore` is authoritative per-save state.
- Only `RoleCommands` and deterministic store lifecycle code may mutate the shared model.
- Approved exception (owner, 2026-08-23): `AutoOptimizer` applies
  `RoleCommands.PasteRoleSet` from deterministic map-tick code at the
  2500-tick hour boundary when `RoleStore.autoOptimize` is on. The shared
  simulation clock is the synchronizer: every client computes the plan from
  synced state only (no view faction, current map, or window state may
  influence the multiplayer outcome), and sync interception is inert during
  ticking. `ColonyFixPlanner` (Core) supplies the targets and changed flags
  shared with the Fix My Colony preview; the single-player-only preview-open
  guard is the sole permitted local input.

## Required testing (additions)

- For recommendation changes, prefer final ordered colony assignments and chosen
  training paths over claims, ledgers, repair scores, selection states, or other
  intermediate planner machinery.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src/WorkRoles.slnx --no-restore
dotnet test src/WorkRoles.Core.Tests --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.
