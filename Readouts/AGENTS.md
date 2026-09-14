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
- Shared game-side UI source from `..\Shared\UiLib` (namespace `RimShared.UiLib`, may reference Verse/Unity) is compiled into `EPrimeReadouts` via `$(RimSharedRoot)`; it must never be included in `EPrimeReadouts.Core`.

## Canonical refresh boundaries

- The canonical resource-count refresh interval is 204 game ticks.
- Published-texture health checks use the same 204-game-tick interval, at least
  30 ticks after the latest actual count refresh or buffer build/publication,
  and at least 30 ticks before the next known count-refresh boundary.
  Equal count refreshes still count as work. A delayed check coalesces missed
  periods; it never catches up with a burst of requests. Periodic checks wait
  while paused; detected faults and recovery verification proceed while paused.
- A new periodic game-data cache must use an explicitly named interval in the approved 200–500 game-tick range unless the owner approves another interval.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Per-map render data | Store/world identity and map identity |
| Pool structure snapshot | `PoolsVersion`, immediately |
| Resource-count snapshot | Canonical map identity (the MultiFloors ground map when the map belongs to a level stack), the map-set stamp while MultiFloors is active, the derived collection needs (storage-only and hide-forbidden count-basis options unioned with the stored count rules via `CountRulesVersion`) immediately, `PlannedWorkOptions` immediately (including while paused), and 204 elapsed game ticks; replace only when contents differ |
| Main readout layout/draw model | Map, width, view state (per-player depths, search text, the display options including the tier layout option, and the kept toolbar toggle's hidden-bands state derived from `Prefs.ResourceReadoutCategorized`, all routed through the view stamp), `GroupsVersion`, `ThresholdsVersion`, `CountRulesVersion`, pool snapshot identity, count snapshot identity; a rebuild with content equal to the published model preserves model identity |
| Hover model variants (per hovered group id) | Every non-hover draw-model input above plus count snapshot identity, immediately (any such change clears all variants; a successful in-place count refresh keeps only the active variant); a pure hover transition republishes the stored DrawModel identity instead of rebuilding |
| Base pixel surface | Draw-model identity, content dimensions, UI metric revision, icon scale revision, icon data revision, visual options; scroll offset and viewport height are presentation-only and never invalidate it. Buffered rendering is selected automatically when supported; the retired per-player buffering preference is ignored. A repaint whose cached presentation fails draws directly, and three consecutive failures request bounded recovery on the main-thread update |
| Content glyph surface | Counter/label content (text, counts, threshold bands, cell rects), UI metric revision, content dimensions |
| Direct glyph geometry (`PanelDirectGlyphs`) | Draw-model identity, the same text revision as the glyph surface, and the raster scale (`Prefs.UIScale`); scroll and panel position only translate the cached quads by a pixel-snapped origin; released on panel reset |
| Header strip surface | Search visibility options, search text, title text and measured width, panel width, header height, UI metric revision, raster scale. The mod-name title rides a second coverage-from-red channel drawn through the font material (the sprite material renders atlas glyphs black); both channels publish from one Ensure and promote together |
| Published texture sample | Exact published pixels and physical dimensions; strongest-coverage pixel selected during the existing conversion, expected premultiplied output and UV promoted with the front. Empty layers use a clear sample. No scan is performed at check time |
| Texture health result | Panel/map owner, frame-buffer owner and publication version, visible title, sample metadata and presentation material; sample up to four actual fronts through that material into an owned 4x1 target, then compare asynchronous readback with a two-byte rounding tolerance. Old owners/versions cannot initiate recovery; panel reset/retirement defers target destruction until an outstanding read completes |
| Editor bands | Selected group, width, `GroupsVersion`, `ThresholdsVersion`, `CountRulesVersion`, pool snapshot identity, count snapshot identity |
| Pool display/list rows | Shared pool snapshot identity and relevant selection state |
| Group assignment tree rows | Store/world identity, `GroupsVersion`, selected group and token, pool snapshot identity, shared filter revision, group expansion state, and language revision |
| Pool membership tree rows | Store/world identity, `PoolsVersion`, selected pool, shared filter revision, pool expansion state, and language revision |
| Tooltip content | Token, render snapshot identity, `ThresholdsVersion`, `CountRulesVersion`; capture when a display session begins and retain until it ends |
| Tooltip geometry | Tooltip model identity, maximum width, UI metric revision; capture when a display session begins and retain until it ends |
| Text width/height | Text, font, available width where applicable, UI metric revision |
| Export snapshot | `GroupsVersion` and `PoolsVersion`; threshold-only edits must not invalidate it |
| Editor tab records (`Dialog_ReadoutConfig`) | `UiVersion.LanguageCurrent`; the selected tab is session-static presentation state, never persisted; dropped on close |
| Help content (shared `HelpContentState` via `ReadoutHelpHost`) | Chapter topic lists loaded from `mod/Help/<Language>/<chapter>` on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiVersion.Current`; word/space/line-height measurements stamped by `UiVersion.Current`; file-loaded textures owned and destroyed by `Release()`; read marks persisted to `ReadoutSettings.helpTopicsRead` from `WindowUpdate`/close, one settings write per batch, never from a draw pass; language change and window close release everything (window-owned via `HelpTabView`) |
| Welcome dialog assets (`Dialog_ReadoutsWelcome`) | About/Preview.png loaded from disk plus translated strings and wrapped-text measurements, all resolved once in `PreOpen` (language cannot change while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `ReadoutSettings.welcomeShownSaves` keyed by the world's persistent random value |

Changes to these dependencies require updated behavioral tests in the same change.

All buffered pixel surfaces also depend on their native render targets
remaining created. The main-thread graphics update checks every channel,
including the header title, before the build/publish gate. A lost target
re-uploads every published front from its retained CPU pixels and cancels any
in-flight publish immediately, including while paused. Lost working targets
are recreated, and pending changes retry through the existing build gate.
Published front, count, pool, and layout snapshot identities are retained;
failed target restoration advances the recovery ladder below. Runtime regression:
release the working targets with unchanged model revisions, erase only the
published GPU pixels, and verify automatic recovery both idle and during a
pending publish.

Presentation preflights the complete required front set (including the title
only when shown) and the owned sprite material before submitting any layer.
Missing base dimensions are a presentation failure, not an empty viewport.
Every surface propagates backend presentation failure to the direct fallback.

Surface publication validates coverage during its existing pixel conversion,
on both asynchronous and synchronous paths. Each gated builder declares when
coverage is expected: a visible solid band stripe, a whole visible glyph quad,
or the always-present header gear. Empty content, whitespace/degenerate/clipped
glyphs, and hidden titles must remain valid. A blank required layer rejects
the back buffer, retains the previous front, and uses the existing bounded
publish retry/fallback path. This checks newly built output only; it does not
detect later corruption of a published GPU texture or final-screen occlusion.
Runtime regressions must cover lost base/glyph/title fronts and material,
blank required publications, legitimate empty layers, and rejected replacement
preservation. The periodic material-path check additionally detects corruption
at the sampled published pixels. It cannot prove every texel or final-screen
visibility. It submits no synchronous periodic readback on platforms without
async readback support; creation, material and publication checks remain active.

Recovery first re-uploads retained front pixels and recreates lost working
targets; then recreates the owned backend/material and re-uploads; then builds
an independent complete surface set from the cached DrawModel, retaining old
fronts until the replacement publishes successfully. On async-capable hardware,
each successful repair is checked through the material path before its retry
budget resets; other hardware retains the existing material/front checks. A stale
result cannot confirm or reject a newer publication. Persistent failures
exhaust the three repairs and retire buffering. Backend recreation uses the
existing initialization round-trip probe, which can synchronously wait on the
GPU only on initialization/recovery, never in the periodic check.

Runtime verification must cover actual tick offsets, erased GPU fronts with
retained CPU pixels, material/front/working-target loss, persistent corruption,
in-flight publication interruption, stale results, reset during a pending
check, title visibility, and unsupported async readback. Normal GPU request
completion is required; a driver-stalled request has no forced timeout.

## Fault ladder

`Patch_ResourceReadout` catches faults from the panel's OnGUI and hands that
frame to vanilla. Five handled faults retire the buffered renderer for the
session (the panel keeps drawing directly, gear tinted amber); five more
hand the readout to vanilla for the session. Each step logs once; a world
reload resets the ladder. Three consecutive presentation failures request the
buffer recovery ladder on the main-thread update. Temporary direct drawing
keeps failed presentations visible while repair runs; permanent retirement
occurs only when the bounded recovery attempts fail.

## Authoritative state

- `ReadoutStore` is authoritative per-save state.
- Only `ReadoutCommands` and deterministic store lifecycle code may mutate the shared model.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src\EPrimeReadouts.slnx --no-restore
dotnet test src\EPrimeReadouts.slnx --no-restore
```

Building never deploys. Automated verification requires creating a fresh managed
run after building, following the root contract; creation deploys its copies.
Player installation deployment uses `pwsh scripts/deploy.ps1` and a game restart.
