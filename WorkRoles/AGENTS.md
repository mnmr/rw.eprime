# WorkRoles Engineering Contract

The general contract in the repository root `AGENTS.md` (one level up) applies
in full. This file records only what is specific to WorkRoles: project names,
canonical cache dependencies, approved refresh boundaries, and verification
commands. Where this file is silent, the root contract governs.

## Project boundaries

- `src/WorkRoles.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/WorkRoles` owns game integration, persistence, patches, rendering, and UI.
- `src/WorkRoles.Core.Tests` owns executable behavioral and regression tests.
- Shared game-side location source from `..\Shared\GameLib\Locations` (namespace `RimShared.GameLib`: `FloorMaps`, `MapClassifications`, `PlayerFactions` and the location-transition Harmony patches) is compiled into `WorkRoles` via `$(RimSharedRoot)` through its own subfolder-scoped include, never a GameLib-wide glob, and never into `WorkRoles.Core`. WorkRoles' side of its partial hooks lives in `LocationHooks.cs`.
- Shared test-only source from `..\Shared\Tests\Support` (namespace `RimShared.Tests.Support`, e.g. the shipped-help validator `HelpContentChecks`) is compiled into `WorkRoles.Core.Tests` via `$(RimSharedRoot)`; it must never be included in a shipped project.

## Layout floor

- The design floor is 1536x864 logical (owner, 2026-09-26): 1920x1080 at
  UI scale 1.25, the managed-run default. The main window never shrinks
  below 1536x829 (the floor less the 35px bottom bar), manual or automatic,
  capped at the screen; every tab must render and scroll at that size.
- Smaller screens are not blocked but are not designed for or tested: the
  window fills the screen, and the welcome dialog warns once per save
  (`MainTabWindow_WorkRoles.ScreenBelowDesignFloor`). Help topic
  `6-advanced/06-screen-size` documents the floor. Verify layout changes at
  1920x1080 @1.25 and above in a managed run; do not verify below the floor.

## Canonical refresh boundaries

