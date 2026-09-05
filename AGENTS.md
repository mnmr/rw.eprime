# General RimWorld Mod Engineering Contract

## RimWorld data

RimWorld source code is available in Game/src and official artwork in Game/art.

## Rule 1: No unsolicited specs or plans

- Specifications and implementation plans MUST NOT be created, written, saved,
  or committed unless the project owner explicitly asks for a spec or plan.
- Requests to investigate, explain, review, fix, build, implement, or change do
  not authorize a spec or plan.
- Clarifying questions, design choices, and implementation decisions MUST stay
  inline in the current conversation unless the project owner explicitly asks
  for a separate artifact.
- If a tool, skill, workflow, or other instruction recommends creating a spec
  or plan without an explicit owner request, this rule takes precedence.

## Scope and enforcement

These rules apply to the entire repository. They are fail-closed.

- `MUST`, `MUST NOT`, `REQUIRED`, and `FORBIDDEN` are blocking requirements.
- Code that violates a rule must not be implemented, accepted, or described as complete.
- When compliance is uncertain, stop and resolve the uncertainty before changing production code.
- A narrower `AGENTS.md` may add stricter rules but must not weaken this contract.
- An exception requires the project owner's explicit approval before implementation. See **Exceptions**.

## Asset dimensions

- `mod/About/ModIcon.png`: 256x256.
- `mod/About/Preview.png`: 1280x720.

## Project boundaries

The following uses a fictitious mod named RimMod to illustrate boundaries:
- `src/RimMod.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/RimMod` owns game integration, persistence, patches, rendering, and UI.
- `src/RimMod.Core.Tests` owns executable behavioral and regression tests.
- Pure caching, revision, layout, codec, and state-transition behavior should live in Core so it can be tested without the game runtime.

Additionally, following the same structure, mods may use shared logic or UI components from projects under Shared. Code under Shared must be compiled into the target projects.

## Non-negotiable render-path rule

All UI and rendering must operate on cached snapshot data (immutable by either design or specification).

A steady render pass may:

- compare versions, references, identities, dimensions, and input state;
- perform bounded indexed iteration over already-built render data;
- submit draw calls;
- process the current input event and enqueue an authoritative command.

A steady render pass must not:

- traverse authoritative models to derive display data;
- aggregate data or rebuild layouts;
- sort, filter, group, flatten, or expand collections;
- resolve defs, game data, icons, roles, or labels;
- call `Text.CalcSize` or `Text.CalcHeight`;
- construct collections, snapshots, render models, or tooltip models;
- perform LINQ, reflection, boxing, interface-based enumeration, or capturing-lambda work;
- concatenate or format strings, translate labels, log, serialize, or access the filesystem;
- poll broad state, compute fingerprints, or use exceptions for normal control flow.

If a render path needs derived data, that data must be built behind an explicit invalidation gate and reused until a declared dependency changes.

## Snapshot and cache rules

- Game-derived render data must be published as immutable snapshots.
- Snapshot immutability is an ownership and publication guarantee, not a requirement to use immutable collection types or defensively copy mod-owned data.
- A buffer created exclusively for a snapshot may be transferred directly without copying or wrapping, provided mutable access does not escape and the buffer is never mutated after publication.
- Snapshots must not expose mutable collections owned by live authoritative models, the game, Unity, Verse, or other mods. When retained source data can change independently, project or copy only the fields required for rendering rather than cloning complete object graphs.
- Projecting authoritative state into a compact render artifact is not a defensive copy. Once published, that render artifact follows the snapshot ownership rules above.
- Stable externally owned assets such as textures may be referenced under their declared invalidation and lifecycle rules; the mod must not copy, mutate, destroy, or dispose them.
- Map-derived data must be keyed by map identity.
- World/store-derived data must be scoped by world/store identity.
- A process-static cache must reset or partition itself when its owning world, store, or map changes.
- Consumers of the same data must share one producer snapshot. Independent consumers must not rebuild equivalent snapshots.
- Snapshot identity is meaningful. If refreshed contents are equal, preserve the existing snapshot/reference identity.
- Cache builders may do expensive work only after their invalidation gate fires.
- Cache hits must not allocate delegates, closures, collections, strings, or wrapper objects.
- Delegates reachable from render or tick paths must be cached static delegates or otherwise proven allocation-free.
- Every cache must have bounded ownership and an explicit teardown/reset path.

