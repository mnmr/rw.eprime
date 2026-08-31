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
(`PlannerModel.EffectiveImplants`). Goal ids are allocated from a
store-global counter so goal keys stay unique across plans. Deleting a base
plan detaches its children.

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
- The approved boundary for resource-gated production dispatch (`PlannerProduction`) is **1020 game ticks**, plus an immediate pass after a production-domain mutation (options edited).
- The latch/reservation/surgery reconciliation pass (`PlannerReconciler`) runs on the root contract's standard **204-game-tick** boundary, plus an immediate next-tick pass after any synced store mutation (user edits stay correctness-fresh, including while paused). At most one pass per tick.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Map classifications (`ColonyScope.mapClassifications`) | Grav-engine spawn/despawn/holder transfer, settlement ownership flips, map settle, map add/remove (the `Patch_LocationTransitions` events); publishes `LocationRevision`; teardown via `ReleaseSnapshot` |
| Location snapshots (`ColonyScope.locationSnapshots`) | `LocationRevision`, map-set membership, faction identity, language; equal rebuilds preserve snapshot identity |
| Floor-map canonicalization (`FloorMaps.cache`) | Hash of live map ids; `ReleaseForTeardown` on map-set invalidation and world teardown |
| Implant catalog (`Catalogs.implants`) | Loaded definition set (static per session) + `UiVersion.LanguageCurrent`; `Release` on world teardown |
| Implant conflict facts (`ImplantConflicts.facts`) | Loaded definition set (static per session; label-free, so deliberately not language-gated); `Release` via `Catalogs.Release` |
| Production recipes (`PlannerProduction.productionRecipes`) | Loaded definition set (static per session); entries never change; `Reset` on world teardown (defensive) |
| Overview snapshot (`OverviewState`) | `UiVersion.Current`, store identity + `Version` (plans/base links, assignments, priorities, latches, reservations, rankings, surgery, options — next-work ordering follows the iteration strategy and star tiers), `ExternalPawnFacts.Revision`, `ColonyScope.LocationRevision`, current map id, grouping selection, group-by key, sort column/direction/name-order; colonist-bar order observed at rebuild; the informational Shooting/Melee columns are sampled at rebuild by design (no skill seam — the window shows the data collected when a dependency moved or it opened); window-owned, released on close |
| Automation snapshot (`AutomationState`) | `UiVersion.Current`, store identity + `OptionsVersion` + `ProductionVersion`; reserve-row set derives from the baseline-reserve table and the implant catalog; window-owned, released on close |
| Colonist detail rows (`OverviewState.detailRows`) | Overview snapshot identity + selected pawn id |
| Plans snapshot (`PlansState`) | `UiVersion.Current`, store identity + `PlansVersion` (structure, goals, base links) + `RankingsVersion` (tier placement) + `AssignmentsVersion` (card colonist counts) + `ExternalPawnFacts.Revision` (card progress aggregates over installed implants and roster), selected plan id, and the anatomy-region filter segment; conflict facts folded in (def-derived, static per session); window-owned, released on close |
| Text fit widths (`WrText.FitWidth`) | `(font, text)` key; cleared when `UiVersion.Current` moves; `Reset` on world teardown |
| Translated labels (`PlannerLabels`) | `UiVersion.LanguageCurrent`; `Reset` on world teardown |
| Toolbar tooltip (`Patch_PlaySettings.tip`) | Active language object; `ResetPresentation` on world teardown |
| Gear icon corrections (`GearIconMetrics`) | uiIcon pixels per ThingDef, display epoch (screen size, UI scale); measured in game-component update batches, never OnGUI; teardown releases only the readback texture |
| Selection-tree tooltips (`PlannerTips`) | Definition set + `UiVersion.LanguageCurrent`; built on hover only; `Reset` on world teardown |

`ExternalPawnFacts.Revision` advances only via the `Patch_PawnFacts` event
seams: apparel/equipment tracker changes (for the display-only gear column),
hediff add/remove, pawn spawn/despawn/faction change, caravan membership.

Changes to these dependencies require updated behavioral tests in the same change.

## Authoritative state

- `ImplannerStore` (a `WorldComponent` wrapping the Core `PlannerModel`) is the authoritative per-save state; it also owns the deterministic plan-id and goal-id counters.
- Only `PlannerCommands` and deterministic `ImplannerStore` lifecycle code may mutate the shared model.
- **Approved third mutation class:** deterministic tick-boundary reconciliation, implemented by `PlannerReconciler`, `PlannerSurgery`, and `PlannerProduction` (delivery latches, reservation lifecycle, tier-ordered implant-item allocation under the iteration strategy, batch-gated operation scheduling and owned-bill lifecycle, the automatic doctor-skill floor at its approved 1020-tick boundary, and resource-gated production-bill dispatch at its approved 1020-tick boundary). It runs inside the synchronized tick path from `ImplannerGameComponent.GameComponentTick` — never from OnGUI or render code — and consumes only authoritative synchronized state and deterministic tick arithmetic, so every multiplayer client derives the identical mutation from the same tick. All colony structure (map stacks, colonists, items) comes from the pass-scoped `ColonyIndex`, which resolves floor/pocket-map canonicalization and factions in one place using `ColonyScope.AuthoritativeFaction` (`Faction.OfPlayer`); `ColonyScope.ViewFaction` (`MP.RealPlayerFaction`) is presentation-only and must never feed a synced mutation. A reservation is released when its pawn is present at a DIFFERENT colony than the item (medical ingredient searches never leave the patient's map stack); a pawn merely away keeps its reservations.
- **Production dispatch rules** (`PlannerProduction`): demand and stock are measured in items while bills are measured in crafts — `Implanner.Core.ProductionMath.CraftsNeeded` is the single conversion point, so multi-output recipes never over-queue; demand counts missing implant slots per colony; a bill is created only when every fixed ingredient keeps its reserve after the bill's full cost (baseline reserves: advanced components 5, components 20, gold 100, plasteel 500, steel 2000; player-overridable per resource, including to zero; the options UI always lists the baseline resources plus every discovered implant ingredient, so absent DLC/mod defs simply never appear); at most `ProductionConcurrency` benches per colony hold Implanner bills (one per bench; with `OnlyIdleBenches` a bench qualifies only when none of its bills currently wants work per `Bill.ShouldDoNow` — suspended bills and satisfied do-until-X bills leave it idle); bills carry `ProductionSkill` as their minimum skill; with `AllowIntermediaries`, ingredient shortfalls expand to full depth through the (shallow) production tree with a once-per-resource guard and a depth cap for modded cycles; bills for no-longer-needed items are deleted; completed bills self-remove and their records are swept. Bill objects belong to the game — the model records only their load ids.
- **Iteration** defaults to one star tier at a time (`IterationStrategy.ImplantTier`), matching the plan editor's tier panel; the automation UI lists it first and maps display order onto the persisted enum values.
- **Doctor floor** modes are exclusive: while `AutoDoctorFloor` is on (the default) each colony's floor tracks its CURRENT best eligible doctor — published up, down, or cleared at the 1020-tick boundary, with no player-facing per-colony state; the manual minimum applies only while auto is off and is seeded from the best currently eligible doctor inside the synced disable command.
- **Level mods stand automation down** (`PlannerAutomation`). With Strata, MultiFloors, or either As above So below active, Implanner runs as a planning tool only: `PlannerReconciler.Tick` releases every reservation and drops every owned-bill record once, then returns immediately on all later ticks. Plans, assignments, priorities, rankings, progress, blockers and the stored doctor floor all keep working. The gate resolves once per session from the active mod list, so every multiplayer client derives it identically.

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