- Time-driven invalidation must fire on computed game-tick boundaries, never on per-frame or per-tick polling.
- The canonical boundary is the 2500-tick hour flip via `FixedTickBoundaryGate`.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Compiled job orders per pawn (`CompiledJobOrders`) | `UiVersion.Current`; role, pawn-lifecycle, and location-rule invalidations; the per-save emergency rule toggle (`InvalidateAll`); a member-role edit also invalidates every composite bundling it and that composite's holders (depth-1 reverse scan in `InvalidateRole`); mid-operation evictions defer reconciles to the next game-component tick |
| Pawn signal snapshot (`PawnSignalSnapshotCache`) | Explicit invalidation via `ExternalPawnFacts`; generation cleared on window open and release; live skill XP intentionally not a dependency |
| External pawn facts (`ExternalPawnFacts.Revisions`) | Per-pawn revision on location/lifecycle change, name/trait presentation change, and downed/undowned transition; `InvalidateAll` on language or definition reload; role and assignment mutations deliberately excluded; `PortraitsCache.SetDirty` deliberately excluded (vanilla dirties portraits on every job start, and portraits are drawn through `PortraitsCache.Get` per repaint instead of being snapshotted) |
| Colonist stats snapshots (`ColonistStatsState`) | `ExternalPawnFacts.Revisions` (`Current`, `FullGeneration`, per-pawn), refreshed at the window's Repaint boundary; presentations stamped by `UiVersion.Current`, RoleStore identity, and `RecommendationTuningRevision`. A partial refresh evicts only the changed pawns' presentations, rows, capability entries, rule results and tips (`SelectiveSnapshotCache.Refresh` reports them); colony-wide consumers (recommendation plan, verdicts, best-fit tips, aggregate widths, row geometry) rebuild on every refresh, and a row whose chip sequence changes identity during that rebuild is evicted too. Only `FullGeneration` clears everything |
| Roles list display (`RolesListState`) | `UiVersion.Current`, `MapClassifications.LocationRevision`, collapse revision, nested/search/job-filter state, language change |
| Structured tips (shared `RimShared.UiLib` `WrTips`/`StructuredTipPresenter`/`WrTipUI` in `Shared/UiLib/Tips`, per-assembly statics; WorkRoles' window id, zero table inset and revisions in `UI/TipHost.cs`) | Stable key + continuous-hover session (0.45s delay); content and geometry frozen per session; geometry keyed by maximum width and `UiRevision.Current` (UI scale, tiny-font preference, language); the translated-tip registry clears on `LanguageChangeCoordinator.Revision`; `SetSuppressed` resets the session |
| Priority grid column cache (`Dialog_PriorityGrid`) | `LanguageChangeCoordinator.Revision` + `DefinitionReloadCoordinator.Revision` via `RevisionPairGate`; sort state discarded on rebuild; pawn rows fixed at dialog construction |
| Text fit widths (shared `RimShared.UiLib.WrText.FitWidth`, per-assembly static) | `(font, text)` key; cleared when `UiRevision.Current` moves (observed each frame in `WorkRolesGameComponent.GameComponentUpdate`); `WrText.Reset` on window data release (`WindowDataLifecycle.ReleaseShared`) |
| Confirmation body heights (shared `RimShared.UiLib.CompactConfirmDialog`, per-assembly static) | Body text, Small font, body wrap width, and the caller-supplied `UiVersion.Current`, measured once at dialog construction; `CompactConfirmDialog.Reset` on world teardown (`Patch_MemoryUtility_ClearAllMapsAndWorld`) |
| Map classification (shared `RimShared.GameLib.MapClassifications`) and locations (`ColonyScope.locationSnapshots`) | Classification invalidation per map and map-set changes (the shared `Patch_LocationTransitions` events; WorkRoles' hooks in `LocationHooks` add the per-map `CompiledJobOrders.InvalidateLocationRules` and the `UiVersion.Bump` on map add/remove), the map-count guard on location reads, the singular landed/traveling Gravship engine identity/state, and language (`LanguageChangeCoordinator`); location snapshots are cleared through the required `MapClassifications.InvalidateLocationSnapshots` hook; publishes `MapClassifications.LocationRevision`; teardown via `MapClassifications.ReleaseSnapshot` (window data release and world teardown) |
| Floor-map canonicalization (shared `RimShared.GameLib.FloorMaps.cache`) | Hash of live map ids; `ReleaseForTeardown` on map-set invalidation, `MapClassifications.ReleaseSnapshot` and world teardown |
| Window scope stamps (roster/recommendation/editor states) | `ScopeCacheStamp` of `UiVersion.Current` and `PawnListRevisionTracker.Revision` (advances on observed-map change or explicit invalidation) |
| Help content (shared `RimShared.UiLib.HelpContentState` and `HelpTabView`, hosted by `WorkRolesHelpHost`, whose `Tour` turns the Start chapter into the Start page with the guided tour; the markdown, flow layout with CJK line breaking, and index planner are `RimShared.Common.Help`) | Chapter topic lists loaded from `mod/Help/<Language>/<chapter>` on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiRevision.Current`; word/space/line-height measurements stamped by `UiRevision.Current`; `@demo` blocks resolved through the host from the fixed `HelpDemos` set when a draw model builds; file-loaded textures owned and destroyed by `Release()`, which advances `Generation`; `LanguageChangeCoordinator.Revision` and window close release everything (window-owned via `HelpTabView`); read marks collected while drawing and persisted through the host from `MainTabWindow_WorkRoles.WindowUpdate` and on close, one write per batch (settings file write deferred by `RequestSettingsWrite`), which also plays the tour-complete chime once, never from a draw pass |
| Help chapter labels, tour captions and tour rows (shared `HelpTabView`) | `LanguageChangeCoordinator.Revision` (the host's language revision); tour rows also `HelpContentState.Generation`; tour read flags, read count and progress line also the view's read revision; cleared by `ReleaseWindowData` |
| Welcome dialog assets (shared `RimShared.UiLib.WelcomeDialog`, built in `WorkRolesGameComponent.QueueWelcome`) | Translated strings and the below-floor warning resolved at construction; About/Preview.png loaded from disk and wrapped-text measurements resolved once in `PreOpen` (language, UI scale and screen size are not observed while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `WorkRolesSettings.welcomeShownSaves` keyed by the world's persistent random value |
| Time-rule boundaries | `FixedTickBoundaryGate(2500)` hour boundary, game ticks only; mid-hour timezone crossings (caravan or live-map tile change) are event-patched via `WorldObject.Tile` and dispatched by `TimezoneCrossingPolicy`. The same per-map boundary observation drives `AutoOptimizer` (no additional gate, no per-tick polling) |

Changes to these dependencies require updated behavioral tests in the same change.

## Text and layout measurement

- Text measurements key on the shared UI metric revision `RimShared.UiLib.UiRevision.Current` (UI scale, tiny-font preference, language), observed once per frame in `WorkRolesGameComponent.GameComponentUpdate` before any OnGUI pass. WorkRoles' own `UiVersion` is a model-mutation stamp and never keys a measurement.
- The shared measurement cache is `RimShared.UiLib.WrText.FitWidth`, keyed by `(font, text)` and cleared when `UiRevision.Current` moves.
- Confirmation dialog body heights are measured by the shared `CompactConfirmDialog` cache, keyed by `(text, font, width)` and stamped with `UiRevision.Current`.
- Help layout measurements (word, space and line heights) live in the shared `HelpContentState` measurer, stamped by `WorkRolesHelpHost.UiMetricRevision` (`UiRevision.Current`) and dropped on language change.
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

Building never deploys. Automated verification requires creating a fresh managed
run after building, following the root contract; creation deploys its copies.
Player installation deployment uses `pwsh scripts/deploy.ps1` and a game restart.
