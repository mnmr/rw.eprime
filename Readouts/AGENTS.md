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
- Shared test-only source from `..\Shared\Tests\Support` (namespace `RimShared.Tests.Support`, e.g. the shipped-help validator `HelpContentChecks`) is compiled into `EPrimeReadouts.Core.Tests` the same way; it must never be included in a shipped project.

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
| Base pixel surface | Draw-model identity, content dimensions, UI metric revision, icon scale revision, icon data revision, visual options; scroll offset and viewport height are presentation-only and never invalidate it. Buffered rendering is selected automatically when supported; the retired per-player buffering preference is ignored. A repaint whose cached presentation fails draws directly; three consecutive publication failures retire buffering for the session |
| Content glyph surface | Counter/label content (text, counts, threshold bands, cell rects), UI metric revision, content dimensions |
| Direct glyph geometry (`PanelDirectGlyphs`) | Draw-model identity, the same text revision as the glyph surface, raster scale (`Prefs.UIScale`), and Waylith's font atlas revision (its atlas may repack without changing texture identity); scroll and panel position only translate the cached quads by a pixel-snapped origin; released on panel reset. Runtime regression: force atlas growth while paused with unchanged panel inputs, verify intact counters and one revision-driven rebuild followed by stable reuse |
| Header strip surface | Search visibility options, search text, title text and measured width, panel width, header height, UI metric revision, raster scale. The mod-name title rides a second coverage-from-red channel drawn through the font material (the sprite material renders atlas glyphs black); both channels publish from one Ensure and promote together |
| Editor bands | Selected group, width, `GroupsVersion`, `ThresholdsVersion`, `CountRulesVersion`, pool snapshot identity, count snapshot identity |
| Pool display/list rows | Shared pool snapshot identity and relevant selection state |
| Group assignment tree rows | Store/world identity, `GroupsVersion`, selected group and token, pool snapshot identity, shared filter revision, group expansion state, and language revision |
| Pool membership tree rows | Store/world identity, `PoolsVersion`, selected pool, shared filter revision, pool expansion state, and language revision |
| Tooltip content | Token (search results are keyed apart from group slots and use the global count basis, never a count rule), render snapshot identity, `ThresholdsVersion`, `CountRulesVersion`; capture when a display session begins and retain until it ends |
| Tooltip geometry (shared `RimShared.UiLib` tooltip stack in `Shared/UiLib/Tips`; Readouts' window id, table inset and revisions in `UI/TipHost.cs`) | Tooltip model identity, maximum width, UI metric revision (`UiRevision.Current`); capture when a display session begins and retain until it ends |
| Text width/height (fit widths: shared `RimShared.UiLib.WrText.FitWidth`, per-assembly static) | Text, font, available width where applicable, UI metric revision (`UiRevision.Current`, shared `RimShared.UiLib.UiRevision`: UI scale, tiny-font preference, language); `WrText.Reset` from `RuntimeTeardown.ResetAll` |
| Help foldout and caption heights (shared `RimShared.UiLib.HelpFoldout`, per-assembly static; `EprStyle.CaptionHeight` reads the same cache) | Caption text, effective Tiny font (`TinyText.Metrics.Font`), wrap width, and the caller-supplied `UiRevision.Current`; `HelpFoldout.Reset` from `RuntimeTeardown.ResetAll` |
| Confirmation body heights (shared `RimShared.UiLib.CompactConfirmDialog`, per-assembly static) | Body text, Small font, body wrap width, and the caller-supplied `UiRevision.Current`, measured once at dialog construction; `CompactConfirmDialog.Reset` from `RuntimeTeardown.ResetAll` |
| Export snapshot | `GroupsVersion` and `PoolsVersion`; threshold-only edits must not invalidate it |
| Editor tab records (`Dialog_ReadoutConfig`) | `UiRevision.LanguageCurrent`; the selected tab is session-static presentation state, never persisted; dropped on close |
| Help content (shared `HelpContentState` via `ReadoutHelpHost`) | Chapter topic lists loaded from `mod/Help/<Language>/<chapter>` on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiRevision.Current`; word/space/line-height measurements stamped by `UiRevision.Current`; file-loaded textures owned and destroyed by `Release()`; read marks persisted to `ReadoutSettings.helpTopicsRead` from `WindowUpdate`/close, one settings write per batch, never from a draw pass; language change and window close release everything (window-owned via `HelpTabView`) |
| Welcome dialog assets (shared `RimShared.UiLib.WelcomeDialog`, built in `ReadoutGameComponent.QueueWelcome`) | Translated strings resolved at construction; About/Preview.png loaded from disk and wrapped-text measurements resolved once in `PreOpen` (language and UI scale cannot change while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `ReadoutSettings.welcomeShownSaves` keyed by the world's persistent random value; held back unclaimed while the scenario opening is on screen (vanilla's start dialog or Immersive Opening, `WindowStack.SecondsSinceClosedGameStartDialog`) and retried by the shared `Notify_GameStartDialogClosed` postfix |

Changes to these dependencies require updated behavioral tests in the same change.

Cached surfaces do not perform periodic GPU probes or idle target restoration.
Only a revision-gated build acquires or recreates its working target. Published
fronts remain visible until replacement publication succeeds; three consecutive
publication failures retire buffering for the session. Coverage validation and
the per-repaint direct fallback remain active.

Presentation preflights the complete required front set (including the title
only when shown) and the backend before submitting any layer. Missing base
dimensions are a presentation failure, not an empty viewport. Every surface
propagates backend presentation failure to the direct fallback.

Screen presentation MUST use Unity's built-in GUI blit material (the
material-less `Graphics.DrawTexture` overload with its neutral 0.5 tint), which
is `ZTest Always` like every vanilla IMGUI draw. The owned sprite material
depth-tests (`Sprites/Default` declares no `ZTest`), and RimWorld never resets the
backbuffer depth at GUI time, so any near-depth write (observed after a
window-surface round trip such as minimize/restore on Linux) would hide every
depth-tested draw for the rest of the process while every offscreen check stayed
green; save/reload cannot clear it. The sprite material is only the premultiplied
compositor for offscreen targets, which have no depth buffer. Runtime regression:
write near depth across the backbuffer during OnGUI, then stop; the panel must
remain visible (`temp/linux-minimize`).

On Waylith (`UnityStubs`), the engine-specific backend uses GL quads into
native GUI render targets during a gated repaint, before entering panel/scroll
clips. Unity shaders, command buffers and the Unity/Linux presentation path
remain unchanged. Waylith screen presentation uses the same material-less
overload with literal white tint; its offscreen GUI blend produces premultiplied
RGB and correct alpha, so it does not use Unity's coverage-from-red workaround.
Published pixels remain independent of later font-atlas changes. Builders warm
all requested content glyphs before retaining their atlas UVs.

Waylith's generated text retains fractional alignment inside its vertex offsets.
Both buffered and direct glyph builders must snap one generated run anchor,
then apply the same translation to every vertex. Snapping only the containing
rectangle leaves counters blurred; rounding vertices independently changes
glyph sizes and spacing. Unity retains its existing zero-anchor placement.
Regression evidence lives in `temp/crispness`: counter pixel comparisons,
font fallback, atlas growth, hover/scroll return, and search/title captures.

Waylith's reported async readback is synchronous and returns owned buffers;
use the existing synchronous publication branch only when a surface changes,
with no periodic health readback. Previous fronts and whole-set promotion are
retained. Working target sizing pads to an exact float viewport extent using
the physical display size at build time (e.g. 448 becomes 449 at 1080p).
Padding never moves logical geometry. Each front retains its own dimensions
and UV window, including layers built at different display sizes. Already
published pixels do not need rebuilding on a display change alone.

The runtime two-color probe checks composition, publication, orientation and
source-over. Unity also probes its presentation call; Waylith's active-target
Graphics overload is an unblended copy, so its distinct screen presentation
path is verified with direct-reference screen captures. Runtime regressions:
`temp/rw17-buffer` covers exact steady/atlas/hover-return/target-recovery pixels,
pixel-sized glyphs, colored-background alpha, count-only publication, scroll
reuse, scale/tiny-font changes, live resolution changes, 1919px initialization,
native missing-icon no-ops, long-search clipping, and menu enter/leave.

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
preservation. There is no periodic presentation-path readback or GPU repair
ladder. Initialization calibrates the backend once; ordinary publication is
revision-gated. Final-screen depth safety comes from the presentation material.

Linux regression evidence in `temp/linux-validation` runs the native 1.6.4871
Unity player under Ubuntu/WSL on a private X display (Mesa OpenGL over D3D12).
The current renderer matches the pre-1.7 renderer pixel-for-pixel across count,
scroll, UI-scale, Tiny fallback and resolution changes. Async publication and
the forced synchronous fallback are both exercised. WSL reports async support;
do not infer that capability from the operating system alone. Depth writes hide
the deliberately depth-tested control while the real panel stays pixel-identical.
This does not establish native Linux driver or suspend/resume behavior.

Cleanup regression evidence in `temp/followup-cleanup` verifies zero periodic
health readbacks, unchanged front identities and zero measured steady managed
allocations after advancing paused game ticks. Windows 1.7 and Linux 1.6/WSL
retain direct-reference fidelity and pixel-stable transitions; the Linux depth
test also compares expanded hover and gear states after icon measurements
settle. Forced publication failure still retires buffering after three attempts.
Fresh clean-release runs verify copied assembly hashes and no Readouts warnings.

## Fault ladder

`Patch_ResourceReadout` catches faults from the panel's OnGUI and hands that
frame to vanilla. Five handled faults retire the buffered renderer for the
session (the panel keeps drawing directly, gear tinted amber); five more
hand the readout to vanilla for the session. Each step logs once; a world
reload resets the ladder. A failed presentation draws directly for that frame.
Unsupported builds or buffer-update exceptions retire buffering once; three
consecutive publication failures do the same. No repair episode runs afterward.

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