### Required cache contract

Every new cache must document all of the following beside its declaration:

- **Owner:** world, map, window, model, or process.
- **Key:** the identity that partitions entries.
- **Value:** the cached artifact and whether it is immutable.
- **Dependencies:** the complete set of revisions, references, dimensions, preferences, and inputs that can change the value.
- **Refresh policy:** immediate, event-driven, or tick-throttled.
- **Equality policy:** when an equal rebuild must preserve identity.
- **Teardown:** how entries and owned resources are released.

If the dependency set cannot be named precisely, the cache must not be introduced.

## Invalidation and refresh rules

- Invalidate only for dependencies that the cached value actually consumes.
- Use the narrowest domain revision available. A catch-all version is forbidden when a domain-specific revision can express the dependency.
- No-op mutations must not advance any revision.
- Multi-domain mutations must report and bump only the domains they actually changed.
- Structural and user-authored configuration edits must become visible immediately, including while paused.
- An active tooltip display session is intentionally frozen: it must retain the content and geometry captured when the session began, even if those dependencies change. The changed dependencies must be observed when the tooltip is reopened or a different token starts a new display session.
- Correctness-sensitive invalidation must never be delayed to satisfy a throttle.
- Dynamic game-derived data such as resource counts must be tick-throttled.
- Timed refresh should never happen at intervals shorter than 204 game ticks.
- A new periodic game-data cache must use an explicitly named boundary or interval approved by the owner.
- Refresh scheduling must use game ticks, not render frames or wall-clock time.
- Rendering correctness must never depend on repaint frequency.
- Tick arithmetic must remain correct across pauses and must not trigger repeated refreshes at the same tick.

## Canonical cache dependency matrix

This section is mod-specific and maintained in AGENTS.md files for each specific mod.

Changes to these dependencies require updated behavioral tests in the same change.

## Text and layout measurement

- `Text.CalcSize` and `Text.CalcHeight` are allowed only inside an explicitly revision-gated cache builder.
- A text measurement cache key must include the text, font, width when wrapping is possible, and UI metric revision.
- UI scale and tiny-font preference changes must automatically advance the UI metric revision.
- Cached tooltip geometry must observe the UI metric revision when a display session begins; during that session it follows the frozen-session rule. Desired heights, label widths, and header measurements must observe the revision immediately.
- Definition- or language-dependent measurements must sit behind their revision gates (e.g. `LanguageChangeCoordinator.Revision`, `DefinitionReloadCoordinator.Revision`).
- Two consumers needing the same measurement must share the measurement cache instead of measuring independently.
- Window resizing may invalidate width-dependent measurements immediately. Unchanged widths must reuse cached measurements.
- A UI metric change may rebuild measurement-dependent geometry, but must not invalidate unrelated model or count snapshots.
- A declared font size is not a safe text rectangle. New text geometry, font
  changes, and any label with an effective-font fallback MUST use the active
  font's measured line height (rounded up) for visual bounds; reusing a
  fixed-height rectangle from another font is forbidden because it clips
  glyphs at some UI scales.
- Establish `Text.Font` before reading `Text.LineHeight`, `Text.CurFontStyle`, or
  any derived metric. Cache those metrics behind the UI metric revision; never
  measure them repeatedly in a steady render pass.
- Changing a font requires re-deriving every coupled dimension in the same
  cache: text width and height, row/parent footprint, adjacent icon or button
  size and alignment, hit geometry, and truncation/tooltip bounds. Reusing
  constants sized for the previous font is forbidden.
- Keep logical layout advance separate from visual text bounds. When a fallback
  font or fractional UI scale needs a taller line box, grow and re-anchor only
  the visual rectangle unless wrapped content truly requires the parent to
  grow. Intentional overlap with the following control must be preserved.
- Glyph-placement corrections MUST be explicit render offsets. Do not obtain an
  apparent text offset indirectly by moving neighboring controls or changing
  row pitch. Font-dependent offsets belong in shared text helpers when the rule
  is reusable across mods.
