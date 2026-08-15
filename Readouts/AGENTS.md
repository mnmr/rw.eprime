# EPrimeReadouts Engineering Contract

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
- `mod/Textures/EPrimeReadouts/ModIcon.png`: 256x256.
- `mod/About/Preview.png`: 1280x720.
- Regenerate only via `assets/workshop/export-assets.ps1`.

## Project boundaries

- `src/EPrimeReadouts.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/EPrimeReadouts` owns game integration, persistence, patches, rendering, and UI.
- `src/EPrimeReadouts.Core.Tests` owns executable behavioral and regression tests.
- Pure caching, revision, layout, codec, and state-transition behavior should live in Core so it can be tested without the game runtime.

## Non-negotiable render-path rule

All UI and rendering must operate on cached snapshot data (immutable by either design or specification).

A steady render pass may:

- compare versions, references, identities, dimensions, and input state;
- perform bounded indexed iteration over already-built render data;
- submit draw calls;
- process the current input event and enqueue an authoritative command.

A steady render pass must not:

- traverse authoritative models to derive display data;
- aggregate resource counts or rebuild layouts;
- sort, filter, group, flatten, or expand collections;
- resolve defs, categories, icons, pool members, or labels;
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
- The canonical resource-count refresh interval is 204 game ticks.
- A new periodic game-data cache must use an explicitly named interval in the approved 200–500 game-tick range unless the owner approves another interval.
- Refresh scheduling must use game ticks, not render frames or wall-clock time.
- Rendering correctness must never depend on repaint frequency.
- Tick arithmetic must remain correct across pauses and must not trigger repeated refreshes at the same tick.

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

## Text and layout measurement

- `Text.CalcSize` and `Text.CalcHeight` are allowed only inside an explicitly revision-gated cache builder.
- A text measurement cache key must include the text, font, width when wrapping is possible, and UI metric revision.
- UI scale and tiny-font preference changes must automatically advance the UI metric revision.
- Cached tooltip geometry must observe the UI metric revision when a display session begins; during that session it follows the frozen-session rule. Desired heights, label widths, and header measurements must observe the revision immediately.
- Two consumers needing the same measurement must share the measurement cache instead of measuring independently.
- Window resizing may invalidate width-dependent measurements immediately. Unchanged widths must reuse cached measurements.
- A UI metric change may rebuild measurement-dependent geometry, but must not invalidate unrelated model or count snapshots.

## Authoritative state and commands

- `ReadoutStore` is authoritative per-save state.
- Only `ReadoutCommands` and deterministic store lifecycle code may mutate the shared model.
- Views, renderers, tooltips, dialogs, and Harmony patches must not mutate the model directly.
- UI interactions must issue a command and render the resulting published state.
- Every command must check whether the requested operation changed state before bumping revisions.
- Setters must normalize semantically equivalent values before comparing them.
- Complex mutations must return enough change information to invalidate exact domains.
- ID allocation and mutation order must be deterministic.

## Multiplayer determinism

- Every multiplayer-visible mutation must be a registered `[SyncMethod]` or be performed by deterministic load/setup code before play.
- Synced method parameters must be primitive, stable, and serialization-safe unless an approved sync worker exists.
- Synced commands must not depend on local selection, current UI state, render order, wall-clock time, unordered enumeration, or unsynchronized randomness.
- All clients must produce identical model state and revision changes from the same command.
- Per-player presentation preferences must remain separate from authoritative shared state.

## RimWorld and Unity rules

- Treat `OnGUI` as a multi-pass hot path. Layout, repaint, and input passes must be idempotent.
- Authoritative state must not be mutated merely because `OnGUI` ran more than once.
- Unity, Verse, and RimWorld objects must be accessed only on the main thread unless the API explicitly documents otherwise.
- Background work may use only detached immutable data. It must not touch maps, defs, Unity objects, or mutable game models.
- Global GUI state must be restored after use, including `Text.Font`, `Text.Anchor`, `Text.WordWrap`, `GUI.color`, groups, clips, and generation scopes.
- Use `try/finally` when an exception could otherwise leave global UI state or ownership scopes unbalanced.
- Def lookup, category expansion, icon resolution, resource-tree flattening, and row construction belong in cache/snapshot builders.
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

## Required testing

For behavior that can reasonably be verified at an automated executable
boundary, bug fixes and behavior changes must begin with a failing regression
test that fails for the intended reason. Runtime-only RimWorld or Unity
behavior may instead use a documented targeted reproduction before the fix and
manual verification afterward. Do not introduce production seams or
source-text tests solely to satisfy this requirement.

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
- UI metric changes invalidate measurement-dependent geometry;
- width changes invalidate wrapped measurements without invalidating unrelated data;
- teardown removes owned registrations, resources, and obsolete entries safely.

Tests must assert observable behavior. Source-text tests are allowed only when no executable boundary can reasonably verify an architectural requirement.

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
dotnet build -c Release src\EPrimeReadouts.sln --no-restore
dotnet test src\EPrimeReadouts.sln --no-restore
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
