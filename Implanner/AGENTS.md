# Implanner Engineering Contract

The general contract in the repository root `AGENTS.md` (one level up) applies
in full. This file records only what is specific to Implanner: project names,
canonical cache dependencies, approved refresh boundaries, and verification
commands. Where this file is silent, the root contract governs.

## Scope

Implanner plans, ranks, and automates colonist implants only. Weapons,
equipment, and utility gear are out of scope: they are never planned, tracked,
ranked, or counted toward progress. The overview table may display a
colonist's equipped weapon and belt item, but that display is observational —
no Implanner logic reads or reacts to it.

Implant star rankings are manual player choices: every implant sits at the
three-star default until the player drags it, and the mod never derives or
overwrites a ranking. Within a tier the player arranges an explicit order
(`PlannerModel.ImplantOrder`; unordered kinds sort after ordered ones by
defName). The `MoveImplantRank` command materializes the target tier's full
sequence from the catalog (membership by stars, ordered by position then
defName — language-independent), so every client applies the identical
order. Tier + position drive production dispatch ordering.

Implant reservations (`PlannerModel.ImplantReserves`, Surgery domain) hold
stock back for manual use: surgery allocation only reserves items while at
least the configured count of the implant's item stays available, releases
excess holdings (newest item id first) when stock shrinks or the reserve
grows, and production does not count held-back items as available stock.

A plan may extend another plan (`Plan.BasePlanId`, chosen at creation): its
effective goals are its own plus the base chain's, with own selections
overriding overlapping slots and suppressing conflicting inherited slots
(`PlannerModel.EffectiveImplants`). Goal identity is natural — the owning
plan id plus the implant kind (one goal per kind per plan by
construction), key format `p{planId}:{defName}:{ordinal}` — so goal keys
need no allocation, cannot collide, and removing and re-adding the same
pick reproduces the same identity; an inherited goal keeps its base
plan's id. Loading migrates retired `i{goalId}:{ordinal}` keys via the
legacy per-goal ids still present in old saves. Deleting a base plan
detaches its children.

Implant-combination validity is derived from definition data only, mirroring
the game's surgery workers (verified in RimWorld source): one part per slot
(replacements occupy it and implants cannot mount on added parts),
replacements clear their subtree, and `incompatibleWithHediffTags` versus
`HediffDef.tags` excludes same-part implants (skin glands). The pure rules
live in `Implanner.Core.ImplantConflictRules`; `ImplantConflicts` extracts
the facts from the catalog and injects `PlannerModel.SlotConflictResolver`.
Evaluation substitution follows the same data: an installed implant
satisfies another kind's goal slot only when it actually EXCLUDES
installing the requested implant there (artificial-part occupancy or tag
conflict, `ImplantConflicts.SameSlotExclusive`) and meets the efficiency
floor — a manually installed archotech leg satisfies a bionic-leg goal,
while coexisting same-part implants (brain implants) never satisfy each
other's goals.
The click IS the choice: selecting a conflicting slot deselects an own
blocker inside the synced command (`SetImplantSlot`) and suppresses an
inherited one in `EffectiveImplants` — the editor annotates such slots with
"overrides X" rather than disabling them. Mutant-only implants (ghoul kit)
are excluded from the catalog: only ordinary humanlikes are plannable.

## Project boundaries

- `src/Implanner.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/Implanner` owns game integration, persistence, patches, rendering, and UI.
- `src/Implanner.Core.Tests` owns executable behavioral and regression tests.
- Shared source from `..\Shared\Common` (namespace `RimShared.Common`) is compiled into `Implanner.Core` via `$(RimSharedRoot)`; it is never shipped as a separate assembly.
- Shared game-side UI source from `..\Shared\UiLib` (namespace `RimShared.UiLib`, may reference Verse/Unity) is compiled into `Implanner` the same way; it must never be included in `Implanner.Core`.

## Canonical refresh boundaries