- Text requested as Tiny MUST use the shared `RimShared.UiLib.TinyText`
  helpers so the Tiny-to-Small fallback, descender room, and compact-caption
  offsets remain consistent. Direct `GameFont.Tiny` drawing is forbidden.

## Authoritative state and commands

- An appropriately named `ModStore` is authoritative per-save state.
- Only custom commands and deterministic store lifecycle code may mutate the shared model.
- Views, renderers, tooltips, dialogs, and Harmony patches must not mutate the model directly.
- UI interactions must issue a command and render the resulting published state.
- Every command must check whether the requested operation changed state before bumping revisions.
- Setters must normalize semantically equivalent values before comparing them.
- Complex mutations must return enough change information to invalidate exact domains.
- ID allocation and mutation order must be deterministic.

## Multiplayer determinism

- Every multiplayer-visible mutation must be a registered `[SyncMethod]` or be performed by deterministic load/setup code before play.
- A `[SyncMethod]` MUST declare no more than six parameters. Larger logical argument sets MUST travel as one plain payload object registered through an
  explicit field-by-field `[SyncWorker(shouldConstruct = true)]`.
- Synced method parameters must be primitive, stable, and serialization-safe unless an approved sync worker exists.
- Synced commands must not depend on local selection, current UI state, render order, wall-clock time, unordered enumeration, or unsynchronized randomness.
- All clients must produce identical model state and revision changes from the same command.
- Per-player presentation preferences must remain separate from authoritative shared state.

## RimWorld and Unity rules

- Treat `OnGUI` as a multi-pass hot path. Layout, repaint, and input passes must be idempotent.
- Authoritative state must not be mutated merely because `OnGUI` ran more than once.
- Unity, Verse, and RimWorld objects must be accessed only on the main thread unless the API explicitly documents otherwise.
- Every type declaring a static `Texture2D` (or other Unity asset) field MUST carry `[StaticConstructorOnStartup]`, and `ContentFinder<T>.Get` MUST run only from such a static constructor or later main-thread code. RimWorld initializes mod assemblies off the main thread and its startup scanner flags any static `Texture2D` field on an unmarked type, even lazily assigned ones. Prefer one shared `[StaticConstructorOnStartup]` texture holder per mod over scattering the attribute across UI classes.
- Background work may use only detached immutable data. It must not touch maps, defs, Unity objects, or mutable game models.
- Global GUI state must be restored after use, including `Text.Font`, `Text.Anchor`, `Text.WordWrap`, `GUI.color`, groups, clips, and generation scopes.
- Use `try/finally` when an exception could otherwise leave global UI state or ownership scopes unbalanced.
- Def lookup, category expansion, icon resolution, data flattening, row construction and similar preparation belong in cache/snapshot builders.
- Missing worlds, maps, defs, categories, and unloaded content must be handled without leaking stale state from another save.
- Static state must not assume `Find.World` or `Find.CurrentMap` remains stable.
- Logging in render or repeated tick code is forbidden unless explicitly rate-limited.

## Harmony integration

- Patches must do the minimum work required at the patch boundary.
- A patch that replaces vanilla behavior must preserve an explicit compatibility/escape hatch where practical.
- Prefix return behavior must be obvious and tested when it suppresses the original method.
- Patches must not swallow broad exceptions or silently leave partially updated state.
- Patch code must delegate substantial logic to ordinary testable code.

## Persistence and migrations

- Save/load code must remain backward-compatible with existing saves unless the owner explicitly approves a breaking migration.
- Cleanup, migration, and default seeding must be deterministic for the same save data and installed defs.
- Load-time normalization must finish before publishing cache revisions.
- Do not serialize render caches, transient UI state, resolved defs, or derived snapshots.
- Import/export and filesystem work must occur only from explicit user actions or cached background-safe workflows, never from rendering loops.
- Failed parsing or missing content must not leave a partially applied authoritative model.

## Lifecycle and teardown

