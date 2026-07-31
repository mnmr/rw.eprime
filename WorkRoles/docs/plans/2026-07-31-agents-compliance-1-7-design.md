# AGENTS Compliance Findings 1-7 Design

## Status

Approved design for correcting audit findings 1 through 7 in the specifically cited production paths. Finding 8, the existing-cache documentation backfill, remains a separate final phase. New caches introduced by findings 1 through 7 must still receive the complete cache contract when declared.

## Scope

The implementation is limited to the paths cited by the audit and the smallest supporting types or direct callees required to test and implement those corrections. It will not perform a repository-wide render/cache cleanup.

The seven findings are:

1. Render methods read live pawn, role, assignment, and store state instead of published render data, especially `ColonistsTabView.DrawColonistCell`, `ColonistsTabView.DrawChipStrip`, `RolesTabView.DrawEditor`, and their direct helpers.
2. `ColonyScope.Locations` and `RoleListSnapshot` expose mutable collections or live-model collections across the snapshot boundary.
3. The cited steady draw paths perform translation, truncation, formatting, or `Text.CalcSize`/`Text.CalcHeight` work in `RolesTabView`, `Dialog_ExportPreview`, `Dialog_RoleFilePicker`, and `MainTabWindow_WorkRoles`.
4. The cited `RoleCommands` mutations, including recommendation ordering and training-path color/band updates, advance revisions for semantically unchanged requests.
5. `RoleIO.Apply` mutates `RoleStore` directly instead of applying the parsed result through the authoritative command boundary.
6. `OptionsTabView.DrawHelpParagraph` can leak global GUI state.
7. Mod-created textures, including the colonist fade texture and `WorkRolesTex.Circle`, do not have an explicit, idempotent teardown path.

Existing user changes in the worktree are not part of this design and must be preserved. Overlapping edits must be integrated rather than replaced.

## Design principles

### Ownership-frozen snapshots

Snapshot immutability is enforced through ownership and publication, not by requiring immutable collection libraries or defensive duplicates.

- A producer may allocate and populate an array or concrete list, transfer exclusive ownership to the snapshot, and publish it without another copy or read-only wrapper.
- After publication, no mutable reference to the buffer may escape and the producer must not mutate it.
- Data retained from live authoritative models or externally owned mutable game/mod objects is projected into the smallest render fields needed by the consumer.
- A render snapshot may retain stable identities needed to issue commands, but draw code must not use those identities to derive display state from live models.
- Stable externally owned assets, such as textures resolved by a gated builder, may be referenced. They are not copied, mutated, destroyed, or disposed by WorkRoles.
- Builders produce flat, indexed render data and avoid copying complete role, pawn, path, or location graphs.
- Equal refreshed contents preserve the existing snapshot reference. Cache hits allocate nothing.

Projection from `RoleStore` into compact draw fields is required separation between authoritative state and rendering; it is not a defensive copy of mod-owned data. Snapshot-owned buffers are not copied again merely to obtain an immutable type.

### Preserve invalidation semantics

The change will reuse existing domain gates and add only dependencies actually consumed by a new render artifact.

- External colonist facts continue to refresh at the window's Repaint boundary and only when `ExternalPawnFacts.Revisions` reports a required refresh.
- After a successful external generation refresh, only downstream consumers of that generation are invalidated, preserving the current ordering that prevents a pre-refresh result from carrying a new stamp.
- Role and assignment presentation continues to observe its existing authoritative revisions. Scope, map, location, language, definition, activity, dimensions, and local disclosure/filter state are separate dependencies where consumed.
- Structural and user-authored commands become visible on the next valid GUI pass while paused. The current pass finishes against one published snapshot rather than switching data midway through indexed iteration.
- Fixed-tick and timezone-crossing invalidation remains event-driven. No render-frame polling, fingerprints, or wall-clock scheduling is introduced.
- Narrow revision sources are preferred. `UiVersion.Current` is used only where it is already the canonical dependency or no narrower approved domain revision represents the consumed state.

## Finding 1: render snapshot retrofit

### Colonist label cell and assignment chips

The existing window-owned colonist producers will publish a compact per-pawn render record consumed by the cited cell and chip-strip paths. The record carries only pre-resolved draw and command data, such as pawn identity, displayed name and color, portrait reference/parameters, tooltip handle, clipboard availability/presentation, chip geometry, role text/style flags, capability warning, activity outline, and stable pawn/role IDs for commands.

The exact record will be split or nested where dependencies differ so an activity change does not rebuild unrelated portrait/name data and a width change does not recapture external pawn facts. Existing `ColonistStatsState`, roster, capability, activity, tooltip, rule-outcome, and chip-layout producers remain shared sources; the implementation composes their published results instead of independently rebuilding equivalent data.

`DrawColonistCell` and `DrawChipStrip` will perform indexed iteration over the published records, submit draw calls, test the current input event, and enqueue `RoleCommands`. They will not resolve a role from `RoleStore`, traverse `pawnSets`, read mutable assignments, derive labels/rules/capabilities, or build tooltip/layout data on a steady hit.

Input that opens menus or dialogs will capture stable primitive IDs or an already-published display value. Any menu/dialog collection construction occurs only in response to the matching input event, never during steady Layout or Repaint passes.

### Selected-role editor

The selected-role editor will consume a view-owned render snapshot keyed by store/world identity, selected role ID, exact authoritative revisions, language/definition revisions, available dimensions, and editor-local disclosure/filter state that affects its contents.

