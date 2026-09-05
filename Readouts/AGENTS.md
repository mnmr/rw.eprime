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
- A new periodic game-data cache must use an explicitly named interval in the approved 200–500 game-tick range unless the owner approves another interval.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Per-map render data | Store/world identity and map identity |
| Pool structure snapshot | `PoolsVersion`, immediately |
| Resource-count snapshot | Canonical map identity (the MultiFloors ground map when the map belongs to a level stack), the map-set stamp while MultiFloors is active, the derived collection needs (storage-only and hide-forbidden count-basis options unioned with the stored count rules via `CountRulesVersion`) immediately, `PlannedWorkOptions` immediately (including while paused), and 204 elapsed game ticks; replace only when contents differ |
| Main readout layout/draw model | Map, width, view state (per-player depths, search text, the display options including the tier layout option, and the kept toolbar toggle's hidden-bands state derived from `Prefs.ResourceReadoutCategorized`, all routed through the view stamp), `GroupsVersion`, `ThresholdsVersion`, `CountRulesVersion`, pool snapshot identity, count snapshot identity; a rebuild with content equal to the published model preserves model identity |
| Hover model variants (per hovered group id) | Every non-hover draw-model input above plus count snapshot identity, immediately (any such change clears all variants; a successful in-place count refresh keeps only the active variant); a pure hover transition republishes the stored DrawModel identity instead of rebuilding |
| Base pixel surface | Draw-model identity, content dimensions, UI metric revision, icon scale revision, icon data revision, visual options; scroll offset and viewport height are presentation-only and never invalidate it. The per-player cached-rendering switch (`ReadoutSettings.bufferedRendering`) gates every cached surface: off releases them at once and the direct renderer draws every frame; a repaint whose cached presentation fails also draws directly, and three consecutive failures retire the buffered renderer for the session |
| Content glyph surface | Counter/label content (text, counts, threshold bands, cell rects), UI metric revision, content dimensions |
| Direct glyph geometry (`PanelDirectGlyphs`) | Draw-model identity, the same text revision as the glyph surface, and the raster scale (`Prefs.UIScale`); scroll and panel position only translate the cached quads by a pixel-snapped origin; released on panel reset |
| Header strip surface | Search visibility options, search text, title text and measured width, panel width, header height, UI metric revision, raster scale. The mod-name title rides a second coverage-from-red channel drawn through the font material (the sprite material renders atlas glyphs black); both channels publish from one Ensure and promote together |
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

## Fault ladder

`Patch_ResourceReadout` catches faults from the panel's OnGUI and hands that
frame to vanilla. Five handled faults retire the buffered renderer for the
session (the panel keeps drawing directly, gear tinted amber); five more
hand the readout to vanilla for the session. Each step logs once; a world
reload resets the ladder. Presentation failures of the buffered surfaces
retire the buffered renderer on their own after three consecutive repaints.

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