- Every component that acquires resources, subscriptions, registrations, or ownership must provide explicit teardown.
- Window close, world unload, map removal, and mod shutdown paths must release applicable tooltip owners, event handlers, disposable resources, temporary Unity objects, and obsolete cache entries.
- Per-map caches must not keep removed maps alive.
- Per-world caches must release the prior world/store when ownership changes.
- Streams and other `IDisposable` objects must use deterministic disposal.
- Unity objects created by this mod must be destroyed or released through the correct Unity lifecycle when no longer needed.
- Never destroy, dispose, or mutate assets owned by RimWorld, Unity, another mod, or a shared content pack.
- Teardown must be idempotent and safe after partial initialization.

## Hot-path implementation details

- Prefer arrays, indexed `List<T>` access, immutable snapshots, and reference/version comparisons.
- Do not use LINQ or allocate enumerators in render, tooltip, or repeated tick paths.
- Do not create method-group delegates at call sites compiled under C# versions that do not cache them. Store reusable delegates in `static readonly` fields.
- Do not use render-frame counters to schedule game-state refreshes.
- Do not rebuild data merely to calculate a fingerprint. Compare exact immutable contents when identity stability matters.
- Avoid dictionary lookups inside inner draw loops when parallel arrays or resolved draw models can carry the value.
- Expensive or failure-prone operations must be moved outside the hot path, cached, and surfaced through explicit state.

## Performance architecture guidelines

- The unchanged steady state should be the primary design target. If the UI is
  pixel-identical across repaints, render it from one cached composite surface
  instead of repeating per-group, per-cell, icon, and text draw work.
- Partition cached rendering by actual invalidation domain. Stable decoration
  and icons, count-dependent text, layout, and the final visible frame should
  not invalidate one another unless a declared dependency crosses that boundary.
- Publish expensive state in stages: refresh the data snapshot, build an
  inactive render buffer later, then atomically swap it on repaint. Continue
  presenting the previous front buffer until the replacement is complete.
- A changed count snapshot does not necessarily imply changed layout. Rebuild
  layout only when count-dependent visibility or another structural input
  changes; otherwise update only the count/text artifact and final frame.
- Match refresh cadence to volatility and cost. Ordinary resource snapshots may
  use the standard 204-tick boundary; slower planned-work or broad map scans
  should use a longer approved boundary such as 1020 ticks or event-driven
  invalidation when reliable lifecycle notifications exist.
- Resolve defs, textures, colors, labels, formatted strings, hit geometry, and
  other repeated lookup results into parallel render data before drawing.
  Steady rendering should perform indexed reads and submit the minimum draw calls.
- Perform hover highlights, tooltip registration, and tooltip model/layout work
  only for the currently eligible hovered target. Hover state must not force
  unrelated stable surfaces or geometry to rebuild.
- Measure padded icon artwork once per icon and display-metric epoch. Cache the
  correction, include the game's native icon draw scale, and remeasure only
  after UI scale or relevant physical display metrics change.
- Apply visibility gates before acquiring snapshots or touching caches. A hidden
  panel, non-render event, or obscuring game state should take the shortest safe
  path while preserving the intended vanilla-suppression behavior.

## Rendering failures: causes and reusable fixes

- **Incorrect alpha contract:** Mixing straight-alpha textures with
  premultiplied intermediate surfaces made cached elements too bright or dark.
  Define the alpha representation at every stage, convert only at explicit
  publication boundaries, and validate the complete blend round-trip with a
  known translucent pixel.
- **Mixed coordinate spaces:** Generated font vertices already reflected the
  text generator's scale, but scaling their offsets again produced oversized,
  smeared, or clipped text. Scale the logical origin exactly once and preserve
  generated offsets in their documented coordinate space.
- **Fractional cached glyph origins:** Centered strings could land on fractional
  physical pixels, so some values blurred while similar values remained sharp.
  Snap the whole glyph-run origin to the physical pixel grid before rasterizing;
  never snap individual glyphs and disturb spacing.
- **Implicit final resampling:** A texture sized with `ceil(logical * scale)`
  was presented into the unrounded logical extent. The resulting tiny rescale
  changed sampling phase when hover changed panel width. Present cached textures
  at `physicalSize / rasterScale` so every texture pixel maps one-to-one.
- **Premature front-buffer invalidation:** Releasing or resizing the visible
  buffer while a hover/count replacement was being built caused blanks and
  flicker. Retain the old front buffer, build an independent back buffer, and
  swap only after successful completion; each buffer retains its own dimensions.