The snapshot contains pre-resolved title/group/assignment/skills/rule/tuning/job-tree/entry-row text and geometry, colors, state flags, stable command IDs, and resolved assets. Producer-owned buffers are published directly and then frozen by specification. Existing list, editor-state, job-tree, and entry producers remain shared where they already own equivalent data.

The steady editor pass uses only the snapshot and current input state. Commands continue to mutate through `RoleCommands`; the editor never mutates a role or store directly. Deferred operations keep their current clean-frame behavior.

## Finding 2: collection ownership boundaries

`ColonyScope.Locations` will publish a collection whose backing storage is owned by the producer generation and cannot be mutated by a live model or consumer. If the current builder already exclusively owns its list, ownership is transferred directly rather than defensively copied; otherwise it is projected once behind the location invalidation gate. The cache remains map/world partitioned and releases prior ownership on lifecycle changes.

`RoleListSnapshot` will own its display-row and filtered-result buffers. Constructor inputs that are already exclusively built for that snapshot are adopted directly; mutable aliases are removed. Consumers receive concrete indexed access without a per-read wrapper allocation. Equal rebuilds preserve the previous snapshot identity.

## Finding 3: text and layout measurement

Each cited draw-time translation, formatting, truncation, and text measurement moves into an existing producer or a narrowly keyed view/dialog cache builder.

- `(font, text)` unwrapped width uses `WrText.FitWidth`.
- Wrapped measurements include text, font, and available width.
- Language-dependent artifacts include `LanguageChangeCoordinator.Revision`.
- Definition-dependent artifacts include `DefinitionReloadCoordinator.Revision` where applicable.
- Width changes invalidate only width-dependent geometry.
- Equal dimensions and revisions reuse cached strings and geometry.

No unrelated model snapshot is invalidated by language, definition, or width changes. Dialog/window close clears view-owned text artifacts.

## Finding 4: no-op command revisions

Each cited command will normalize the requested value into its stored semantic form before comparison. It will return without mutating or advancing any revision when the normalized current and requested values are equivalent.

Multi-field operations compute exact change information first, apply only changed fields, and bump only the domains actually changed. Ordering mutations compare the final deterministic order rather than the request representation. Array/band/color comparisons are element/value based and do not rely on reference inequality.

## Finding 5: import command boundary

Parsing and validation remain detached from authoritative state. `RoleIO.Apply` will produce a complete normalized import result without partially mutating `RoleStore`. A `RoleCommands` entry point will apply that result deterministically after validation succeeds.

The command will use stable serialization-safe inputs for multiplayer-visible state, preserve existing IDs/migration behavior, report exact changed domains, and avoid revision bumps for an import equivalent to current state. A failed parse, missing content, or failed validation leaves the store untouched.

## Finding 6: GUI state restoration

`OptionsTabView.DrawHelpParagraph` will save every global GUI value it changes and restore those values in `finally`. Restoration will use the captured prior values rather than assumed defaults so nested callers remain correct. Group/clip or ownership scopes, if touched by the cited helper, receive the same balanced treatment.

## Finding 7: texture lifecycle

Mod-created textures receive explicit ownership and idempotent release methods. Release destroys only textures created by WorkRoles, clears their static/view references, and is wired into the appropriate window/mod shutdown lifecycle. Repeated release and partial initialization are safe.

Externally owned textures resolved from RimWorld, Unity, Verse, or content packs are referenced only; they are never destroyed, disposed, or mutated.

## Error handling and compatibility

- Missing worlds, maps, stores, pawns, roles, definitions, and unloaded content publish an empty or unavailable render state scoped to the current owner; stale prior-save data is never reused.
- Builders fail before publication. The previously published snapshot remains intact unless its owner has changed, in which case the cache is cleared fail-closed.
- No broad exception swallowing is introduced.
- Existing Harmony escape hatches, persistence compatibility, multiplayer registration, command ordering, and deterministic ID behavior are preserved.

## Test strategy

Every production correction begins with a regression test observed failing for the intended reason.

Applicable cache tests prove:

- repeated reads reuse the same buffer/snapshot identity;
- the relevant dependency rebuilds and unrelated dependencies do not;
- equal refreshes preserve identity;
- no-op commands preserve model state, domain revisions, and snapshot identity;
- separate maps/worlds/stores do not share mutable or stale data;
- the Repaint refresh boundary installs external colonist changes once;
- the preceding pass reuses the existing generation;
- structural edits update while paused;
- language/definition and width changes invalidate only affected text geometry;
- teardown releases only owned textures/resources and is idempotent;
- import validation failure is atomic and a successful import goes through the command boundary.

Executable behavioral tests are preferred. Source-text architecture tests are used only where there is no reasonable executable boundary, such as proving the absence of a forbidden direct production call in a Unity-only render method.

## Verification

Focused tests are run after each red/green change. Completion requires:

```powershell
dotnet build -c Release
dotnet test tests/WorkRoles.Core.Tests
```

The final review also inspects the cited render/repeated-tick paths for live-model traversal, collection construction, LINQ, translation/formatting, `Text.CalcSize`/`Text.CalcHeight`, logging, boxing/interface enumeration, and hidden delegate creation. Invalidation is reviewed for both stale-data risk and unnecessary rebuilds; multiplayer determinism and ownership teardown are reviewed explicitly.

## Deferred finding 8

After findings 1 through 7 are complete and verified, finding 8 will backfill the complete Owner/Key/Value/Dependencies/Refresh/Equality/Teardown contract beside existing cache declarations in the agreed scope. This deferral does not permit a new cache added earlier to omit its contract.