- Time-driven invalidation must fire on computed game-tick boundaries, never on per-frame or per-tick polling.
- The approved boundary for automatic doctor-skill-floor evaluation is **1020 game ticks**.
- The approved boundary for resource-gated production dispatch (`PlannerProduction`) is **1020 game ticks**, plus an early dispatch on the pass after a production-domain mutation (the store's scribed `PendingProductionPass` flag).
- The reservation/surgery reconciliation pass (`PlannerReconciler`) runs on the owner-approved **1020-game-tick** boundary (owner, 2026-08-31: all Implanner periodic work shares the 1020-tick cadence), plus a pass on the next simulated tick after any synced store mutation (the store's scribed `PendingReconcile` flag; ticks do not advance while paused, so a paused edit reconciles on the first tick after unpausing). Both triggers derive only from synced state (tick arithmetic and save-carried flags), so a late-joining or resynced multiplayer client never runs a pass the host does not. Each pass begins with `PlannerModel.CleanupMissing`. At most one pass per tick.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Map classifications (`ColonyScope.mapClassifications`) | Grav-engine spawn/despawn/holder transfer, settlement ownership flips, map settle, map add/remove (the `Patch_LocationTransitions` events); publishes `LocationRevision`; teardown via `ReleaseSnapshot` |
| Location snapshots (`ColonyScope.locationSnapshots`) | `LocationRevision`, map-set membership, faction identity, language; equal rebuilds preserve snapshot identity |
| Floor-map canonicalization (`FloorMaps.cache`) | Hash of live map ids; `ReleaseForTeardown` on map-set invalidation and world teardown |
| Implant catalog (`Catalogs.implants`) | Loaded definition set (static per session) + `UiVersion.LanguageCurrent`; `Release` on world teardown |
| Implant conflict facts (`ImplantConflicts.facts`) | Loaded definition set (static per session; label-free, so deliberately not language-gated); `Release` via `Catalogs.Release` |
| Production recipes (`PlannerProduction.productionRecipes`) | Loaded definition set (static per session); entries never change; `Reset` on world teardown (defensive) |
| Recipe bench users (`PlannerProduction.recipeUsers`) | Loaded definition set (static per session); entries never change; `Reset` on world teardown (defensive) |
| Overview data (`OverviewState`, `OverviewData`) | `UiVersion.Current`, store identity + `Version` (plans/base links, assignments, priorities, reservations, rankings, surgery, options — the strip's surgery batch line derives the single next colonist and their batch tier from the reconciler's dispatch order; the State column derives Waiting/Preparing/Operating/Done from the reservation and owned-operation-bill bookkeeping), `ExternalPawnFacts.Revision`, `ColonyScope.LocationRevision`, grouping selection (kind + location id; the current map is read only when the selection must be revalidated, never as a key); colonist-bar order observed at rebuild; the informational Shooting/Melee columns are sampled at rebuild by design (no skill seam — the window shows the data collected when a dependency moved or it opened); the strip's production column samples item stock (with per-item surgery reservations split out of the free-stock count), in-scope bench bills, and ingredient resource counts at rebuild the same way, ranking and reserve-blocking by `PlannerProduction`'s own dispatch rules (bill creation/removal bumps `Version`, so automation activity refreshes it promptly); the strip's batch and automation-chip text widths are measured at rebuild; effective goals memoized per plan within one build as a private copy (the model's own goal list never enters a snapshot); rebuilt from `WindowUpdate` (the draw pass rebuilds only as a fallback for a tab switched mid-frame, and detail rows rebuild behind their own gate); window-owned, released on close |
| Overview ordering (`OverviewSnapshot`) | Overview data identity, group-by key, sort column/direction/name-order; a pure re-sort/re-group sharing the data's `OverviewRow` instances (new snapshot object per ordering change); no game state read; window-owned, released on close |
| Strip column tooltips (`StripTipSource`, one per column) | Overview data identity (the data carries the per-item production rows and per-kind surgery rows, built by `OverviewState` from the same stock, bill, reservation, and evaluation scans as the strip texts; `Implanner.Core.StripBreakdown` does the pipeline partition and dispatch ordering); the tip model is assembled only when a hover session opens and frozen for that session by `StructuredTipPresenter`; window-owned, released on close |
| Automation snapshot (`AutomationState`) | `UiVersion.Current`, store identity + `OptionsVersion` + `SurgeryVersion` (implant reserves live in the Surgery domain) + `ProductionVersion`; carries the master switch, iteration, doctor-floor/hospitalized and production flags so the tab never reads the live model; reserve-row set derives from the baseline-reserve table and the implant catalog; reserve edit buffers are arrays parallel to the rows, reseeded on rebuild from the durable per-key dictionary; window-owned, released on close |
| Automation tooltip holder (`AutomationTips`) | `UiVersion.Current` (the `WrTips` registry clears on it); window-owned, released on close |
| Colonist detail rows (`OverviewState.detailRows`) | Overview data identity + selected pawn id + panel body width (the header sentence wraps, so its height is measured at rebuild and the width is part of the key); grouping follows the iteration strategy (tier headers with player-arranged order, or anatomy-region headers A-Z), and the header carries the pipeline status plus a Collecting/Implanting summary derived from reservations and owned operation bills — all inputs (options, rankings, reservations, surgery) already inside the data's `Version` dependency; live map state is sampled at rebuild by design (reserved-item readiness, best eligible doctor behind the floor gate, the Recovering health gate); the facts revision moves only for implant hediffs, so Recovering refreshes on the next store `Version` or facts bump |
| Fold and read flags (`OverviewState.CollapsedFlags`, `PlansState.FoldedFlags`, `HelpTabView` topic flags) | Owning snapshot/topic-array identity + fold/read revision; parallel `bool[]` so draw loops never hash; window-owned |
| Store reference (`Dialog_Implanner`) | `Find.World` identity, resolved in `WindowUpdate`; window-owned, cleared on close |
| Plans snapshot (`PlansState`) | `UiVersion.Current`, store identity + `PlansVersion` (structure, goals, base links) + `RankingsVersion` (tier placement) + `AssignmentsVersion` (card colonist counts) + `ExternalPawnFacts.Revision` (card progress aggregates over installed implants and roster), selected plan id, and the anatomy-region filter segment; conflict facts folded in (def-derived, static per session); plan-name (Medium) and extends-caption (Tiny) widths measured at rebuild; rebuilt from `WindowUpdate`; window-owned, released on close |
| Text fit widths (`WrText.FitWidth`) | `(font, text)` key; cleared when `UiVersion.Current` moves; `Reset` on world teardown |
| Translated labels (`PlannerLabels`) | `UiVersion.LanguageCurrent`; `Reset` on world teardown |
| Toolbar tooltip (`Patch_PlaySettings.tip`) | Active language object; `ResetPresentation` on world teardown |
| Gear icon corrections (`GearIconMetrics`) | uiIcon pixels per ThingDef, display epoch (screen size, UI scale); measured in game-component update batches, never OnGUI; teardown releases only the readback texture |
| Selection-tree tooltips (`PlannerTips`) | Definition set + `UiVersion.LanguageCurrent`; built on hover only; `Reset` on world teardown |
| Structured tips (`WrTips`/`StructuredTipPresenter`) | Stable key + continuous-hover session (0.45s delay); content resolved once per session; registries and frozen geometry cleared when `UiVersion.Current` moves and on world teardown via `WrTips.Reset` |
| Help content (`HelpContentState`) | Chapter topic lists loaded from disk on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiVersion.Current`; word/space/line-height measurements stamped by `UiVersion.Current`; file-loaded textures owned and destroyed by `Release()`; read marks persisted from `WindowUpdate`/close, one settings write per batch, never from a draw pass; language change and window close release everything (window-owned via `HelpTabView`) |
| Settings label (`ImplannerMod`) | `UiVersion.LanguageCurrent` |
| Help chapter labels (`HelpTabView`) | `UiVersion.LanguageCurrent`; cleared by `ReleaseWindowData` |
| Welcome dialog assets (`Dialog_ImplannerWelcome`) | About/Preview.png loaded from disk plus translated strings and wrapped-text measurements, all resolved once in `PreOpen` (language cannot change while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `ImplannerSettings.welcomeShownSaves` keyed by the world's persistent random value |
| Plans export snapshot (`Dialog_ExportPlans`) | Store identity + `PlansVersion`; rebuilt only in `WindowUpdate`, never in OnGUI; window-owned |
| Import/export picker path state (`Dialog_PlanPickerBase`) | Location, file name, custom directory; filesystem sampled in `WindowUpdate` only; the import file list is keyed by location + custom directory plus explicit invalidation (open, delete, back), and the clipboard is sampled on open, on game-window focus regain, and on the paste button, never per event; window-owned |

`ExternalPawnFacts.Revision` advances only via the `Patch_PawnFacts` event
seams, for humanlike player-faction pawns only: apparel/equipment tracker
changes (for the display-only gear column), implant hediff add/remove
(`countsAsAddedPartOrImplant`, the same filter `PawnProjection` applies),
pawn spawn/despawn/faction change, caravan membership.

Changes to these dependencies require updated behavioral tests in the same change.

## Authoritative state

- `ImplannerStore` (a `WorldComponent` wrapping the Core `PlannerModel`) is the authoritative per-save state; it also owns the deterministic plan-id counter (goals carry natural identities and need none). Loading clamps the counter above every loaded plan id and repairs duplicated plan ids deterministically (`PlannerModel.NormalizeLoadedIds`).
- Only `PlannerCommands` and deterministic `ImplannerStore` lifecycle code may mutate the shared model.
- **Plan import/export** (`Implanner.Core.PlansXml` + `PlannerCommands.ImportPlans`): the raw export XML is the sync payload; every client re-parses and applies it deterministically, validation happens before the first mutation (invalid payloads apply nothing anywhere), import is strictly additive with names uniquified via `CatalogNameRules`, save-local ids never travel (base links travel as plan names, plan ids re-allocated from the store counter, goals taking natural identity from the applied plan), and modded implants carry vanilla-style `MayRequire` attributes honored on import. Help content ships as markdown under `mod/Help/<language>/<chapter>/<NN>-<slug>.md` with images in `mod/Help/Images` (native resolution, clipped to fit the content width — never scaled down).
- **Approved third mutation class:** deterministic tick-boundary reconciliation, implemented by `PlannerReconciler`, `PlannerSurgery`, and `PlannerProduction` (reservation lifecycle, tier-ordered implant-item allocation under the iteration strategy, batch-gated operation scheduling and owned-bill lifecycle, the automatic doctor-skill floor at its approved 1020-tick boundary, and resource-gated production-bill dispatch at its approved 1020-tick boundary). It runs inside the synchronized tick path from `ImplannerGameComponent.GameComponentTick` — never from OnGUI or render code — and consumes only authoritative synchronized state and deterministic tick arithmetic, so every multiplayer client derives the identical mutation from the same tick. All colony structure (map stacks, colonists, items) comes from the pass-scoped `ColonyIndex`, which resolves floor/pocket-map canonicalization and factions in one place using `ColonyScope.AuthoritativeFaction` (`Faction.OfPlayer`); `ColonyScope.ViewFaction` (`MP.RealPlayerFaction`) is presentation-only and must never feed a synced mutation. Presence (record retention) means alive anywhere: spawned or held on a map, in a caravan, in a travelling transporter, or aboard a gravship in flight. Being AT a colony (`ColonyScope.IsOperable`) additionally requires the pawn to be spawned or carried by another pawn: a pawn sealed in a casket, pod or landed transporter, or off every serviceable map, is Away, keeps its records, receives no work, takes no surgery slot, and never sets a doctor floor. Production records are never forgotten while a gravship is in flight. A reservation is released when its pawn is present at a DIFFERENT colony than the item (medical ingredient searches never leave the patient's map stack); a pawn merely away keeps its reservations. There is no delivered-once latch or regressed state (owner, 2026-08-31): a lost implant simply becomes missing again and is re-pursued automatically — a player deliberately removing implants turns automation off first.
- **Production dispatch rules** (`PlannerProduction`): demand and stock are measured in items while bills are measured in crafts — `Implanner.Core.ProductionMath.CraftsNeeded` is the single conversion point, so multi-output recipes never over-queue; demand counts missing implant slots per colony; a bill is created only when every fixed ingredient keeps its reserve after the bill's full cost (baseline reserves: advanced components 5, components 20, gold 100, plasteel 500, steel 2000; player-overridable per resource, including to zero; the options UI always lists the baseline resources plus every discovered implant ingredient, so absent DLC/mod defs simply never appear); at most `ProductionConcurrency` benches per colony hold Implanner bills (one per bench; with `OnlyIdleBenches` a bench qualifies only when none of its bills currently wants work per `Bill.ShouldDoNow` — suspended bills and satisfied do-until-X bills leave it idle); bills carry `ProductionSkill` as their minimum skill; with `AllowIntermediaries`, ingredient shortfalls expand to full depth through the (shallow) production tree with a once-per-resource guard and a depth cap for modded cycles — expansion is whitelisted to manufactured items (the `Manufactured` thing category: components, advanced components, modded kin); raw resources never receive bills regardless of recycling or smelting recipes that could produce them; bills for no-longer-needed items are deleted; completed bills self-remove and their records are swept. Bill objects belong to the game — the model records only their load ids.
- **Iteration** defaults to one star tier at a time (`IterationStrategy.ImplantTier`), matching the plan editor's tier panel; the automation UI lists it first and maps display order onto the persisted enum values. **SurgeryConcurrency** (1–20, Options domain) caps how many colonists per colony hold Implanner-scheduled operations at once; only colonists without scheduled operations are gated, ids in deterministic order take the free slots, and the value seeds once per save on the first reconcile pass that observes an authoritative colonist (old saves that already have colonists seed at load) to max(1, colonist count / 10); an explicit player edit ends seeding (scribed 0 = unseeded sentinel, preserved across saves until seeded). With **CountHospitalized** (on by default, Options domain) humanlike pawns lying in a medical bed or downed and needing medical rest occupy cap slots too (deduplicated against owned-bill holders); the check reads live map state inside the synchronized tick pass, which is deterministic across clients.
- **Doctor floor** modes are exclusive: while `AutoDoctorFloor` is on (the default) each colony's floor tracks its CURRENT best eligible doctor — published up, down, or cleared at the 1020-tick boundary, with no player-facing per-colony state; the manual minimum applies only while auto is off and is seeded from the best currently eligible doctor inside the synced disable command.
- **Master-switch hand-back** (`PlannerCommands.CleanupAutomation` + `Dialog_AutomationCleanup`): clicking `Enable automation` off opens a local dialog on the clicking client listing every live Implanner-owned bill with a remove toggle (checked by default); automation stays ON until the dialog resolves. OK issues the pause command followed by the synced cleanup command (payload: newline-joined bill load ids, a plain string for serialization safety): listed bills are deleted from the game and their records dropped (stale records drop even without a bill object), every item reservation is released, and unlisted bills keep both bill and record so re-enabling automation resumes managing them without duplicating operations. Cancel/ESC aborts the switch entirely and changes nothing. With no owned bills automation pauses directly, no dialog.
- **Level mods stand automation down** (`PlannerAutomation`). With Strata, MultiFloors, or either As above So below active, Implanner runs as a planning tool only: `PlannerReconciler.Tick` releases every reservation and drops every owned-bill record once, then returns immediately on all later ticks. Plans, assignments, priorities, rankings, progress and the stored doctor floor all keep working. The gate resolves once per session from the active mod list, so every multiplayer client derives it identically.

  The reason, verified in each mod's source rather than inferred: a medical
  bill's ingredient search never leaves the patient's map, and none of the
  three covers `Bill_Medical` — Strata's shortfall hauling only walks colonist
  buildings that are `IBillGiver`, MultiFloors additionally demands
  `Bill_Production` (a sibling of `Bill_Medical`, not a base), and As above So
  below II's only `WorkGiver_DoBill` patch records timings and changes
  nothing. Scheduling operations that can silently never complete is worse
  than a clear boundary.

  `FloorMaps.Canonical` is unaffected and stays exactly as it is: it answers
  colony identity for grouping and display, which still matters with a level
  mod active. It was never the automation problem.

## Verification

Canonical verification commands:

```powershell
dotnet build -c Release src/Implanner.slnx --no-restore
dotnet test src/Implanner.Core.Tests --no-restore
```

Building never deploys: in-game verification requires `pwsh scripts/deploy.ps1` and a game restart.