- **Unstable geometry dependencies:** Header/search width accidentally followed
  hover-expanded content width. Keep stable controls keyed to their configured
  geometry and let transient content expansion affect only its own surface.
- **Broken icon coverage math:** Integer division collapsed partial alpha bounds
  to zero and silently disabled normalization. Use floating-point coverage,
  combine it with native draw scale, and invalidate the stable icon surface when
  an asynchronous measurement publishes.
- **Incorrect generated-mesh assumptions:** Dropping a presumed sentinel or
  terminal quad removed real glyphs and clipped final characters. Derive usable
  geometry from the API's actual vertex contract and verify first and last glyphs.
- **Leaked or stale GUI state:** Reading anchor/style state before changing fonts
  reused values belonging to another font and made results order-dependent.
  Establish font/style first, then read derived state, and restore all global GUI
  state through balanced scopes.
- Cached rendering must be checked against a direct-render fidelity reference.
  Use exact-window captures and pixel comparisons for stable, hovered, and
  enter/leave states; visual similarity at a glance is insufficient for
  subpixel movement, clipping, alpha, and sampling regressions.

## Required testing

For behavior that can reasonably be verified at an automated executable
boundary, bug fixes and behavior changes must begin with a failing regression
test that fails for the intended reason. Runtime-only RimWorld or Unity
behavior may instead use a documented targeted reproduction before the fix and
manual verification afterward. Do not introduce production seams or
source-text tests solely to satisfy this requirement.

Test count is not a goal and must never be used as evidence of behavioral
quality. A smaller scenario test that exposes the complete interaction is
preferred over many isolated tests that merely reproduce implementation
steps.

Tests must not:
- mirror the production algorithm or assert its mutation sequence;
- turn temporary internal types, enum members, collection shapes, or stage
  boundaries into behavioral contracts;
- assert intermediate state when the same rule can be verified through the
  published result;
- use a simplified fixture that omits interactions central to the behavior
  under test, such as coverage, automatic roles, training-path bands, real
  demand scales, or required skills;
- generate expected values from the implementation and accept them without
  human review;
- add one test per mechanical branch when a single end-to-end scenario makes
  the intended distinctions reviewable.

A focused internal test is appropriate only when the invariant has no stable
observable boundary, or when it protects an independently meaningful safety,
determinism, cache, codec, or lifecycle contract. Such a test must state why
the published behavior cannot prove the invariant.

Cache tests must prove, where applicable:

- repeated reads reuse the cached value or object identity;
- the relevant dependency rebuilds the value;
- unrelated dependency changes do not rebuild it;
- no-op mutations preserve revisions and identity;
- separate maps and worlds do not share mutable or stale data;
- the tick immediately before the refresh boundary reuses data;
- the configured refresh tick rebuilds data;
- equal refreshed contents preserve identity;
- structural edits update immediately while paused;
- an active tooltip display session remains unchanged across dependency changes, and reopening it observes those changes;
- language and definition reloads invalidate measurement-dependent geometry;
- width changes invalidate wrapped measurements without invalidating unrelated data;
- teardown removes owned registrations, resources, and obsolete entries safely.

Tests must assert observable behavior. Seeded or generated fixtures must model
every relevant input faithfully, and their expected outputs must remain easy
for a human to review. Source-text tests are not allowed. 

## Automated in-game testing

- The canonical automation runtime is the single shared profile at
  `D:\Code\RimWorld\AutomationProfiles\Shared`. Shared lifecycle, input, and
  capture commands live in `D:\Code\RimWorld\Shared\tools\automation`.
  Per-mod or date-stamped profiles and copies of these commands are forbidden;
  mod-specific automation may contain only navigation and assertions that call
  the shared commands.
- Before a clean launch, run `refresh-profile.ps1`. It copies the newest
  `Fisso-NAM*.rws` from the player's save directory as a read-only source into
  the canonical repository save and the shared profile's `Saves\Autostart.rws`,
  verifies SHA-256 equality, and verifies that `ModsConfig.xml` exactly matches
  the save's ordered mod list. `refresh-profile.ps1 -ModSet <name>` then
  layers the installed extra mods listed in `modsets\<name>.txt` into the
  profile's copy at named anchors (an order-preserving superset, which the
  dev-mode autostart loader accepts with a logged mismatch only); a plain
  refresh restores the exact list. Mod sets are the only sanctioned way to
  test with mods the save does not carry.
