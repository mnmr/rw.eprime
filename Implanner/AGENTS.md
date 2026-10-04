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

Implant quality (owner, 2026-09-26/27): items may carry a quality (Quality
Bionics Remastered adds `CompQuality` to replacement items and scales the
part's efficiency and, through Elite Bionics Framework, its max HP; Vanilla
Genetics Expanded's hybrid implants carry one whose hediff stage offsets the
part's efficiency). Both are read by type name in `ImplantQualities`. A plan
carries a minimum item quality (`Plan.MinQuality`, Plans domain, 0 Awful =
any, synced `SetPlanMinQuality`, scribed `minQuality`, exported as
`MinQuality="Good"`; a new extending plan starts with its base's value; the
assigned plan's value covers every effective goal, inherited ones included;
items without quality ignore it). The plan editor offers it only while a
catalog item carries a quality. A slot accepts the lowest quality at or above
that minimum whose implant leaves the part no worse than it is now
(`ImplantQuality.MinimumAcceptable` over `ImplantQualities.AfterInstall`
against `PawnCapacityUtility.CalculatePartEfficiency`): an implant worse than
a healthy part never replaces it, but fills a missing or damaged one; a slot
nothing satisfies waits. Allocation takes the HIGHEST acceptable item, oldest
among equals, or the lowest with the "better implants to high-priority
colonists" option (below). Evaluation reads each installed implant's
efficiency per instance (`ImplantQualities.InstalledEfficiency`), and the
substitution floor is the goal's implant at the plan minimum raised until it
is no worse than a natural part. An installed exact kind below the minimum
still counts as done. A reservation whose item
falls below its plan's minimum (the player raised it) is released.
Operations install their reserved item: the implant is a fixed ingredient,
accepted by `Bill.IsFixedOrAllowedIngredient` before the bill's filter, so
`Patch_BoundSurgeryIngredient` refuses every other implant item for a bill in
the published `SurgeryBindings` map (reservation plus owned-bill record).

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
(replacements occupy it and implants cannot mount on added parts, except
kinds whose surgery worker inherits neither vanilla install worker and so
never had that refusal: `ImplantCatalogEntry.MountsOnArtificialParts`,
verified for Bionic modularity's `Recipe_InstallModule : Recipe_Surgery`,
which mounts modules ON a bionic limb; such a pair coexists and the bionic
never stands in for the module goal), replacements clear their subtree
(again except mounts-on-artificial kinds), `incompatibleWithHediffTags` versus
`HediffDef.tags` excludes same-part implants (skin glands), and mutual
`HediffDef.removeWithTags` versus `tags` excludes a pair anywhere on the
body (`Hediff.PostAdd` removes the tagged hediff pawn-wide, exact-match;
modded mechanite strains). One-sided removal is not a conflict: installing
the remover first leaves both in place. The pure rules
live in `Implanner.Core.ImplantConflictRules`; `ImplantConflicts` extracts
the facts from the catalog and injects `PlannerModel.SlotConflictResolver`.
A replacement (`ImplantCatalogEntry.IsReplacement`) is a hediff whose class
is `Hediff_AddedPart`, the class every game check for an artificial part
tests (`HediffSet.HasDirectlyAddedPartFor`), whatever its worker or
`addedPartProps`. A part wiper (`ImplantCatalogEntry.WipesPart`, owner
2026-09-26) is an implant-class hediff whose surgery restores the part and
its subtree first (`ModCompatibility.ClearsPart`: the artificial-part worker
or the curated `GeneticRim.Recipe_InstallGeneticBodyPart`, Vanilla Genetics
Expanded's worker, which derives straight from `Recipe_Surgery` and would
otherwise read as a module worker). The part stays natural, so implants
installed after a wiper stay: a wiper and a same-part or subtree implant are
no conflict (order decides), two wipers or a wiper and a replacement on one
part are (`ImplantConflictRules.CompeteForSlot`, also the evaluator's
substitution gate, so an installed neuron reinforcement never stands in for
a brain implant goal). Automation installs a wiper before what it would push
out: the wiper joins every batch holding a key it precedes
(`SurgeryPlanner.ComputeBatch` `precededBy`) and such keys wait while the
wiper is ready (`Releasable` `held`). What a wiper does push out is recorded
when its operation is scheduled and reinstalled right after it
(`PlannerReinstall`, below). No part-clearing surgery (replacement or wiper,
in-place upgrades exempt) is ever scheduled while it would destroy something
for good (`PlannerSurgery.WouldDestroy`: a hediff on the part or below that is
not `isBad`, not `keepOnBodyPartRestoration`, and has no item, or an implant a
wiper pushes out that no available surgery can install again); the details
panel reads "Would destroy X".
Curated exceptions (owner, 2026-09-05): the modded bladder implants and
hygiene enhancers (`ImplantCompatibility`, groups `Bladder` and
`HygieneEnhancer`: Dubs Bad Hygiene and FSF Advanced Bionics Expansion) are
untagged torso implants the game stacks freely. The Options-domain
`PlannerModel.AllowMultipleBladders` / `AllowMultipleHygieneEnhancers`
(default on, scribed `allowMultipleBladders` / `allowMultipleHygieneEnhancers`,
synced `SetAllowMultiple*`) switched off make the group exclusive by kind
(`PlannerModel.KindsExclusive`), honored everywhere: the command and the
picker captions apply it beside the resolver; `EffectiveImplants` keeps only
the first-picked kind of a group per plan (picks stay stored, so switching
back on restores them); and the evaluator's substitution gate accepts an
installed kind of the group for the goal (`KindsExclusiveDelegate`, allocated
once per model since the reconcile tick reaches it), so automation never
installs the second kind while a colonist who already carries both keeps
both. Nothing is ever removed. Shown on the Options tab (`OptionsState`).
Evaluation substitution follows the same data: an installed implant
satisfies another kind's goal slot only when it actually EXCLUDES
installing the requested implant there (artificial-part occupancy or tag
conflict, `ImplantConflicts.SameSlotExclusive`) and meets the efficiency
floor — a manually installed archotech leg satisfies a bionic-leg goal,
while coexisting same-part implants (brain implants) never satisfy each
other's goals.
In-place upgrades (owner, 2026-09-05; Integrated Implants' "modularize
bionic X" surgeries: `removesHediff` = the base kind, `addsHediff` = the
modular kind, no fixed body parts, worker `LTS_Recipe_ReplaceHediff`
targeting wherever the base sits) are catalog entries of their own
(`ImplantCatalogEntry.UpgradesFrom`, built by `Catalogs.AddUpgrades` only
when the base kind is in the catalog): the base's anatomy, the upgrade's own
efficiency and item (its `spawnThingOnRemoved`, the base item), and a
replacement like its base (`Catalogs.AddUpgrades` marks every upgrade one).
Consequences, all data-driven: the upgrade and its base exclude
each other in plans (picking the upgrade supersedes the base); an installed
base never stands in for its upgrade goal (`SameSlotExclusive` walks
`Catalogs.UpgradeChainContains`); `PawnProjection.RequiredItem` answers
null for a slot whose part already carries a chain member, so reservation,
surgery release (`NeedsNoItem`), production demand and the strip all skip
the item; and `EnsureOperation` schedules the base's own install under the
upgrade's goal key while the part lacks it, then the upgrade surgery on a
later pass (`SelectRecipe` accepts recipes without fixed parts on the
worker's part filter alone). Integrated Implants' module items (EBSG
item-use, no surgery) stay outside the catalog by construction.

Purchase-only kinds (owner, 2026-09-05): the catalog keeps every kind whose
surgery item no recipe produces, flagged `ImplantCatalogEntry.PurchaseOnly`
(an upgrade inherits its base's flag). The Options-domain
`PlannerModel.ShowPurchaseOnly` (default off, scribed `showPurchaseOnly`,
synced `SetShowPurchaseOnly`, Options tab "Catalog" section) governs picker
and reserve-menu visibility only; goals holding such kinds keep working from
stock. Their slots are "optional" to batching (`PlannerSurgery.OptionalFlags`
→ `SurgeryPlanner.ComputeBatch`/`Releasable`): they join every batch so
stock is used at once, never decide the active tier while craftable work
remains, and never block a release — a missing archotech leg holds up
nothing else. A mod adding a crafting recipe makes the kind ordinary
automatically (the producible set is data).
The click IS the choice: selecting a conflicting slot deselects an own
blocker inside the synced command (`SetImplantSlot`) and suppresses an
inherited one in `EffectiveImplants` — the editor annotates such slots with
"overrides X" rather than disabling them. A kind whose every surgery uses a
mounts-on-artificial worker (`ImplantCatalogEntry.RequiresArtificialPart`,
inferred from the recipe set: Bionic modularity limb modules) is offered by
the game only on a replacement; ticking such a slot while the plan's
effective goals hold no host replacement for that anatomy instance opens
`Dialog_ImplantRequirements` (owner, 2026-09-05) listing the hosts
(catalog replacements on the record carrying Bionic modularity's
`DefExtension_ModularHediff`, matched by type name in
`ModCompatibility.IsModularReplacement`; every replacement when that mod is
absent; purchase-only hosts only while `ShowPurchaseOnly` is on, the same
filter as the picker rows), cheapest efficiency first. Confirm issues two ordinary
`SetImplantSlot` commands (host, then the slot); Cancel/ESC issues nothing,
so the row simply keeps its published state. The candidates are resolved
into the plans snapshot (`PickerRow.Requirement`), never in the draw pass. Mutant-only implants (ghoul kit)
are excluded from the catalog: only ordinary humanlikes are plannable.

## Project boundaries

- `src/Implanner.Core` must remain deterministic and independent of RimWorld, Verse, Unity, Harmony, and Multiplayer APIs.
- `src/Implanner` owns game integration, persistence, patches, rendering, and UI.
- `src/Implanner.Core.Tests` owns executable behavioral and regression tests.
- Shared source from `..\Shared\Common` (namespace `RimShared.Common`) is compiled into `Implanner.Core` via `$(RimSharedRoot)`; it is never shipped as a separate assembly.
- Shared game-side UI source from `..\Shared\UiLib` (namespace `RimShared.UiLib`, may reference Verse/Unity) is compiled into `Implanner` the same way; it must never be included in `Implanner.Core`.
- Shared game-side location source from `..\Shared\GameLib\Locations` (namespace `RimShared.GameLib`: `FloorMaps`, `MapClassifications`, `PlayerFactions` and the location-transition Harmony patches) is compiled into `Implanner` through its own subfolder-scoped include, never a GameLib-wide glob, and never into `Implanner.Core`. Implanner's side of its partial hooks lives in `LocationHooks.cs`.
- Shared test-only source from `..\Shared\Tests\Support` (namespace `RimShared.Tests.Support`, e.g. the shipped-help validator `HelpContentChecks`) is compiled into `Implanner.Core.Tests` the same way; it must never be included in a shipped project.

## Canonical refresh boundaries

- Time-driven invalidation must fire on computed game-tick boundaries, never on per-frame or per-tick polling.
- The approved boundary for automatic doctor-skill-floor evaluation is **1020 game ticks**.
- The approved boundary for resource-gated production dispatch (`PlannerProduction`) is **1020 game ticks**, plus an early dispatch on the pass after a production-domain mutation (the store's scribed `PendingProductionPass` flag).
- Quality Bionics Remastered's efficiency multiplier settings are compared on the same **1020-game-tick** boundary (owner, 2026-09-27), only while that mod is loaded; local presentation only.
- The reservation/surgery reconciliation pass (`PlannerReconciler`) runs on the owner-approved **1020-game-tick** boundary (owner, 2026-08-31: all Implanner periodic work shares the 1020-tick cadence), plus a pass on the next simulated tick after any synced store mutation (the store's scribed `PendingReconcile` flag; ticks do not advance while paused, so a paused edit reconciles on the first tick after unpausing). Both triggers derive only from synced state (tick arithmetic and save-carried flags), so a late-joining or resynced multiplayer client never runs a pass the host does not. Each pass begins with `PlannerModel.CleanupMissing`. At most one pass per tick.

## Canonical cache dependency matrix

| Cached artifact | Required invalidation inputs |
|---|---|
| Map classifications (shared `RimShared.GameLib.MapClassifications`) | Grav-engine spawn/despawn/holder transfer, settlement ownership flips, map settle, map add/remove (the shared `Patch_LocationTransitions` events; Implanner implements none of the optional `LocationTransitions` hooks), plus the map-count guard on location reads; publishes `MapClassifications.LocationRevision`; teardown via `MapClassifications.ReleaseSnapshot` on world teardown |
| Location snapshots (`ColonyScope.locationSnapshots`) | `MapClassifications.LocationRevision`, map-set membership, faction identity, language; cleared through the required `MapClassifications.InvalidateLocationSnapshots` hook on every classification or map-set invalidation and on teardown; equal rebuilds preserve snapshot identity |
| Floor-map canonicalization (shared `RimShared.GameLib.FloorMaps.cache`) | Hash of live map ids; `ReleaseForTeardown` on map-set invalidation and world teardown |
| Implant catalog (`Catalogs.implants`) | Loaded definition set (static per session) + `UiRevision.LanguageCurrent`; `Release` on world teardown |
| Modular-host extension type (`ModCompatibility.modularExtension`) | Loaded assembly set (static per session); resolved once by type name; no teardown (process-lifetime immutable fact) |
| Quality mod handles (`ImplantQualities`) | Loaded assembly set (static per session): Quality Bionics Remastered's API delegates and hediff comp field, Vanilla Genetics Expanded's comp types, Elite Bionics Framework's comp props fields, resolved once by type name; never a result (Quality Bionics Remastered's multipliers are player settings, read through its API on every call); the last observed efficiency multiplier table is compared on the approved 1020-tick boundary (`ImplantQualities.CheckSettings`, no-op without the mod; the mod has no change notification) and a change advances `ImplantQualities.SettingsRevision` and `ExternalPawnFacts.Revision`; no teardown |
| Quality Jobs bridge (`QualityJobsBridge.manageBill`) | Active mod list + loaded assemblies (static per session); `QualityJobsApi.ManageBill` bound once when `ApiVersion` >= 2; a thrown call disables it for the process (one warning); no teardown |
| Surgery ingredient bindings (`SurgeryBindings`) | The model's reservations (Reservations domain) and owned operation bills (Surgery domain): bill loadID to reserved item id, republished wholesale by the store on every change of either domain, on construction and after load; read only by `Patch_BoundSurgeryIngredient`; `Reset` on world teardown |
| Endless Growth bill-limit callable (`ModCompatibility.endlessGrowthBillMaximum`) | Loaded assembly set (static per session); resolves the same provider used by the mod's bill constructors, only when repairing an owned bill with the legacy maximum of 20; caches the delegate, never its result; retains no world or game object |
| Implant conflict facts (`ImplantConflicts.facts`) | Loaded definition set (static per session; label-free, so deliberately not language-gated); `Release` via `Catalogs.Release` |
| Production recipes (`PlannerProduction.productionRecipes`) | Loaded definition set (static per session); entries never change; `Reset` on world teardown (defensive) |
| Recipe bench users (`PlannerProduction.recipeUsers`) | Loaded definition set (static per session); entries never change; `Reset` on world teardown (defensive) |
| Implant bench defs (`PlannerProduction.implantBenches`) | Loaded definition set (static per session): the bench defs able to work any catalog implant item's production recipe; built once on the first Bills-tab query (the catalog's language gate changes labels, never its kinds); `Reset` on world teardown |
| Designated bench ids (`BenchDesignations`) | The model's designations (`Benches` domain); republished as a new immutable set on every `Benches` change (synced command or reconcile pass), on store construction, and after load normalization; the bill-UI patches read only this set; `Reset` on world teardown |
| Bench toggle labels (`BenchLock`) | Active language object; `Reset` on world teardown |
| Overview data (`OverviewState`, `OverviewData`) | `UiRevision.Current`, store identity + `Version` (plans/base links, assignments, priorities, reservations, rankings, surgery, options — the strip's surgery batch line derives the single next colonist and their batch tier from the reconciler's dispatch order; the State column derives Waiting/Preparing/Operating/Done from the reservation and owned-operation-bill bookkeeping), `ExternalPawnFacts.Revision`, `MapClassifications.LocationRevision`, grouping selection (kind + location id; the current map is read only when the selection must be revalidated, never as a key); colonist-bar order observed at rebuild; the informational Shooting/Melee columns are sampled at rebuild by design (no skill seam — the window shows the data collected when a dependency moved or it opened); the strip's production column samples item stock (with per-item surgery reservations split out of the free-stock count), in-scope bench bills, and ingredient resource counts at rebuild the same way, ranking and reserve-blocking by `PlannerProduction`'s own dispatch rules (bill creation/removal bumps `Version`, so automation activity refreshes it promptly); the strip's batch and automation-chip text widths are measured at rebuild; effective goals memoized per plan within one build as a private copy (the model's own goal list never enters a snapshot); rebuilt from `WindowUpdate` (the draw pass rebuilds only as a fallback for a tab switched mid-frame, and detail rows rebuild behind their own gate); window-owned, released on close |
| Overview ordering (`OverviewSnapshot`) | Overview data identity, group-by key, sort column/direction/name-order; a pure re-sort/re-group sharing the data's `OverviewRow` instances (new snapshot object per ordering change); no game state read; window-owned, released on close |
| Strip column tooltips (`StripTipSource`, one per column) | Overview data identity (the data carries the per-item production rows and per-kind surgery rows, built by `OverviewState` from the same stock, bill, reservation, and evaluation scans as the strip texts; `Implanner.Core.StripBreakdown` does the pipeline partition and dispatch ordering); the tip model is assembled only when a hover session opens and frozen for that session by `StructuredTipPresenter`; window-owned, released on close |
| Automation snapshot (`AutomationState`) | `UiRevision.Current`, store identity + `OptionsVersion` + `SurgeryVersion` (implant reserves live in the Surgery domain) + `ProductionVersion`; carries the master switch, iteration, doctor-floor/hospitalized, upgrade-by-priority (plus whether any catalog item carries a quality, static per session) and production flags so the tab never reads the live model; reserve-row set derives from the baseline-reserve table and the implant catalog; reserve edit buffers are arrays parallel to the rows, reseeded on rebuild from the durable per-key dictionary; window-owned, released on close |
| Automation tooltip holder (`AutomationTips`) | `UiRevision.Current` (the `WrTips` registry clears on it); window-owned, released on close |
| Options snapshot (`OptionsState`) | `UiRevision.Current`, store identity + `OptionsVersion`; carries the mod compatibility and catalog flags so the tab never reads the live model; rebuilt from `WindowUpdate`; window-owned, released on close |
| Options tooltip holder (`OptionsTips`) | `UiRevision.Current` (the `WrTips` registry clears on it); each tip's argument (the affected loaded implants with their mod names, `ModCompatibility.TipLines` / `PurchaseOnlyTipLines`) is resolved here since the definition set is static per session and the language sits inside the revision; window-owned, released on close |
| Skill slider presentation (`SkillSliderState`, shared by Options and both skill controls) | `UiRevision.Current` and local `ImplannerSettings.skillSliderMaximum`; immutable maximum/value text and measured Small-font line box; window-owned, released on close; preference changes preserve automation/model snapshot identities |
| Colonist detail rows (`OverviewState.detailRows`) | Overview data identity + selected pawn id + panel body width (the header sentence wraps, so its height is measured at rebuild and the width is part of the key); grouping follows the iteration strategy (tier headers with player-arranged order, or anatomy-region headers A-Z), and the header carries the pipeline status plus a Collecting/Implanting summary derived from reservations and owned operation bills — all inputs (options, rankings, reservations, surgery) already inside the data's `Version` dependency; live map state is sampled at rebuild by design (reserved-item readiness, best eligible doctor behind the floor gate, the Recovering health gate); the facts revision moves only for implant hediffs, so Recovering refreshes on the next store `Version` or facts bump |
| Fold and read flags (`OverviewState.CollapsedFlags`, `PlansState.FoldedFlags`, `HelpTabView` topic flags) | Owning snapshot/topic-array identity + fold/read revision; parallel `bool[]` so draw loops never hash; window-owned |
| Store reference (`Dialog_Implanner`) | `Find.World` identity, resolved in `WindowUpdate`; window-owned, cleared on close |
| Plans snapshot (`PlansState`) | `UiRevision.Current`, store identity + `PlansVersion` (structure, goals, base links, minimum quality; the minimum-quality button shows while a catalog item carries a quality and its tip names a missing Quality Jobs, both static per session, its Small-font text width measured at rebuild) + `RankingsVersion` (tier placement) + `AssignmentsVersion` (card colonist counts) + `OptionsVersion` (the mod compatibility option feeds override captions) + `ExternalPawnFacts.Revision` (card progress aggregates over installed implants and roster), selected plan id, the anatomy-region filter segment, and the active search query (the vanilla `QuickSearchWidget` text normalized by `RimShared.Common.SearchMatcher`: trimmed, inactive only when blank; while active the tree is one flat region-free list of the slots whose label or body-part group matches, owner 2026-09-06); conflict facts folded in (def-derived, static per session); plan-name (Medium), extends-caption (Tiny) and picker leaf-label (Small) widths measured at rebuild; rebuilt from `WindowUpdate`; window-owned, released on close |
| Picker captions (`PlansState.Captions`) | Plans snapshot identity + the tree row width the picker draws at (window size, scroll gutter); one "overrides X" / "inherited" caption per row fitted to exactly the room its label leaves (the caption reserves nothing; omitted when not even an ellipsis fits), tail-truncated behind an ellipsis via `RimShared.Common.TailTruncation` when it cannot fit, carrying the full text for the hover tooltip; Tiny measurements happen only inside this gate; window-owned, released on close |
| Text fit widths (shared `RimShared.UiLib.WrText.FitWidth`, per-assembly static; `MeasureFitWidth` is the same rule unmemoized for snapshot builders) | `(font, text)` key; cleared when `UiRevision.Current` (shared `RimShared.UiLib.UiRevision`: UI scale, tiny-font preference, language) moves; `WrText.Reset` on world teardown |
| Help foldout caption heights (shared `RimShared.UiLib.HelpFoldout`, per-assembly static) | Caption text, effective Tiny font (`TinyText.Metrics.Font`), wrap width, and the caller-supplied `UiRevision.Current`; `HelpFoldout.Reset` on world teardown |
| Translated labels (`PlannerLabels`) | `UiRevision.LanguageCurrent`; `Reset` on world teardown |
| Toolbar tooltip (`Patch_PlaySettings.tip`) | Active language object; `ResetPresentation` on world teardown |
| Gear icon corrections (`GearIconMetrics`) | uiIcon pixels per ThingDef, display epoch (screen size, UI scale); measured in game-component update batches, never OnGUI; teardown releases only the readback texture |
| Selection-tree tooltips (`PlannerTips`) | Definition set + `UiRevision.LanguageCurrent`; the part efficiency span across qualities reads Quality Bionics Remastered's multiplier settings when a tip builds, cleared when `ImplantQualities.SettingsRevision` moves, and Elite Bionics Framework's declared HP adjustment; built on hover only; `Reset` on world teardown |
| Structured tips (shared `RimShared.UiLib` `WrTips`/`StructuredTipPresenter`/`WrTipUI` in `Shared/UiLib/Tips`, per-assembly statics; Implanner's window id, table inset and revisions in `UI/TipHost.cs`) | Stable key + continuous-hover session (0.45s delay); content resolved once per session; registries and frozen geometry cleared when `UiRevision.Current` moves and on world teardown via `WrTips.Reset` |
| Help content (shared `RimShared.UiLib.HelpContentState` and `HelpTabView`, hosted by `ImplannerHelpHost`; the markdown, flow layout with CJK line breaking, and index planner are `RimShared.Common.Help`) | Chapter topic lists loaded from disk on demand (tab open, chapter click, dev Reload); draw models keyed by chapter + slug + width + `UiRevision.Current`; word/space/line-height measurements stamped by `UiRevision.Current`; file-loaded textures owned and destroyed by `Release()`; read marks persisted from `WindowUpdate`/close, one settings write per batch, never from a draw pass; language change and window close release everything (window-owned via `HelpTabView`) |
| Settings label (`ImplannerMod`) | `UiRevision.LanguageCurrent` |
| Help chapter labels (`HelpTabView`) | `UiRevision.LanguageCurrent`; cleared by `ReleaseWindowData` |
| Welcome dialog assets (shared `RimShared.UiLib.WelcomeDialog`, built in `ImplannerGameComponent.QueueWelcome`) | Translated strings resolved at construction; About/Preview.png loaded from disk and wrapped-text measurements resolved once in `PreOpen` (language and UI scale cannot change while open); window-owned, texture destroyed in `PostClose`; shown once per player per save via `ImplannerSettings.welcomeShownSaves` keyed by the world's persistent random value |
| Plans export snapshot (`Dialog_ExportPlans`) | Store identity + `PlansVersion`; rebuilt only in `WindowUpdate`, never in OnGUI; window-owned |
| Import/export picker path state (shared `RimShared.UiLib.ExportLocationPicker`, composed by `Dialog_PlanPickerBase`) | Location, file name, custom directory; shell folders and the filesystem sampled in `WindowUpdate` only (the export folder, shared `RimShared.UiLib.ExportFolder`, is a cached path created on the first save, never when read); its labels (`PlanIoLabels.Picker`) follow `UiRevision.LanguageCurrent` and their measured Enter-path width `UiRevision.Current`; the import file list is keyed by location + custom directory plus explicit invalidation (open, delete, back), and the clipboard is sampled on open, on game-window focus regain, and on the paste button, never per event; window-owned |

`ExternalPawnFacts.Revision` advances via the `Patch_PawnFacts` event
seams (and when Quality Bionics Remastered's efficiency multipliers change,
see Quality mod handles), for humanlike player-faction pawns only: apparel/equipment tracker
changes (for the display-only gear column), implant hediff add/remove
(`PawnProjection.IsTrackedImplant`: `countsAsAddedPartOrImplant` or any
catalog kind, since Bionic modularity's modules clear the flag; the same
filter `PawnProjection` applies when projecting installed implants),
pawn spawn/despawn/faction change, caravan membership.

Changes to these dependencies require updated behavioral tests in the same change.

## Authoritative state

- `ImplannerStore` (a `WorldComponent` wrapping the Core `PlannerModel`) is the authoritative per-save state; it also owns the deterministic plan-id counter (goals carry natural identities and need none). Loading clamps the counter above every loaded plan id and repairs duplicated plan ids deterministically (`PlannerModel.NormalizeLoadedIds`).
- Only `PlannerCommands` and deterministic `ImplannerStore` lifecycle code may mutate the shared model.
- **Plan import/export** (`Implanner.Core.PlansXml` + `PlannerCommands.ImportPlans`): the raw export XML is the sync payload; every client re-parses and applies it deterministically, validation happens before the first mutation (invalid payloads apply nothing anywhere), import is strictly additive with names uniquified via `CatalogNameRules`, save-local ids never travel (base links travel as plan names, plan ids re-allocated from the store counter, goals taking natural identity from the applied plan), and modded implants carry vanilla-style `MayRequire` attributes honored on import. Help content ships as markdown under `mod/Help/<language>/<chapter>/<NN>-<slug>.md` with images in `mod/Help/Images` (native resolution, clipped to fit the content width — never scaled down).
- **Approved third mutation class:** deterministic tick-boundary reconciliation, implemented by `PlannerReconciler`, `PlannerSurgery`, `PlannerBenches`, and `PlannerProduction` (reservation lifecycle, Implanner-bench upkeep, tier-ordered implant-item allocation under the iteration strategy, batch-gated operation scheduling and owned-bill lifecycle (a bill is created only for a recipe whose worker's own part filter, `GetPartsToApplyOn`, accepts the slot part, so module workers demanding a bionic and vanilla workers refusing one simply leave the goal waiting), the automatic doctor-skill floor at its approved 1020-tick boundary, and resource-gated production-bill dispatch at its approved 1020-tick boundary). It runs inside the synchronized tick path from `ImplannerGameComponent.GameComponentTick` — never from OnGUI or render code — and consumes only authoritative synchronized state and deterministic tick arithmetic, so every multiplayer client derives the identical mutation from the same tick. All colony structure (map stacks, colonists, items) comes from the pass-scoped `ColonyIndex`, which resolves floor/pocket-map canonicalization and factions in one place using `PlayerFactions.AuthoritativeFaction` (`Faction.OfPlayer`); `PlayerFactions.ViewFaction` (`MP.RealPlayerFaction`) is presentation-only and must never feed a synced mutation. Presence (record retention) means alive anywhere: spawned or held on a map, in a caravan, in a travelling transporter, or aboard a gravship in flight. Being AT a colony (`ColonyScope.IsOperable`) additionally requires the pawn to be spawned or carried by another pawn: a pawn sealed in a casket, pod or landed transporter, or off every serviceable map, is Away, keeps its records, receives no work, takes no surgery slot, and never sets a doctor floor. Production records are never forgotten while a gravship is in flight. A reservation is released when its pawn is present at a DIFFERENT colony than the item (medical ingredient searches never leave the patient's map stack); a pawn merely away keeps its reservations. There is no delivered-once latch or regressed state (owner, 2026-08-31): a lost implant simply becomes missing again and is re-pursued automatically — a player deliberately removing implants turns automation off first.
- **Production dispatch rules** (`PlannerProduction`, owner 2026-09-26), per colony: every bill is ONE craft; a bench holds at most `PlannerModel.BillsPerBench` (2) Implanner bills so the next craft is queued when the first completes, and at most `ProductionConcurrency` benches hold any. Crafts follow the surgery rollout order — `SurgeryPlanner.Order` over every missing craftable slot (tier batching: tier by tier; full sets: colonist by colonist; ASAP: priority, tier, kind), within a colonist's tier by the plan-ranked position (`SurgeryWorkItem.Position` = `PlannerModel.ImplantOrderOf`), never per item unless that is the rollout; `Implanner.Core.ProductionQueue.UncoveredCrafts` covers the earliest slots with stock (unforbidden, less implant hold-backs), then pending Implanner crafts, and every slot left is one craft (multi-output recipes cover the following slots of their kind). A craft is queued only when stock covers its cost plus the materials promised to queued (not started) Implanner bills plus the reserve (`Implanner.Core.ProductionBudget`; started = a bound unfinished thing or a colony pawn's current job, `PlannerProduction.CollectInProgress`); a craft that does not fit is skipped and later crafts proceed; an affordable craft without a free bench still promises its materials, so what counts as skipped depends on stock and rollout alone. Ingredient costs are the game's own item units (`IngredientCount.CountRequiredOfFor`: small-volume resources such as gold are listed per 10). Queued bills promise their materials oldest first (bill load id); one that no longer fits above the reserves (the player spent the stock) is cancelled and returns when stock allows (owner 2026-09-26). Bench slots left after the rollout build the manufactured ingredients the first `ProductionConcurrency` skipped crafts will need (`BuildAhead`, owner 2026-09-26: advanced components are the long pole), one craft per bill, within the budget. The Overview's blocker text applies the same next-craft-plus-promised rule (`PlannerProduction.PromisedMaterials`). Baseline reserves: advanced components 5, components 20, gold 100, plasteel 500, steel 2000; player-overridable per resource, including to zero; the options UI always lists the baseline resources plus every discovered implant ingredient, so absent DLC/mod defs simply never appear. With `OnlyIdleBenches` an ordinary bench qualifies only when none of its other bills currently wants work per `Bill.ShouldDoNow` — suspended bills and satisfied do-until-X bills leave it idle; Implanner benches (below) take bills first and skip the idle rule, other benches serve only while `OnlyDesignatedBenches` (Production domain, default off, scribed `onlyDesignatedBenches`) is off; within a group an empty bench beats a second bill (`PlannerModel.ChooseProductionBench`). Bills carry `ProductionSkill` as their minimum skill. With `AllowIntermediaries`, the first craft in rollout order blocked by a manufactured ingredient sets that ingredient's need for the pass (enough one-craft bills to cover the shortfall, pending ones counted), recursively with a once-per-resource guard and a depth cap for modded cycles; every craft of the rollout is examined each pass even with full benches, so the needs are stable — expansion is whitelisted to manufactured items (the `Manufactured` thing category: components, advanced components, modded kin); raw resources never receive bills regardless of recycling or smelting recipes that could produce them. Queued bills beyond what the rollout (implants) or the intermediary needs use are withdrawn; started bills always finish. Vanilla leaves a finished "do X times" bill on the bench at 0, so every pass deletes Implanner bills at repeat count 0 (they would hold a bench slot); records of bills that no longer exist are swept. Stock counts implant items a colony pawn is carrying (a finished implant on its way to storage), so that window never queues a duplicate bill (both observed in-game 2026-09-26). Bill objects belong to the game — the model records only their load ids — and are created only through the game's own factory (`BillUtility.MakeNewBill`, `PlannerProduction.MakeBill`): a recipe with an `unfinishedThingDef` (every vanilla bionic via `BodyPartBionicBase`'s recipe maker, both component recipes, most modded bionics) must be a `Bill_ProductionWithUft`, since `Toils_Recipe.MakeUnfinishedThingIfNeeded` casts the job's bill to it when the crafter starts and a plain `Bill_Production` ends the job with `InvalidCastException` (reported and reproduced in-game 2026-09-07).
- **Production quality** (owner, 2026-09-26/27): each rollout slot asks for
  its lowest acceptable quality (see Implant quality); stock covers a slot
  only at that quality or better, the best acceptable item first, and the
  held-back items are the worst ones (`Implanner.Core.ProductionQueue`,
  `CraftNeed`). A new bill promises the slot's minimum where that can be
  arranged, recorded beside its record (`PlannerModel.ProductionBillQualityOf`,
  Production domain, scribed `productionBillQualities`, absent = unknown): an
  ingredient that decides quality (Vanilla Genetics Expanded's genoframes,
  `GeneticRim.DefExtension_Quality`) is limited to that tier and up through
  the bill's ingredient filter (`IngredientQuality.Restrict`); otherwise, for
  a minimum above Awful, Quality Jobs (API v2, `QualityJobsBridge`) manages
  the bill at that target. A pending bill covers slots up to its promise; one
  without a promise covers any slot until its product lands. Without Quality
  Jobs a craft below the minimum simply does not cover the slot and is
  crafted again; resource reserves are the brake. Unused bills are withdrawn
  by item and promise.
- **Reinstalls** (owner, 2026-09-26; `PlannerReinstall`, Surgery domain,
  `PlannerModel.Reinstalls`, scribed as parallel `reinstall*` lists): when a
  part wiper's operation is created (and while it waits) the implants it will
  push out are recorded, planned or not, as pending records keyed
  `r{partIndex}:{defName}` with the pushed-out quality; once the wiper sits on
  its part they wake, the dropped item is reserved back for the same pawn
  (the same quality first) and installed again outside the batch and
  concurrency gates (health gate and doctor floor still apply). A record ends
  when its implant is back, its part or def is gone, or no item of the kind is
  left at the colony; pending records of a wiper whose operation vanished
  unrun are dropped. Active reinstall keys keep their reservations and owned
  operations without a plan (the stale sweeps skip them), and the pass index
  includes their items even outside the catalog. While a record is active,
  allocation reserves nothing for a plan slot of the same kind on that part.
- **Better implants to high-priority colonists** (owner, 2026-09-27;
  `PlannerModel.UpgradeByPriority`, Options domain, default off, scribed
  `upgradeByPriority`, synced `SetUpgradeByPriority`; the Automation tab row
  shows only while a catalog item carries a quality): allocation takes the
  lowest acceptable item, then `PlannerUpgrades` (after allocation, before
  scheduling) runs `Implanner.Core.QualityRebalance` per colony and quality
  kind. Holders in priority order (then pawn id, key) take the best item
  improving on what they hold: a free item (less the worst player hold-backs)
  or one reserved for a strictly lower-priority colonist whose operation is
  not scheduled. A reservation swaps (the old item goes to the colonist it
  took from when acceptable, else back to the free items); an installed plan
  implant is replaced: a removal operation under `GoalKeys.Upgrade`
  (`u{partIndex}:{defName}`; `RecipeDefOf.RemoveBodyPart` for an added part,
  the kind's own `removesHediff` surgery for a single-instance implant) plus a
  pending reinstall record for the better item, reserved at once
  (`PlannerModel.HasReinstall` keeps a pending record's reservation). The
  record wakes when the old implant is off the part and installs the better
  item; the removed item drops (both quality mods keep its quality) and reaches
  the next colonist through allocation. A removal that vanishes unrun drops
  the upgrade and frees the item. Upgrades start only for a healthy colonist at
  the colony with no owned operation and no reinstall record, one per colonist
  per pass, inside the concurrency cap (`PlannerSurgery.PlannedByColony`), and
  never for in-place upgrade kinds or when anything else non-harmful sits on
  the part or below an added part or wiper (`PlannerUpgrades.RemovalFor`).
  Proven in-game 2026-09-27 (managed run, Fisso): a High-priority colonist's Normal
  arm was replaced by the Excellent one reserved for the next colonist, who
  then received the removed Normal arm.
- **Implanner benches** (owner, 2026-09-26; `Benches` domain, `PlannerModel.DesignatedBenches`, scribed `designatedBenches` + parallel `designatedBillBenches`/`designatedBillIds`): the Bills-tab toggle issues the synced `SetBenchDesignated(benchId, designated)`. Designating suspends every bill without an owned-production record and records the load ids of the bills that were ALREADY suspended; releasing resumes every bill except those. While designated the UI cannot add (Add bill shows one disabled entry), paste, or suspend/resume bills on the bench: the patches in `Patch_BenchDesignation` only swallow the click before the vanilla control (`ITab_Bills.FillTab`, `BillStack.DoListing`, `Bill.DoInterface`, `Dialog_BillConfig.DoWindowContents`) and read only `BenchDesignations`; they never mutate state. Every reconcile pass (paused automation included) suspends any unsuspended non-owned bill that reached a designated bench another way and prunes designations of benches no longer on any map (never while a gravship is in flight) — `PlannerBenches`. No game object carries Implanner data, so removing the mod leaves working benches with their bills still suspended. The toggle is offered only on benches able to craft an implant item, and on any designated bench so it can always be released.
- **Iteration** defaults to one star tier at a time (`IterationStrategy.ImplantTier`, "Tier batching"), matching the plan editor's tier panel; the automation UI lists it first and maps display order onto the persisted enum values (`AutomationSnapshot.IterationByDisplay`). "Full sets" (`Colonist`) batches the whole plan per colonist. "ASAP" (`Asap`) has no batch gate: the batch is every missing key, `SurgeryPlanner.Releasable` schedules whatever is reserved on site instead of waiting for the whole batch, and stock of each implant kind is allocated by priority and then the candidate ranking (`SurgeryCandidate`, sampled once per pawn per pass by `PawnProjection.CandidateOf` inside the synchronized tick pass): legs to the slowest colonist by MoveSpeed, arms to a melee-weapon holder first and then the higher Intellectual plus Crafting sum, anything else by pawn id. The ranking applies to ASAP only: under a batch strategy it would split a scarce tier's stock across colonists and stall every batch. Limb family comes from the targeted part's Moving/Manipulation body part tags (`ImplantCatalogEntry.Limb`). **SurgeryConcurrency** (1–20, Options domain) caps how many colonists per colony hold Implanner-scheduled operations at once; only colonists without scheduled operations are gated, ids in deterministic order take the free slots, and the value seeds once per save on the first reconcile pass that observes an authoritative colonist (old saves that already have colonists seed at load) to max(1, colonist count / 10); an explicit player edit ends seeding (scribed 0 = unseeded sentinel, preserved across saves until seeded). With **CountHospitalized** (on by default, Options domain) humanlike pawns of the authoritative faction (colonists and slaves; never prisoners, guests, or other factions' patients such as the Hospital mod's visitors, owner 2026-09-06) lying in a medical bed or downed and needing medical rest occupy cap slots too (deduplicated against owned-bill holders); the check reads live map state inside the synchronized tick pass, which is deterministic across clients.
- **Doctor floor** modes are exclusive: while `AutoDoctorFloor` is on (the default) each colony's floor tracks its CURRENT best eligible doctor — published up, down, or cleared at the 1020-tick boundary, with no player-facing per-colony state; the manual minimum applies only while auto is off and is seeded from the best currently eligible doctor inside the synced disable command. Nobody operates on themselves (the game's `WorkGiver_DoBill` skips a pawn's own bill stack), so a bill's enforced floor is per patient (`PlannerModel.PatientDoctorFloor`, owner 2026-09-06): while auto is on and the patient is the colony's best doctor, the bill's minimum is the best OTHER eligible doctor's skill at that colony (a peer of equal skill keeps the floor; the only doctor gets no floor), evaluated from the pass index every pass so the bill never waits on a floor only the patient meets; the manual minimum is never lowered. The details panel's floor and Blocked-by-floor status use the same patient-excluded rule.
- **Extended skills** (owner, 2026-09-13): doctor floors and manual/production thresholds preserve nonnegative skill values above 20, including on load. New bills set only the minimum, preserving the game's/mods' constructor-provided maximum. Reconciliation repairs legacy Implanner-owned bills whose maximum is 20 through Endless Growth's existing bill-limit provider. The Options-tab `skillSliderMaximum` preference (20–100, steps of 10, default 20) controls only the Medical/Crafting slider bounds; it is local presentation state and never clamps existing thresholds, changes bill eligibility, enters synchronized commands, or invalidates automation snapshots.
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

Building never deploys. Automated verification requires creating a fresh managed
run after building, following the root contract; creation deploys its copies.
Player installation deployment uses `pwsh scripts/deploy.ps1` and a game restart.
