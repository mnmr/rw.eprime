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
| Compiled job orders per pawn (`CompiledJobOrders`) | Explicit per-pawn assignment/runtime invalidation, effective activation/work/history/rules/capability changes for actual holders, dependent composite holders, pawn lifecycle/location, definition reload, and emergency rule policy. Equivalent assignment/global activation outcomes preserve compiled identity while publishing presentation changes. Hour boundaries compare the cached local-hour observation with current effective rule outcomes; unchanged outcomes advance observation metadata without recompilation. Pending work and boundary changes reconcile once after collection. Labels/icons, pins, recommendation settings and unrelated pawns preserve compiled orders. Save-time forced fallback publication remains independent of cache reuse. Full clear is lifecycle-only. |
| Pawn signal snapshot (`PawnSignalSnapshotCache`) | Explicit invalidation via `ExternalPawnFacts`; generation cleared on window open and release; live skill XP intentionally not a dependency |
| External pawn facts (`ExternalPawnFacts`) | Separate per-pawn presentation and factual/skill revisions. Names update presentation/tooltip grammar only; location/lifecycle, trait and downed/undowned notifications recapture applicable factual/skill inputs. `InvalidateAll` on language or definition reload; world teardown releases all registries. Role and assignment mutations deliberately excluded; `PortraitsCache.SetDirty` deliberately excluded (vanilla dirties portraits on every job start, and portraits are drawn through `PortraitsCache.Get` per repaint instead of being snapshotted). |
| Colonist stats snapshots (`ColonistStatsState`) | Per-pawn skill-capture identity, store, consumed skill-promotion tuning values, language, definitions and `UiRevision`. Role/assignment mutations, names and unrelated tuning are excluded. Equal per-skill inputs preserve presentations across recapture; columns and selected panel share one producer. Tooltips resolve current name/grammar facts only when a hover session opens; active sessions remain frozen. Departed owners are removed, while changed captures retain prior stats for exact component reuse. |
| Roles list display (`RolesListState`) | Sections: Catalog, RoleWork, RoleCapability, RoleRules, definitions and language. Detached rows add RolePresentation, RoleActivation, location/collapse/filter/display preferences and UI metrics. Selection lists use Catalog/RolePresentation. Role tips are delayed session sources, separate from rows. |
| Structured tips (shared `RimShared.UiLib` `WrTips`/`StructuredTipPresenter`/`WrTipUI` in `Shared/UiLib/Tips`, per-assembly statics; WorkRoles' window id, zero table inset and revisions in `UI/TipHost.cs`) | Stable key + continuous-hover session (0.45s delay); content and geometry frozen per session; geometry keyed by maximum width and `UiRevision.Current` (UI scale, tiny-font preference, language); the translated-tip registry clears on `LanguageChangeCoordinator.Revision`; `SetSuppressed` resets the session |
| Priority grid (`Dialog_PriorityGrid`) | Columns: language, definitions and UI metrics; sort state discarded on rebuild. Fixed pawn cohort: per-pawn `PriorityGridFacts` and `RuntimeForPawn`, store, definitions, manual-priority display mode. Aggregate revisions only gate scanning the fixed cohort. |
| Text fit widths (shared `RimShared.UiLib.WrText.FitWidth`, per-assembly static) | `(font, text)` key; cleared when `UiRevision.Current` moves (observed each frame in `WorkRolesGameComponent.GameComponentUpdate`); `WrText.Reset` on window data release (`WindowDataLifecycle.ReleaseShared`) |
| Confirmation body heights (shared `RimShared.UiLib.CompactConfirmDialog`, per-assembly static) | Body text, Small font, body wrap width, and `UiRevision.Current`; `CompactConfirmDialog.Reset` on world teardown (`Patch_MemoryUtility_ClearAllMapsAndWorld`) |
| Map classification (shared `RimShared.GameLib.MapClassifications`) and locations (`ColonyScope.locationSnapshots`) | Classification invalidation per map and map-set changes (the shared `Patch_LocationTransitions` events; WorkRoles' hooks in `LocationHooks` add the per-map `CompiledJobOrders.InvalidateLocationRules` and the `UiVersion.RosterChanged` on map add/remove), the map-count guard on location reads, the singular landed/traveling Gravship engine identity/state, and language (`LanguageChangeCoordinator`); location snapshots are cleared through the required `MapClassifications.InvalidateLocationSnapshots` hook; publishes `MapClassifications.LocationRevision`; teardown via `MapClassifications.ReleaseSnapshot` (window data release and world teardown) |
| Floor-map canonicalization (shared `RimShared.GameLib.FloorMaps.cache`) | Hash of live map ids; `ReleaseForTeardown` on map-set invalidation, `MapClassifications.ReleaseSnapshot` and world teardown |
| Window scopes and editor states | Roster cohorts use store/map identity, Roster and the explicit scope revision. Filtering consumes assignments, catalog/work/capability (blocker coverage), and labels only for active text search. Editor work indexes use the selected role work revision; editor display observes the selected role presentation/rules/capability plus consumed assignment, scope, location and preference facts. Recommendation configuration views use catalog/work/rules/presentation/order and selected-role policy. Options observes exact settings values. UI geometry also observes `UiRevision`. |
| Help content (shared `RimShared.UiLib.HelpContentState` and `HelpTabView`, hosted by `WorkRolesHelpHost`, whose `Tour` turns the Start chapter into the Start page with the guided tour; the markdown, flow layout with CJK line breaking, and index planner are `RimShared.Common.Help`) | Chapter topic lists loaded from `mod/Help/<Language>/<chapter>` on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiRevision.Current`; word/space/line-height measurements stamped by `UiRevision.Current`; `@demo` blocks resolved through the host from the fixed `HelpDemos` set when a draw model builds; file-loaded textures owned and destroyed by `Release()`, which advances `Generation`; `LanguageChangeCoordinator.Revision` and window close release everything (window-owned via `HelpTabView`); read marks collected while drawing and persisted through the host from `MainTabWindow_WorkRoles.WindowUpdate` and on close, one write per batch (settings file write deferred by `RequestSettingsWrite`), which also plays the tour-complete chime once, never from a draw pass |
| Help chapter labels, tour captions and tour rows (shared `HelpTabView`) | `LanguageChangeCoordinator.Revision` (the host's language revision); tour rows also `HelpContentState.Generation`; tour read flags, read count and progress line also the view's read revision; cleared by `ReleaseWindowData` |
| Welcome dialog assets (shared `RimShared.UiLib.WelcomeDialog`, built in `WorkRolesGameComponent.QueueWelcome`) | Translated strings and the below-floor warning resolved at construction; About/Preview.png loaded from disk and wrapped-text measurements resolved once in `PreOpen` (language, UI scale and screen size are not observed while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `WorkRolesSettings.welcomeShownSaves` keyed by the world's persistent random value; held back unclaimed while the scenario opening is on screen (vanilla's start dialog or Immersive Opening, `WindowStack.SecondsSinceClosedGameStartDialog`) and retried by the shared `Notify_GameStartDialogClosed` postfix |
| Time-rule boundaries | `FixedTickBoundaryGate(2500)` hour boundary, game ticks only; mid-hour timezone crossings (caravan or live-map tile change) are event-patched via `WorldObject.Tile` and dispatched by `TimezoneCrossingPolicy`. The same per-map boundary observation drives `AutoOptimizer` (no additional gate, no per-tick polling) |
| Model revisions (`RoleStore.Revisions`) | Store-owned role/pawn domain stamps; no-op commands publish nothing. Multi-domain commands publish only changed fields. Per-key stamps include true global-domain invalidation floors; aggregate collection notifications do not invalidate unrelated keys. Removed identities are forgotten; world teardown releases the store. |
| Pawn location transitions (`PawnLocationTracker`) | Spawn/despawn notifications capture canonical location for managed and unmanaged roster members. Same-canonical synchronous transfers publish nothing; settled departures and subsequent arrivals publish Roster plus that pawn's external/runtime revisions. Pending departures drain at the existing component-tick boundary; no live-model polling. Store replacement, destruction and teardown release pending entries. |
| Work specifications (`RoleWorkSpecs`) | Store/role identity, that role's work revision and definitions. Composite dependencies follow actual included members. Activation, ordinary blocker state, labels/icons and age capability gates do not rebuild ordinary work specs. Equal work artifacts are shared with the recommendation catalog; teardown clears ownership. |
| Recommendation computations (`ColonistRecommendationProducer`) | Store and exact ordered pawn cohort (at most two retained), shared role work/policy/training/activation/authored-rule presence, catalog order, definitions/research availability; per-pawn assignment membership/order/pins, order template, tuning and external facts. Overlapping cohorts share per-pawn projections and suitability maps; plan and suitability share one engine context per detached colony identity. Suitability consumes only pawn skill/capability/signal inputs and relevant role inputs, independent of colony demand and assignment changes. Assignment enabled/disabled/force-on is excluded from the engine. Scalar policy and activation overlay existing work; unchanged training exclusions preserve ordinary/composite work even when path bands change. Ordering controls and After Basics placement share ordering projections, with catalog order retained for default-order ties. Equal refreshed inputs/results retain identity. Preview targets additionally consume current assignment presentation. |
| Colonist chip sequences, rows and geometry | Outer domain gates check exact per-pawn assignment/runtime revisions, assigned-role presentation/capability/activation/rules, external capture and shared verdict map identity. Icon publication, UI metrics, definitions and display/naming preferences are explicit dependencies. Only changed sequences rebuild; rows reuse skill/caption data and existing chip rectangles when widths/counts are unchanged. Table geometry and desired sizes observe published geometry changes, not activation. Window release drops all pawn/store references. |
| Skill-column widths (`ColumnWidthSnapshot`) | Window/store, ordered selected skills, definitions/UI metrics and the explicit roster-width revision. Per-skill maxima depend only on disabled state and decorator counts across the cohort; names, levels, unrelated skills and tuning preserve them. One immutable width vector and total serve header, rows and desired sizes. Exact equal vectors retain identity; equal totals with different boundaries do not. Headers also observe UI metrics directly. Reset/release drops owner, columns and vectors. |
| Role capability and rule results | Capability: per-pawn external capture, per-role Capability revision, language/definitions and store. Rules: per-role authored rules and per-pawn runtime plus definitions/store. Neither consumes unrelated assignment state; changed pawns and removed roles are evicted explicitly. |
| Palette geometry and role tooltip sources | Geometry: store, Catalog/RolePresentation/RoleWork/RoleCapability/RoleRules, definitions, width/mode/verdict slots and UI metrics. Activation updates chip state while reusing geometry. Tooltips: stable role/context/pawn sources resolve at hover-session opening; per-role facts, training-parent/member labels, pawn activity/runtime/assignments, external facts, scope for best fits, tuning, language/definitions/metrics. Active sessions retain content and geometry; unrelated role labels do not expire cached tips. |
| Dialog projections | Rename: catalog and role labels; import: catalog, role labels and preferences; restore: catalog, presentation, work, capability, recommendation/order and historical work-type snapshots (`RoleHistory`). Changes preview observes only its shown roles and pawns after aggregate gates. All measurement-dependent artifacts observe UI metrics. Historical snapshot backfill does not invalidate coverage or work specifications. |

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