- The shared preference baseline is windowed 1920x1080 at UI scale 1.25, paused
  on load, with `adaptiveTrainingEnabled=False` and `runInBackground=True`.
  Change a copy only for a test that explicitly exercises another display
  metric, then restore it with `refresh-profile.ps1`.
- Run automation against a disposable profile passed through
  `RimWorldWin64.exe -savedatafolder=<isolated-profile>`. Never modify or launch
  automation against the player's real RimWorld profile, preferences, mod list,
  or saves.
- Seed the isolated profile with only the required inputs: copy the intended
  save as `Saves/Autostart.rws`, and provide isolated preferences and a mod
  configuration compatible with that save. Treat the source save as read-only.
- Build, deploy, and restart the game before testing. A successful build does
  not update the installed mod, and an already-running game retains its loaded
  assemblies.
- Before focusing, driving, closing, or restarting RimWorld, identify the test
  process by its full command line and exact isolated-profile path. Require at
  most one match and never act on unrelated RimWorld processes.
- The disposable game process must remain open only while input, capture, or
  runtime assertions are actively in progress. Stop the exact shared-profile
  process immediately after the verification run; do not leave it running
  during code investigation, builds, result analysis, or user handoff.
- Prefer condition-based startup detection: poll the isolated run's
  `Player.log` for save-load completion, mod errors, or an explicit test marker.
  Fixed sleeps may be a bounded fallback but must not be the only readiness
  check.
- Send input only after focusing the verified test window. Preserve and restore
  the prior foreground window and cursor position, and account for the
  difference between client coordinates and the outer window rectangle.
- Make visual scenes deterministic before comparing them: pause simulation when
  possible, keep camera and UI scale fixed, and capture only the exact game
  window rather than the desktop or unrelated applications.
- Exercise state transitions, not only steady screenshots. Applicable cases
  include initial load, stable repaint, hover enter/leave, tooltip delay,
  count publication, buffer swap, menu visibility, scrolling, resolution, and
  UI-scale changes.
- Compare unchanged regions pixel-for-pixel across transitions. For cached or
  replacement renderers, retain a direct-render reference and inspect text
  bounds, final glyphs, icon coverage, alpha, sampling phase, and control
  geometry—not merely overall visual similarity.
- Runtime diagnostics must be narrowly targeted to the failing boundary. After
  verification, restore the clean build, redeploy it, restart the isolated
  game, confirm the fresh log contains no relevant errors, and verify the
  installed assembly hash matches the tested build artifact.

## Definition of done

A change is not complete until all applicable items are true:

- New cache dependencies and teardown behavior are documented beside the cache.
- Applicable regression tests were observed failing before the production fix. Runtime-only behavior has documented reproduction and verification results.
- Relevant focused tests pass.
- The complete repository test suite passes.
- The repository builds with zero warnings and zero errors.
- Remaining `Text.CalcSize` and `Text.CalcHeight` calls are confirmed to be behind measurement caches.
- Render and repeated tick paths were reviewed for allocations, LINQ, model traversal, def lookup, translation, logging, string creation, and hidden delegate creation.
- Cache invalidations were reviewed for both stale-data risk and unnecessary rebuilds.
- Multiplayer-visible mutations were reviewed for deterministic behavior.
- Resource ownership and teardown were reviewed.

Canonical verification commands:

```powershell
dotnet build -c Release --no-restore
dotnet test src/RimMod.Core.Tests --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.

## Exceptions

Exceptions are rare and fail-closed. Before implementation, provide the owner with:

1. The exact rule that would be violated.
2. Why a compliant implementation is not practical.
3. The measured or bounded correctness, performance, multiplayer, and lifecycle impact.
4. The narrowest proposed exception.
5. Tests or instrumentation that will prevent the exception from expanding silently.

The exception is not approved until the owner explicitly accepts it. “Small,” “infrequent,” “temporary,” or “probably harmless” is not sufficient justification.
